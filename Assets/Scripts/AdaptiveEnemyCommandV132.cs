using UnityEngine;

namespace TankRevival
{
    public enum AdaptiveCommandDoctrineV132
    {
        Balanced = 0,
        HunterKiller = 1,
        SiegePressure = 2,
        FortressBreaker = 3,
        Interdiction = 4,
        ElasticDefense = 5,
        RecoveryWindow = 6
    }

    public struct CombatRoundTelemetryV132
    {
        public int Round;
        public int TotalKills;
        public int SpecialistKills;
        public int SupplyKills;
        public int PlayerLosses;
        public int EagleDamage;
        public bool ObjectiveResolved;
        public bool ObjectiveSucceeded;
        public int Signature;
    }

    public struct CombatHistorySnapshotV132
    {
        public int Count;
        public int TotalKills;
        public int SpecialistKills;
        public int SupplyKills;
        public int PlayerLosses;
        public int EagleDamage;
        public int ObjectiveSuccesses;
        public int ObjectiveFailures;
        public float Distress01;
        public float OffensivePressure01;
        public int Signature;
    }

    public struct AdaptiveCommandPlanV132
    {
        public int Round;
        public AdaptiveCommandDoctrineV132 Doctrine;
        public int DoctrineRunLength;
        public float Confidence01;
        public int ConcurrencyDelta;
        public float SpawnIntervalScale;
        public int ForcedStride;
        public EnemyKind ForcedKind;
        public float MovementScale;
        public float ReloadScale;
        public float SpreadScale;
        public bool PreferPlayer;
        public int HistoryCount;
        public float Distress01;
        public float OffensivePressure01;
        public int Signature;
    }

    public struct EnemyCommandPostureV132
    {
        public float MovementScale;
        public float ReloadScale;
        public float SpreadScale;
        public bool PreferPlayer;
    }

    /// <summary>Fixed-capacity recent-round history. Allocates once and never grows with campaign length.</summary>
    public sealed class CombatHistoryBufferV132
    {
        public const int Capacity = 8;
        private readonly CombatRoundTelemetryV132[] _entries = new CombatRoundTelemetryV132[Capacity];
        private int _count;
        private int _next;

        public int Count => _count;

        public void Reset()
        {
            _count = 0;
            _next = 0;
            for (int i = 0; i < _entries.Length; i++) _entries[i] = default;
        }

        public void Push(CombatRoundTelemetryV132 telemetry)
        {
            telemetry.Round = Mathf.Clamp(telemetry.Round, 1, 100);
            telemetry.TotalKills = Mathf.Max(0, telemetry.TotalKills);
            telemetry.SpecialistKills = Mathf.Clamp(telemetry.SpecialistKills, 0, telemetry.TotalKills);
            telemetry.SupplyKills = Mathf.Clamp(telemetry.SupplyKills, 0, telemetry.TotalKills);
            telemetry.PlayerLosses = Mathf.Clamp(telemetry.PlayerLosses, 0, 8);
            telemetry.EagleDamage = Mathf.Clamp(telemetry.EagleDamage, 0, 12);
            telemetry.Signature = SignatureFor(telemetry);
            _entries[_next] = telemetry;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public CombatHistorySnapshotV132 Snapshot()
        {
            CombatHistorySnapshotV132 result = default;
            result.Count = _count;
            int signature = 23;
            for (int i = 0; i < _count; i++)
            {
                int index = (_next - _count + i + Capacity) % Capacity;
                CombatRoundTelemetryV132 e = _entries[index];
                result.TotalKills += e.TotalKills;
                result.SpecialistKills += e.SpecialistKills;
                result.SupplyKills += e.SupplyKills;
                result.PlayerLosses += e.PlayerLosses;
                result.EagleDamage += e.EagleDamage;
                if (e.ObjectiveResolved)
                {
                    if (e.ObjectiveSucceeded) result.ObjectiveSuccesses++;
                    else result.ObjectiveFailures++;
                }
                signature = unchecked(signature * 31 + e.Signature);
            }

            if (_count > 0)
            {
                float lossRate = Mathf.Clamp01(result.PlayerLosses / Mathf.Max(1f, _count * 1.25f));
                float eagleRate = Mathf.Clamp01(result.EagleDamage / Mathf.Max(1f, _count * 2.0f));
                float failureRate = Mathf.Clamp01(result.ObjectiveFailures / Mathf.Max(1f, (float)_count));
                float killRate = Mathf.Clamp01(result.TotalKills / Mathf.Max(1f, _count * 8f));
                float specialistRate = Mathf.Clamp01(result.SpecialistKills / Mathf.Max(1f, _count * 4f));
                float successRate = Mathf.Clamp01(result.ObjectiveSuccesses / Mathf.Max(1f, (float)_count));
                result.Distress01 = Mathf.Clamp01(lossRate * 0.45f + eagleRate * 0.35f + failureRate * 0.20f);
                result.OffensivePressure01 = Mathf.Clamp01(killRate * 0.45f + specialistRate * 0.35f + successRate * 0.20f);
            }
            result.Signature = signature;
            return result;
        }

        private static int SignatureFor(CombatRoundTelemetryV132 e)
        {
            int s = 17;
            s = unchecked(s * 31 + e.Round);
            s = unchecked(s * 31 + e.TotalKills);
            s = unchecked(s * 31 + e.SpecialistKills);
            s = unchecked(s * 31 + e.SupplyKills);
            s = unchecked(s * 31 + e.PlayerLosses);
            s = unchecked(s * 31 + e.EagleDamage);
            s = unchecked(s * 31 + (e.ObjectiveResolved ? 1 : 0));
            s = unchecked(s * 31 + (e.ObjectiveSucceeded ? 1 : 0));
            return s;
        }
    }

