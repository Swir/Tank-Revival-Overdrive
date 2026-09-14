using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum SectorWarState
    {
        Unresolved = 0,
        Victory = 1,
        Defeat = -1
    }

    [DefaultExecutionOrder(280)]
    public sealed class WarStateCampaignMemoryDirector : MonoBehaviour
    {
        public const int SectorCount = 10;
        public const int OperationResolutionSectorRound = 7;
        public const int VictoryEntryBondReward = 4;
        public const int VictoryEagleRepair = 2;
        public const int VictoryPlayerRepair = 1;
        public const int MomentumMinimum = -6;
        public const int MomentumMaximum = 6;

        public const float VictoryHealthMultiplier = 0.95f;
        public const float DefeatHealthMultiplier = 1.06f;
        public const float VictorySupportMultiplier = 0.90f;
        public const float DefeatSupportMultiplier = 1.10f;
        public const float VictoryBossHealthMultiplier = 0.97f;
        public const float DefeatBossHealthMultiplier = 1.04f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);

        private static WarStateCampaignMemoryDirector _instance;
        private readonly SectorWarState[] _sectorResults = new SectorWarState[SectorCount];
        private TankGame _game;
        private int _lastRound;
        private int _lastRecordedSector = -1;
        private int _entryRewardedSector = -1;
        private int _momentum;
        private float _briefUntil;
        private string _brief = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;

        public static WarStateCampaignMemoryDirector Instance => _instance;
        public int Momentum => _momentum;
        public static bool BridgeAvailable => EncounterProfileField != null && EncounterNextStrikeField != null;
        public static bool ConfigurationValid =>
            SectorCount == 10 && OperationResolutionSectorRound == 7 &&
            VictoryEntryBondReward >= 2 && VictoryEntryBondReward <= 8 &&
            VictoryEagleRepair >= 1 && VictoryEagleRepair <= 3 && VictoryPlayerRepair >= 0 && VictoryPlayerRepair <= 2 &&
            MomentumMinimum == -MomentumMaximum && MomentumMaximum >= 4 && MomentumMaximum <= 8 &&
            VictoryHealthMultiplier >= 0.90f && VictoryHealthMultiplier < 1f &&
            DefeatHealthMultiplier > 1f && DefeatHealthMultiplier <= 1.10f &&
            VictorySupportMultiplier >= 0.85f && VictorySupportMultiplier < 1f &&
            DefeatSupportMultiplier > 1f && DefeatSupportMultiplier <= 1.15f &&
            VictoryBossHealthMultiplier >= 0.94f && VictoryBossHealthMultiplier < 1f &&
            DefeatBossHealthMultiplier > 1f && DefeatBossHealthMultiplier <= 1.08f && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarStateCampaignMemoryDirector>() != null) return;
            GameObject go = new GameObject("WarStateCampaignMemoryDirector_v8_4");
            DontDestroyOnLoad(go);
            go.AddComponent<WarStateCampaignMemoryDirector>();
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
            if (round == _lastRound) return;
            _lastRound = round;

            int sector = SectorForRound(round);
            int sectorRound = SectorRound(round);

            if (sectorRound == OperationResolutionSectorRound && _lastRecordedSector != sector)
                CaptureOperationResult(sector);

            if (sectorRound == 1 && sector > 0 && _entryRewardedSector != sector)
                ApplySectorEntryLogistics(sector);

            SectorWarState memory = ResolveMemoryForRound(round, _sectorResults);
            if (memory != SectorWarState.Unresolved)
                ApplyCampaignMemory(round, memory);

            _brief = BuildBrief(round, memory, _momentum);
            _briefUntil = Time.unscaledTime + 3.6f;
        }

        public SectorWarState GetSectorResult(int sector)
        {
            if (sector < 0 || sector >= SectorCount) return SectorWarState.Unresolved;
            return _sectorResults[sector];
        }

        public static int SectorForRound(int round) => Mathf.Clamp((Mathf.Max(1, round) - 1) / 10, 0, SectorCount - 1);
        public static int SectorRound(int round) => ((Mathf.Max(1, round) - 1) % 10) + 1;
        public static int ClampMomentum(int value) => Mathf.Clamp(value, MomentumMinimum, MomentumMaximum);

        public static SectorWarState ResolveMemoryForRound(int round, SectorWarState[] results)
        {
            if (results == null || results.Length < SectorCount) return SectorWarState.Unresolved;
            int sector = SectorForRound(round);
            int sectorRound = SectorRound(round);
            if (sectorRound >= OperationResolutionSectorRound && results[sector] != SectorWarState.Unresolved)
                return results[sector];
            if (sector > 0) return results[sector - 1];
            return SectorWarState.Unresolved;
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, SectorWarState state, int momentum)
        {
            if (state == SectorWarState.Unresolved) return baseline;

            bool victory = state == SectorWarState.Victory;
            bool boss = baseline.BossRound;
            float momentumWeight = Mathf.Clamp(Mathf.Abs(ClampMomentum(momentum)) * 0.004f, 0f, 0.024f);
            float healthFactor;
            float supportFactor;

            if (boss)
            {
                healthFactor = victory ? VictoryBossHealthMultiplier : DefeatBossHealthMultiplier;
                supportFactor = victory ? 0.95f : 1.06f;
            }
            else
            {
                healthFactor = victory ? VictoryHealthMultiplier : DefeatHealthMultiplier;
                supportFactor = victory ? VictorySupportMultiplier : DefeatSupportMultiplier;
            }

            if (victory && momentum > 0)
            {
                healthFactor -= momentumWeight;
                supportFactor -= momentumWeight;
            }
            else if (!victory && momentum < 0)
            {
                healthFactor += momentumWeight;
                supportFactor += momentumWeight;
            }

            float health = Mathf.Clamp(baseline.HealthMultiplier * healthFactor, 0.80f, 1.52f);
            float support = Mathf.Clamp(baseline.FireSupportMultiplier * supportFactor, 0.76f, 1.62f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * (victory ? 1.06f : 0.94f), 4.2f, 15.5f);
            bool champion = victory ? baseline.ChampionEnabled : (baseline.ChampionEnabled || (!boss && baseline.Round >= 30));
            bool strikes = victory ? (baseline.StrategicStrikes && baseline.SectorRound >= 6) : (baseline.StrategicStrikes || baseline.Round >= 32);
            string suffix = victory ? " // WAR ADVANTAGE" : " // ENEMY INITIATIVE";

            return new RoundEncounterProfile(
                baseline.Round, baseline.Sector, baseline.SectorRound, baseline.Archetype,
                baseline.Codename + suffix, baseline.Objective,
                health, support, cadence, champion, strikes, baseline.BossRound);
        }

        private void CaptureOperationResult(int sector)
        {
            OperationChainDirector chain = OperationChainDirector.Instance;
            if (chain == null) return;

            SectorWarState result = chain.ChainSuccesses >= 2 ? SectorWarState.Victory : SectorWarState.Defeat;
            _sectorResults[sector] = result;
            _lastRecordedSector = sector;
            _momentum = ClampMomentum(_momentum + (result == SectorWarState.Victory ? 2 : -2));

            _brief = result == SectorWarState.Victory
                ? $"SECTOR {sector + 1} ADVANTAGE SECURED // MOMENTUM +{_momentum}"
                : $"SECTOR {sector + 1} INITIATIVE LOST // MOMENTUM {_momentum}";
            _briefUntil = Time.unscaledTime + 4.5f;
            BattleAudio.PlayGlobal(result == SectorWarState.Victory ? SoundCue.RoundClear : SoundCue.BossAlarm, result == SectorWarState.Victory ? 0.42f : 0.25f, 0f);
        }

        private void ApplySectorEntryLogistics(int sector)
        {
            _entryRewardedSector = sector;
            SectorWarState previous = _sectorResults[sector - 1];
            if (previous != SectorWarState.Victory) return;

            _game.RepairEagle(VictoryEagleRepair);
            PlayerTank player = CombatRoster.Player;
            if (player != null && player.Health != null && !player.Health.IsDead)
                player.Health.Heal(VictoryPlayerRepair);
            WarEconomyDirector.AwardMissionBonds(VictoryEntryBondReward, "SECTOR MOMENTUM SUPPLY");
        }

        private void ApplyCampaignMemory(int round, SectorWarState memory)
        {
            CampaignEncounterDirector encounter = CampaignEncounterDirector.Instance;
            if (encounter == null || EncounterProfileField == null) return;

            RoundEncounterProfile baseline = encounter.ActiveProfile;
            if (baseline.Round != round) baseline = CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, memory, _momentum);
            EncounterProfileField.SetValue(encounter, refined);
            if (refined.StrategicStrikes && EncounterNextStrikeField != null)
                EncounterNextStrikeField.SetValue(encounter, Time.time + Mathf.Max(3.2f, refined.StrikeCadence * 0.80f));
        }

        private static string BuildBrief(int round, SectorWarState state, int momentum)
        {
            if (state == SectorWarState.Unresolved) return "WAR STATE // SECTOR OUTCOME PENDING";
            string stateText = state == SectorWarState.Victory ? "ADVANTAGE" : "ENEMY INITIATIVE";
            string inherited = SectorRound(round) < OperationResolutionSectorRound ? "INHERITED" : "CURRENT";
            return $"WAR STATE // {stateText} // {inherited} // MOMENTUM {momentum:+0;-0;0}";
        }

        private void ResetRunState()
        {
            for (int i = 0; i < _sectorResults.Length; i++) _sectorResults[i] = SectorWarState.Unresolved;
            _lastRound = 0;
            _lastRecordedSector = -1;
            _entryRewardedSector = -1;
            _momentum = 0;
            _brief = string.Empty;
            _briefUntil = 0f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(0.98f, 0.82f, 0.30f);
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _body.normal.textColor = new Color(0.82f, 0.90f, 1f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime > _briefUntil || string.IsNullOrEmpty(_brief)) return;
            EnsureStyles();
            float width = Mathf.Min(620f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.86f);
            GUI.Box(new Rect(x, 194f, width, 42f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, 198f, width - 16f, 18f), "CAMPAIGN MEMORY", _header);
            GUI.Label(new Rect(x + 8f, 216f, width - 16f, 16f), _brief, _body);
        }
    }
}
