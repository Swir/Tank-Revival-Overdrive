using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(520)]
    public sealed class StrongpointTerritoryWarfareDirector : MonoBehaviour
    {
        public const int EarliestRound = 15;
        public const int BaseBuildCost = 18;
        public const int ReinforceCost = 10;
        public const int BaseStrongpointHealth = 7;
        public const int MaxStrongpointHealth = 12;
        public const int MaxCounterattackActors = 4;
        public const int MaxVolleyShots = 2;
        public const float CounterattackInterval = 4.5f;
        public const float EnemyVolleyInterval = 2.8f;
        public const float StrongpointFireInterval = 2.65f;
        public const float SupportPulseInterval = 8.0f;
        public const int MaxSupportPulses = 2;
        public const float ControlBuildBoost = 10f;
        public const float ControlReinforceBoost = 4f;
        public const float ControlHoldPulse = 1.25f;
        public const float ControlLossOnDestroyed = 12f;

        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FrontlineStatesField = typeof(DynamicFrontlineTerritoryDirector).GetField("_states", PrivateInstance);
        private static readonly FieldInfo FrontlineControlField = typeof(DynamicFrontlineTerritoryDirector).GetField("_control", PrivateInstance);
        private static readonly MethodInfo EconomySpendMethod = typeof(WarEconomyDirector).GetMethod("Spend", PrivateInstance);

        private static StrongpointTerritoryWarfareDirector _instance;
        private TankGame _game;
        private GameObject _strongpoint;
        private Health _strongpointHealth;
        private int _strongpointLane = -1;
        private int _round = -1;
        private bool _builtThisRound;
        private bool _reinforcedThisRound;
        private int _supportPulses;
        private float _nextCounterattack;
        private float _nextEnemyVolley;
        private float _nextStrongpointShot;
        private float _nextSupportPulse;
        private float _nextControlPulse;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static StrongpointTerritoryWarfareDirector Instance => _instance;
        public int StrongpointLane => _strongpointLane;
        public bool HasStrongpoint => _strongpoint != null && _strongpointHealth != null && !_strongpointHealth.IsDead;
        public int StrongpointHealth => HasStrongpoint ? _strongpointHealth.Current : 0;
        public int StrongpointMaxHealth => HasStrongpoint ? _strongpointHealth.Maximum : 0;
        public static bool BridgeAvailable => FrontlineStatesField != null && FrontlineControlField != null && EconomySpendMethod != null;
        public static bool ConfigurationValid => EarliestRound >= 10 && BaseBuildCost >= 12 && BaseBuildCost <= 30 && ReinforceCost >= 6 && ReinforceCost < BaseBuildCost && BaseStrongpointHealth >= 5 && MaxStrongpointHealth <= 14 && MaxCounterattackActors >= 3 && MaxCounterattackActors <= 5 && MaxVolleyShots <= 2 && CounterattackInterval >= 3.5f && EnemyVolleyInterval >= 2.0f && StrongpointFireInterval >= 2.0f && SupportPulseInterval >= 6.0f && MaxSupportPulses <= 3 && ControlBuildBoost <= 12f && ControlLossOnDestroyed <= 15f && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<StrongpointTerritoryWarfareDirector>() != null) return;
            GameObject go = new GameObject("StrongpointTerritoryWarfareDirector_v9_1");
            DontDestroyOnLoad(go);
            go.AddComponent<StrongpointTerritoryWarfareDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupStrongpoint();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round) BeginRound(current);
            if (current < EarliestRound || current % 10 == 0) return;

            if (Input.GetKeyDown(KeyCode.F4)) TryBuildNearestFriendlyStrongpoint();
            if (Input.GetKeyDown(KeyCode.F5)) TryReinforceStrongpoint();

            if (!HasStrongpoint) return;
            if (Time.time >= _nextControlPulse)
            {
                _nextControlPulse = Time.time + 4.0f;
                AdjustLaneControl(_strongpointLane, ControlHoldPulse);
            }
            if (Time.time >= _nextStrongpointShot)
            {
                _nextStrongpointShot = Time.time + StrongpointFireInterval;
                FireStrongpointSupport();
            }
            if (Time.time >= _nextSupportPulse)
            {
                _nextSupportPulse = Time.time + SupportPulseInterval;
                ApplyLocalRecoveryPulse();
            }
            if (Time.time >= _nextCounterattack)
            {
                _nextCounterattack = Time.time + CounterattackInterval;
                OrderCounterattack();
            }
            if (Time.time >= _nextEnemyVolley)
            {
                _nextEnemyVolley = Time.time + EnemyVolleyInterval;
                FireCounterattackVolley();
            }
        }

        public static bool CanFortifyRound(int round) => round >= EarliestRound && round <= 100 && round % 10 != 0;
        public static int BuildCostForRound(int round) => Mathf.Clamp(BaseBuildCost + Mathf.Max(0, round - 20) / 30, BaseBuildCost, BaseBuildCost + 2);
        public static int StrongpointHealthForRound(int round) => Mathf.Clamp(BaseStrongpointHealth + Mathf.Max(0, round) / 25, BaseStrongpointHealth, MaxStrongpointHealth - 1);
        public static int CounterattackSizeForRound(int round) => Mathf.Clamp(2 + Mathf.Max(0, round) / 35, 2, MaxCounterattackActors);

        private void BeginRound(int round)
        {
            CleanupStrongpoint();
            _round = round;
            _builtThisRound = false;
            _reinforcedThisRound = false;
            _supportPulses = 0;
            _nextCounterattack = Time.time + 4.0f;
            _nextEnemyVolley = Time.time + 4.8f;
            _nextStrongpointShot = Time.time + 1.6f;
            _nextSupportPulse = Time.time + SupportPulseInterval;
            _nextControlPulse = Time.time + 2.5f;
        }

        private void TryBuildNearestFriendlyStrongpoint()
        {
            if (_builtThisRound || HasStrongpoint)
            {
                ShowStatus("STRONGPOINT ALREADY COMMITTED THIS ROUND");
                return;
            }
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            PlayerTank player = CombatRoster.Player;
            if (frontline == null || player == null || player.Health == null || player.Health.IsDead)
            {
                ShowStatus("STRONGPOINT LINK UNAVAILABLE");
                return;
            }

            int lane = NearestLane(player.transform.position);
            if (ReadLaneState(frontline, lane) != FrontlineControlState.Friendly || Vector2.Distance(player.transform.position, DynamicFrontlineTerritoryDirector.LanePosition(lane)) > DynamicFrontlineTerritoryDirector.LaneRadius + 1.35f)
            {
                ShowStatus("MOVE INTO A FRIENDLY FRONTLINE LANE TO FORTIFY");
                return;
            }

            int cost = BuildCostForRound(_round);
            if (!TrySpendBonds(cost, "FRONTLINE STRONGPOINT")) return;
            BuildStrongpoint(lane, StrongpointHealthForRound(_round));
            _builtThisRound = true;
            AdjustLaneControl(lane, ControlBuildBoost);
            ShowStatus("STRONGPOINT DEPLOYED // LANE " + LaneName(lane));
        }

        private void TryReinforceStrongpoint()
        {
            if (!HasStrongpoint || _reinforcedThisRound)
            {
                ShowStatus(HasStrongpoint ? "STRONGPOINT ALREADY REINFORCED" : "NO ACTIVE STRONGPOINT");
                return;
            }
            if (!TrySpendBonds(ReinforceCost, "STRONGPOINT REINFORCEMENT")) return;
            int targetMax = Mathf.Min(MaxStrongpointHealth, _strongpointHealth.Maximum + 3);
            _strongpointHealth.SetMaximum(targetMax, true);
            _strongpointHealth.Heal(2);
            _supportPulses = Mathf.Max(0, _supportPulses - 1);
            _reinforcedThisRound = true;
            AdjustLaneControl(_strongpointLane, ControlReinforceBoost);
            VisualFactory.RingPulse(_strongpoint.transform.position, new Color(0.18f, 0.92f, 1f), 1.55f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.42f, 0.02f);
            ShowStatus("STRONGPOINT REINFORCED // +ARMOR +SUPPORT");
        }

        private void BuildStrongpoint(int lane, int hp)
        {
            Vector2 pos = DynamicFrontlineTerritoryDirector.LanePosition(lane) + new Vector2(0f, -0.35f);
            GameObject root = new GameObject("FRONTLINE_STRONGPOINT_" + LaneName(lane));
            root.transform.SetParent(transform, false);
            root.transform.position = pos;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.35f, 0.95f);
            Health health = root.AddComponent<Health>();
            health.Initialize(Team.Player, hp);
            health.Died += OnStrongpointDestroyed;

            VisualFactory.Rect("Bunker", root.transform, new Vector2(1.25f, 0.78f), new Color(0.16f, 0.28f, 0.30f), Vector3.zero, 8);
            VisualFactory.Rect("Armor", root.transform, new Vector2(1.05f, 0.18f), new Color(0.32f, 0.72f, 0.78f), new Vector3(0f, 0.24f, 0f), 9);
            VisualFactory.Rect("Gun", root.transform, new Vector2(0.15f, 0.85f), new Color(0.65f, 0.88f, 0.92f), new Vector3(0f, 0.52f, 0f), 10);
            VisualFactory.Disc("Beacon", root.transform, new Vector2(0.22f, 0.22f), new Color(0.16f, 1f, 0.62f), new Vector3(0f, -0.28f, 0f), 11);
            VisualFactory.RingPulse(pos, new Color(0.16f, 1f, 0.62f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.36f, -0.04f);

            _strongpoint = root;
            _strongpointHealth = health;
            _strongpointLane = lane;
        }

        private void OnStrongpointDestroyed(Health health)
        {
            if (_strongpointLane >= 0) AdjustLaneControl(_strongpointLane, -ControlLossOnDestroyed);
            if (_strongpoint != null) VisualFactory.Explosion(_strongpoint.transform.position, new Color(1f, 0.28f, 0.08f), 1.45f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.52f, 0.02f);
            ShowStatus("STRONGPOINT LOST // ENEMY BREAKTHROUGH");
            _strongpoint = null;
            _strongpointHealth = null;
            _strongpointLane = -1;
        }

        private void FireStrongpointSupport()
        {
            EnemyTank target = FindNearestEnemy(_strongpoint.transform.position, 7.5f);
            if (target == null) return;
            Vector2 origin = (Vector2)_strongpoint.transform.position + new Vector2(0f, 0.55f);
            Vector2 direction = ((Vector2)target.transform.position - origin).normalized;
            _game.SpawnProjectile(origin, direction, Team.Player, 1, 8.4f, new Color(0.20f, 0.92f, 1f), AmmoType.Basic);
        }

        private void ApplyLocalRecoveryPulse()
        {
            if (_supportPulses >= MaxSupportPulses) return;
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;
            if (Vector2.Distance(player.transform.position, _strongpoint.transform.position) > 2.35f) return;
            if (player.Health.Current >= player.Health.Maximum) return;
            player.Health.Heal(1);
            _supportPulses++;
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 1f, 0.58f), 0.95f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.28f, 0.06f);
            ShowStatus("STRONGPOINT FIELD REPAIR // " + _supportPulses + "/" + MaxSupportPulses);
        }

        private void OrderCounterattack()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int budget = CounterattackSizeForRound(_round);
            int ordered = 0;
            Vector2 target = _strongpoint.transform.position;
            for (int pass = 0; pass < enemies.Length && ordered < budget; pass++)
            {
                EnemyTank best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank e = enemies[i];
                    if (e == null || e.Health == null || e.Health.IsDead || e.Kind == EnemyKind.Boss || e.Kind == EnemyKind.Supply) continue;
                    TacticalNavigationAgent agent = e.GetComponent<TacticalNavigationAgent>();
                    if (agent == null) continue;
                    float d = Vector2.Distance(e.transform.position, target) + i * 0.001f;
                    if (d < bestDistance && !AlreadyOrdered(e, ordered, enemies)) { bestDistance = d; best = e; }
                }
                if (best == null) break;
                TacticalNavigationAgent nav = best.GetComponent<TacticalNavigationAgent>();
                nav.SetRole(ordered == 0 ? SquadTacticalRole.Breaker : SquadTacticalRole.Suppressor);
                nav.SetOrder(target, 0.75f + ordered * 0.18f, 1.10f, enemies);
                best.gameObject.name = best.gameObject.name.Replace("_SP_ASSAULT", string.Empty) + "_SP_ASSAULT";
                ordered++;
            }
        }

        private static bool AlreadyOrdered(EnemyTank enemy, int ordered, EnemyTank[] enemies)
        {
            if (enemy == null || ordered <= 0) return false;
            return enemy.gameObject.name.EndsWith("_SP_ASSAULT", StringComparison.Ordinal);
        }

        private void FireCounterattackVolley()
        {
            if (!HasStrongpoint) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shots = 0;
            Vector2 target = _strongpoint.transform.position;
            for (int i = 0; i < enemies.Length && shots < MaxVolleyShots; i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead || e.Kind == EnemyKind.Boss || e.Kind == EnemyKind.Supply) continue;
                if (!e.gameObject.name.EndsWith("_SP_ASSAULT", StringComparison.Ordinal)) continue;
                Vector2 origin = e.transform.position;
                if (Vector2.Distance(origin, target) > 7.6f) continue;
                Vector2 direction = (target - origin).normalized;
                _game.SpawnProjectile(origin + direction * 0.42f, direction, Team.Enemy, 1, 6.8f, new Color(1f, 0.34f, 0.10f), AmmoType.Basic);
                shots++;
            }
        }

        private EnemyTank FindNearestEnemy(Vector2 origin, float maxDistance)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestDistance = maxDistance;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead || e.Kind == EnemyKind.Supply) continue;
                float d = Vector2.Distance(origin, e.transform.position);
                if (d < bestDistance) { bestDistance = d; best = e; }
            }
            return best;
        }

        private static int NearestLane(Vector2 position)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int lane = 0; lane < DynamicFrontlineTerritoryDirector.LaneCount; lane++)
            {
                float d = Vector2.Distance(position, DynamicFrontlineTerritoryDirector.LanePosition(lane));
                if (d < bestDistance) { bestDistance = d; best = lane; }
            }
            return best;
        }

        private static FrontlineControlState ReadLaneState(DynamicFrontlineTerritoryDirector frontline, int lane)
        {
            if (frontline == null || FrontlineStatesField == null) return FrontlineControlState.Contested;
            FrontlineControlState[] states = FrontlineStatesField.GetValue(frontline) as FrontlineControlState[];
            if (states == null || lane < 0 || lane >= states.Length) return FrontlineControlState.Contested;
            return states[lane];
        }

        private static void AdjustLaneControl(int lane, float amount)
        {
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            if (frontline == null || FrontlineControlField == null || lane < 0 || lane >= DynamicFrontlineTerritoryDirector.LaneCount) return;
            float[] control = FrontlineControlField.GetValue(frontline) as float[];
            if (control == null || lane >= control.Length) return;
            control[lane] = Mathf.Clamp(control[lane] + amount, 0f, 100f);
        }

        private static bool TrySpendBonds(int amount, string label)
        {
            WarEconomyDirector economy = FindAnyObjectByType<WarEconomyDirector>();
            if (economy == null || EconomySpendMethod == null) return false;
            object result = EconomySpendMethod.Invoke(economy, new object[] { amount, label });
            return result is bool value && value;
        }

        private void CleanupStrongpoint()
        {
            if (_strongpointHealth != null) _strongpointHealth.Died -= OnStrongpointDestroyed;
            if (_strongpoint != null) Destroy(_strongpoint);
            _strongpoint = null;
            _strongpointHealth = null;
            _strongpointLane = -1;
        }

        private void ResetRun()
        {
            CleanupStrongpoint();
            _round = -1;
            _builtThisRound = false;
            _reinforcedThisRound = false;
            _supportPulses = 0;
        }

        private static string LaneName(int lane) => lane == 0 ? "WEST" : lane == 2 ? "EAST" : "CENTER";

        private void ShowStatus(string message)
        {
            _status = message;
            _statusUntil = Time.unscaledTime + 2.6f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.24f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _round < EarliestRound) return;
            EnsureStyles();
            GUI.color = new Color(0.02f, 0.055f, 0.065f, 0.84f);
            GUI.Box(new Rect(Screen.width - 365f, Screen.height - 94f, 345f, 74f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 352f, Screen.height - 88f, 320f, 18f), "FRONTLINE FORTIFICATIONS", _header);
            if (HasStrongpoint)
                GUI.Label(new Rect(Screen.width - 352f, Screen.height - 67f, 320f, 18f), "F4 BUILD  // F5 REINFORCE " + ReinforceCost + "B // " + LaneName(_strongpointLane) + " HP " + StrongpointHealth + "/" + StrongpointMaxHealth, _body);
            else
                GUI.Label(new Rect(Screen.width - 352f, Screen.height - 67f, 320f, 18f), "F4 BUILD IN FRIENDLY LANE // COST " + BuildCostForRound(_round) + " BONDS", _body);
            if (Time.unscaledTime < _statusUntil)
                GUI.Label(new Rect(Screen.width - 352f, Screen.height - 48f, 320f, 18f), _status, _body);
        }
    }
}