    /// <summary>Pure deterministic v13.2 enemy-command planner. It returns intent only; canonical systems execute it.</summary>
    public static class AdaptiveEnemyCommandPlannerV132
    {
        public const int DoctrineCount = 7;
        public const int MinimumHoldRounds = 2;
        public const int MaximumRepeatRounds = 3;
        public const int MaxConcurrencyDelta = 1;
        public const float MinSpawnScale = 0.92f;
        public const float MaxSpawnScale = 1.08f;
        public const float MinMovementScale = 0.94f;
        public const float MaxMovementScale = 1.06f;
        public const float MinReloadScale = 0.94f;
        public const float MaxReloadScale = 1.06f;
        public const float MinSpreadScale = 0.94f;
        public const float MaxSpreadScale = 1.06f;
        public const int MinForcedStride = 5;

        public static bool ConfigurationValid =>
            DoctrineCount == 7 && CombatHistoryBufferV132.Capacity == 8 && MinimumHoldRounds == 2 && MaximumRepeatRounds == 3 &&
            MaxConcurrencyDelta == 1 && MinSpawnScale >= 0.90f && MaxSpawnScale <= 1.10f &&
            MinMovementScale >= 0.92f && MaxMovementScale <= 1.08f && MinReloadScale >= 0.92f && MaxReloadScale <= 1.08f &&
            MinSpreadScale >= 0.92f && MaxSpreadScale <= 1.08f && MinForcedStride >= 5;

