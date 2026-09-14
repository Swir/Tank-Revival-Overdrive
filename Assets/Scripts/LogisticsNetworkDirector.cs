using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum LogisticsNodeKind
    {
        SupplyDepot,
        MobileConvoy,
        RepairHub
    }

    [DefaultExecutionOrder(310)]
    public sealed class LogisticsNetworkDirector : MonoBehaviour
    {
        public const int EarliestRound = 12;
        public const int MaxSectorInterdiction = 6;
        public const int NodeHealthMin = 8;
        public const int NodeHealthMax = 18;
        public const int DestroyReserveCost = 3;
        public const int SurvivalRestore = 2;
        public const int RewardMin = 6;
        public const int RewardMax = 14;
        public const float ConvoySpeed = 0.85f;
        public const float InterdictionHealthFloor = 0.94f;
        public const float InterdictionSupportFloor = 0.84f;
        public const float InterdictionCadenceCeiling = 1.12f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);
        private static readonly FieldInfo ArmorField = typeof(StrategicReserveAttritionDirector).GetField("_armor", PrivateInstance);
        private static readonly FieldInfo ArmorCapacityField = typeof(StrategicReserveAttritionDirector).GetField("_armorCapacity", PrivateInstance);
        private static readonly FieldInfo FireSupportField = typeof(StrategicReserveAttritionDirector).GetField("_fireSupport", PrivateInstance);
        private static readonly FieldInfo FireSupportCapacityField = typeof(StrategicReserveAttritionDirector).GetField("_fireSupportCapacity", PrivateInstance);
        private static readonly FieldInfo EwField = typeof(StrategicReserveAttritionDirector).GetField("_ew", PrivateInstance);
        private static readonly FieldInfo EwCapacityField = typeof(StrategicReserveAttritionDirector).GetField("_ewCapacity", PrivateInstance);
        private static readonly MethodInfo StoreSnapshotMethod = typeof(StrategicReserveAttritionDirector).GetMethod("StoreSnapshot", PrivateInstance);

        private static LogisticsNetworkDirector _instance;
        private readonly int[] _sectorInterdiction = new int[StrategicReserveAttritionDirector.SectorCount];
        private readonly int[] _sectorDestroyed = new int[StrategicReserveAttritionDirector.SectorCount];
        private readonly int[] _sectorSurvived = new int[StrategicReserveAttritionDirector.SectorCount];
        private TankGame _game;
        private int _round;
        private int _activeSector = -1;
        private LogisticsNodeKind _nodeKind;
        private GameObject _node;
        private Health _nodeHealth;
        private bool _nodeResolved;
        private float _convoyDirection = 1f;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static LogisticsNetworkDirector Instance => _instance;
        public int ActiveSectorInterdiction => _activeSector < 0 ? 0 : _sectorInterdiction[_activeSector];
        public int ActiveSectorDestroyed => _activeSector < 0 ? 0 : _sectorDestroyed[_activeSector];
        public int ActiveSectorSurvived => _activeSector < 0 ? 0 : _sectorSurvived[_activeSector];
        public static bool BridgeAvailable => EncounterProfileField != null && EncounterNextStrikeField != null &&
            ArmorField != null && ArmorCapacityField != null && FireSupportField != null && FireSupportCapacityField != null &&
            EwField != null && EwCapacityField != null && StoreSnapshotMethod != null;
        public static bool ConfigurationValid => EarliestRound >= 10 && EarliestRound <= 20 && MaxSectorInterdiction >= 4 && MaxSectorInterdiction <= 8 &&
            NodeHealthMin >= 6 && NodeHealthMax <= 22 && NodeHealthMin < NodeHealthMax && DestroyReserveCost >= 2 && DestroyReserveCost <= 4 &&
            SurvivalRestore >= 1 && SurvivalRestore < DestroyReserveCost && RewardMin >= 4 && RewardMax <= 18 && RewardMin < RewardMax &&
            ConvoySpeed >= 0.5f && ConvoySpeed <= 1.2f && InterdictionHealthFloor >= 0.90f && InterdictionHealthFloor <= 0.98f &&
            InterdictionSupportFloor >= 0.78f && InterdictionSupportFloor <= 0.90f && InterdictionCadenceCeiling >= 1.05f && InterdictionCadenceCeiling <= 1.18f &&
            BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LogisticsNetworkDirector>() != null) return;
            GameObject go = new GameObject("LogisticsNetworkDirector_v8_6");
            DontDestroyOnLoad(go);
            go.AddComponent<LogisticsNetworkDirector>();
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
            UnhookNode();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round != 0) ResetRunState();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _round)
            {
                ResolvePreviousNodeAsSurvived();
                _round = round;
                _activeSector = WarStateCampaignMemoryDirector.SectorForRound(round);
                ApplyInterdictionPressure(round);
                if (HasLogisticsForRound(round)) BeginNode(round);
            }

            if (_node != null && !_nodeResolved && _nodeKind == LogisticsNodeKind.MobileConvoy)
                MoveConvoy();
        }

        public static bool HasLogisticsForRound(int round)
        {
            if (round < EarliestRound || round > 100 || round % 10 == 0) return false;
            int sectorRound = ((round - 1) % 10) + 1;
            return sectorRound == 2 || sectorRound == 7 || sectorRound == 9;
        }

        public static LogisticsNodeKind NodeKindForRound(int round)
        {
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            int sectorRound = ((Mathf.Clamp(round, 1, 100) - 1) % 10) + 1;
            int slot = sectorRound == 2 ? 0 : sectorRound == 7 ? 1 : 2;
            return (LogisticsNodeKind)((sector + slot) % 3);
        }

        public static StrategicReserveKind ReserveKindForNode(LogisticsNodeKind kind)
        {
            switch (kind)
            {
                case LogisticsNodeKind.MobileConvoy: return StrategicReserveKind.Armor;
                case LogisticsNodeKind.SupplyDepot: return StrategicReserveKind.FireSupport;
                default: return StrategicReserveKind.ElectronicWarfare;
            }
        }

        public static int HealthForRound(int round)
        {
            return Mathf.Clamp(NodeHealthMin + Mathf.Clamp(round, 1, 100) / 10, NodeHealthMin, NodeHealthMax);
        }

        public static int RewardForRound(int round)
        {
            return Mathf.Clamp(RewardMin + Mathf.Clamp(round, 1, 100) / 14, RewardMin, RewardMax);
        }

        public static StrategicReserveSnapshot ResolveReserveAfterNode(StrategicReserveSnapshot input, LogisticsNodeKind kind, bool destroyed)
        {
            int armor = input.Armor;
            int support = input.FireSupport;
            int ew = input.ElectronicWarfare;
            int delta = destroyed ? -DestroyReserveCost : SurvivalRestore;
            switch (ReserveKindForNode(kind))
            {
                case StrategicReserveKind.Armor: armor = Mathf.Clamp(armor + delta, 0, input.ArmorCapacity); break;
                case StrategicReserveKind.FireSupport: support = Mathf.Clamp(support + delta, 0, input.FireSupportCapacity); break;
                case StrategicReserveKind.ElectronicWarfare: ew = Mathf.Clamp(ew + delta, 0, input.ElectronicWarfareCapacity); break;
            }
            return new StrategicReserveSnapshot(armor, input.ArmorCapacity, support, input.FireSupportCapacity, ew, input.ElectronicWarfareCapacity);
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, int interdiction)
        {
            float pressure = Mathf.Clamp01(interdiction / (float)MaxSectorInterdiction);
            if (baseline.BossRound) pressure *= 0.45f;
            float health = Mathf.Clamp(baseline.HealthMultiplier * Mathf.Lerp(1f, InterdictionHealthFloor, pressure), 0.72f, 1.55f);
            float support = Mathf.Clamp(baseline.FireSupportMultiplier * Mathf.Lerp(1f, InterdictionSupportFloor, pressure), 0.66f, 1.65f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * Mathf.Lerp(1f, InterdictionCadenceCeiling, pressure), 4.2f, 17.5f);
            bool champion = baseline.ChampionEnabled && (baseline.BossRound || pressure < 0.72f);
            bool strikes = baseline.StrategicStrikes && (baseline.BossRound || pressure < 0.88f);
            string suffix = pressure >= 0.66f ? " // LOGISTICS BROKEN" : pressure >= 0.33f ? " // SUPPLY DISRUPTED" : string.Empty;
            return new RoundEncounterProfile(baseline.Round, baseline.Sector, baseline.SectorRound, baseline.Archetype,
                baseline.Codename + suffix, baseline.Objective, health, support, cadence, champion, strikes, baseline.BossRound);
        }

        private void BeginNode(int round)
        {
            _nodeKind = NodeKindForRound(round);
            _nodeResolved = false;
            _node = new GameObject("ENEMY_LOGISTICS_" + _nodeKind.ToString().ToUpperInvariant());
            Vector3 position = SpawnPosition(round, _nodeKind);
            _node.transform.position = position;
            Color color = NodeColor(_nodeKind);

            VisualFactory.Rect("Base", _node.transform, new Vector2(_nodeKind == LogisticsNodeKind.MobileConvoy ? 1.30f : 1.12f, 0.72f), new Color(0.12f, 0.14f, 0.16f), Vector3.zero, 8);
            VisualFactory.Rect("Cargo", _node.transform, new Vector2(0.72f, 0.42f), color, new Vector3(0f, 0.08f, 0f), 9);
            if (_nodeKind == LogisticsNodeKind.MobileConvoy)
            {
                VisualFactory.Disc("WheelL", _node.transform, new Vector2(0.24f, 0.24f), Color.black, new Vector3(-0.42f, -0.34f, 0f), 10);
                VisualFactory.Disc("WheelR", _node.transform, new Vector2(0.24f, 0.24f), Color.black, new Vector3(0.42f, -0.34f, 0f), 10);
                _convoyDirection = position.x >= 0f ? -1f : 1f;
            }
            else
            {
                VisualFactory.Rect("Mast", _node.transform, new Vector2(0.08f, 0.66f), Color.white, new Vector3(0.32f, 0.52f, 0f), 10);
                VisualFactory.Disc("Beacon", _node.transform, new Vector2(0.22f, 0.22f), color, new Vector3(0.32f, 0.90f, 0f), 11);
            }

            BoxCollider2D collider = _node.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(_nodeKind == LogisticsNodeKind.MobileConvoy ? 1.28f : 1.08f, 0.70f);
            _nodeHealth = _node.AddComponent<Health>();
            _nodeHealth.Initialize(Team.Enemy, HealthForRound(round));
            _nodeHealth.Damaged += OnNodeDamaged;
            _nodeHealth.Died += OnNodeDied;
            VisualFactory.RingPulse(position, color, 1.55f);
            _status = NodeLabel(_nodeKind) + " // INTERDICT ENEMY SUPPLY // " + _nodeHealth.Current + " HP";
            _statusUntil = Time.unscaledTime + 5.5f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.26f, -0.10f);
        }

        private void OnNodeDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            _status = NodeLabel(_nodeKind) + " HIT // " + health.Current + "/" + health.Maximum + " HP";
            _statusUntil = Time.unscaledTime + 2.2f;
            VisualFactory.RingPulse(health.transform.position, NodeColor(_nodeKind), 0.62f);
        }

        private void OnNodeDied(Health health)
        {
            ResolveNodeDestroyed();
        }

        private void ResolveNodeDestroyed()
        {
            if (_nodeResolved) return;
            _nodeResolved = true;
            int sector = Mathf.Clamp(_activeSector, 0, _sectorInterdiction.Length - 1);
            _sectorInterdiction[sector] = Mathf.Min(MaxSectorInterdiction, _sectorInterdiction[sector] + 1);
            _sectorDestroyed[sector]++;
            StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
            if (reserves != null) reserves.Consume(ReserveKindForNode(_nodeKind), DestroyReserveCost);
            WarEconomyDirector.AwardMissionBonds(RewardForRound(_round), "ENEMY LOGISTICS INTERDICTED");
            _status = NodeLabel(_nodeKind) + " DESTROYED // ENEMY REPLENISHMENT CUT";
            _statusUntil = Time.unscaledTime + 4.2f;
            if (_node != null)
            {
                VisualFactory.Explosion(_node.transform.position, NodeColor(_nodeKind), 1.32f);
                Destroy(_node);
            }
            UnhookNode();
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.40f, 0f);
        }

        private void ResolvePreviousNodeAsSurvived()
        {
            if (_node == null || _nodeResolved || _nodeHealth == null || _nodeHealth.IsDead)
            {
                CleanupNode();
                return;
            }
            _nodeResolved = true;
            int sector = Mathf.Clamp(_activeSector, 0, _sectorInterdiction.Length - 1);
            _sectorInterdiction[sector] = Mathf.Max(0, _sectorInterdiction[sector] - 1);
            _sectorSurvived[sector]++;
            RestoreReserve(ReserveKindForNode(_nodeKind), SurvivalRestore);
            _status = NodeLabel(_nodeKind) + " ESCAPED // ENEMY RESERVES REPLENISHED";
            _statusUntil = Time.unscaledTime + 3.0f;
            CleanupNode();
        }

        private void RestoreReserve(StrategicReserveKind kind, int amount)
        {
            StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
            if (reserves == null || !BridgeAvailable) return;
            FieldInfo valueField;
            FieldInfo capacityField;
            switch (kind)
            {
                case StrategicReserveKind.Armor: valueField = ArmorField; capacityField = ArmorCapacityField; break;
                case StrategicReserveKind.FireSupport: valueField = FireSupportField; capacityField = FireSupportCapacityField; break;
                default: valueField = EwField; capacityField = EwCapacityField; break;
            }
            int current = (int)valueField.GetValue(reserves);
            int capacity = (int)capacityField.GetValue(reserves);
            valueField.SetValue(reserves, Mathf.Clamp(current + amount, 0, capacity));
            StoreSnapshotMethod.Invoke(reserves, null);
        }

        private void ApplyInterdictionPressure(int round)
        {
            CampaignEncounterDirector encounter = CampaignEncounterDirector.Instance;
            if (encounter == null || EncounterProfileField == null) return;
            RoundEncounterProfile baseline = encounter.ActiveProfile;
            if (baseline.Round != round) baseline = CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, _sectorInterdiction[Mathf.Clamp(_activeSector, 0, _sectorInterdiction.Length - 1)]);
            EncounterProfileField.SetValue(encounter, refined);
            if (refined.StrategicStrikes && EncounterNextStrikeField != null)
                EncounterNextStrikeField.SetValue(encounter, Time.time + Mathf.Max(4f, refined.StrikeCadence * 0.86f));
        }

        private void MoveConvoy()
        {
            Vector3 p = _node.transform.position;
            p.x += _convoyDirection * ConvoySpeed * Time.deltaTime;
            if (p.x > 7.0f) { p.x = 7.0f; _convoyDirection = -1f; }
            else if (p.x < -7.0f) { p.x = -7.0f; _convoyDirection = 1f; }
            _node.transform.position = p;
        }

        private static Vector3 SpawnPosition(int round, LogisticsNodeKind kind)
        {
            int seed = round * 7919 + (int)kind * 503 + 86;
            System.Random rng = new System.Random(seed);
            float x = (float)(rng.NextDouble() * 11.0 - 5.5);
            float y = (float)(rng.NextDouble() * 1.8 + 2.3);
            return new Vector3(x, y, 0f);
        }

        private static string NodeLabel(LogisticsNodeKind kind)
        {
            switch (kind)
            {
                case LogisticsNodeKind.SupplyDepot: return "FIRE-SUPPORT DEPOT";
                case LogisticsNodeKind.MobileConvoy: return "ARMORED SUPPLY CONVOY";
                default: return "EW REPAIR HUB";
            }
        }

        private static Color NodeColor(LogisticsNodeKind kind)
        {
            switch (kind)
            {
                case LogisticsNodeKind.SupplyDepot: return new Color(1f, 0.48f, 0.12f);
                case LogisticsNodeKind.MobileConvoy: return new Color(0.90f, 0.78f, 0.18f);
                default: return new Color(0.32f, 0.82f, 1f);
            }
        }

        private void CleanupNode()
        {
            UnhookNode();
            if (_node != null) Destroy(_node);
            _node = null;
            _nodeHealth = null;
        }

        private void UnhookNode()
        {
            if (_nodeHealth == null) return;
            _nodeHealth.Damaged -= OnNodeDamaged;
            _nodeHealth.Died -= OnNodeDied;
        }

        private void ResetRunState()
        {
            CleanupNode();
            for (int i = 0; i < _sectorInterdiction.Length; i++)
            {
                _sectorInterdiction[i] = 0;
                _sectorDestroyed[i] = 0;
                _sectorSurvived[i] = 0;
            }
            _round = 0;
            _activeSector = -1;
            _nodeResolved = false;
            _status = string.Empty;
            _statusUntil = 0f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(1f, 0.72f, 0.22f);
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _body.normal.textColor = new Color(0.86f, 0.93f, 1f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime > _statusUntil || string.IsNullOrEmpty(_status)) return;
            EnsureStyles();
            float width = Mathf.Min(720f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
            GUI.Box(new Rect(x, 286f, width, 42f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, 290f, width - 16f, 18f), "STRATEGIC INTERDICTION", _header);
            GUI.Label(new Rect(x + 8f, 308f, width - 16f, 16f), _status, _body);
        }
    }
}
