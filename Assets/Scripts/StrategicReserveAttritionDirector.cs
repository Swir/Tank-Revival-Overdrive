using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum StrategicReserveKind
    {
        Armor,
        FireSupport,
        ElectronicWarfare
    }

    public readonly struct StrategicReserveSnapshot
    {
        public readonly int Armor;
        public readonly int ArmorCapacity;
        public readonly int FireSupport;
        public readonly int FireSupportCapacity;
        public readonly int ElectronicWarfare;
        public readonly int ElectronicWarfareCapacity;

        public StrategicReserveSnapshot(int armor, int armorCapacity, int fireSupport, int fireSupportCapacity, int ew, int ewCapacity)
        {
            Armor = armor;
            ArmorCapacity = armorCapacity;
            FireSupport = fireSupport;
            FireSupportCapacity = fireSupportCapacity;
            ElectronicWarfare = ew;
            ElectronicWarfareCapacity = ewCapacity;
        }

        public float ArmorRatio => ArmorCapacity <= 0 ? 0f : Mathf.Clamp01((float)Armor / ArmorCapacity);
        public float FireSupportRatio => FireSupportCapacity <= 0 ? 0f : Mathf.Clamp01((float)FireSupport / FireSupportCapacity);
        public float ElectronicWarfareRatio => ElectronicWarfareCapacity <= 0 ? 0f : Mathf.Clamp01((float)ElectronicWarfare / ElectronicWarfareCapacity);
    }

    [DefaultExecutionOrder(290)]
    public sealed class StrategicReserveAttritionDirector : MonoBehaviour
    {
        public const int SectorCount = 10;
        public const int ArmorBase = 12;
        public const int FireSupportBase = 10;
        public const int ElectronicWarfareBase = 8;
        public const int VictoryReinforcementPenalty = 2;
        public const int DefeatReinforcementBonus = 3;
        public const float CarryoverRatio = 0.45f;
        public const float LowReserveThreshold = 0.35f;
        public const float CriticalReserveThreshold = 0.15f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);

        private static StrategicReserveAttritionDirector _instance;
        private readonly StrategicReserveSnapshot[] _sectorSnapshots = new StrategicReserveSnapshot[SectorCount];
        private readonly bool[] _sectorInitialized = new bool[SectorCount];
        private TankGame _game;
        private int _lastRound;
        private int _activeSector = -1;
        private int _armor;
        private int _armorCapacity;
        private int _fireSupport;
        private int _fireSupportCapacity;
        private int _ew;
        private int _ewCapacity;
        private float _briefUntil;
        private string _brief = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;

        public static StrategicReserveAttritionDirector Instance => _instance;
        public StrategicReserveSnapshot Current => new StrategicReserveSnapshot(_armor, _armorCapacity, _fireSupport, _fireSupportCapacity, _ew, _ewCapacity);
        public static bool BridgeAvailable => EncounterProfileField != null && EncounterNextStrikeField != null;
        public static bool ConfigurationValid =>
            SectorCount == 10 && ArmorBase >= 8 && FireSupportBase >= 6 && ElectronicWarfareBase >= 5 &&
            VictoryReinforcementPenalty >= 1 && VictoryReinforcementPenalty <= 4 &&
            DefeatReinforcementBonus >= 1 && DefeatReinforcementBonus <= 5 &&
            CarryoverRatio >= 0.30f && CarryoverRatio <= 0.60f &&
            CriticalReserveThreshold > 0f && CriticalReserveThreshold < LowReserveThreshold && LowReserveThreshold <= 0.40f &&
            BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<StrategicReserveAttritionDirector>() != null) return;
            GameObject go = new GameObject("StrategicReserveAttritionDirector_v8_5");
            DontDestroyOnLoad(go);
            go.AddComponent<StrategicReserveAttritionDirector>();
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
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_lastRound != 0) ResetRunState();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            int sector = WarStateCampaignMemoryDirector.SectorForRound(round);
            if (sector != _activeSector)
                EnterSector(sector);

            if (round != _lastRound)
            {
                _lastRound = round;
                ApplyReservePressure(round);
                _brief = BuildBrief(Current);
                _briefUntil = Time.unscaledTime + 3.8f;
            }

            AttachTrackers();
        }

        public StrategicReserveSnapshot GetSectorSnapshot(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return default;
            if (sector == _activeSector) return Current;
            return _sectorSnapshots[sector];
        }

        public void Consume(StrategicReserveKind kind, int amount)
        {
            amount = Mathf.Clamp(amount, 1, 4);
            switch (kind)
            {
                case StrategicReserveKind.Armor:
                    _armor = Mathf.Max(0, _armor - amount);
                    break;
                case StrategicReserveKind.FireSupport:
                    _fireSupport = Mathf.Max(0, _fireSupport - amount);
                    break;
                case StrategicReserveKind.ElectronicWarfare:
                    _ew = Mathf.Max(0, _ew - amount);
                    break;
            }
            StoreSnapshot();
        }

        public static StrategicReserveSnapshot ResolveSectorEntry(int sector, StrategicReserveSnapshot previous, SectorWarState previousResult)
        {
            sector = Mathf.Clamp(sector, 0, SectorCount - 1);
            int escalation = sector / 2;
            int armorCap = ArmorBase + sector + escalation;
            int supportCap = FireSupportBase + sector;
            int ewCap = ElectronicWarfareBase + escalation;

            if (sector == 0)
                return new StrategicReserveSnapshot(armorCap, armorCap, supportCap, supportCap, ewCap, ewCap);

            int resultShift = previousResult == SectorWarState.Victory ? -VictoryReinforcementPenalty :
                              previousResult == SectorWarState.Defeat ? DefeatReinforcementBonus : 0;

            int armorCarry = Mathf.RoundToInt(previous.Armor * CarryoverRatio);
            int supportCarry = Mathf.RoundToInt(previous.FireSupport * CarryoverRatio);
            int ewCarry = Mathf.RoundToInt(previous.ElectronicWarfare * CarryoverRatio);

            int armor = Mathf.Clamp(armorCarry + (armorCap / 2) + resultShift, 0, armorCap);
            int support = Mathf.Clamp(supportCarry + (supportCap / 2) + resultShift, 0, supportCap);
            int ew = Mathf.Clamp(ewCarry + (ewCap / 2) + resultShift, 0, ewCap);
            return new StrategicReserveSnapshot(armor, armorCap, support, supportCap, ew, ewCap);
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, StrategicReserveSnapshot reserves)
        {
            float armor = reserves.ArmorRatio;
            float supportReserve = reserves.FireSupportRatio;
            float ew = reserves.ElectronicWarfareRatio;

            float healthFactor = Mathf.Lerp(0.90f, 1f, armor);
            float supportFactor = Mathf.Lerp(0.82f, 1f, supportReserve);
            float cadenceFactor = Mathf.Lerp(1.14f, 1f, supportReserve);
            if (baseline.BossRound)
            {
                healthFactor = Mathf.Lerp(0.95f, 1f, armor);
                supportFactor = Mathf.Lerp(0.90f, 1f, supportReserve);
                cadenceFactor = Mathf.Lerp(1.07f, 1f, supportReserve);
            }

            float health = Mathf.Clamp(baseline.HealthMultiplier * healthFactor, 0.76f, 1.55f);
            float fireSupport = Mathf.Clamp(baseline.FireSupportMultiplier * supportFactor, 0.70f, 1.65f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * cadenceFactor, 4.2f, 16.5f);
            bool strikes = baseline.StrategicStrikes && (baseline.BossRound || supportReserve > CriticalReserveThreshold);
            bool champion = baseline.ChampionEnabled && (baseline.BossRound || ew > CriticalReserveThreshold);

            string reserveState = armor <= CriticalReserveThreshold || supportReserve <= CriticalReserveThreshold || ew <= CriticalReserveThreshold
                ? " // RESERVES CRITICAL"
                : armor <= LowReserveThreshold || supportReserve <= LowReserveThreshold || ew <= LowReserveThreshold
                    ? " // RESERVES LOW"
                    : "";

            return new RoundEncounterProfile(
                baseline.Round, baseline.Sector, baseline.SectorRound, baseline.Archetype,
                baseline.Codename + reserveState, baseline.Objective,
                health, fireSupport, cadence, champion, strikes, baseline.BossRound);
        }

        private void EnterSector(int sector)
        {
            if (_activeSector >= 0) StoreSnapshot();
            _activeSector = sector;

            if (!_sectorInitialized[sector])
            {
                StrategicReserveSnapshot previous = sector > 0 ? _sectorSnapshots[sector - 1] : default;
                SectorWarState previousResult = sector > 0 && WarStateCampaignMemoryDirector.Instance != null
                    ? WarStateCampaignMemoryDirector.Instance.GetSectorResult(sector - 1)
                    : SectorWarState.Unresolved;
                StrategicReserveSnapshot next = ResolveSectorEntry(sector, previous, previousResult);
                LoadSnapshot(next);
                _sectorInitialized[sector] = true;
                StoreSnapshot();
            }
            else
            {
                LoadSnapshot(_sectorSnapshots[sector]);
            }
        }

        private void AttachTrackers()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (!TryReserveKind(enemy.Kind, out StrategicReserveKind kind, out int cost)) continue;
                StrategicReserveAttritionTracker tracker = enemy.GetComponent<StrategicReserveAttritionTracker>();
                if (tracker == null) tracker = enemy.gameObject.AddComponent<StrategicReserveAttritionTracker>();
                tracker.Configure(this, enemy.Health, kind, cost, _activeSector);
            }
        }

        public static bool TryReserveKind(EnemyKind kind, out StrategicReserveKind reserveKind, out int cost)
        {
            switch (kind)
            {
                case EnemyKind.Heavy:
                    reserveKind = StrategicReserveKind.Armor;
                    cost = 2;
                    return true;
                case EnemyKind.Siege:
                    reserveKind = StrategicReserveKind.FireSupport;
                    cost = 2;
                    return true;
                case EnemyKind.Sniper:
                    reserveKind = StrategicReserveKind.FireSupport;
                    cost = 1;
                    return true;
                case EnemyKind.Elite:
                    reserveKind = StrategicReserveKind.ElectronicWarfare;
                    cost = 2;
                    return true;
                default:
                    reserveKind = StrategicReserveKind.Armor;
                    cost = 0;
                    return false;
            }
        }

        internal void ReportDestroyed(StrategicReserveKind kind, int cost, int sector)
        {
            if (sector != _activeSector || _game == null || !_game.IsPlaying) return;
            Consume(kind, cost);
            StrategicReserveSnapshot now = Current;
            if (now.ArmorRatio <= CriticalReserveThreshold || now.FireSupportRatio <= CriticalReserveThreshold || now.ElectronicWarfareRatio <= CriticalReserveThreshold)
            {
                _brief = "ENEMY STRATEGIC RESERVES CRITICAL // PRESS THE ADVANTAGE";
                _briefUntil = Time.unscaledTime + 3.6f;
            }
        }

        private void ApplyReservePressure(int round)
        {
            CampaignEncounterDirector encounter = CampaignEncounterDirector.Instance;
            if (encounter == null || EncounterProfileField == null) return;
            RoundEncounterProfile baseline = encounter.ActiveProfile;
            if (baseline.Round != round) baseline = CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, Current);
            EncounterProfileField.SetValue(encounter, refined);
            if (refined.StrategicStrikes && EncounterNextStrikeField != null)
                EncounterNextStrikeField.SetValue(encounter, Time.time + Mathf.Max(3.8f, refined.StrikeCadence * 0.82f));
        }

        private void StoreSnapshot()
        {
            if (_activeSector < 0 || _activeSector >= SectorCount) return;
            _sectorSnapshots[_activeSector] = Current;
        }

        private void LoadSnapshot(StrategicReserveSnapshot snapshot)
        {
            _armor = snapshot.Armor;
            _armorCapacity = snapshot.ArmorCapacity;
            _fireSupport = snapshot.FireSupport;
            _fireSupportCapacity = snapshot.FireSupportCapacity;
            _ew = snapshot.ElectronicWarfare;
            _ewCapacity = snapshot.ElectronicWarfareCapacity;
        }

        private static string BuildBrief(StrategicReserveSnapshot snapshot)
        {
            return $"ENEMY RESERVES // ARMOR {snapshot.Armor}/{snapshot.ArmorCapacity} // FIRE {snapshot.FireSupport}/{snapshot.FireSupportCapacity} // EW {snapshot.ElectronicWarfare}/{snapshot.ElectronicWarfareCapacity}";
        }

        private void ResetRunState()
        {
            for (int i = 0; i < SectorCount; i++)
            {
                _sectorSnapshots[i] = default;
                _sectorInitialized[i] = false;
            }
            _lastRound = 0;
            _activeSector = -1;
            _armor = _armorCapacity = _fireSupport = _fireSupportCapacity = _ew = _ewCapacity = 0;
            _brief = string.Empty;
            _briefUntil = 0f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(1f, 0.70f, 0.20f);
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _body.normal.textColor = new Color(0.84f, 0.91f, 1f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime > _briefUntil || string.IsNullOrEmpty(_brief)) return;
            EnsureStyles();
            float width = Mathf.Min(700f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.86f);
            GUI.Box(new Rect(x, 240f, width, 42f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, 244f, width - 16f, 18f), "STRATEGIC ATTRITION", _header);
            GUI.Label(new Rect(x + 8f, 262f, width - 16f, 16f), _brief, _body);
        }
    }

    public sealed class StrategicReserveAttritionTracker : MonoBehaviour
    {
        private StrategicReserveAttritionDirector _director;
        private Health _health;
        private StrategicReserveKind _kind;
        private int _cost;
        private int _sector;
        private bool _configured;
        private bool _reported;

        public void Configure(StrategicReserveAttritionDirector director, Health health, StrategicReserveKind kind, int cost, int sector)
        {
            if (_configured) return;
            _director = director;
            _health = health;
            _kind = kind;
            _cost = cost;
            _sector = sector;
            _configured = true;
        }

        private void Update()
        {
            if (_reported || !_configured || _health == null || !_health.IsDead) return;
            _reported = true;
            if (_director != null) _director.ReportDestroyed(_kind, _cost, _sector);
        }
    }
}
