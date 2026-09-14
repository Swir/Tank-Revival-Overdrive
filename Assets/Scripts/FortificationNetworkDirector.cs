using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(530)]
    public sealed class FortificationNetworkDirector : MonoBehaviour
    {
        public const int EarliestRound = 20;
        public const int MaxAuxNodes = 2;
        public const int ArtilleryHealth = 6;
        public const int RepairHealth = 7;
        public const float ArtilleryInterval = 4.8f;
        public const float RepairInterval = 8.5f;
        public const int MaxRepairPulses = 2;
        public const float BreakthroughInterval = 5.6f;
        public const float BreakthroughVolleyInterval = 3.2f;
        public const int MaxBreakthroughActors = 5;
        public const int MaxBreakthroughShots = 3;
        public const float NetworkControlPulse = 1.2f;
        public const float NodeLossPenalty = 7f;

        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FrontlineStatesField = typeof(DynamicFrontlineTerritoryDirector).GetField("_states", PrivateInstance);
        private static readonly FieldInfo FrontlineControlField = typeof(DynamicFrontlineTerritoryDirector).GetField("_control", PrivateInstance);

        private static FortificationNetworkDirector _instance;
        private TankGame _game;
        private GameObject _artillery;
        private Health _artilleryHealth;
        private int _artilleryLane = -1;
        private GameObject _repair;
        private Health _repairHealth;
        private int _repairLane = -1;
        private int _round = -1;
        private int _repairPulses;
        private float _nextArtillery;
        private float _nextRepair;
        private float _nextBreakthrough;
        private float _nextVolley;
        private float _nextControl;
        private string _status = string.Empty;
        private float _statusUntil;

        public static FortificationNetworkDirector Instance => _instance;
        public bool HasArtillery => _artillery != null && _artilleryHealth != null && !_artilleryHealth.IsDead;
        public bool HasRepairPost => _repair != null && _repairHealth != null && !_repairHealth.IsDead;
        public bool NetworkActive => StrongpointTerritoryWarfareDirector.Instance != null && StrongpointTerritoryWarfareDirector.Instance.HasStrongpoint && (HasArtillery || HasRepairPost);
        public static bool BridgeAvailable => FrontlineStatesField != null && FrontlineControlField != null;
        public static bool ConfigurationValid => EarliestRound >= 20 && MaxAuxNodes == 2 && ArtilleryHealth >= 5 && RepairHealth >= 6 && ArtilleryInterval >= 4f && RepairInterval >= 7f && MaxRepairPulses <= 3 && BreakthroughInterval >= 4f && MaxBreakthroughActors <= 5 && MaxBreakthroughShots <= 3 && NodeLossPenalty <= 8f && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<FortificationNetworkDirector>() != null) return;
            GameObject go = new GameObject("FortificationNetworkDirector_v9_2");
            DontDestroyOnLoad(go);
            go.AddComponent<FortificationNetworkDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupNodes();
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
            if (!CanNetworkRound(current)) return;

            if (Input.GetKeyDown(KeyCode.F6)) TryDeployArtillery();
            if (Input.GetKeyDown(KeyCode.F7)) TryDeployRepairPost();

            if (NetworkActive && Time.time >= _nextControl)
            {
                _nextControl = Time.time + 4f;
                StrongpointTerritoryWarfareDirector sp = StrongpointTerritoryWarfareDirector.Instance;
                if (sp != null && sp.StrongpointLane >= 0) AdjustLaneControl(sp.StrongpointLane, NetworkControlPulse);
            }
            if (HasArtillery && Time.time >= _nextArtillery)
            {
                _nextArtillery = Time.time + ArtilleryInterval;
                FireArtillery();
            }
            if (HasRepairPost && Time.time >= _nextRepair)
            {
                _nextRepair = Time.time + RepairInterval;
                RepairPulse();
            }
            if ((HasArtillery || HasRepairPost) && Time.time >= _nextBreakthrough)
            {
                _nextBreakthrough = Time.time + BreakthroughInterval;
                OrderBreakthrough();
            }
            if ((HasArtillery || HasRepairPost) && Time.time >= _nextVolley)
            {
                _nextVolley = Time.time + BreakthroughVolleyInterval;
                FireBreakthroughVolley();
            }
        }

        public static bool CanNetworkRound(int round) => round >= EarliestRound && round <= 100 && round % 10 != 0;
        public static int BreakthroughSizeForRound(int round) => Mathf.Clamp(2 + Mathf.Max(0, round - 20) / 25, 2, MaxBreakthroughActors);

        private void BeginRound(int round)
        {
            CleanupNodes();
            _round = round;
            _repairPulses = 0;
            _nextArtillery = Time.time + 2.5f;
            _nextRepair = Time.time + RepairInterval;
            _nextBreakthrough = Time.time + 4f;
            _nextVolley = Time.time + 5f;
            _nextControl = Time.time + 3f;
        }

        private void TryDeployArtillery()
        {
            if (HasArtillery) { ShowStatus("ARTILLERY POSITION ALREADY ACTIVE"); return; }
            int lane = ResolveDeploymentLane();
            if (lane < 0) { ShowStatus("NEED STRONGPOINT + FRIENDLY LANE"); return; }
            BuildNode(true, lane);
            ShowStatus("FORTIFICATION NETWORK // ARTILLERY ONLINE");
        }

        private void TryDeployRepairPost()
        {
            if (HasRepairPost) { ShowStatus("REPAIR POST ALREADY ACTIVE"); return; }
            int lane = ResolveDeploymentLane(_artilleryLane);
            if (lane < 0) { ShowStatus("NEED ANOTHER FRIENDLY LANE"); return; }
            BuildNode(false, lane);
            ShowStatus("FORTIFICATION NETWORK // REPAIR POST ONLINE");
        }

        private int ResolveDeploymentLane(int avoid = -1)
        {
            StrongpointTerritoryWarfareDirector sp = StrongpointTerritoryWarfareDirector.Instance;
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            PlayerTank player = CombatRoster.Player;
            if (sp == null || !sp.HasStrongpoint || frontline == null || player == null) return -1;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int lane = 0; lane < 3; lane++)
            {
                if (lane == sp.StrongpointLane || lane == avoid) continue;
                if (ReadLaneState(frontline, lane) != FrontlineControlState.Friendly) continue;
                float distance = Vector2.Distance(player.transform.position, DynamicFrontlineTerritoryDirector.LanePosition(lane));
                if (distance < bestDistance) { bestDistance = distance; best = lane; }
            }
            return best;
        }

        private void BuildNode(bool artillery, int lane)
        {
            Vector2 pos = DynamicFrontlineTerritoryDirector.LanePosition(lane) + new Vector2(0f, artillery ? 0.45f : -0.55f);
            GameObject root = new GameObject((artillery ? "ARTILLERY_POSITION_" : "REPAIR_POST_") + lane);
            root.transform.SetParent(transform, false);
            root.transform.position = pos;
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.size = artillery ? new Vector2(1.15f, 0.8f) : new Vector2(1.1f, 0.9f);
            Health hp = root.AddComponent<Health>();
            hp.Initialize(Team.Player, artillery ? ArtilleryHealth : RepairHealth);
            hp.Died += OnNodeDestroyed;
            VisualFactory.Rect("Base", root.transform, new Vector2(1.05f, 0.7f), artillery ? new Color(0.30f, 0.32f, 0.28f) : new Color(0.18f, 0.34f, 0.30f), Vector3.zero, 8);
            VisualFactory.Rect(artillery ? "Barrel" : "Crane", root.transform, artillery ? new Vector2(0.14f, 1.0f) : new Vector2(0.12f, 0.72f), artillery ? new Color(0.92f, 0.72f, 0.28f) : new Color(0.24f, 0.95f, 0.64f), new Vector3(0f, 0.48f, 0f), 9);
            VisualFactory.RingPulse(pos, artillery ? new Color(1f, 0.68f, 0.18f) : new Color(0.18f, 1f, 0.58f), 1.45f);
            if (artillery) { _artillery = root; _artilleryHealth = hp; _artilleryLane = lane; }
            else { _repair = root; _repairHealth = hp; _repairLane = lane; }
            AdjustLaneControl(lane, 5f);
        }

        private void OnNodeDestroyed(Health hp)
        {
            if (hp == _artilleryHealth)
            {
                if (_artilleryLane >= 0) AdjustLaneControl(_artilleryLane, -NodeLossPenalty);
                _artillery = null; _artilleryHealth = null; _artilleryLane = -1;
                ShowStatus("ARTILLERY POSITION LOST // BREAKTHROUGH");
            }
            else if (hp == _repairHealth)
            {
                if (_repairLane >= 0) AdjustLaneControl(_repairLane, -NodeLossPenalty);
                _repair = null; _repairHealth = null; _repairLane = -1;
                ShowStatus("REPAIR POST LOST // BREAKTHROUGH");
            }
        }

        private void FireArtillery()
        {
            EnemyTank target = FindPriorityEnemy(_artillery.transform.position, 9.5f);
            if (target == null) return;
            Vector2 origin = (Vector2)_artillery.transform.position + new Vector2(0f, 0.55f);
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            _game.SpawnProjectile(origin, dir, Team.Player, 2, 7.2f, new Color(1f, 0.62f, 0.16f), AmmoType.Explosive);
        }

        private void RepairPulse()
        {
            if (_repairPulses >= MaxRepairPulses) return;
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;
            if (Vector2.Distance(player.transform.position, _repair.transform.position) > 2.8f) return;
            if (player.Health.Current >= player.Health.Maximum) return;
            player.Health.Heal(1);
            _repairPulses++;
            VisualFactory.RingPulse(player.transform.position, new Color(0.18f, 1f, 0.58f), 1f);
        }

        private void OrderBreakthrough()
        {
            Vector2 target = WeakestNodePosition();
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int budget = BreakthroughSizeForRound(_round);
            int ordered = 0;
            for (int pass = 0; pass < 3 && ordered < budget; pass++)
            {
                for (int i = 0; i < enemies.Length && ordered < budget; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!ValidBreakthroughEnemy(enemy) || enemy.gameObject.name.EndsWith("_V92_BREAK", StringComparison.Ordinal)) continue;
                    bool preferred = enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite;
                    if ((pass == 0 && !preferred) || (pass > 0 && preferred)) continue;
                    TacticalNavigationAgent nav = enemy.GetComponent<TacticalNavigationAgent>();
                    if (nav == null) continue;
                    nav.SetRole(ordered == 0 ? SquadTacticalRole.Breaker : SquadTacticalRole.Suppressor);
                    nav.SetOrder(target, 0.7f + ordered * 0.12f, 1.05f, enemies);
                    enemy.gameObject.name += "_V92_BREAK";
                    ordered++;
                }
            }
        }

        private void FireBreakthroughVolley()
        {
            Vector2 target = WeakestNodePosition();
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shots = 0;
            for (int i = 0; i < enemies.Length && shots < MaxBreakthroughShots; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!ValidBreakthroughEnemy(enemy) || !enemy.gameObject.name.EndsWith("_V92_BREAK", StringComparison.Ordinal)) continue;
                Vector2 origin = enemy.transform.position;
                if (Vector2.Distance(origin, target) > 8.3f) continue;
                Vector2 dir = (target - origin).normalized;
                AmmoType ammo = enemy.Kind == EnemyKind.Siege ? AmmoType.Explosive : AmmoType.Basic;
                _game.SpawnProjectile(origin + dir * 0.4f, dir, Team.Enemy, 1, 6.6f, new Color(1f, 0.22f, 0.08f), ammo);
                shots++;
            }
        }

        private static bool ValidBreakthroughEnemy(EnemyTank enemy) => enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;

        private Vector2 WeakestNodePosition()
        {
            if (HasArtillery && HasRepairPost) return _artilleryHealth.Current <= _repairHealth.Current ? _artillery.transform.position : _repair.transform.position;
            if (HasArtillery) return _artillery.transform.position;
            if (HasRepairPost) return _repair.transform.position;
            StrongpointTerritoryWarfareDirector sp = StrongpointTerritoryWarfareDirector.Instance;
            return sp != null && sp.StrongpointLane >= 0 ? DynamicFrontlineTerritoryDirector.LanePosition(sp.StrongpointLane) : Vector2.zero;
        }

        private static EnemyTank FindPriorityEnemy(Vector2 position, float range)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!ValidBreakthroughEnemy(enemy)) continue;
                float distance = Vector2.Distance(position, enemy.transform.position);
                if (distance > range) continue;
                float score = (enemy.Kind == EnemyKind.Siege ? 5f : enemy.Kind == EnemyKind.Heavy ? 4f : enemy.Kind == EnemyKind.Elite ? 3f : 1f) - distance * 0.08f;
                if (score > bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private static FrontlineControlState ReadLaneState(DynamicFrontlineTerritoryDirector frontline, int lane)
        {
            if (FrontlineStatesField == null || frontline == null) return FrontlineControlState.Contested;
            FrontlineControlState[] states = FrontlineStatesField.GetValue(frontline) as FrontlineControlState[];
            return states != null && lane >= 0 && lane < states.Length ? states[lane] : FrontlineControlState.Contested;
        }

        private static void AdjustLaneControl(int lane, float delta)
        {
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            if (FrontlineControlField == null || frontline == null || lane < 0) return;
            float[] control = FrontlineControlField.GetValue(frontline) as float[];
            if (control == null || lane >= control.Length) return;
            control[lane] = Mathf.Clamp(control[lane] + delta, -100f, 100f);
        }

        private void CleanupNodes()
        {
            if (_artilleryHealth != null) _artilleryHealth.Died -= OnNodeDestroyed;
            if (_repairHealth != null) _repairHealth.Died -= OnNodeDestroyed;
            if (_artillery != null) Destroy(_artillery);
            if (_repair != null) Destroy(_repair);
            _artillery = null; _artilleryHealth = null; _artilleryLane = -1;
            _repair = null; _repairHealth = null; _repairLane = -1;
        }

        private void ResetRun() { CleanupNodes(); _round = -1; _repairPulses = 0; }
        private void ShowStatus(string message) { _status = message; _statusUntil = Time.unscaledTime + 2.5f; }
        private void OnGUI() { if (string.IsNullOrEmpty(_status) || Time.unscaledTime > _statusUntil) return; GUI.Label(new Rect(Screen.width * 0.5f - 260f, Screen.height - 118f, 520f, 32f), _status); }
    }
}
