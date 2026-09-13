using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum CampaignPacingBeat
    {
        Recovery,
        Skirmish,
        Offensive,
        SpecialOperation,
        Escalation,
        BossClimax
    }

    public readonly struct CampaignPacingProfile
    {
        public readonly int Round;
        public readonly CampaignPacingBeat Beat;
        public readonly float WaveMultiplier;
        public readonly int MaxAliveDelta;
        public readonly float SpawnDelayMultiplier;
        public readonly float EncounterHealthMultiplier;
        public readonly float FireSupportMultiplier;
        public readonly bool AllowStrategicStrikes;
        public readonly string Label;

        public CampaignPacingProfile(
            int round,
            CampaignPacingBeat beat,
            float waveMultiplier,
            int maxAliveDelta,
            float spawnDelayMultiplier,
            float encounterHealthMultiplier,
            float fireSupportMultiplier,
            bool allowStrategicStrikes,
            string label)
        {
            Round = round;
            Beat = beat;
            WaveMultiplier = waveMultiplier;
            MaxAliveDelta = maxAliveDelta;
            SpawnDelayMultiplier = spawnDelayMultiplier;
            EncounterHealthMultiplier = encounterHealthMultiplier;
            FireSupportMultiplier = fireSupportMultiplier;
            AllowStrategicStrikes = allowStrategicStrikes;
            Label = label;
        }

        public int ApplyWaveBudget(int baseline)
        {
            return Mathf.Clamp(Mathf.RoundToInt(baseline * WaveMultiplier), 4, 62);
        }

        public int ApplyAliveCap(int baseline)
        {
            return Mathf.Clamp(baseline + MaxAliveDelta, 3, 14);
        }
    }

    /// <summary>
    /// v8.1 master pacing layer. TankGame remains the round/spawn authority; this director
    /// applies one bounded pressure envelope per round after TankGame initializes it and
    /// refines CampaignEncounterDirector before encounter combatants are configured.
    /// Reflection is cached once because the legacy round counters are private; no per-frame
    /// member discovery or parallel spawn/damage authority is introduced.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public sealed class CampaignPacingDirector : MonoBehaviour
    {
        public const float MinWaveMultiplier = 0.76f;
        public const float MaxWaveMultiplier = 1.16f;
        public const float MinSpawnDelayMultiplier = 0.86f;
        public const float MaxSpawnDelayMultiplier = 1.16f;
        public const float MinEncounterHealthMultiplier = 0.92f;
        public const float MaxEncounterHealthMultiplier = 1.08f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo EnemiesToSpawnField = typeof(TankGame).GetField("_enemiesToSpawn", PrivateInstance);
        private static readonly FieldInfo MaxAliveField = typeof(TankGame).GetField("_maxAlive", PrivateInstance);
        private static readonly FieldInfo NextSpawnField = typeof(TankGame).GetField("_nextSpawn", PrivateInstance);
        private static readonly FieldInfo EncounterProfileField = typeof(CampaignEncounterDirector).GetField("_profile", PrivateInstance);
        private static readonly FieldInfo EncounterNextStrikeField = typeof(CampaignEncounterDirector).GetField("_nextStrike", PrivateInstance);

        private static CampaignPacingDirector _instance;
        private TankGame _game;
        private int _lastRound;
        private CampaignPacingProfile _active;
        private float _bannerUntil;
        private float _lastRawSpawnAt = -1f;
        private float _lastAppliedSpawnAt = -1f;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static CampaignPacingDirector Instance => _instance;
        public CampaignPacingProfile ActiveProfile => _active;
        public static bool BridgeAvailable =>
            EnemiesToSpawnField != null && MaxAliveField != null && NextSpawnField != null &&
            EncounterProfileField != null && EncounterNextStrikeField != null;

        public static bool ConfigurationValid
        {
            get
            {
                if (!BridgeAvailable) return false;
                int recoveries = 0;
                int climaxes = 0;
                int operations = 0;
                for (int round = 1; round <= 100; round++)
                {
                    CampaignPacingProfile p = Resolve(round);
                    if (p.WaveMultiplier < MinWaveMultiplier || p.WaveMultiplier > MaxWaveMultiplier) return false;
                    if (p.SpawnDelayMultiplier < MinSpawnDelayMultiplier || p.SpawnDelayMultiplier > MaxSpawnDelayMultiplier) return false;
                    if (p.EncounterHealthMultiplier < MinEncounterHealthMultiplier || p.EncounterHealthMultiplier > MaxEncounterHealthMultiplier) return false;
                    if (p.Beat == CampaignPacingBeat.Recovery) recoveries++;
                    if (p.Beat == CampaignPacingBeat.BossClimax) climaxes++;
                    if (p.Beat == CampaignPacingBeat.SpecialOperation) operations++;
                }
                return recoveries >= 9 && climaxes == 10 && operations >= 10;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CampaignPacingDirector>() != null) return;
            GameObject go = new GameObject("CampaignPacingDirector_v8_1");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignPacingDirector>();
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
                _lastRound = 0;
                _lastRawSpawnAt = -1f;
                _lastAppliedSpawnAt = -1f;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _lastRound)
            {
                _lastRound = round;
                _active = Resolve(round);
                ApplyRoundEnvelope(_game, _active);
                ApplyEncounterEnvelope(round, _active);
                _bannerUntil = Time.unscaledTime + (_active.Beat == CampaignPacingBeat.BossClimax ? 4.0f : 2.8f);
            }

            RescaleSpawnCadence(_game, _active);
        }

        private void ApplyRoundEnvelope(TankGame game, CampaignPacingProfile profile)
        {
            if (!BridgeAvailable) return;
            int baselineWave = (int)EnemiesToSpawnField.GetValue(game);
            int baselineCap = (int)MaxAliveField.GetValue(game);
            EnemiesToSpawnField.SetValue(game, profile.ApplyWaveBudget(baselineWave));
            MaxAliveField.SetValue(game, profile.ApplyAliveCap(baselineCap));

            float next = (float)NextSpawnField.GetValue(game);
            if (next > Time.time)
            {
                float adjusted = Time.time + (next - Time.time) * profile.SpawnDelayMultiplier;
                NextSpawnField.SetValue(game, adjusted);
                _lastRawSpawnAt = next;
                _lastAppliedSpawnAt = adjusted;
            }
        }

        private void RescaleSpawnCadence(TankGame game, CampaignPacingProfile profile)
        {
            if (NextSpawnField == null) return;
            float next = (float)NextSpawnField.GetValue(game);
            if (next <= Time.time || Mathf.Abs(next - _lastAppliedSpawnAt) < 0.0001f || Mathf.Abs(next - _lastRawSpawnAt) < 0.0001f) return;

            _lastRawSpawnAt = next;
            float remaining = Mathf.Max(0f, next - Time.time);
            float adjusted = Time.time + remaining * profile.SpawnDelayMultiplier;
            NextSpawnField.SetValue(game, adjusted);
            _lastAppliedSpawnAt = adjusted;
        }

        private static void ApplyEncounterEnvelope(int round, CampaignPacingProfile pacing)
        {
            CampaignEncounterDirector director = CampaignEncounterDirector.Instance;
            if (director == null || EncounterProfileField == null) return;

            RoundEncounterProfile baseline = CampaignEncounterDirector.Resolve(round);
            RoundEncounterProfile refined = RefineEncounter(baseline, pacing);
            EncounterProfileField.SetValue(director, refined);

            if (EncounterNextStrikeField != null && refined.StrategicStrikes)
                EncounterNextStrikeField.SetValue(director, Time.time + Mathf.Max(3.8f, refined.StrikeCadence * 0.72f));
        }

        public static CampaignPacingProfile Resolve(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            int local = ((round - 1) % 10) + 1;
            bool mobileHq = round >= MobileHQWarfareDirector.MinimumOperationRound &&
                            ((round - MobileHQWarfareDirector.MinimumOperationRound) % MobileHQWarfareDirector.OperationRoundCadence) == 0;

            if (local == 10)
                return new CampaignPacingProfile(round, CampaignPacingBeat.BossClimax, 1.08f, 1, 0.90f, 1.06f, 1.08f, round >= 20, "BOSS CLIMAX");

            if (round > 1 && local == 1)
                return new CampaignPacingProfile(round, CampaignPacingBeat.Recovery, 0.76f, -2, 1.16f, 0.92f, 0.90f, false, "RECOVERY WINDOW");

            if (mobileHq || local == 5 || round == 28 || round == 36)
                return new CampaignPacingProfile(round, CampaignPacingBeat.SpecialOperation, 0.98f, 0, 1.00f, 1.01f, 1.06f, false, "SPECIAL OPERATION");

            switch (local)
            {
                case 2:
                case 6:
                    return new CampaignPacingProfile(round, CampaignPacingBeat.Skirmish, 0.88f, -1, 1.08f, 0.96f, 0.96f, false, "CONTACT / MANEUVER");
                case 3:
                case 7:
                    return new CampaignPacingProfile(round, CampaignPacingBeat.Offensive, 1.05f, 0, 0.96f, 1.02f, 1.03f, round >= 45 && local == 7, "OFFENSIVE PUSH");
                case 4:
                case 8:
                case 9:
                    return new CampaignPacingProfile(round, CampaignPacingBeat.Escalation, 1.16f, 1, 0.86f, 1.08f, 1.10f, round >= 35, "ESCALATION");
                default:
                    return new CampaignPacingProfile(round, CampaignPacingBeat.Skirmish, 0.92f, -1, 1.06f, 0.98f, 0.98f, false, "CONTACT / MANEUVER");
            }
        }

        public static RoundEncounterProfile RefineEncounter(RoundEncounterProfile baseline, CampaignPacingProfile pacing)
        {
            EncounterArchetype archetype = baseline.Archetype;
            string code = baseline.Codename;
            string objective = baseline.Objective;
            bool champion = baseline.ChampionEnabled;
            bool strikes = baseline.StrategicStrikes && pacing.AllowStrategicStrikes;

            if (!baseline.BossRound)
            {
                switch (pacing.Beat)
                {
                    case CampaignPacingBeat.Recovery:
                        archetype = baseline.Round >= 3 ? EncounterArchetype.SupplyInterdiction : EncounterArchetype.FrontlineAssault;
                        code = "FIELD RESET";
                        objective = "REGROUP // INTERCEPT SUPPLIES // REBUILD THE LINE";
                        champion = false;
                        strikes = false;
                        break;
                    case CampaignPacingBeat.Offensive:
                        archetype = (baseline.Round & 1) == 0 ? EncounterArchetype.ArmoredColumn : EncounterArchetype.Wolfpack;
                        code = "PRESSURE FRONT";
                        objective = "BREAK THE ATTACK BEFORE IT REACHES ORZELEK";
                        break;
                    case CampaignPacingBeat.SpecialOperation:
                        if (baseline.SectorRound != 5)
                            archetype = baseline.Round >= 42 ? EncounterArchetype.SiegePush : EncounterArchetype.SniperNet;
                        code = "SPECIAL OPERATION";
                        objective = baseline.Round >= 36 ? "DISRUPT COMMAND ASSETS // PROTECT ORZELEK" : "ELIMINATE THE PRIORITY NETWORK";
                        champion = baseline.Round >= 20;
                        strikes = false;
                        break;
                    case CampaignPacingBeat.Escalation:
                        archetype = baseline.Round >= 55 ? EncounterArchetype.LastStand : EncounterArchetype.ArtilleryScreen;
                        code = "REDLINE ASSAULT";
                        objective = "SURVIVE THE ESCALATION // HOLD THE DEFENSE GRID";
                        champion = baseline.Round >= 45;
                        break;
                }
            }

            float health = Mathf.Clamp(baseline.HealthMultiplier * pacing.EncounterHealthMultiplier, 0.84f, 1.38f);
            float support = Mathf.Clamp(baseline.FireSupportMultiplier * pacing.FireSupportMultiplier, 0.82f, 1.42f);
            float cadence = Mathf.Clamp(baseline.StrikeCadence * pacing.SpawnDelayMultiplier, 4.8f, 14f);
            return new RoundEncounterProfile(
                baseline.Round,
                baseline.Sector,
                baseline.SectorRound,
                archetype,
                code,
                objective,
                health,
                support,
                cadence,
                champion,
                strikes,
                baseline.BossRound);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _titleStyle.normal.textColor = _active.Beat == CampaignPacingBeat.Recovery
                ? new Color(0.28f, 1f, 0.70f)
                : _active.Beat == CampaignPacingBeat.BossClimax
                    ? new Color(1f, 0.26f, 0.12f)
                    : new Color(1f, 0.72f, 0.22f);
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
            _bodyStyle.normal.textColor = new Color(0.80f, 0.90f, 0.98f);
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime > _bannerUntil) return;
            _titleStyle = null;
            EnsureStyles();
            float width = Mathf.Min(440f, Screen.width - 32f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 18f, width, 50f);
            Color old = GUI.color;
            GUI.color = new Color(0.02f, 0.035f, 0.055f, 0.86f);
            GUI.Box(box, GUIContent.none);
            GUI.color = old;
            GUI.Label(new Rect(box.x + 8f, box.y + 5f, box.width - 16f, 20f), $"R{_active.Round:000} // {_active.Label}", _titleStyle);
            GUI.Label(new Rect(box.x + 8f, box.y + 25f, box.width - 16f, 18f), BeatHint(_active.Beat), _bodyStyle);
        }

        private static string BeatHint(CampaignPacingBeat beat)
        {
            switch (beat)
            {
                case CampaignPacingBeat.Recovery: return "tempo spada — odbuduj pozycję i zapasy";
                case CampaignPacingBeat.Skirmish: return "kontakt manewrowy — czytaj pole walki";
                case CampaignPacingBeat.Offensive: return "ofensywa — przełam natarcie zanim się rozwinie";
                case CampaignPacingBeat.SpecialOperation: return "priorytet: cele dowodzenia / sieci / HVT";
                case CampaignPacingBeat.Escalation: return "presja rośnie — przygotuj aktywne kontry";
                default: return "kulminacja sektora — boss i pełna presja bojowa";
            }
        }
    }
}