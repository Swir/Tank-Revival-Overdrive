using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum OperationChainStage
    {
        Opening,
        Exploitation,
        Resolution
    }

    public enum OperationChainOutcome
    {
        Neutral,
        Success,
        Failure
    }

    public readonly struct OperationChainProfile
    {
        public readonly int Round;
        public readonly int Sector;
        public readonly OperationChainStage Stage;
        public readonly string Codename;
        public readonly string Objective;

        public OperationChainProfile(int round, int sector, OperationChainStage stage, string codename, string objective)
        {
            Round = round;
            Sector = sector;
            Stage = stage;
            Codename = codename;
            Objective = objective;
        }
    }

    [DefaultExecutionOrder(255)]
    public sealed class OperationChainDirector : MonoBehaviour
    {
        public const int ChainLength = 3;
        public const int FirstSectorRound = 4;
        public const float SuccessEagleLossLimit = 0.12f;
        public const float FailureEagleLossThreshold = 0.24f;
        public const float SuccessHealthMultiplier = 0.94f;
        public const float FailureHealthMultiplier = 1.08f;
        public const float SuccessSupportMultiplier = 0.90f;
        public const float FailureSupportMultiplier = 1.12f;
        public const int CompletionReward = 16;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);

        private static OperationChainDirector _instance;
        private TankGame _game;
        private int _lastRound;
        private int _trackedRound;
        private float _eagleStartRatio = 1f;
        private OperationChainOutcome _carryOutcome;
        private int _chainSuccesses;
        private OperationChainProfile _active;
        private float _briefUntil;
        private string _resultLine = string.Empty;
        private GUIStyle _header;
        private GUIStyle _body;

        public static OperationChainDirector Instance => _instance;
        public OperationChainProfile ActiveProfile => _active;
        public OperationChainOutcome CarryOutcome => _carryOutcome;
        public int ChainSuccesses => _chainSuccesses;
        public static bool BridgeAvailable => EncounterProfileField != null && EncounterNextStrikeField != null;

        public static bool ConfigurationValid =>
            ChainLength == 3 && FirstSectorRound >= 2 && FirstSectorRound + ChainLength - 1 <= 9 &&
            SuccessEagleLossLimit > 0f && SuccessEagleLossLimit < FailureEagleLossThreshold && FailureEagleLossThreshold < 0.5f &&
            SuccessHealthMultiplier >= 0.90f && SuccessHealthMultiplier <= 1f &&
            FailureHealthMultiplier >= 1f && FailureHealthMultiplier <= 1.15f &&
            SuccessSupportMultiplier >= 0.85f && SuccessSupportMultiplier <= 1f &&
            FailureSupportMultiplier >= 1f && FailureSupportMultiplier <= 1.18f &&
            CompletionReward >= 10 && CompletionReward <= 30 && BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<OperationChainDirector>() != null) return;
            GameObject go = new GameObject("OperationChainDirector_v8_3");
            DontDestroyOnLoad(go);
            go.AddComponent<OperationChainDirector>();
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
                ResetRunState();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round == _lastRound) return;

            if (_lastRound > 0 && IsChainRound(_lastRound))
                ResolvePreviousStage(_lastRound);

            bool newSector = _lastRound == 0 || SectorForRound(round) != SectorForRound(_lastRound);
            if (newSector)
            {
                _carryOutcome = OperationChainOutcome.Neutral;
                _chainSuccesses = 0;
            }

            _lastRound = round;
            _trackedRound = round;
            SnapshotHealth();

            if (!IsChainRound(round)) return;

            _active = Resolve(round);
            ApplyConsequence(round, _carryOutcome);
            _briefUntil = Time.unscaledTime + 4.2f;
            _resultLine = _carryOutcome == OperationChainOutcome.Neutral
                ? "CHAIN OPEN // PERFORMANCE WILL SHAPE THE NEXT STAGE"
                : (_carryOutcome == OperationChainOutcome.Success
                    ? "ADVANTAGE CARRIED FORWARD // ENEMY PRESSURE REDUCED"
                    : "SETBACK CARRIED FORWARD // ENEMY PRESSURE ESCALATED");
        }

        public static bool IsChainRound(int round)
        {
            if (round < 1 || round > 100 || round % 10 == 0) return false;
            int sectorRound = ((round - 1) % 10) + 1;
            return sectorRound >= FirstSectorRound && sectorRound < FirstSectorRound + ChainLength;
        }

        public static int SectorForRound(int round) => Mathf.Clamp((Mathf.Max(1, round) - 1) / 10, 0, 9);

        public static OperationChainProfile Resolve(int round)
        {
            int sector = SectorForRound(round);
            int sectorRound = ((Mathf.Max(1, round) - 1) % 10) + 1;
            OperationChainStage stage = (OperationChainStage)Mathf.Clamp(sectorRound - FirstSectorRound, 0, ChainLength - 1);
            string codename = CodenameForSector(sector);
            string objective;
            switch (stage)
            {
                case OperationChainStage.Opening: objective = "SECURE THE INITIATIVE // PROTECT ORZELEK"; break;
                case OperationChainStage.Exploitation: objective = "EXPLOIT THE OPENING // BREAK PRIORITY ARMOR"; break;
                default: objective = "CLOSE THE OPERATION // DENY ENEMY RECOVERY"; break;
            }
            return new OperationChainProfile(round, sector, stage, codename, objective);
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, OperationChainOutcome carry)
        {
            if (baseline.BossRound || carry == OperationChainOutcome.Neutral) return baseline;

            bool success = carry == OperationChainOutcome.Success;
            float health = Mathf.Clamp(baseline.HealthMultiplier * (success ? SuccessHealthMultiplier : FailureHealthMultiplier), 0.82f, 1.48f);
            float support = Mathf.Clamp(baseline.FireSupportMultiplier * (success ? SuccessSupportMultiplier : FailureSupportMultiplier), 0.78f, 1.58f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * (success ? 1.08f : 0.92f), 4.4f, 15.2f);
            bool champion = success ? baseline.ChampionEnabled : (baseline.ChampionEnabled || baseline.SectorRound >= 5);
            bool strikes = success ? (baseline.StrategicStrikes && baseline.SectorRound >= 7) : (baseline.StrategicStrikes || baseline.Round >= 35);
            string suffix = success ? " // ADVANTAGE" : " // SETBACK";

            return new RoundEncounterProfile(
                baseline.Round, baseline.Sector, baseline.SectorRound, baseline.Archetype,
                baseline.Codename + suffix, baseline.Objective,
                health, support, cadence, champion, strikes, baseline.BossRound);
        }

        public static OperationChainOutcome EvaluateStage(float eagleStartRatio, float eagleEndRatio, float playerEndRatio, bool playerAlive)
        {
            float eagleLoss = Mathf.Max(0f, eagleStartRatio - eagleEndRatio);
            if (!playerAlive || eagleEndRatio <= 0.05f || eagleLoss >= FailureEagleLossThreshold)
                return OperationChainOutcome.Failure;
            if (eagleLoss <= SuccessEagleLossLimit && playerEndRatio >= 0.25f)
                return OperationChainOutcome.Success;
            return OperationChainOutcome.Neutral;
        }

        private void ResolvePreviousStage(int round)
        {
            float eagleEnd = HealthRatio(CombatRoster.Eagle);
            PlayerTank player = CombatRoster.Player;
            float playerEnd = player != null ? HealthRatio(player.Health) : 0f;
            bool playerAlive = player != null && player.Health != null && !player.Health.IsDead;
            OperationChainOutcome result = EvaluateStage(_eagleStartRatio, eagleEnd, playerEnd, playerAlive);

            _carryOutcome = result;
            if (result == OperationChainOutcome.Success) _chainSuccesses++;

            OperationChainProfile previous = Resolve(round);
            if (previous.Stage == OperationChainStage.Resolution)
            {
                if (_chainSuccesses >= 2)
                {
                    WarEconomyDirector.AwardMissionBonds(CompletionReward, "OPERATION CHAIN VICTORY");
                    _resultLine = "OPERATION WON // " + CompletionReward + " WAR BONDS";
                    BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.48f, 0.04f);
                }
                else
                {
                    _resultLine = "OPERATION CONTESTED // ENEMY RETAINS INITIATIVE";
                    BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.24f, 0f);
                }
                _carryOutcome = OperationChainOutcome.Neutral;
            }
        }

        private void ApplyConsequence(int round, OperationChainOutcome carry)
        {
            if (carry == OperationChainOutcome.Neutral || CampaignEncounterDirector.Instance == null || EncounterProfileField == null) return;

            RoundEncounterProfile baseline = CampaignEncounterDirector.Instance.ActiveProfile;
            if (baseline.Round != round) baseline = CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, carry);
            EncounterProfileField.SetValue(CampaignEncounterDirector.Instance, refined);
            if (refined.StrategicStrikes && EncounterNextStrikeField != null)
                EncounterNextStrikeField.SetValue(CampaignEncounterDirector.Instance, Time.time + Mathf.Max(3.4f, refined.StrikeCadence * 0.78f));
        }

        private void SnapshotHealth()
        {
            _eagleStartRatio = HealthRatio(CombatRoster.Eagle);
        }

        private static float HealthRatio(Health health)
        {
            if (health == null || health.Maximum <= 0) return 1f;
            return Mathf.Clamp01((float)health.Current / health.Maximum);
        }

        private static string CodenameForSector(int sector)
        {
            string[] names =
            {
                "IRON ENTRY", "THUNDER ROAD", "WOLF TRAP", "STONE HAMMER", "SILENT CIRCUIT",
                "BROKEN SKY", "STEEL RESERVE", "HIGHWAY KNIFE", "BLACK SIGNAL", "ORZEL SHIELD"
            };
            return names[Mathf.Clamp(sector, 0, names.Length - 1)];
        }

        private void ResetRunState()
        {
            _lastRound = 0;
            _trackedRound = 0;
            _carryOutcome = OperationChainOutcome.Neutral;
            _chainSuccesses = 0;
            _eagleStartRatio = 1f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(1f, 0.78f, 0.25f);
            _body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            _body.normal.textColor = new Color(0.90f, 0.94f, 1f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !IsChainRound(_trackedRound) || Time.unscaledTime > _briefUntil) return;
            EnsureStyles();
            float width = Mathf.Min(600f, Screen.width - 36f);
            float x = (Screen.width - width) * 0.5f;
            GUI.Label(new Rect(x, 132f, width, 22f), $"OPERATION {_active.Codename} // STAGE {(int)_active.Stage + 1}/3", _header);
            GUI.Label(new Rect(x, 153f, width, 19f), _active.Objective, _body);
            GUI.Label(new Rect(x, 171f, width, 19f), _resultLine, _body);
        }
    }
}
