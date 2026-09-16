using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum OperationalSustainmentState
    {
        None,
        Moving,
        Supporting,
        Delivered,
        Intercepted,
        Withdrawn,
        Exhausted
    }

    /// <summary>
    /// v12.1 operational sustainment layer. One bounded logistics column follows an active v12.0
    /// mobile-front operation. The column uses canonical Health/collision, finite manifests and the
    /// existing TacticalNavigationAgent for enemy escorts. Rewards/restoration flow through existing
    /// TankGame, PlayerTank, WarEconomy and StrategicReserve authorities. Temporary interdiction
    /// pressure only stretches existing Siege/engineering timers; it never fires projectiles or deals
    /// direct damage and therefore does not create a parallel combat authority.
    /// </summary>
    [DefaultExecutionOrder(620)]
    public sealed class OperationalSustainmentDirector : MonoBehaviour
    {
        public const int EarliestRound = 60;
        public const int MaxColumns = 1;
        public const int MaxEscorts = 4;
        public const int FuelMin = 18;
        public const int FuelMax = 26;
        public const int AmmoMin = 4;
        public const int AmmoMax = 8;
        public const int RepairMin = 3;
        public const int RepairMax = 6;
        public const int ColumnHealthMin = 8;
        public const int ColumnHealthMax = 14;
        public const int ReserveRestoreMax = 2;
        public const float ColumnSpeed = 0.92f;
        public const float FuelTickSeconds = 0.72f;
        public const float SupportPulseSeconds = 2.8f;
        public const float SupportRadius = 2.45f;
        public const float EscortCadence = 0.34f;
        public const float EscortRadius = 8.5f;
        public const float EnemyDeficitSeconds = 18f;
        public const float EnemyDeficitMultiplier = 0.72f;
        public const float MaxTimerStretchSeconds = 2.0f;
        public const int InterceptBondReward = 6;
        public const int DeliveryBondReward = 4;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ReserveArmorField = typeof(StrategicReserveAttritionDirector).GetField("_armor", PrivateInstance);
        private static readonly FieldInfo ReserveArmorCapacityField = typeof(StrategicReserveAttritionDirector).GetField("_armorCapacity", PrivateInstance);
        private static readonly FieldInfo ReserveSupportField = typeof(StrategicReserveAttritionDirector).GetField("_fireSupport", PrivateInstance);
        private static readonly FieldInfo ReserveSupportCapacityField = typeof(StrategicReserveAttritionDirector).GetField("_fireSupportCapacity", PrivateInstance);
        private static readonly MethodInfo ReserveStoreSnapshotMethod = typeof(StrategicReserveAttritionDirector).GetMethod("StoreSnapshot", PrivateInstance);
        private static readonly FieldInfo SiegeNextBatteryFireField = typeof(SiegeLineWarfareDirector).GetField("_nextBatteryFire", PrivateInstance);
        private static readonly FieldInfo SiegeNextBreachOrderField = typeof(SiegeLineWarfareDirector).GetField("_nextBreachOrder", PrivateInstance);
        private static readonly FieldInfo EngineeringNextDecisionField = typeof(CombatEngineeringCounterBreachDirector).GetField("_nextDecision", PrivateInstance);

        private static OperationalSustainmentDirector _instance;
        private readonly EnemyTank[] _escorts = new EnemyTank[MaxEscorts];
        private readonly float[] _escortDistances = new float[MaxEscorts];
        private TankGame _game;
        private CombinedArmsMobileFrontDirector _front;
        private GameObject _column;
        private Rigidbody2D _body;
        private Health _health;
        private Team _team = Team.Neutral;
        private OperationalSustainmentState _state;
        private int _round = -1;
        private int _lane = -1;
        private int _fuel;
        private int _ammo;
        private int _repair;
        private int _reserveRestores;
        private int _escortCount;
        private int _columnsStarted;
        private int _columnsDelivered;
        private int _columnsIntercepted;
        private int _supportPulses;
        private int _frontWinsAtStart;
        private int _frontLossesAtStart;
        private float _nextFuelTick;
        private float _nextSupportPulse;
        private float _nextEscortOrder;
        private float _enemyDeficitUntil;
        private float _lastSiegeFireTimer = -1f;
        private float _lastSiegeBreachTimer = -1f;
        private float _lastEngineeringTimer = -1f;
        private bool _frontWasActive;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _bodyStyle;

        public static OperationalSustainmentDirector Instance => _instance;
        public bool ColumnActive => _column != null && _health != null && !_health.IsDead && (_state == OperationalSustainmentState.Moving || _state == OperationalSustainmentState.Supporting || _state == OperationalSustainmentState.Exhausted);
        public Team ColumnTeam => _team;
        public OperationalSustainmentState State => _state;
        public int FuelRemaining => _fuel;
        public int AmmoRemaining => _ammo;
        public int RepairRemaining => _repair;
        public int EscortCount => _escortCount;
        public int ColumnsStarted => _columnsStarted;
        public int ColumnsDelivered => _columnsDelivered;
        public int ColumnsIntercepted => _columnsIntercepted;
        public int SupportPulses => _supportPulses;
        public bool EnemySustainmentDeficit => Time.time < _enemyDeficitUntil;
        public float EnemySustainmentMultiplier => EnemySustainmentDeficit ? EnemyDeficitMultiplier : 1f;

        public static bool BridgeAvailable =>
            ReserveArmorField != null && ReserveArmorCapacityField != null &&
            ReserveSupportField != null && ReserveSupportCapacityField != null && ReserveStoreSnapshotMethod != null &&
            SiegeNextBatteryFireField != null && SiegeNextBreachOrderField != null && EngineeringNextDecisionField != null;

        public static bool ConfigurationValid =>
            EarliestRound >= CombinedArmsMobileFrontDirector.EarliestRound && EarliestRound <= 70 &&
            MaxColumns == 1 && MaxEscorts >= 3 && MaxEscorts <= 4 &&
            FuelMin >= 14 && FuelMax <= 30 && FuelMin < FuelMax &&
            AmmoMin >= 3 && AmmoMax <= 10 && AmmoMin < AmmoMax &&
            RepairMin >= 2 && RepairMax <= 8 && RepairMin < RepairMax &&
            ColumnHealthMin >= 6 && ColumnHealthMax <= 16 && ColumnHealthMin < ColumnHealthMax &&
            ReserveRestoreMax >= 1 && ReserveRestoreMax <= 2 &&
            ColumnSpeed >= 0.7f && ColumnSpeed <= 1.15f && FuelTickSeconds >= 0.5f && FuelTickSeconds <= 1.0f &&
            SupportPulseSeconds >= 2.0f && SupportPulseSeconds <= 4.0f && SupportRadius >= 1.8f && SupportRadius <= 3.0f &&
            EscortCadence >= TacticalNavigationDirector.DecisionCadence && EscortCadence <= 0.55f && EscortRadius <= 10f &&
            EnemyDeficitSeconds >= 12f && EnemyDeficitSeconds <= 24f && EnemyDeficitMultiplier >= 0.60f && EnemyDeficitMultiplier <= 0.82f &&
            MaxTimerStretchSeconds <= 2.5f && InterceptBondReward <= 10 && DeliveryBondReward <= 8 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OperationalSustainmentDirector>() != null) return;
            GameObject go = new GameObject("OperationalSustainmentDirector_v12_1");
            DontDestroyOnLoad(go);
            go.AddComponent<OperationalSustainmentDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupColumn();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_front == null) _front = CombinedArmsMobileFrontDirector.Instance;
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round)
            {
                if (_column != null) ResolveWithdrawal("OPERATIONAL LOGISTICS RECALLED");
                _round = current;
                _frontWasActive = false;
            }

            ApplyEnemyDeficitPressure();

            bool active = _front != null && _front.IsOperationActive;
            if (active && !_frontWasActive && !ColumnActive && CanLaunchForRound(current))
                BeginColumn(current, _front.CurrentKind, _front.CurrentLane);

            if (ColumnActive)
            {
                if (_health.IsDead) ResolveInterception();
                else
                {
                    UpdateColumnState();
                    if (_team == Team.Enemy && Time.time >= _nextEscortOrder)
                    {
                        _nextEscortOrder = Time.time + EscortCadence;
                        AssignEnemyEscorts();
                    }
                }
            }

            if (!active && _frontWasActive && _column != null && _health != null && !_health.IsDead)
                ResolveFrontOutcome();

            _frontWasActive = active;
        }

        private void FixedUpdate()
        {
            if (!ColumnActive || _body == null || _front == null || !_front.IsOperationActive) return;
            Vector2 target = SupportAnchor(_lane, _front.CurrentProgress, _team == Team.Enemy);
            float distance = Vector2.Distance(_body.position, target);
            if (_fuel <= 0)
            {
                _state = OperationalSustainmentState.Exhausted;
                return;
            }

            if (distance <= 0.42f)
            {
                _state = OperationalSustainmentState.Supporting;
                return;
            }

            _state = OperationalSustainmentState.Moving;
            _body.MovePosition(Vector2.MoveTowards(_body.position, target, ColumnSpeed * Time.fixedDeltaTime));
        }

        public static bool CanLaunchForRound(int round)
        {
            if (round < EarliestRound || round > CombinedArmsMobileFrontDirector.LatestRound) return false;
            if (!CombinedArmsMobileFrontDirector.IsCandidateRound(round)) return false;
            if (ConvoyWarfareDirector.HasMissionForRound(round)) return false;
            if (LogisticsNetworkDirector.HasLogisticsForRound(round)) return false;
            return true;
        }

        public static int EligibleColumnCount()
        {
            int count = 0;
            for (int round = 1; round <= 100; round++) if (CanLaunchForRound(round)) count++;
            return count;
        }

        public static int FuelForRound(int round) => Mathf.Clamp(FuelMin + Mathf.Max(0, round - EarliestRound) / 8, FuelMin, FuelMax);
        public static int AmmoForRound(int round) => Mathf.Clamp(AmmoMin + Mathf.Max(0, round - EarliestRound) / 14, AmmoMin, AmmoMax);
        public static int RepairForRound(int round) => Mathf.Clamp(RepairMin + Mathf.Max(0, round - EarliestRound) / 18, RepairMin, RepairMax);
        public static int HealthForRound(int round) => Mathf.Clamp(ColumnHealthMin + Mathf.Max(0, round - EarliestRound) / 12, ColumnHealthMin, ColumnHealthMax);

        public static Vector2 SupportAnchor(int lane, float frontProgress, bool enemyOperation)
        {
            lane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            float x = DynamicFrontlineTerritoryDirector.LanePosition(lane).x;
            float sideBias = lane == 1 ? (enemyOperation ? 0.42f : -0.42f) : lane == 0 ? 0.30f : -0.30f;
            float p = Mathf.Clamp01(frontProgress);
            float frontY = Mathf.Lerp(enemyOperation ? CombinedArmsMobileFrontDirector.ArenaYLimit : -CombinedArmsMobileFrontDirector.ArenaYLimit,
                                      enemyOperation ? -CombinedArmsMobileFrontDirector.ArenaYLimit : CombinedArmsMobileFrontDirector.ArenaYLimit, p);
            float behind = enemyOperation ? 1.35f : -1.35f;
            return new Vector2(Mathf.Clamp(x + sideBias, -CombinedArmsMobileFrontDirector.ArenaXLimit, CombinedArmsMobileFrontDirector.ArenaXLimit),
                               Mathf.Clamp(frontY + behind, -4.15f, 4.15f));
        }

        public static Vector2 ColumnStart(int lane, bool enemyOperation)
        {
            Vector2 anchor = SupportAnchor(lane, 0f, enemyOperation);
            anchor.y = enemyOperation ? 4.45f : -4.45f;
            return anchor;
        }

        public static float StretchedDelay(float baseDelay, float sustainmentMultiplier)
        {
            float safeDelay = Mathf.Max(0f, baseDelay);
            float multiplier = Mathf.Clamp(sustainmentMultiplier, 0.50f, 1f);
            return Mathf.Min(safeDelay / multiplier, safeDelay + MaxTimerStretchSeconds);
        }

        public static bool IsEscortKind(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Elite || kind == EnemyKind.Fast;
        }

        private void BeginColumn(int round, MobileFrontOperationKind operation, int lane)
        {
            if (operation == MobileFrontOperationKind.None || lane < 0) return;
            _team = operation == MobileFrontOperationKind.EnemyBreakthrough ? Team.Enemy : Team.Player;
            _lane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            _fuel = FuelForRound(round);
            _ammo = AmmoForRound(round);
            _repair = RepairForRound(round);
            _reserveRestores = 0;
            _escortCount = 0;
            _frontWinsAtStart = _front != null ? _front.OperationsWon : 0;
            _frontLossesAtStart = _front != null ? _front.OperationsLost : 0;
            _nextFuelTick = Time.time + FuelTickSeconds;
            _nextSupportPulse = Time.time + 1.1f;
            _nextEscortOrder = Time.time;
            _state = OperationalSustainmentState.Moving;
            _columnsStarted++;

            _column = new GameObject(_team == Team.Enemy ? "ENEMY_OPERATIONAL_LOGISTICS_v12_1" : "ORZELEK_OPERATIONAL_LOGISTICS_v12_1");
            _column.transform.position = ColumnStart(_lane, _team == Team.Enemy);
            Color bodyColor = _team == Team.Enemy ? new Color(0.54f, 0.14f, 0.08f) : new Color(0.08f, 0.40f, 0.58f);
            Color accent = _team == Team.Enemy ? new Color(1f, 0.34f, 0.08f) : new Color(0.20f, 0.92f, 1f);
            VisualFactory.Rect("LogisticsHull", _column.transform, new Vector2(1.22f, 0.74f), bodyColor, Vector3.zero, 8);
            VisualFactory.Rect("FuelPod", _column.transform, new Vector2(0.30f, 0.42f), new Color(0.92f, 0.70f, 0.16f), new Vector3(-0.34f, 0.05f, 0f), 9);
            VisualFactory.Rect("AmmoPod", _column.transform, new Vector2(0.30f, 0.42f), accent, new Vector3(0f, 0.05f, 0f), 9);
            VisualFactory.Rect("RepairPod", _column.transform, new Vector2(0.30f, 0.42f), new Color(0.30f, 0.92f, 0.48f), new Vector3(0.34f, 0.05f, 0f), 9);
            VisualFactory.Disc("LogisticsBeacon", _column.transform, new Vector2(0.18f, 0.18f), accent, new Vector3(0f, 0.48f, 0f), 10);

            BoxCollider2D collider = _column.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.12f, 0.68f);
            _body = _column.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.gravityScale = 0f;
            _body.freezeRotation = true;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _health = _column.AddComponent<Health>();
            _health.Initialize(_team, HealthForRound(round));
            _health.Damaged += OnColumnDamaged;
            _health.Died += OnColumnDied;

            VisualFactory.RingPulse(_column.transform.position, accent, 1.6f);
            ShowStatus(_team == Team.Enemy ? "ENEMY OPERATIONAL SUPPLY COLUMN // INTERDICT" : "ORZELEK LOGISTICS COLUMN // ESCORT + RESUPPLY", 4.0f);
        }

        private void UpdateColumnState()
        {
            if (_front == null || !_front.IsOperationActive || _body == null) return;
            Vector2 target = SupportAnchor(_lane, _front.CurrentProgress, _team == Team.Enemy);
            float distance = Vector2.Distance(_body.position, target);

            if (_state == OperationalSustainmentState.Moving && distance > 0.45f && Time.time >= _nextFuelTick)
            {
                _nextFuelTick = Time.time + FuelTickSeconds;
                _fuel = Mathf.Max(0, _fuel - 1);
                if (_fuel == 0)
                {
                    _state = OperationalSustainmentState.Exhausted;
                    ShowStatus("LOGISTICS FUEL EXHAUSTED // COLUMN IMMOBILE", 3.2f);
                }
            }

            if (distance <= SupportRadius && Time.time >= _nextSupportPulse && (_ammo > 0 || _repair > 0))
            {
                _nextSupportPulse = Time.time + SupportPulseSeconds;
                PulseSupport();
            }
        }

        private void PulseSupport()
        {
            if (_team == Team.Player) PulseFriendlySupport();
            else if (_team == Team.Enemy) PulseEnemySupport();
        }

        private void PulseFriendlySupport()
        {
            PlayerTank player = CombatRoster.Player;
            bool helped = false;
            if (_repair > 0)
            {
                if (player != null && player.Health != null && !player.Health.IsDead && player.Health.Current < player.Health.Maximum && Vector2.Distance(player.transform.position, _body.position) <= SupportRadius + 0.7f)
                {
                    player.Health.Heal(1);
                    _repair--;
                    helped = true;
                }
                else
                {
                    Health eagle = CombatRoster.Eagle;
                    if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum && Vector2.Distance(eagle.transform.position, _body.position) <= SupportRadius + 1.4f)
                    {
                        eagle.Heal(1);
                        _repair--;
                        helped = true;
                    }
                }
            }

            if (_ammo > 0 && player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, _body.position) <= SupportRadius + 0.7f)
            {
                player.AddAmmo((_supportPulses & 1) == 0 ? AmmoType.ArmorPiercing : AmmoType.Explosive, 1);
                _ammo--;
                helped = true;
            }

            if (!helped) return;
            _supportPulses++;
            VisualFactory.RingPulse(_body.position, new Color(0.18f, 0.92f, 1f), 0.85f);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.24f, 0.02f);
            ShowStatus("ORZELEK SUSTAINMENT PULSE // MANIFEST " + _fuel + "/" + _ammo + "/" + _repair, 2.2f);
        }

        private void PulseEnemySupport()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            bool helped = false;
            if (_repair > 0 && enemies != null)
            {
                EnemyTank best = null;
                float bestDistance = SupportRadius * SupportRadius;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Supply || enemy.Health.Current >= enemy.Health.Maximum) continue;
                    float d = ((Vector2)enemy.transform.position - _body.position).sqrMagnitude;
                    if (d > bestDistance) continue;
                    bestDistance = d;
                    best = enemy;
                }
                if (best != null)
                {
                    best.Health.Heal(1);
                    _repair--;
                    helped = true;
                    VisualFactory.RingPulse(best.transform.position, new Color(1f, 0.34f, 0.08f), 0.62f);
                }
            }

            if (_ammo > 0 && _reserveRestores < ReserveRestoreMax && RestoreEnemyReserve(StrategicReserveKind.FireSupport, 1))
            {
                _ammo--;
                _reserveRestores++;
                helped = true;
            }

            if (!helped) return;
            _supportPulses++;
            ShowStatus("ENEMY SUSTAINMENT ACTIVE // INTERDICT BEFORE DELIVERY", 2.2f);
        }

        private void AssignEnemyEscorts()
        {
            if (_team != Team.Enemy || _body == null) { _escortCount = 0; return; }
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) { _escortCount = 0; return; }
            for (int i = 0; i < MaxEscorts; i++) { _escorts[i] = null; _escortDistances[i] = float.MaxValue; }

            int count = 0;
            float limitSq = EscortRadius * EscortRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsEscortKind(enemy.Kind)) continue;
                float distanceSq = ((Vector2)enemy.transform.position - _body.position).sqrMagnitude;
                if (distanceSq > limitSq) continue;

                int insert;
                if (count < MaxEscorts) { insert = count; count++; }
                else
                {
                    if (distanceSq >= _escortDistances[MaxEscorts - 1]) continue;
                    insert = MaxEscorts - 1;
                }
                while (insert > 0 && distanceSq < _escortDistances[insert - 1])
                {
                    _escortDistances[insert] = _escortDistances[insert - 1];
                    _escorts[insert] = _escorts[insert - 1];
                    insert--;
                }
                _escortDistances[insert] = distanceSq;
                _escorts[insert] = enemy;
            }

            _escortCount = count;
            for (int i = 0; i < count; i++)
            {
                EnemyTank escort = _escorts[i];
                if (escort == null || escort.Health == null || escort.Health.IsDead) continue;
                TacticalNavigationAgent nav = escort.GetComponent<TacticalNavigationAgent>();
                if (nav == null)
                {
                    nav = escort.gameObject.AddComponent<TacticalNavigationAgent>();
                    nav.Initialize(escort);
                }
                float side = (i & 1) == 0 ? -1f : 1f;
                float rear = i < 2 ? 0.35f : 0.95f;
                Vector2 target = _body.position + new Vector2(side * (0.72f + 0.18f * i), rear);
                nav.SetRole(SquadTacticalRole.Escort);
                nav.SetOrder(target, 1.25f + i * 0.08f, escort.Kind == EnemyKind.Heavy ? 0.90f : 1.02f, enemies);
            }
        }

        private void ResolveFrontOutcome()
        {
            bool playerWon = _front != null && _front.OperationsWon > _frontWinsAtStart;
            bool playerLost = _front != null && _front.OperationsLost > _frontLossesAtStart;
            bool ownSideSucceeded = _team == Team.Player ? playerWon : playerLost;
            if (ownSideSucceeded) CompleteDelivery();
            else ResolveWithdrawal(_team == Team.Player ? "FRIENDLY LOGISTICS WITHDREW WITH FRONT" : "ENEMY LOGISTICS FORCED TO WITHDRAW");
        }

        private void CompleteDelivery()
        {
            if (_column == null) return;
            _state = OperationalSustainmentState.Delivered;
            _columnsDelivered++;
            if (_team == Team.Player)
            {
                Health eagle = CombatRoster.Eagle;
                PlayerTank player = CombatRoster.Player;
                if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum) eagle.Heal(1);
                if (player != null && player.Health != null && !player.Health.IsDead)
                {
                    if (player.Health.Current < player.Health.Maximum) player.Health.Heal(1);
                    player.AddAmmo(AmmoType.ArmorPiercing, 1);
                    player.AddAmmo(AmmoType.Explosive, 1);
                }
                WarEconomyDirector.AwardMissionBonds(DeliveryBondReward, "OPERATIONAL LOGISTICS DELIVERED");
                ShowStatus("ORZELEK LOGISTICS DELIVERED // FRONT SUSTAINED", 4.0f);
            }
            else
            {
                RestoreEnemyReserve(StrategicReserveKind.Armor, 1);
                RestoreEnemyReserve(StrategicReserveKind.FireSupport, 1);
                _enemyDeficitUntil = 0f;
                ShowStatus("ENEMY LOGISTICS DELIVERED // RESERVES REPLENISHED", 4.0f);
            }
            VisualFactory.RingPulse(_column.transform.position, _team == Team.Player ? new Color(0.18f, 1f, 0.58f) : new Color(1f, 0.34f, 0.08f), 1.8f);
            CleanupColumn(false);
        }

        private void ResolveInterception()
        {
            if (_state == OperationalSustainmentState.Intercepted || _column == null) return;
            Vector3 position = _column.transform.position;
            _state = OperationalSustainmentState.Intercepted;
            _columnsIntercepted++;
            if (_team == Team.Enemy)
            {
                _enemyDeficitUntil = Mathf.Max(_enemyDeficitUntil, Time.time + EnemyDeficitSeconds);
                WarEconomyDirector.AwardMissionBonds(InterceptBondReward, "ENEMY OPERATIONAL LOGISTICS INTERDICTED");
                ShowStatus("ENEMY SUSTAINMENT BROKEN // SIEGE + ENGINEERING CADENCE DEGRADED", 4.4f);
            }
            else
            {
                ShowStatus("ORZELEK LOGISTICS LOST // FORWARD SUPPORT UNAVAILABLE", 4.0f);
            }
            VisualFactory.Explosion(position, _team == Team.Enemy ? new Color(1f, 0.30f, 0.08f) : new Color(0.18f, 0.78f, 1f), 1.25f);
            CleanupColumn(false);
        }

        private void ResolveWithdrawal(string reason)
        {
            if (_column == null) return;
            _state = OperationalSustainmentState.Withdrawn;
            ShowStatus(reason, 3.2f);
            CleanupColumn(false);
        }

        private void OnColumnDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus((_team == Team.Enemy ? "ENEMY" : "ORZELEK") + " LOGISTICS UNDER FIRE // HP " + health.Current + "/" + health.Maximum, 2.0f);
        }

        private void OnColumnDied(Health health)
        {
            ResolveInterception();
        }

        private void ApplyEnemyDeficitPressure()
        {
            if (!EnemySustainmentDeficit)
            {
                _lastSiegeFireTimer = -1f;
                _lastSiegeBreachTimer = -1f;
                _lastEngineeringTimer = -1f;
                return;
            }

            SiegeLineWarfareDirector siege = SiegeLineWarfareDirector.Instance;
            if (siege != null)
            {
                StretchTimerOnce(siege, SiegeNextBatteryFireField, ref _lastSiegeFireTimer, EnemyDeficitMultiplier);
                StretchTimerOnce(siege, SiegeNextBreachOrderField, ref _lastSiegeBreachTimer, EnemyDeficitMultiplier);
            }

            CombatEngineeringCounterBreachDirector engineering = CombatEngineeringCounterBreachDirector.Instance;
            if (engineering != null)
            {
                Vector2 anchor = DynamicFrontlineTerritoryDirector.LanePosition(Mathf.Clamp(_lane < 0 ? 1 : _lane, 0, 2));
                if (ReactiveCoverBreachDirector.TryFindRecentBreachNear(anchor, Team.Player, CombatEngineeringCounterBreachDirector.EnemyResponseRadius + 1.0f, out ReactiveCoverBreachDirector.BreachSnapshot breach) && breach.Valid)
                    StretchTimerOnce(engineering, EngineeringNextDecisionField, ref _lastEngineeringTimer, EnemyDeficitMultiplier);
            }
        }

        private static void StretchTimerOnce(object target, FieldInfo field, ref float lastApplied, float multiplier)
        {
            if (target == null || field == null) return;
            float next = (float)field.GetValue(target);
            if (next <= Time.time + 0.02f) return;
            if (lastApplied >= 0f && Mathf.Abs(next - lastApplied) <= 0.01f) return;
            float remaining = Mathf.Max(0f, next - Time.time);
            float stretched = Time.time + StretchedDelay(remaining, multiplier);
            if (stretched <= next + 0.01f) { lastApplied = next; return; }
            field.SetValue(target, stretched);
            lastApplied = stretched;
        }

        private static bool RestoreEnemyReserve(StrategicReserveKind kind, int amount)
        {
            StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
            if (reserves == null || !BridgeAvailable || amount <= 0) return false;
            FieldInfo valueField = kind == StrategicReserveKind.Armor ? ReserveArmorField : ReserveSupportField;
            FieldInfo capacityField = kind == StrategicReserveKind.Armor ? ReserveArmorCapacityField : ReserveSupportCapacityField;
            int current = (int)valueField.GetValue(reserves);
            int capacity = (int)capacityField.GetValue(reserves);
            if (current >= capacity) return false;
            valueField.SetValue(reserves, Mathf.Clamp(current + amount, 0, capacity));
            ReserveStoreSnapshotMethod.Invoke(reserves, null);
            return true;
        }

        private void CleanupColumn(bool resetState = true)
        {
            if (_health != null)
            {
                _health.Damaged -= OnColumnDamaged;
                _health.Died -= OnColumnDied;
            }
            if (_column != null) Destroy(_column);
            _column = null;
            _body = null;
            _health = null;
            _team = Team.Neutral;
            _escortCount = 0;
            for (int i = 0; i < _escorts.Length; i++) _escorts[i] = null;
            if (resetState) _state = OperationalSustainmentState.None;
        }

        private void ResetRun()
        {
            CleanupColumn();
            _round = -1;
            _lane = -1;
            _frontWasActive = false;
            _enemyDeficitUntil = 0f;
            _columnsStarted = 0;
            _columnsDelivered = 0;
            _columnsIntercepted = 0;
            _supportPulses = 0;
            _status = string.Empty;
            _lastSiegeFireTimer = -1f;
            _lastSiegeBreachTimer = -1f;
            _lastEngineeringTimer = -1f;
        }

        private void ShowStatus(string text, float duration)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.40f, 0.92f, 1f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!ColumnActive && Time.unscaledTime >= _statusUntil && !EnemySustainmentDeficit) return;
            EnsureStyles();
            float width = 520f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 82f;
            GUI.color = new Color(0.02f, 0.04f, 0.055f, 0.90f);
            GUI.Box(new Rect(x, y, width, ColumnActive ? 58f : 34f), string.Empty);
            GUI.color = Color.white;
            string title = ColumnActive ? (_team == Team.Enemy ? "ENEMY OPERATIONAL SUSTAINMENT" : "ORZELEK OPERATIONAL SUSTAINMENT") : _status;
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f), title, _header);
            if (ColumnActive)
            {
                string deficit = EnemySustainmentDeficit ? " // ENEMY SUPPLY " + Mathf.RoundToInt(EnemySustainmentMultiplier * 100f) + "%" : string.Empty;
                GUI.Label(new Rect(x + 8f, y + 26f, width - 16f, 20f), "FUEL " + _fuel + " // AMMO " + _ammo + " // REPAIR " + _repair + " // ESCORT " + _escortCount + deficit, _bodyStyle);
            }
        }
    }
}