        public static AdaptiveCommandPlanV132 Resolve(int requestedRound, EncounterPlan encounter, ObjectivePlanV131 objective,
            CombatHistorySnapshotV132 history, AdaptiveCommandDoctrineV132 previousDoctrine, int previousRunLength)
        {
            int round = Mathf.Clamp(requestedRound, 1, 100);
            int previousRun = Mathf.Clamp(previousRunLength, 0, MaximumRepeatRounds);
            AdaptiveCommandDoctrineV132 candidate = CandidateFor(round, encounter, objective, history);

            if (history.Count == 0 || round <= 2)
                candidate = AdaptiveCommandDoctrineV132.Balanced;
            else if (candidate != AdaptiveCommandDoctrineV132.RecoveryWindow && previousDoctrine != AdaptiveCommandDoctrineV132.Balanced &&
                     previousRun > 0 && previousRun < MinimumHoldRounds)
                candidate = previousDoctrine;

            if (candidate == previousDoctrine && previousRun >= MaximumRepeatRounds && candidate != AdaptiveCommandDoctrineV132.RecoveryWindow)
                candidate = RotateDoctrine(candidate, round + history.Signature);
            if (candidate == AdaptiveCommandDoctrineV132.RecoveryWindow && previousDoctrine == candidate && previousRun >= MinimumHoldRounds && history.Distress01 < 0.72f)
                candidate = AdaptiveCommandDoctrineV132.Balanced;

            int runLength = candidate == previousDoctrine ? Mathf.Min(MaximumRepeatRounds, previousRun + 1) : 1;
            AdaptiveCommandPlanV132 plan = BuildPlan(round, candidate, runLength, history);
            int signature = 29;
            signature = unchecked(signature * 31 + round);
            signature = unchecked(signature * 31 + encounter.Signature);
            signature = unchecked(signature * 31 + objective.Signature);
            signature = unchecked(signature * 31 + history.Signature);
            signature = unchecked(signature * 31 + (int)candidate);
            signature = unchecked(signature * 31 + runLength);
            signature = unchecked(signature * 31 + plan.ConcurrencyDelta);
            signature = unchecked(signature * 31 + Mathf.RoundToInt(plan.SpawnIntervalScale * 1000f));
            signature = unchecked(signature * 31 + plan.ForcedStride);
            signature = unchecked(signature * 31 + (int)plan.ForcedKind);
            plan.Signature = signature;
            return plan;
        }

        public static EnemyKind EnemyForSpawn(AdaptiveCommandPlanV132 plan, int spawnOrdinal, EnemyKind fallback, bool bossRound)
        {
            if (bossRound || fallback == EnemyKind.Boss || fallback == EnemyKind.Supply || plan.ForcedStride < MinForcedStride)
                return fallback;
            int ordinal = Mathf.Max(0, spawnOrdinal);
            if (ordinal == 0 || ordinal % plan.ForcedStride != 0) return fallback;
            return plan.ForcedKind == EnemyKind.Boss || plan.ForcedKind == EnemyKind.Supply ? fallback : plan.ForcedKind;
        }

        public static EnemyCommandPostureV132 PostureFor(AdaptiveCommandPlanV132 plan, EnemyKind kind)
        {
            bool focus = DoctrineFocuses(plan.Doctrine, kind);
            float blend = focus || plan.Doctrine == AdaptiveCommandDoctrineV132.RecoveryWindow ? 1f : 0.35f;
            return new EnemyCommandPostureV132
            {
                MovementScale = Mathf.Clamp(Mathf.Lerp(1f, plan.MovementScale, blend), MinMovementScale, MaxMovementScale),
                ReloadScale = Mathf.Clamp(Mathf.Lerp(1f, plan.ReloadScale, blend), MinReloadScale, MaxReloadScale),
                SpreadScale = Mathf.Clamp(Mathf.Lerp(1f, plan.SpreadScale, blend), MinSpreadScale, MaxSpreadScale),
                PreferPlayer = plan.PreferPlayer && focus
            };
        }

        private static AdaptiveCommandDoctrineV132 CandidateFor(int round, EncounterPlan encounter, ObjectivePlanV131 objective, CombatHistorySnapshotV132 history)
        {
            if (history.Count >= 2 && history.Distress01 >= 0.58f)
                return AdaptiveCommandDoctrineV132.RecoveryWindow;
            if (history.Count >= 2 && history.SupplyKills >= Mathf.Max(2, history.Count / 2 + 1))
                return AdaptiveCommandDoctrineV132.Interdiction;
            if (history.Count >= 2 && history.OffensivePressure01 >= 0.72f)
                return AdaptiveCommandDoctrineV132.HunterKiller;
            if (objective.Kind == ObjectiveArchetypeV131.SectorDefense || objective.Kind == ObjectiveArchetypeV131.CommandBreakthrough)
                return round >= 35 ? AdaptiveCommandDoctrineV132.FortressBreaker : AdaptiveCommandDoctrineV132.SiegePressure;
            if (objective.Kind == ObjectiveArchetypeV131.Counterattack || objective.CrossSystemBand >= 2)
                return AdaptiveCommandDoctrineV132.ElasticDefense;
            if (round >= 45 && (objective.Kind == ObjectiveArchetypeV131.Annihilation || objective.Kind == ObjectiveArchetypeV131.EmitterHunt))
                return AdaptiveCommandDoctrineV132.SiegePressure;

            int selector = PositiveMod(encounter.Signature ^ objective.Signature ^ history.Signature ^ round * 37, 5);
            switch (selector)
            {
                case 0: return AdaptiveCommandDoctrineV132.HunterKiller;
                case 1: return AdaptiveCommandDoctrineV132.SiegePressure;
                case 2: return AdaptiveCommandDoctrineV132.FortressBreaker;
                case 3: return AdaptiveCommandDoctrineV132.Interdiction;
                default: return AdaptiveCommandDoctrineV132.ElasticDefense;
            }
        }

