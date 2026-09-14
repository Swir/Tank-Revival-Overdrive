using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(470)]
    public sealed class SupplyRouteWarfareDirector : MonoBehaviour
    {
        public const int MaxEscorts = 4;
        public const float EscortOrderCadence = 0.42f;
        public const float EscortStandoff = 1.65f;
        public const float EscortSpeedScale = 0.94f;
        public const float RepairCadence = 3.25f;
        public const float RepairRange = 2.85f;
        public const int RepairAmount = 1;
        public const int MaxRepairsPerNode = 4;
        public const float RerouteCooldown = 5.5f;
        public const int MaxReroutesPerConvoy = 2;
        public const float RerouteVerticalShift = 0.85f;
        public const float RerouteDamageFraction = 0.22f;
        public const int CaptureBondReward = 2;
        public const int CaptureRepairAmount = 1;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo NodeField = typeof(LogisticsNetworkDirector).GetField("_node", PrivateInstance);
        private static readonly FieldInfo NodeHealthField = typeof(LogisticsNetworkDirector).GetField("_nodeHealth", PrivateInstance);
        private static readonly FieldInfo NodeKindField = typeof(LogisticsNetworkDirector).GetField("_nodeKind", PrivateInstance);
        private static readonly FieldInfo ConvoyDirectionField = typeof(LogisticsNetworkDirector).GetField("_convoyDirection", PrivateInstance);
        private static readonly FieldInfo StatusField = typeof(LogisticsNetworkDirector).GetField("_status", PrivateInstance);
        private static readonly FieldInfo StatusUntilField = typeof(LogisticsNetworkDirector).GetField("_statusUntil", PrivateInstance);
        private static readonly FieldInfo BaseHealthField = typeof(TankGame).GetField("_baseHealth", PrivateInstance);

        private static SupplyRouteWarfareDirector _instance;
        private readonly EnemyTank[] _escorts = new EnemyTank[MaxEscorts];
        private TankGame _game;
        private LogisticsNetworkDirector _logistics;
        private GameObject _trackedNode;
        private Health _trackedHealth;
        private LogisticsNodeKind _trackedKind;
        private int _escortCount;
        private int _repairsUsed;
        private int _reroutesUsed;
        private int _damageSinceReroute;
        private bool _captureResolved;
        private float _nextEscortOrder;
        private float _nextRepair;
        private float _nextReroute;

        public static SupplyRouteWarfareDirector Instance => _instance;
        public int ActiveEscortCount => _escortCount;
        public int RepairsUsed => _repairsUsed;
        public int ReroutesUsed => _reroutesUsed;
        public static bool BridgeAvailable => NodeField != null && NodeHealthField != null && NodeKindField != null &&
            ConvoyDirectionField != null && StatusField != null && StatusUntilField != null && BaseHealthField != null;
        public static bool ConfigurationValid => MaxEscorts >= 3 && MaxEscorts <= 5 && EscortOrderCadence >= 0.30f && EscortOrderCadence <= 0.60f &&
            EscortStandoff >= 1.2f && EscortStandoff <= 2.2f && EscortSpeedScale >= 0.82f && EscortSpeedScale <= 1.02f &&
            RepairCadence >= 2.5f && RepairCadence <= 4.5f && RepairRange >= 2.2f && RepairRange <= 3.4f && RepairAmount == 1 &&
            MaxRepairsPerNode >= 2 && MaxRepairsPerNode <= 5 && RerouteCooldown >= 4f && RerouteCooldown <= 8f &&
            MaxReroutesPerConvoy >= 1 && MaxReroutesPerConvoy <= 3 && RerouteVerticalShift >= 0.5f && RerouteVerticalShift <= 1.1f &&
            RerouteDamageFraction >= 0.18f && RerouteDamageFraction <= 0.30f && CaptureBondReward >= 1 && CaptureBondReward <= 4 &&
            CaptureRepairAmount == 1 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SupplyRouteWarfareDirector>() != null) return;
            GameObject go = new GameObject("SupplyRouteWarfareDirector_v8_7");
            DontDestroyOnLoad(go);
            go.AddComponent<SupplyRouteWarfareDirector>();
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
            UnhookTrackedNode();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_logistics == null) _logistics = LogisticsNetworkDirector.Instance;
            if (_game == null || _logistics == null || !_game.IsPlaying)
            {
                if (_trackedNode != null) ClearNodeState();
                return;
            }

            GameObject node = NodeField.GetValue(_logistics) as GameObject;
            Health health = NodeHealthField.GetValue(_logistics) as Health;
            if (node != _trackedNode) TrackNode(node, health);
            if (_trackedNode == null || _trackedHealth == null || _trackedHealth.IsDead) return;

            if (Time.time >= _nextEscortOrder)
            {
                _nextEscortOrder = Time.time + EscortOrderCadence;
                AssignAndOrderEscorts();
            }

            if (Time.time >= _nextRepair)
            {
                _nextRepair = Time.time + RepairCadence;
                TryRepairNode();
            }
        }

        public static int EscortCountForRound(int round)
        {
            if (!LogisticsNetworkDirector.HasLogisticsForRound(round)) return 0;
            if (round < 30) return 2;
            if (round < 60) return 3;
            return MaxEscorts;
        }

        public static bool ShouldReroute(LogisticsNodeKind kind, int accumulatedDamage, int maximumHealth, int reroutesUsed, float secondsSinceLastReroute)
        {
            if (kind != LogisticsNodeKind.MobileConvoy || maximumHealth <= 0 || reroutesUsed >= MaxReroutesPerConvoy || secondsSinceLastReroute < RerouteCooldown) return false;
            int threshold = Mathf.Max(2, Mathf.CeilToInt(maximumHealth * RerouteDamageFraction));
            return accumulatedDamage >= threshold;
        }

        public static bool CanRepairNode(int current, int maximum, int repairsUsed, float nearestEscortDistance)
        {
            return current > 0 && current < maximum && repairsUsed < MaxRepairsPerNode && nearestEscortDistance <= RepairRange;
        }

        public static int CapturedSupplyRepair(LogisticsNodeKind kind)
        {
            return kind == LogisticsNodeKind.SupplyDepot || kind == LogisticsNodeKind.MobileConvoy || kind == LogisticsNodeKind.RepairHub ? CaptureRepairAmount : 0;
        }

        private void TrackNode(GameObject node, Health health)
        {
            UnhookTrackedNode();
            _trackedNode = node;
            _trackedHealth = health;
            _escortCount = 0;
            _repairsUsed = 0;
            _reroutesUsed = 0;
            _damageSinceReroute = 0;
            _captureResolved = false;
            _nextEscortOrder = 0f;
            _nextRepair = Time.time + RepairCadence;
            _nextReroute = Time.time;
            for (int i = 0; i < _escorts.Length; i++) _escorts[i] = null;
            if (_trackedNode == null || _trackedHealth == null) return;
            _trackedKind = (LogisticsNodeKind)NodeKindField.GetValue(_logistics);
            _trackedHealth.Damaged += OnNodeDamaged;
            _trackedHealth.Died += OnNodeDied;
            SetLogisticsStatus("COUNTER-INTERDICTION // ESCORT SCREEN DEPLOYING", 3.4f);
        }

        private void UnhookTrackedNode()
        {
            if (_trackedHealth == null) return;
            _trackedHealth.Damaged -= OnNodeDamaged;
            _trackedHealth.Died -= OnNodeDied;
        }

        private void ClearNodeState()
        {
            UnhookTrackedNode();
            _trackedNode = null;
            _trackedHealth = null;
            _escortCount = 0;
            for (int i = 0; i < _escorts.Length; i++) _escorts[i] = null;
        }

        private void AssignAndOrderEscorts()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int desired = Mathf.Min(MaxEscorts, EscortCountForRound(_game.CurrentRound));
            _escortCount = 0;

            for (int slot = 0; slot < desired; slot++)
            {
                EnemyTank best = FindBestEscort(enemies, slot);
                if (best == null) break;
                _escorts[_escortCount++] = best;
            }
            for (int i = _escortCount; i < _escorts.Length; i++) _escorts[i] = null;

            for (int i = 0; i < _escortCount; i++)
            {
                EnemyTank escort = _escorts[i];
                if (escort == null || escort.Health == null || escort.Health.IsDead) continue;
                TacticalNavigationAgent agent = escort.GetComponent<TacticalNavigationAgent>();
                if (agent == null)
                {
                    agent = escort.gameObject.AddComponent<TacticalNavigationAgent>();
                    agent.Initialize(escort);
                }
                agent.SetRole(SquadTacticalRole.Escort);
                Vector2 offset = EscortOffset(i, _escortCount);
                agent.SetOrder((Vector2)_trackedNode.transform.position + offset, EscortStandoff, EscortSpeedScale, enemies);
            }
        }

        private EnemyTank FindBestEscort(EnemyTank[] enemies, int slot)
        {
            EnemyTank best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) continue;
                bool already = false;
                for (int j = 0; j < slot && j < _escortCount; j++) if (_escorts[j] == enemy) { already = true; break; }
                if (already) continue;
                float classBias = enemy.Kind == EnemyKind.Heavy ? -2.4f : enemy.Kind == EnemyKind.Elite ? -2.0f : enemy.Kind == EnemyKind.Fast ? -1.1f : 0f;
                float distance = Vector2.Distance(enemy.transform.position, _trackedNode.transform.position);
                float score = distance + classBias;
                if (score < bestScore) { bestScore = score; best = enemy; }
            }
            return best;
        }

        private static Vector2 EscortOffset(int index, int count)
        {
            if (count <= 1) return Vector2.zero;
            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.72f;
        }

        private void TryRepairNode()
        {
            if (_trackedHealth == null || _trackedHealth.IsDead || _repairsUsed >= MaxRepairsPerNode) return;
            float nearest = NearestLiveEscortDistance();
            if (!CanRepairNode(_trackedHealth.Current, _trackedHealth.Maximum, _repairsUsed, nearest)) return;
            _trackedHealth.Heal(RepairAmount);
            _repairsUsed++;
            VisualFactory.RingPulse(_trackedNode.transform.position, new Color(0.35f, 1f, 0.62f), 0.72f);
            SetLogisticsStatus("FIELD REPAIR TEAM // LOGISTICS NODE +" + RepairAmount + " HP", 2.2f);
        }

        private float NearestLiveEscortDistance()
        {
            float best = float.MaxValue;
            for (int i = 0; i < _escortCount; i++)
            {
                EnemyTank escort = _escorts[i];
                if (escort == null || escort.Health == null || escort.Health.IsDead) continue;
                best = Mathf.Min(best, Vector2.Distance(escort.transform.position, _trackedNode.transform.position));
            }
            return best;
        }

        private void OnNodeDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            _damageSinceReroute += Mathf.Max(0, amount);
            if (Time.time < _nextReroute) return;
            float elapsed = RerouteCooldown + 0.01f;
            if (!ShouldReroute(_trackedKind, _damageSinceReroute, health.Maximum, _reroutesUsed, elapsed)) return;
            ExecuteEmergencyReroute();
        }

        private void ExecuteEmergencyReroute()
        {
            if (_trackedNode == null || _trackedKind != LogisticsNodeKind.MobileConvoy) return;
            float direction = (float)ConvoyDirectionField.GetValue(_logistics);
            ConvoyDirectionField.SetValue(_logistics, -direction);
            Vector3 p = _trackedNode.transform.position;
            float sign = ((_game.CurrentRound + _reroutesUsed) & 1) == 0 ? 1f : -1f;
            p.y = Mathf.Clamp(p.y + sign * RerouteVerticalShift, 1.4f, 5.4f);
            _trackedNode.transform.position = p;
            _reroutesUsed++;
            _damageSinceReroute = 0;
            _nextReroute = Time.time + RerouteCooldown;
            VisualFactory.RingPulse(p, new Color(1f, 0.82f, 0.22f), 1.0f);
            SetLogisticsStatus("CONVOY AMBUSH DETECTED // EMERGENCY REROUTE", 3.2f);
        }

        private void OnNodeDied(Health health)
        {
            if (_captureResolved) return;
            _captureResolved = true;
            ApplyCapturedSupplies(_trackedKind);
        }

        private void ApplyCapturedSupplies(LogisticsNodeKind kind)
        {
            WarEconomyDirector.AwardMissionBonds(CaptureBondReward, "CAPTURED ENEMY SUPPLIES");
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            Health baseHealth = _game == null ? null : BaseHealthField.GetValue(_game) as Health;
            if (kind == LogisticsNodeKind.MobileConvoy)
            {
                player?.Health?.Heal(CaptureRepairAmount);
            }
            else if (kind == LogisticsNodeKind.SupplyDepot)
            {
                baseHealth?.Heal(CaptureRepairAmount);
            }
            else
            {
                player?.Health?.Heal(CaptureRepairAmount);
                baseHealth?.Heal(CaptureRepairAmount);
            }
        }

        private void SetLogisticsStatus(string text, float duration)
        {
            if (_logistics == null || StatusField == null || StatusUntilField == null) return;
            StatusField.SetValue(_logistics, text);
            StatusUntilField.SetValue(_logistics, Time.unscaledTime + duration);
        }
    }
}
