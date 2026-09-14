using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum FrontlineControlState { Enemy, Contested, Friendly }

    [DefaultExecutionOrder(515)]
    public sealed class DynamicFrontlineTerritoryDirector : MonoBehaviour
    {
        public const int LaneCount = 3;
        public const float LaneRadius = 2.15f;
        public const float CaptureRate = 18f;
        public const float EnemyCaptureRate = 8f;
        public const float NeutralDriftRate = 2f;
        public const float FriendlyThreshold = 68f;
        public const float EnemyThreshold = 32f;
        public const int EarliestOperationRound = 12;
        public const int OperationInterval = 6;
        public const float OperationDuration = 38f;
        public const int RequiredObjectives = 2;
        public const float FriendlyArtilleryDelay = 2.25f;
        public const float EnemyArtilleryAdvance = 1.10f;
        public const int MaxLanePressureOrders = 4;

        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo DynamicObjectiveCompleteField = typeof(DynamicBattlefieldDirector).GetField("_objectiveComplete", PrivateInstance);
        private static readonly FieldInfo DynamicObjectiveRootField = typeof(DynamicBattlefieldDirector).GetField("_objectiveRoot", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);
        private static readonly FieldInfo EagleHealthField = typeof(TankGame).GetField("_baseHealth", PrivateInstance);

        private static DynamicFrontlineTerritoryDirector _instance;
        private readonly float[] _control = { 50f, 50f, 50f };
        private readonly FrontlineControlState[] _states = { FrontlineControlState.Contested, FrontlineControlState.Contested, FrontlineControlState.Contested };
        private readonly GameObject[] _visuals = new GameObject[LaneCount];
        private TankGame _game;
        private DynamicBattlefieldDirector _dynamic;
        private int _round = -1;
        private bool _operationActive;
        private bool _operationResolved;
        private float _operationEndsAt;
        private int _eagleStartHealth;
        private bool _territoryObjective;
        private bool _battlefieldObjective;
        private bool _defenseObjective;
        private int _operationsWon;
        private int _operationsLost;
        private float _nextPressureOrder;
        private bool _roundConsequenceApplied;
        private GUIStyle _header;
        private GUIStyle _body;

        public static DynamicFrontlineTerritoryDirector Instance => _instance;
        public bool OperationActive => _operationActive;
        public int OperationsWon => _operationsWon;
        public int OperationsLost => _operationsLost;
        public int FriendlyLanes => CountState(FrontlineControlState.Friendly);
        public int EnemyLanes => CountState(FrontlineControlState.Enemy);
        public int CompletedObjectives => (_territoryObjective ? 1 : 0) + (_battlefieldObjective ? 1 : 0) + (_defenseObjective ? 1 : 0);
        public static bool BridgeAvailable => DynamicObjectiveCompleteField != null && DynamicObjectiveRootField != null && EncounterNextStrikeField != null && EagleHealthField != null;
        public static bool ConfigurationValid => LaneCount == 3 && LaneRadius >= 1.7f && LaneRadius <= 2.6f && CaptureRate >= 12f && CaptureRate <= 24f && EnemyCaptureRate >= 5f && EnemyCaptureRate <= 12f && FriendlyThreshold > 60f && EnemyThreshold < 40f && EarliestOperationRound >= 10 && OperationInterval >= 3 && OperationInterval <= 8 && OperationDuration >= 28f && OperationDuration <= 48f && RequiredObjectives == 2 && FriendlyArtilleryDelay <= 3f && EnemyArtilleryAdvance <= 1.5f && MaxLanePressureOrders >= 2 && MaxLanePressureOrders <= 5 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DynamicFrontlineTerritoryDirector>() != null) return;
            var go = new GameObject("DynamicFrontlineTerritoryDirector_v9_0");
            DontDestroyOnLoad(go);
            go.AddComponent<DynamicFrontlineTerritoryDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupVisuals();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_dynamic == null) _dynamic = FindAnyObjectByType<DynamicBattlefieldDirector>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round) BeginRound(current);
            UpdateLaneControl();
            UpdateOperation();
            ApplyTerritoryConsequences();
            if (Time.time >= _nextPressureOrder) { _nextPressureOrder = Time.time + 1.25f; ApplyEnemyLanePressure(); }
        }

        public static bool HasOperationForRound(int round) => round >= EarliestOperationRound && round % OperationInterval == 0 && round % 10 != 0;
        public static Vector2 LanePosition(int lane)
        {
            lane = Mathf.Clamp(lane, 0, 2);
            return new Vector2(lane == 0 ? -5.4f : lane == 2 ? 5.4f : 0f, 1.15f);
        }
        public static FrontlineControlState StateForScore(float score) => score >= FriendlyThreshold ? FrontlineControlState.Friendly : score <= EnemyThreshold ? FrontlineControlState.Enemy : FrontlineControlState.Contested;
        public static float CarryScore(float previous) => Mathf.Lerp(50f, Mathf.Clamp(previous, 0f, 100f), 0.62f);
        public static int RewardForRound(int round) => Mathf.Clamp(8 + round / 20, 8, 13);

        private void BeginRound(int current)
        {
            for (int i = 0; i < LaneCount; i++) _control[i] = _round < 0 ? 50f : CarryScore(_control[i]);
            _round = current;
            _operationActive = HasOperationForRound(current);
            _operationResolved = false;
            _operationEndsAt = Time.time + OperationDuration;
            _territoryObjective = false;
            _battlefieldObjective = false;
            _defenseObjective = false;
            _roundConsequenceApplied = false;
            _nextPressureOrder = Time.time + 0.8f;
            Health eagle = GetEagleHealth();
            _eagleStartHealth = eagle != null ? eagle.Current : 0;
            BuildLaneVisuals();
        }

        private void UpdateLaneControl()
        {
            PlayerTank player = CombatRoster.Player;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int lane = 0; lane < LaneCount; lane++)
            {
                Vector2 center = LanePosition(lane);
                bool playerInside = player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, center) <= LaneRadius;
                int enemyCount = 0;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                    if (Vector2.Distance(enemy.transform.position, center) <= LaneRadius) enemyCount++;
                }

                float delta = 0f;
                if (playerInside) delta += CaptureRate;
                if (enemyCount > 0) delta -= EnemyCaptureRate * Mathf.Min(enemyCount, 3);
                if (!playerInside && enemyCount == 0) delta += (50f - _control[lane]) * NeutralDriftRate * 0.01f;
                _control[lane] = Mathf.Clamp(_control[lane] + delta * Time.deltaTime, 0f, 100f);
                _states[lane] = StateForScore(_control[lane]);
                UpdateLaneVisual(lane);
            }
            if (FriendlyLanes >= 2) _territoryObjective = true;
        }

        private void UpdateOperation()
        {
            if (!_operationActive || _operationResolved) return;
            if (ReadExistingBattlefieldObjectiveComplete()) _battlefieldObjective = true;
            Health eagle = GetEagleHealth();
            bool eagleHeld = eagle != null && !eagle.IsDead && eagle.Current >= Mathf.Max(1, _eagleStartHealth - 1);
            bool recoveryHeld = ForwardRecoveryFrontlineDirector.Instance != null && ForwardRecoveryFrontlineDirector.Instance.Phase == ForwardRecoveryPhase.Support;
            if (eagleHeld || recoveryHeld) _defenseObjective = true;

            if (CompletedObjectives >= RequiredObjectives) ResolveOperation(true);
            else if (Time.time >= _operationEndsAt) ResolveOperation(false);
        }

        private void ResolveOperation(bool success)
        {
            _operationResolved = true;
            _operationActive = false;
            if (success)
            {
                _operationsWon++;
                WarEconomyDirector.AwardMissionBonds(RewardForRound(_round), "FRONTLINE OPERATION SECURED");
                if (FriendlyLanes >= 2) _game.RepairEagle(1);
                VisualFactory.RingPulse(LanePosition(1), new Color(0.15f, 1f, 0.55f), 2.6f);
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.45f, 0.02f);
            }
            else
            {
                _operationsLost++;
                VisualFactory.RingPulse(LanePosition(1), new Color(1f, 0.22f, 0.10f), 2.6f);
                BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.38f, 0.02f);
            }
        }

        private void ApplyTerritoryConsequences()
        {
            if (_roundConsequenceApplied || CampaignEncounterDirector.Instance == null || EncounterNextStrikeField == null) return;
            int friendly = FriendlyLanes;
            int enemy = EnemyLanes;
            if (friendly < 2 && enemy < 2) return;
            float next = Convert.ToSingle(EncounterNextStrikeField.GetValue(CampaignEncounterDirector.Instance));
            if (next <= 0f) return;
            next += friendly >= 2 ? FriendlyArtilleryDelay : -EnemyArtilleryAdvance;
            EncounterNextStrikeField.SetValue(CampaignEncounterDirector.Instance, Mathf.Max(Time.time + 0.5f, next));
            _roundConsequenceApplied = true;
        }

        private void ApplyEnemyLanePressure()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int ordered = 0;
            for (int lane = 0; lane < LaneCount && ordered < MaxLanePressureOrders; lane++)
            {
                if (_states[lane] == FrontlineControlState.Enemy) continue;
                Vector2 target = LanePosition(lane);
                EnemyTank best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank e = enemies[i];
                    if (e == null || e.Health == null || e.Health.IsDead || e.Kind == EnemyKind.Boss || e.Kind == EnemyKind.Supply) continue;
                    float d = Vector2.Distance(e.transform.position, target);
                    if (d < bestDistance) { bestDistance = d; best = e; }
                }
                if (best == null) continue;
                TacticalNavigationAgent agent = best.GetComponent<TacticalNavigationAgent>();
                if (agent == null) continue;
                agent.SetRole(SquadTacticalRole.Breaker);
                agent.SetOrder(target, 0.9f, 1.08f, enemies);
                ordered++;
            }
        }

        private bool ReadExistingBattlefieldObjectiveComplete()
        {
            if (_dynamic == null || DynamicObjectiveCompleteField == null || DynamicObjectiveRootField == null) return false;
            object root = DynamicObjectiveRootField.GetValue(_dynamic);
            if (root == null) return false;
            return (bool)(DynamicObjectiveCompleteField.GetValue(_dynamic) ?? false);
        }

        private Health GetEagleHealth() => _game != null && EagleHealthField != null ? EagleHealthField.GetValue(_game) as Health : null;

        private void BuildLaneVisuals()
        {
            CleanupVisuals();
            for (int i = 0; i < LaneCount; i++)
            {
                GameObject root = new GameObject("FRONTLINE_LANE_" + i);
                root.transform.SetParent(transform, false);
                root.transform.position = LanePosition(i);
                VisualFactory.Disc("Zone", root.transform, Vector2.one * LaneRadius * 2f, new Color(0.7f, 0.7f, 0.7f, 0.07f), Vector3.zero, -5);
                VisualFactory.Rect("Marker", root.transform, new Vector2(0.10f, 0.92f), Color.white, Vector3.zero, 5);
                _visuals[i] = root;
            }
        }

        private void UpdateLaneVisual(int lane)
        {
            GameObject root = _visuals[lane];
            if (root == null) return;
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
            Color c = _states[lane] == FrontlineControlState.Friendly ? new Color(0.15f, 0.95f, 0.50f) : _states[lane] == FrontlineControlState.Enemy ? new Color(1f, 0.20f, 0.12f) : new Color(1f, 0.72f, 0.14f);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color x = c;
                if (renderers[i].gameObject.name == "Zone") x.a = 0.08f;
                renderers[i].color = x;
            }
        }

        private static int CountState(FrontlineControlState state)
        {
            if (_instance == null) return 0;
            int count = 0;
            for (int i = 0; i < LaneCount; i++) if (_instance._states[i] == state) count++;
            return count;
        }

        private void CleanupVisuals()
        {
            for (int i = 0; i < LaneCount; i++) { if (_visuals[i] != null) Destroy(_visuals[i]); _visuals[i] = null; }
        }

        private void ResetRun()
        {
            CleanupVisuals();
            _round = -1;
            _operationActive = false;
            _operationResolved = false;
            for (int i = 0; i < LaneCount; i++) { _control[i] = 50f; _states[i] = FrontlineControlState.Contested; }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.42f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            GUI.color = new Color(0.02f, 0.05f, 0.07f, 0.82f);
            GUI.Box(new Rect(18f, Screen.height - 92f, 320f, 72f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(30f, Screen.height - 86f, 290f, 20f), "FRONTLINE CONTROL", _header);
            string lanes = "W " + LaneToken(0) + "   C " + LaneToken(1) + "   E " + LaneToken(2);
            GUI.Label(new Rect(30f, Screen.height - 65f, 290f, 18f), lanes, _body);
            if (_operationActive)
                GUI.Label(new Rect(30f, Screen.height - 46f, 290f, 18f), "OPERATION " + CompletedObjectives + "/3 // NEED 2", _body);
        }

        private string LaneToken(int lane)
        {
            string state = _states[lane] == FrontlineControlState.Friendly ? "FRIENDLY" : _states[lane] == FrontlineControlState.Enemy ? "ENEMY" : "CONTESTED";
            return state + " " + Mathf.RoundToInt(_control[lane]);
        }
    }
}