        private static AdaptiveCommandPlanV132 BuildPlan(int round, AdaptiveCommandDoctrineV132 doctrine, int runLength, CombatHistorySnapshotV132 history)
        {
            AdaptiveCommandPlanV132 plan = new AdaptiveCommandPlanV132
            {
                Round = round,
                Doctrine = doctrine,
                DoctrineRunLength = runLength,
                Confidence01 = Mathf.Clamp01(0.20f + history.Count / (float)CombatHistoryBufferV132.Capacity * 0.55f + Mathf.Abs(history.OffensivePressure01 - history.Distress01) * 0.25f),
                ConcurrencyDelta = 0,
                SpawnIntervalScale = 1f,
                ForcedStride = 0,
                ForcedKind = EnemyKind.Basic,
                MovementScale = 1f,
                ReloadScale = 1f,
                SpreadScale = 1f,
                PreferPlayer = false,
                HistoryCount = history.Count,
                Distress01 = history.Distress01,
                OffensivePressure01 = history.OffensivePressure01
            };

            switch (doctrine)
            {
                case AdaptiveCommandDoctrineV132.HunterKiller:
                    plan.ConcurrencyDelta = 1; plan.SpawnIntervalScale = 0.97f; plan.ForcedStride = 5;
                    plan.ForcedKind = round >= 60 ? EnemyKind.Elite : EnemyKind.Fast;
                    plan.MovementScale = 1.06f; plan.ReloadScale = 0.97f; plan.SpreadScale = 0.96f; plan.PreferPlayer = true;
                    break;
                case AdaptiveCommandDoctrineV132.SiegePressure:
                    plan.SpawnIntervalScale = 0.96f; plan.ForcedStride = 6;
                    plan.ForcedKind = round >= 45 ? EnemyKind.Siege : EnemyKind.Heavy;
                    plan.MovementScale = 0.97f; plan.ReloadScale = 0.95f; plan.SpreadScale = 0.97f;
                    break;
                case AdaptiveCommandDoctrineV132.FortressBreaker:
                    plan.ConcurrencyDelta = 1; plan.SpawnIntervalScale = 0.94f; plan.ForcedStride = 5;
                    plan.ForcedKind = round >= 50 ? EnemyKind.Siege : EnemyKind.Heavy;
                    plan.MovementScale = 1.04f; plan.ReloadScale = 0.98f; plan.SpreadScale = 1.00f;
                    break;
                case AdaptiveCommandDoctrineV132.Interdiction:
                    plan.SpawnIntervalScale = 0.98f; plan.ForcedStride = 5;
                    plan.ForcedKind = round >= 55 ? EnemyKind.Sniper : EnemyKind.Fast;
                    plan.MovementScale = 1.05f; plan.ReloadScale = 0.98f; plan.SpreadScale = 0.95f; plan.PreferPlayer = true;
                    break;
                case AdaptiveCommandDoctrineV132.ElasticDefense:
                    plan.SpawnIntervalScale = 1.02f; plan.ForcedStride = 6;
                    plan.ForcedKind = round >= 60 ? EnemyKind.Elite : EnemyKind.Sniper;
                    plan.MovementScale = 0.98f; plan.ReloadScale = 1.00f; plan.SpreadScale = 0.94f;
                    break;
                case AdaptiveCommandDoctrineV132.RecoveryWindow:
                    plan.ConcurrencyDelta = -1; plan.SpawnIntervalScale = 1.08f;
                    plan.MovementScale = 0.96f; plan.ReloadScale = 1.06f; plan.SpreadScale = 1.06f;
                    break;
            }

            plan.ConcurrencyDelta = Mathf.Clamp(plan.ConcurrencyDelta, -MaxConcurrencyDelta, MaxConcurrencyDelta);
            plan.SpawnIntervalScale = Mathf.Clamp(plan.SpawnIntervalScale, MinSpawnScale, MaxSpawnScale);
            plan.MovementScale = Mathf.Clamp(plan.MovementScale, MinMovementScale, MaxMovementScale);
            plan.ReloadScale = Mathf.Clamp(plan.ReloadScale, MinReloadScale, MaxReloadScale);
            plan.SpreadScale = Mathf.Clamp(plan.SpreadScale, MinSpreadScale, MaxSpreadScale);
            if (plan.ForcedStride > 0) plan.ForcedStride = Mathf.Max(MinForcedStride, plan.ForcedStride);
            return plan;
        }

