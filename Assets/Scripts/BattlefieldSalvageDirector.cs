using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum SalvageRecoveryMode
    {
        Field,
        Strategic,
        EnemyReclaimed,
        Expired
    }

    [DefaultExecutionOrder(485)]
    public sealed class BattlefieldSalvageDirector : MonoBehaviour
    {
        public const int MaxActiveSalvage = 3;
        public const int MaxEnemyDropsPerRound = 2;
        public const float SalvageLifetime = 16f;
        public const float InteractionRange = 1.45f;
        public const float ReclaimRange = 0.82f;
        public const float CounterOrderCadence = 0.55f;
        public const int MaxCounterRecoveryUnits = 3;
        public const int EnemyReclaimReserveRestore = 1;
        public const int FieldRepairAmount = 1;
        public const int FieldAmmoAmount = 2;
        public const int StrategicBondReward = 4;
        public const int StrategicLogisticsBondReward = 6;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo NodeField = typeof(LogisticsNetworkDirector).GetField("_node", PrivateInstance);
        private static readonly FieldInfo NodeHealthField = typeof(LogisticsNetworkDirector).GetField("_nodeHealth", PrivateInstance);
        private static readonly FieldInfo NodeKindField = typeof(LogisticsNetworkDirector).GetField("_nodeKind", PrivateInstance);
        private static readonly MethodInfo RestoreReserveMethod = typeof(LogisticsNetworkDirector).GetMethod("RestoreReserve", PrivateInstance);
        private static readonly FieldInfo BaseHealthField = typeof(TankGame).GetField("_baseHealth", PrivateInstance);

        private sealed class SalvageEntry
        {
            public GameObject Root;
            public StrategicReserveKind ReserveKind;
            public bool LogisticsGrade;
            public float ExpiresAt;
            public float NextCounterOrder;
            public bool Resolved;
        }

        private static BattlefieldSalvageDirector _instance;
        private readonly List<SalvageEntry> _salvage = new List<SalvageEntry>(MaxActiveSalvage);
        private TankGame _game;
        private LogisticsNetworkDirector _logistics;
        private GameObject _trackedNode;
        private Health _trackedNodeHealth;
        private LogisticsNodeKind _trackedNodeKind;
        private int _round;
        private int _enemyDropsThisRound;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _promptStyle;
        private GUIStyle _statusStyle;

        public static BattlefieldSalvageDirector Instance => _instance;
        public int ActiveSalvageCount => _salvage.Count;
        public static bool BridgeAvailable => NodeField != null && NodeHealthField != null && NodeKindField != null && RestoreReserveMethod != null && BaseHealthField != null;
        public static bool ConfigurationValid => MaxActiveSalvage >= 2 && MaxActiveSalvage <= 4 && MaxEnemyDropsPerRound >= 1 && MaxEnemyDropsPerRound <= 3 &&
            SalvageLifetime >= 12f && SalvageLifetime <= 22f && InteractionRange >= 1.1f && InteractionRange <= 1.8f &&
            ReclaimRange >= 0.60f && ReclaimRange <= 1.05f && ReclaimRange < InteractionRange && CounterOrderCadence >= 0.35f && CounterOrderCadence <= 0.80f &&
            MaxCounterRecoveryUnits >= 2 && MaxCounterRecoveryUnits <= 4 && EnemyReclaimReserveRestore == 1 && FieldRepairAmount == 1 &&
            FieldAmmoAmount >= 2 && FieldAmmoAmount <= 4 && StrategicBondReward >= 3 && StrategicBondReward <= 6 &&
            StrategicLogisticsBondReward > StrategicBondReward && StrategicLogisticsBondReward <= 8 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldSalvageDirector>() != null) return;
            GameObject go = new GameObject("BattlefieldSalvageDirector_v8_8");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldSalvageDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            UnhookLogisticsNode();
            ClearSalvage();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_logistics == null) _logistics = LogisticsNetworkDirector.Instance;
            if (_game == null || !_game.IsPlaying)
            {
                if (_round != 0) ResetRun();
                return;
            }

            int currentRound = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (currentRound != _round)
            {
                _round = currentRound;
                _enemyDropsThisRound = 0;
            }

            TrackLogisticsNode();
            InstallEnemyTrackers();
            UpdateSalvageEntries();
        }

        public static bool IsSalvageEligible(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
        }

        public static StrategicReserveKind ReserveKindForEnemy(EnemyKind kind)
        {
            if (kind == EnemyKind.Heavy) return StrategicReserveKind.Armor;
            if (kind == EnemyKind.Elite) return StrategicReserveKind.ElectronicWarfare;
            return StrategicReserveKind.FireSupport;
        }

        public static AmmoType FieldAmmoForReserve(StrategicReserveKind kind)
        {
            switch (kind)
            {
                case StrategicReserveKind.Armor: return AmmoType.ArmorPiercing;
                case StrategicReserveKind.ElectronicWarfare: return AmmoType.EMP;
                default: return AmmoType.Explosive;
            }
        }

        public static int CounterRecoveryCountForRound(int round)
        {
            if (round < 25) return 1;
            if (round < 60) return 2;
            return MaxCounterRecoveryUnits;
        }

        public static int BondsForRecovery(bool logisticsGrade, SalvageRecoveryMode mode)
        {
            if (mode == SalvageRecoveryMode.Strategic)
                return logisticsGrade ? StrategicLogisticsBondReward : StrategicBondReward;
            return mode == SalvageRecoveryMode.Field ? 1 : 0;
        }

        public static bool CanPlayerRecover(float distance, bool resolved, float remainingLifetime)
        {
            return !resolved && remainingLifetime > 0f && distance <= InteractionRange;
        }

        public static bool CanEnemyReclaim(float distance, bool resolved, float remainingLifetime)
        {
            return !resolved && remainingLifetime > 0f && distance <= ReclaimRange;
        }

        private void TrackLogisticsNode()
        {
            if (_logistics == null || !BridgeAvailable) return;
            GameObject node = NodeField.GetValue(_logistics) as GameObject;
            Health health = NodeHealthField.GetValue(_logistics) as Health;
            if (node == _trackedNode && health == _trackedNodeHealth) return;

            UnhookLogisticsNode();
            _trackedNode = node;
            _trackedNodeHealth = health;
            if (_trackedNode == null || _trackedNodeHealth == null) return;
            _trackedNodeKind = (LogisticsNodeKind)NodeKindField.GetValue(_logistics);
            _trackedNodeHealth.Died += OnLogisticsNodeDied;
        }

        private void UnhookLogisticsNode()
        {
            if (_trackedNodeHealth != null) _trackedNodeHealth.Died -= OnLogisticsNodeDied;
            _trackedNode = null;
            _trackedNodeHealth = null;
        }

        private void OnLogisticsNodeDied(Health health)
        {
            if (health == null) return;
            SpawnSalvage(health.transform.position, LogisticsNetworkDirector.ReserveKindForNode(_trackedNodeKind), true);
        }

        private void InstallEnemyTrackers()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsSalvageEligible(enemy.Kind)) continue;
                if (enemy.GetComponent<BattlefieldSalvageDeathTracker>() != null) continue;
                BattlefieldSalvageDeathTracker tracker = enemy.gameObject.AddComponent<BattlefieldSalvageDeathTracker>();
                tracker.Initialize(this, enemy);
            }
        }

        internal void NotifyEnemyDestroyed(EnemyKind kind, Vector3 position)
        {
            if (_game == null || !_game.IsPlaying || !IsSalvageEligible(kind)) return;
            if (_enemyDropsThisRound >= MaxEnemyDropsPerRound) return;
            if (_salvage.Count >= MaxActiveSalvage) return;
            _enemyDropsThisRound++;
            SpawnSalvage(position, ReserveKindForEnemy(kind), false);
        }

        private void SpawnSalvage(Vector3 position, StrategicReserveKind reserveKind, bool logisticsGrade)
        {
            while (_salvage.Count >= MaxActiveSalvage)
                ResolveEntry(_salvage[0], SalvageRecoveryMode.Expired);

            GameObject root = new GameObject(logisticsGrade ? "LOGISTICS_SALVAGE_CACHE" : "BATTLEFIELD_SALVAGE");
            root.transform.position = position;
            Color color = ReserveColor(reserveKind);
            VisualFactory.Rect("Crate", root.transform, logisticsGrade ? new Vector2(0.92f, 0.62f) : new Vector2(0.68f, 0.48f), new Color(0.16f, 0.18f, 0.20f), Vector3.zero, 15);
            VisualFactory.Rect("Stripe", root.transform, logisticsGrade ? new Vector2(0.72f, 0.12f) : new Vector2(0.50f, 0.10f), color, new Vector3(0f, 0.08f, 0f), 16);
            VisualFactory.Disc("Beacon", root.transform, new Vector2(0.18f, 0.18f), color, new Vector3(0f, 0.44f, 0f), 17);
            VisualFactory.RingPulse(position, color, logisticsGrade ? 1.15f : 0.82f);

            SalvageEntry entry = new SalvageEntry
            {
                Root = root,
                ReserveKind = reserveKind,
                LogisticsGrade = logisticsGrade,
                ExpiresAt = Time.time + SalvageLifetime,
                NextCounterOrder = Time.time + 0.35f,
                Resolved = false
            };
            _salvage.Add(entry);
            _status = logisticsGrade ? "LOGISTICS CACHE DOWN // SECURE SALVAGE" : "HIGH-VALUE WRECK // SALVAGE AVAILABLE";
            _statusUntil = Time.unscaledTime + 3.2f;
        }

        private void UpdateSalvageEntries()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            for (int i = _salvage.Count - 1; i >= 0; i--)
            {
                SalvageEntry entry = _salvage[i];
                if (entry == null || entry.Root == null)
                {
                    _salvage.RemoveAt(i);
                    continue;
                }
                float remaining = entry.ExpiresAt - Time.time;
                if (remaining <= 0f)
                {
                    ResolveEntry(entry, SalvageRecoveryMode.Expired);
                    continue;
                }

                if (player != null && player.Health != null && !player.Health.IsDead)
                {
                    float playerDistance = Vector2.Distance(player.transform.position, entry.Root.transform.position);
                    if (CanPlayerRecover(playerDistance, entry.Resolved, remaining))
                    {
                        if (Input.GetKeyDown(KeyCode.Z))
                        {
                            ApplyFieldRecovery(entry, player);
                            continue;
                        }
                        if (Input.GetKeyDown(KeyCode.X))
                        {
                            ApplyStrategicRecovery(entry);
                            continue;
                        }
                    }
                }

                if (Time.time >= entry.NextCounterOrder)
                {
                    entry.NextCounterOrder = Time.time + CounterOrderCadence;
                    if (OrderAndCheckCounterRecovery(entry)) continue;
                }
            }
        }

        private bool OrderAndCheckCounterRecovery(SalvageEntry entry)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int desired = Mathf.Min(MaxCounterRecoveryUnits, CounterRecoveryCountForRound(_round));
            EnemyTank[] chosen = new EnemyTank[MaxCounterRecoveryUnits];
            int count = 0;

            for (int slot = 0; slot < desired; slot++)
            {
                EnemyTank best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) continue;
                    bool duplicate = false;
                    for (int j = 0; j < count; j++) if (chosen[j] == enemy) { duplicate = true; break; }
                    if (duplicate) continue;
                    float distance = Vector2.Distance(enemy.transform.position, entry.Root.transform.position);
                    if (distance < bestDistance) { bestDistance = distance; best = enemy; }
                }
                if (best == null) break;
                chosen[count++] = best;
            }

            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = chosen[i];
                float distance = Vector2.Distance(enemy.transform.position, entry.Root.transform.position);
                if (CanEnemyReclaim(distance, entry.Resolved, entry.ExpiresAt - Time.time))
                {
                    RestoreEnemyReserve(entry.ReserveKind);
                    ResolveEntry(entry, SalvageRecoveryMode.EnemyReclaimed);
                    return true;
                }

                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent == null)
                {
                    agent = enemy.gameObject.AddComponent<TacticalNavigationAgent>();
                    agent.Initialize(enemy);
                }
                agent.SetRole(SquadTacticalRole.Hunter);
                agent.SetOrder(entry.Root.transform.position, 0.38f, 1.02f, enemies);
            }
            return false;
        }

        private void ApplyFieldRecovery(SalvageEntry entry, PlayerTank player)
        {
            AmmoType ammo = FieldAmmoForReserve(entry.ReserveKind);
            int ammoAmount = FieldAmmoAmount + (entry.LogisticsGrade ? 1 : 0);
            player.AddAmmo(ammo, ammoAmount);
            player.Health?.Heal(FieldRepairAmount);

            Health eagle = BaseHealthField.GetValue(_game) as Health;
            if (entry.ReserveKind == StrategicReserveKind.FireSupport || entry.LogisticsGrade)
                eagle?.Heal(FieldRepairAmount);

            int bonds = BondsForRecovery(entry.LogisticsGrade, SalvageRecoveryMode.Field);
            if (bonds > 0) WarEconomyDirector.AwardMissionBonds(bonds, "FIELD SALVAGE SECURED");
            _status = "FIELD RECOVERY // +" + ammoAmount + " " + ammo + " // REPAIR +" + FieldRepairAmount;
            _statusUntil = Time.unscaledTime + 3.0f;
            ResolveEntry(entry, SalvageRecoveryMode.Field);
        }

        private void ApplyStrategicRecovery(SalvageEntry entry)
        {
            int bonds = BondsForRecovery(entry.LogisticsGrade, SalvageRecoveryMode.Strategic);
            WarEconomyDirector.AwardMissionBonds(bonds, "STRATEGIC SALVAGE EXTRACTED");
            _status = "STRATEGIC EXTRACTION // +" + bonds + " WAR BONDS // ENEMY RECOVERY DENIED";
            _statusUntil = Time.unscaledTime + 3.0f;
            ResolveEntry(entry, SalvageRecoveryMode.Strategic);
        }

        private void RestoreEnemyReserve(StrategicReserveKind reserveKind)
        {
            if (_logistics == null || RestoreReserveMethod == null) return;
            RestoreReserveMethod.Invoke(_logistics, new object[] { reserveKind, EnemyReclaimReserveRestore });
        }

        private void ResolveEntry(SalvageEntry entry, SalvageRecoveryMode mode)
        {
            if (entry == null || entry.Resolved) return;
            entry.Resolved = true;
            if (mode == SalvageRecoveryMode.EnemyReclaimed)
            {
                _status = "ENEMY RECOVERY TEAM RECLAIMED SALVAGE // RESERVE +1";
                _statusUntil = Time.unscaledTime + 3.2f;
            }
            if (entry.Root != null)
            {
                if (mode != SalvageRecoveryMode.Expired)
                    VisualFactory.RingPulse(entry.Root.transform.position, mode == SalvageRecoveryMode.EnemyReclaimed ? new Color(1f, 0.24f, 0.16f) : new Color(0.22f, 0.95f, 0.72f), 0.85f);
                Destroy(entry.Root);
            }
            _salvage.Remove(entry);
        }

        private void ResetRun()
        {
            _round = 0;
            _enemyDropsThisRound = 0;
            UnhookLogisticsNode();
            ClearSalvage();
            _status = string.Empty;
            _statusUntil = 0f;
        }

        private void ClearSalvage()
        {
            for (int i = 0; i < _salvage.Count; i++)
                if (_salvage[i] != null && _salvage[i].Root != null) Destroy(_salvage[i].Root);
            _salvage.Clear();
        }

        private static Color ReserveColor(StrategicReserveKind kind)
        {
            switch (kind)
            {
                case StrategicReserveKind.Armor: return new Color(0.92f, 0.60f, 0.20f);
                case StrategicReserveKind.ElectronicWarfare: return new Color(0.35f, 0.78f, 1f);
                default: return new Color(1f, 0.32f, 0.18f);
            }
        }

        private SalvageEntry NearestRecoverable(PlayerTank player, out float distance)
        {
            SalvageEntry best = null;
            distance = float.MaxValue;
            if (player == null) return null;
            for (int i = 0; i < _salvage.Count; i++)
            {
                SalvageEntry entry = _salvage[i];
                if (entry == null || entry.Root == null || entry.Resolved) continue;
                float d = Vector2.Distance(player.transform.position, entry.Root.transform.position);
                if (d < distance) { distance = d; best = entry; }
            }
            return best;
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            SalvageEntry near = NearestRecoverable(player, out float distance);
            if (near != null && distance <= InteractionRange)
            {
                string grade = near.LogisticsGrade ? "LOGISTICS CACHE" : "BATTLEFIELD SALVAGE";
                string ammo = FieldAmmoForReserve(near.ReserveKind).ToString();
                GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height - 112f, 500f, 52f),
                    grade + " // Z: FIELD (REPAIR + " + ammo + ")    X: STRATEGIC (WAR BONDS)", _promptStyle);
            }
            if (!string.IsNullOrEmpty(_status) && Time.unscaledTime < _statusUntil)
                GUI.Label(new Rect(Screen.width * 0.5f - 245f, 72f, 490f, 38f), _status, _statusStyle);
        }

        private void EnsureStyles()
        {
            if (_promptStyle != null) return;
            _promptStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _statusStyle = new GUIStyle(_promptStyle) { fontSize = 13 };
        }
    }

    public sealed class BattlefieldSalvageDeathTracker : MonoBehaviour
    {
        private BattlefieldSalvageDirector _director;
        private EnemyTank _enemy;
        private bool _notified;

        public void Initialize(BattlefieldSalvageDirector director, EnemyTank enemy)
        {
            _director = director;
            _enemy = enemy;
            if (_enemy != null && _enemy.Health != null) _enemy.Health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (_enemy != null && _enemy.Health != null) _enemy.Health.Died -= OnDied;
        }

        private void OnDied(Health health)
        {
            if (_notified || _director == null || _enemy == null) return;
            _notified = true;
            _director.NotifyEnemyDestroyed(_enemy.Kind, transform.position);
        }
    }
}
