using System;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(540)]
    public sealed class SiegeLineWarfareDirector : MonoBehaviour
    {
        public const int EarliestRound = 24;
        public const int BatteryHealth = 7;
        public const int MaxBatteries = 2;
        public const float BatteryFireInterval = 5.4f;
        public const float CounterBatteryInterval = 4.2f;
        public const float BreachOrderInterval = 6.0f;
        public const int MaxBreachActors = 4;
        public const int MaxBatteryShots = 2;
        public const int MaxCounterBatteryShots = 1;
        public const float BreachSuppressionSeconds = 8.0f;

        private static SiegeLineWarfareDirector _instance;
        private TankGame _game;
        private readonly GameObject[] _batteries = new GameObject[MaxBatteries];
        private readonly Health[] _batteryHealth = new Health[MaxBatteries];
        private readonly int[] _batteryLanes = new int[MaxBatteries] { -1, -1 };
        private int _round = -1;
        private int _destroyedThisRound;
        private float _nextBatteryFire;
        private float _nextCounterBattery;
        private float _nextBreachOrder;
        private float _suppressedUntil;
        private string _status = string.Empty;
        private float _statusUntil;

        public static SiegeLineWarfareDirector Instance => _instance;
        public int ActiveBatteryCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxBatteries; i++) if (BatteryAlive(i)) count++;
                return count;
            }
        }
        public int DestroyedThisRound => _destroyedThisRound;
        public bool FortificationSuppressed => Time.time < _suppressedUntil;
        public static bool ConfigurationValid => EarliestRound >= 20 && BatteryHealth >= 5 && MaxBatteries == 2 && BatteryFireInterval >= 4f && CounterBatteryInterval >= 3f && BreachOrderInterval >= 5f && MaxBreachActors <= 4 && MaxBatteryShots <= 2 && MaxCounterBatteryShots == 1 && BreachSuppressionSeconds <= 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SiegeLineWarfareDirector>() != null) return;
            GameObject go = new GameObject("SiegeLineWarfareDirector_v9_3");
            DontDestroyOnLoad(go);
            go.AddComponent<SiegeLineWarfareDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupBatteries();
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
            if (!IsSiegeRound(current)) return;

            if (ActiveBatteryCount > 0 && Time.time >= _nextBatteryFire)
            {
                _nextBatteryFire = Time.time + BatteryFireInterval;
                FireSiegeVolley();
            }
            if (ActiveBatteryCount > 0 && Time.time >= _nextCounterBattery)
            {
                _nextCounterBattery = Time.time + CounterBatteryInterval;
                FireCounterBattery();
            }
            if (ActiveBatteryCount > 0 && Time.time >= _nextBreachOrder)
            {
                _nextBreachOrder = Time.time + BreachOrderInterval;
                OrderBreachTeam();
            }
        }

        public static bool IsSiegeRound(int round)
        {
            return round >= EarliestRound && round <= 100 && round % 10 != 0 && (round % 6 == 0 || round % 6 == 3);
        }

        public static int BatteryCountForRound(int round)
        {
            return round >= 60 ? 2 : 1;
        }

        public static int BreachTeamSizeForRound(int round)
        {
            return Mathf.Clamp(2 + Mathf.Max(0, round - EarliestRound) / 28, 2, MaxBreachActors);
        }

        private void BeginRound(int round)
        {
            CleanupBatteries();
            _round = round;
            _destroyedThisRound = 0;
            _suppressedUntil = 0f;
            _nextBatteryFire = Time.time + 4f;
            _nextCounterBattery = Time.time + 5f;
            _nextBreachOrder = Time.time + 5.5f;
            if (!IsSiegeRound(round)) return;

            int count = BatteryCountForRound(round);
            for (int i = 0; i < count; i++) BuildBattery(i, ResolveBatteryLane(i));
            ShowStatus(count > 1 ? "ENEMY SIEGE LINE // TWO BATTERIES ACTIVE" : "ENEMY SIEGE BATTERY DETECTED");
        }

        private int ResolveBatteryLane(int index)
        {
            StrongpointTerritoryWarfareDirector strongpoint = StrongpointTerritoryWarfareDirector.Instance;
            int defended = strongpoint != null ? strongpoint.StrongpointLane : 1;
            if (defended < 0) defended = 1;
            if (index == 0) return defended;
            return defended == 1 ? 2 : 1;
        }

        private void BuildBattery(int index, int lane)
        {
            Vector2 basePos = DynamicFrontlineTerritoryDirector.LanePosition(lane);
            Vector2 pos = new Vector2(basePos.x, 4.15f + index * 0.35f);
            GameObject root = new GameObject("ENEMY_SIEGE_BATTERY_V93_" + index);
            root.transform.SetParent(transform, false);
            root.transform.position = pos;
            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.25f, 0.9f);
            Health hp = root.AddComponent<Health>();
            hp.Initialize(Team.Enemy, BatteryHealth + (_round >= 70 ? 1 : 0));
            hp.Died += OnBatteryDestroyed;
            VisualFactory.Rect("Emplacement", root.transform, new Vector2(1.2f, 0.72f), new Color(0.36f, 0.12f, 0.10f), Vector3.zero, 8);
            VisualFactory.Rect("Gun", root.transform, new Vector2(0.15f, 1.05f), new Color(0.95f, 0.30f, 0.10f), new Vector3(0f, -0.5f, 0f), 9);
            VisualFactory.RingPulse(pos, new Color(1f, 0.22f, 0.08f), 1.4f);
            _batteries[index] = root;
            _batteryHealth[index] = hp;
            _batteryLanes[index] = lane;
        }

        private void FireSiegeVolley()
        {
            Vector2 target = ResolveFortificationTarget();
            int shots = 0;
            for (int i = 0; i < MaxBatteries && shots < MaxBatteryShots; i++)
            {
                if (!BatteryAlive(i)) continue;
                Vector2 origin = _batteries[i].transform.position;
                Vector2 dir = (target - origin).normalized;
                _game.SpawnProjectile(origin + dir * 0.45f, dir, Team.Enemy, 1, 6.1f, new Color(1f, 0.18f, 0.06f), AmmoType.Explosive);
                shots++;
            }
            if (shots > 0) _suppressedUntil = Mathf.Max(_suppressedUntil, Time.time + BreachSuppressionSeconds);
        }

        private void FireCounterBattery()
        {
            FortificationNetworkDirector network = FortificationNetworkDirector.Instance;
            StrongpointTerritoryWarfareDirector strongpoint = StrongpointTerritoryWarfareDirector.Instance;
            if (network == null || !network.HasArtillery || strongpoint == null || !strongpoint.HasStrongpoint) return;

            int targetIndex = LowestHealthBattery();
            if (targetIndex < 0) return;
            Vector2 origin = DynamicFrontlineTerritoryDirector.LanePosition(strongpoint.StrongpointLane) + new Vector2(0f, 0.55f);
            Vector2 target = _batteries[targetIndex].transform.position;
            Vector2 dir = (target - origin).normalized;
            _game.SpawnProjectile(origin, dir, Team.Player, 2, 7.4f, new Color(1f, 0.72f, 0.18f), AmmoType.Explosive);
            VisualFactory.RingPulse(target, new Color(1f, 0.74f, 0.22f), 0.85f);
        }

        private void OrderBreachTeam()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int budget = BreachTeamSizeForRound(_round);
            int ordered = 0;
            Vector2 target = ResolveFortificationTarget();

            for (int pass = 0; pass < 2 && ordered < budget; pass++)
            {
                for (int i = 0; i < enemies.Length && ordered < budget; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!ValidBreachEnemy(enemy)) continue;
                    bool preferred = enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite;
                    if ((pass == 0 && !preferred) || (pass == 1 && preferred)) continue;
                    TacticalNavigationAgent nav = enemy.GetComponent<TacticalNavigationAgent>();
                    if (nav == null) continue;
                    nav.SetRole(ordered == 0 ? SquadTacticalRole.Breaker : SquadTacticalRole.Suppressor);
                    nav.SetOrder(target, 0.65f + ordered * 0.12f, 1.1f, enemies);
                    ordered++;
                }
            }
        }

        private static bool ValidBreachEnemy(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;
        }

        private Vector2 ResolveFortificationTarget()
        {
            StrongpointTerritoryWarfareDirector strongpoint = StrongpointTerritoryWarfareDirector.Instance;
            if (strongpoint != null && strongpoint.HasStrongpoint && strongpoint.StrongpointLane >= 0)
                return DynamicFrontlineTerritoryDirector.LanePosition(strongpoint.StrongpointLane);
            PlayerTank player = CombatRoster.Player;
            return player != null ? (Vector2)player.transform.position : Vector2.zero;
        }

        private int LowestHealthBattery()
        {
            int best = -1;
            int hp = int.MaxValue;
            for (int i = 0; i < MaxBatteries; i++)
            {
                if (!BatteryAlive(i)) continue;
                if (_batteryHealth[i].Current < hp) { hp = _batteryHealth[i].Current; best = i; }
            }
            return best;
        }

        private void OnBatteryDestroyed(Health hp)
        {
            for (int i = 0; i < MaxBatteries; i++)
            {
                if (_batteryHealth[i] != hp) continue;
                _batteries[i] = null;
                _batteryHealth[i] = null;
                _batteryLanes[i] = -1;
                _destroyedThisRound++;
                _suppressedUntil = 0f;
                ShowStatus(ActiveBatteryCount == 0 ? "COUNTER-BATTERY SUCCESS // BREACH PRESSURE BROKEN" : "SIEGE BATTERY DESTROYED");
                break;
            }
        }

        private bool BatteryAlive(int index)
        {
            return index >= 0 && index < MaxBatteries && _batteries[index] != null && _batteryHealth[index] != null && !_batteryHealth[index].IsDead;
        }

        private void CleanupBatteries()
        {
            for (int i = 0; i < MaxBatteries; i++)
            {
                if (_batteryHealth[i] != null) _batteryHealth[i].Died -= OnBatteryDestroyed;
                if (_batteries[i] != null) Destroy(_batteries[i]);
                _batteries[i] = null;
                _batteryHealth[i] = null;
                _batteryLanes[i] = -1;
            }
        }

        private void ResetRun()
        {
            CleanupBatteries();
            _round = -1;
            _destroyedThisRound = 0;
            _suppressedUntil = 0f;
            _status = string.Empty;
        }

        private void ShowStatus(string text)
        {
            _status = text;
            _statusUntil = Time.time + 3.2f;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_status) || Time.time > _statusUntil) return;
            GUI.Box(new Rect(Screen.width * 0.5f - 230f, 78f, 460f, 30f), _status);
        }
    }
}