        private static bool DoctrineFocuses(AdaptiveCommandDoctrineV132 doctrine, EnemyKind kind)
        {
            switch (doctrine)
            {
                case AdaptiveCommandDoctrineV132.HunterKiller: return kind == EnemyKind.Fast || kind == EnemyKind.Elite || kind == EnemyKind.Sniper;
                case AdaptiveCommandDoctrineV132.SiegePressure: return kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Sniper;
                case AdaptiveCommandDoctrineV132.FortressBreaker: return kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
                case AdaptiveCommandDoctrineV132.Interdiction: return kind == EnemyKind.Fast || kind == EnemyKind.Sniper || kind == EnemyKind.Elite;
                case AdaptiveCommandDoctrineV132.ElasticDefense: return kind == EnemyKind.Sniper || kind == EnemyKind.Elite || kind == EnemyKind.Fast;
                case AdaptiveCommandDoctrineV132.RecoveryWindow: return true;
                default: return false;
            }
        }

        private static AdaptiveCommandDoctrineV132 RotateDoctrine(AdaptiveCommandDoctrineV132 doctrine, int seed)
        {
            int value = 1 + PositiveMod((int)doctrine + seed, 5);
            return (AdaptiveCommandDoctrineV132)value;
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    /// <summary>
    /// Observes canonical round outcomes and publishes bounded intent. It never moves actors, fires projectiles,
    /// applies damage/healing, spawns enemies or awards currency.
    /// </summary>
    public sealed class AdaptiveEnemyCommandDirector : MonoBehaviour
    {
        public static AdaptiveEnemyCommandDirector Instance { get; private set; }
        public static bool ConfigurationValid => AdaptiveEnemyCommandPlannerV132.ConfigurationValid;

        private readonly CombatHistoryBufferV132 _history = new CombatHistoryBufferV132();
        private CombatRoundTelemetryV132 _liveTelemetry;
        private AdaptiveCommandPlanV132 _plan;
        private AdaptiveCommandDoctrineV132 _previousDoctrine = AdaptiveCommandDoctrineV132.Balanced;
        private int _previousRunLength;
        private bool _hasLiveRound;
        private string _hudText = "CMD STANDBY";

        public AdaptiveCommandPlanV132 CurrentPlan => _plan;
        public CombatHistorySnapshotV132 CurrentHistory => _history.Snapshot();
        public string HudText => _hudText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static AdaptiveEnemyCommandDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            AdaptiveEnemyCommandDirector existing = FindAnyObjectByType<AdaptiveEnemyCommandDirector>();
            if (existing != null) return existing;
            GameObject go = new GameObject("AdaptiveEnemyCommandDirector_v13_2");
            DontDestroyOnLoad(go);
            return go.AddComponent<AdaptiveEnemyCommandDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginRound(EncounterPlan encounter, ObjectivePlanV131 objective, ObjectiveRuntimeStateV131 previousObjective)
        {
            if (encounter.Round <= 1)
                ResetCampaign();
            else
                CommitPreviousRound(previousObjective);

            CombatHistorySnapshotV132 snapshot = _history.Snapshot();
            _plan = AdaptiveEnemyCommandPlannerV132.Resolve(encounter.Round, encounter, objective, snapshot, _previousDoctrine, _previousRunLength);
            if (_plan.Doctrine == _previousDoctrine) _previousRunLength = _plan.DoctrineRunLength;
            else { _previousDoctrine = _plan.Doctrine; _previousRunLength = 1; }

            _liveTelemetry = new CombatRoundTelemetryV132 { Round = encounter.Round };
            _hasLiveRound = true;
            RefreshHud(snapshot);
        }

        public void NotifyEnemyDestroyed(EnemyKind kind)
        {
            if (!_hasLiveRound) return;
            _liveTelemetry.TotalKills++;
            if (IsSpecialist(kind)) _liveTelemetry.SpecialistKills++;
            if (kind == EnemyKind.Supply) _liveTelemetry.SupplyKills++;
        }

        public void NotifyPlayerDestroyed()
        {
            if (!_hasLiveRound) return;
            _liveTelemetry.PlayerLosses = Mathf.Min(8, _liveTelemetry.PlayerLosses + 1);
        }

        public static EnemyCommandPostureV132 PostureFor(EnemyKind kind)
        {
            AdaptiveEnemyCommandDirector d = Instance;
            if (d == null) return new EnemyCommandPostureV132 { MovementScale = 1f, ReloadScale = 1f, SpreadScale = 1f, PreferPlayer = false };
            return AdaptiveEnemyCommandPlannerV132.PostureFor(d._plan, kind);
        }

        public static bool PreferPlayer(EnemyKind kind) => PostureFor(kind).PreferPlayer;
        public static float MovementScale(EnemyKind kind) => PostureFor(kind).MovementScale;
        public static float ReloadScale(EnemyKind kind) => PostureFor(kind).ReloadScale;
        public static float SpreadScale(EnemyKind kind) => PostureFor(kind).SpreadScale;

        private void ResetCampaign()
        {
            _history.Reset();
            _previousDoctrine = AdaptiveCommandDoctrineV132.Balanced;
            _previousRunLength = 0;
            _hasLiveRound = false;
            _liveTelemetry = default;
        }

        private void CommitPreviousRound(ObjectiveRuntimeStateV131 previousObjective)
        {
            if (!_hasLiveRound) return;
            _liveTelemetry.EagleDamage = Mathf.Clamp(previousObjective.EagleDamage, 0, 12);
            _liveTelemetry.ObjectiveResolved = previousObjective.Outcome == ObjectiveOutcomeV131.Success || previousObjective.Outcome == ObjectiveOutcomeV131.Failure;
            _liveTelemetry.ObjectiveSucceeded = previousObjective.Outcome == ObjectiveOutcomeV131.Success;
            _history.Push(_liveTelemetry);
        }

        private void RefreshHud(CombatHistorySnapshotV132 snapshot)
        {
            string forced = _plan.ForcedStride >= AdaptiveEnemyCommandPlannerV132.MinForcedStride
                ? _plan.ForcedKind + "@" + _plan.ForcedStride
                : "NONE";
            _hudText = "CMD " + _plan.Doctrine + " C" + Mathf.RoundToInt(_plan.Confidence01 * 100f).ToString("00") +
                       " H" + snapshot.Count + "/" + CombatHistoryBufferV132.Capacity +
                       " D" + Mathf.RoundToInt(snapshot.Distress01 * 100f).ToString("00") +
                       " P" + Mathf.RoundToInt(snapshot.OffensivePressure01 * 100f).ToString("00") +
                       " CONC " + _plan.ConcurrencyDelta.ToString("+#;-#;0") + " " + forced + " SIG " + _plan.Signature.ToString("X8");
        }

        private static bool IsSpecialist(EnemyKind kind)
        {
            return kind == EnemyKind.Fast || kind == EnemyKind.Heavy || kind == EnemyKind.Sniper ||
                   kind == EnemyKind.Siege || kind == EnemyKind.Elite || kind == EnemyKind.Boss;
        }
    }
}
