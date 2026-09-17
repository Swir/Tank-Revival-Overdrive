using System;
using UnityEngine;

namespace TankRevival
{
    public enum ObjectiveArchetypeV131
    {
        Annihilation = 0,
        SectorDefense = 1,
        CommandBreakthrough = 2,
        EmitterHunt = 3,
        SupplyInterception = 4,
        ConvoyRescue = 5,
        Counterattack = 6
    }

    public enum BattlefieldMutatorV131
    {
        None = 0,
        ReinforcementTempo = 1,
        SupplySurge = 2,
        HunterScreen = 3,
        HeavyBreakers = 4,
        SignalFriction = 5,
        ElasticDefense = 6
    }

    public enum ObjectiveOutcomeV131
    {
        Inactive = 0,
        Active = 1,
        Success = 2,
        Failure = 3
    }

    public struct ObjectivePlanV131
    {
        public int Round;
        public ObjectiveArchetypeV131 Kind;
        public BattlefieldMutatorV131 Mutator;
        public EnemyKind PriorityKind;
        public int TargetCount;
        public int MaxEagleDamage;
        public float TimeLimitSeconds;
        public int RewardScore;
        public int CrossSystemBand;
        public int Signature;

        public string CompactLabel => Kind + " / " + Mutator;
    }

    public struct ObjectiveRuntimeBudgetV131
    {
        public int ConcurrencyDelta;
        public float SpawnIntervalScale;
        public int ForcedStride;
        public EnemyKind ForcedKind;
        public int Signature;
    }

    public struct ObjectiveRuntimeStateV131
    {
        public ObjectivePlanV131 Plan;
        public ObjectiveOutcomeV131 Outcome;
        public int TotalKills;
        public int PriorityKills;
        public int SupplyKills;
        public int EagleDamage;
        public float CounterattackDeadline;
        public bool ConvoyObserved;
        public bool ConvoyResolved;
        public bool ConvoySucceeded;
        public float ConvoyProgress01;
        public int Signature;
    }

    /// <summary>
    /// Pure deterministic v13.1 planner. It layers objective doctrine on the qualified v13.0 encounter plan
    /// and a bounded read-only readiness snapshot. TankGame remains the only round/spawn authority.
    /// </summary>
    public static class ObjectivePlannerV131
    {
        public const int PlannedRounds = 100;
        public const int ArchetypeCount = 7;
        public const int MutatorCount = 7;
        public const int MaxConcurrencyDelta = 1;
        public const float MinSpawnScale = 0.90f;
        public const float MaxSpawnScale = 1.12f;
        public const float MinTimedObjective = 12f;
        public const float MaxTimedObjective = 35f;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 && ArchetypeCount == 7 && MutatorCount == 7 && MaxConcurrencyDelta == 1 &&
            MinSpawnScale >= 0.88f && MinSpawnScale < 1f && MaxSpawnScale > 1f && MaxSpawnScale <= 1.12f &&
            MinTimedObjective >= 10f && MaxTimedObjective <= 40f;

        public static ObjectivePlanV131 PlanForRound(int requestedRound, EncounterPlan encounter, EncounterReadinessV130 readiness)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            int band = ReadinessBand(readiness);
            ObjectiveArchetypeV131 kind = ResolveKind(round, band);
            BattlefieldMutatorV131 mutator = round % 10 == 0 ? BattlefieldMutatorV131.None : ResolveMutator(round, kind, band);
            EnemyKind priority = ResolvePriorityKind(kind, round, encounter);
            int target = TargetCount(kind, round, encounter);
            int maxEagleDamage = kind == ObjectiveArchetypeV131.SectorDefense ? 1 : 99;
            float timeLimit = TimeLimitFor(kind, round);
            int reward = Mathf.Clamp(420 + round * 9 + ((int)kind + 1) * 70, 500, 1800);

            int signature = 31;
            signature = unchecked(signature * 31 + round);
            signature = unchecked(signature * 31 + encounter.Signature);
            signature = unchecked(signature * 31 + (int)kind);
            signature = unchecked(signature * 31 + (int)mutator);
            signature = unchecked(signature * 31 + (int)priority);
            signature = unchecked(signature * 31 + target);
            signature = unchecked(signature * 31 + maxEagleDamage);
            signature = unchecked(signature * 31 + Mathf.RoundToInt(timeLimit * 100f));
            signature = unchecked(signature * 31 + band);

            return new ObjectivePlanV131
            {
                Round = round,
                Kind = kind,
                Mutator = mutator,
                PriorityKind = priority,
                TargetCount = target,
                MaxEagleDamage = maxEagleDamage,
                TimeLimitSeconds = timeLimit,
                RewardScore = reward,
                CrossSystemBand = band,
                Signature = signature
            };
        }

        public static ObjectiveRuntimeBudgetV131 RuntimeBudget(ObjectivePlanV131 plan)
        {
            int delta = 0;
            float scale = 1f;
            int stride = 0;
            EnemyKind forced = EnemyKind.Basic;
            switch (plan.Mutator)
            {
                case BattlefieldMutatorV131.ReinforcementTempo:
                    delta = 1; scale = 0.92f; break;
                case BattlefieldMutatorV131.SupplySurge:
                    scale = 1.02f; stride = 4; forced = EnemyKind.Supply; break;
                case BattlefieldMutatorV131.HunterScreen:
                    scale = 0.98f; stride = 5; forced = plan.Round >= 60 ? EnemyKind.Elite : EnemyKind.Fast; break;
                case BattlefieldMutatorV131.HeavyBreakers:
                    scale = 1.00f; stride = 6; forced = plan.Round >= 55 ? EnemyKind.Siege : EnemyKind.Heavy; break;
                case BattlefieldMutatorV131.SignalFriction:
                    scale = 1.08f; stride = 5; forced = plan.Round >= 55 ? EnemyKind.Elite : EnemyKind.Sniper; break;
                case BattlefieldMutatorV131.ElasticDefense:
                    delta = -1; scale = 0.96f; break;
            }
            delta = Mathf.Clamp(delta, -MaxConcurrencyDelta, MaxConcurrencyDelta);
            scale = Mathf.Clamp(scale, MinSpawnScale, MaxSpawnScale);
            int signature = unchecked(plan.Signature * 31 + delta * 17 + Mathf.RoundToInt(scale * 1000f) + stride * 7 + (int)forced);
            return new ObjectiveRuntimeBudgetV131
            {
                ConcurrencyDelta = delta,
                SpawnIntervalScale = scale,
                ForcedStride = stride,
                ForcedKind = forced,
                Signature = signature
            };
        }

        public static EnemyKind EnemyForObjectiveSpawn(ObjectivePlanV131 plan, EncounterPlan encounter, int spawnOrdinal, EnemyKind fallback)
        {
            int ordinal = Mathf.Max(0, spawnOrdinal);
            if (encounter.BossRound) return fallback;

            if (plan.Kind == ObjectiveArchetypeV131.CommandBreakthrough && ordinal == 0)
                return plan.PriorityKind;
            if (plan.Kind == ObjectiveArchetypeV131.EmitterHunt && ordinal == 1)
                return plan.PriorityKind;
            if (plan.Kind == ObjectiveArchetypeV131.SupplyInterception && (ordinal == 1 || ordinal == 4))
                return EnemyKind.Supply;

            ObjectiveRuntimeBudgetV131 budget = RuntimeBudget(plan);
            if (budget.ForcedStride > 0 && ordinal > 0 && ordinal % budget.ForcedStride == 0)
                return budget.ForcedKind;
            return fallback;
        }

        public static int ReadinessBand(EncounterReadinessV130 readiness)
        {
            float composite = readiness.ActiveChannels > 0 ? Mathf.Clamp01(readiness.Composite) : 0.50f;
            if (composite <= 0.34f) return 0;
            if (composite >= 0.66f) return 2;
            return 1;
        }

        private static ObjectiveArchetypeV131 ResolveKind(int round, int band)
        {
            ObjectiveArchetypeV131 current = RawKind(round, band);
            if (round <= 1) return current;
            ObjectiveArchetypeV131 previous = RawKind(round - 1, band);
            if (current != previous) return current;

            if (round % 10 == 0)
                return current == ObjectiveArchetypeV131.Annihilation ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;
            if (current == ObjectiveArchetypeV131.ConvoyRescue)
                return ObjectiveArchetypeV131.Counterattack;
            int rotated = ((int)current + 1) % 6;
            return (ObjectiveArchetypeV131)rotated;
        }

        private static ObjectiveArchetypeV131 RawKind(int round, int band)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            if (r % 10 == 0)
                return ((r / 10) & 1) == 0 ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;

            if (ConvoyWarfareDirector.HasMissionForRound(r) &&
                ConvoyWarfareDirector.MissionForRound(r) != ConvoyMissionKind.EnemyInterdiction)
                return ObjectiveArchetypeV131.ConvoyRescue;

            int act = Mathf.Clamp((r - 1) / 20, 0, 4);
            int slot = PositiveMod(r * 11 + act * 3 + band * 5, 6);
            return (ObjectiveArchetypeV131)slot;
        }

        private static BattlefieldMutatorV131 ResolveMutator(int round, ObjectiveArchetypeV131 kind, int band)
        {
            int slot = PositiveMod(round * 7 + (int)kind * 5 + band * 3 + round / 10, MutatorCount);
            return (BattlefieldMutatorV131)slot;
        }

        private static EnemyKind ResolvePriorityKind(ObjectiveArchetypeV131 kind, int round, EncounterPlan encounter)
        {
            if (kind == ObjectiveArchetypeV131.CommandBreakthrough)
                return round >= 60 ? EnemyKind.Elite : round >= 35 ? EnemyKind.Heavy : EnemyKind.Fast;
            if (kind == ObjectiveArchetypeV131.EmitterHunt)
                return round >= 55 ? EnemyKind.Elite : EnemyKind.Sniper;
            if (kind == ObjectiveArchetypeV131.SupplyInterception)
                return EnemyKind.Supply;
            return encounter.PrimaryEnemy == EnemyKind.Boss ? EnemyKind.Heavy : encounter.PrimaryEnemy;
        }

        private static int TargetCount(ObjectiveArchetypeV131 kind, int round, EncounterPlan encounter)
        {
            switch (kind)
            {
                case ObjectiveArchetypeV131.CommandBreakthrough:
                case ObjectiveArchetypeV131.EmitterHunt: return 1;
                case ObjectiveArchetypeV131.SupplyInterception: return 2;
                case ObjectiveArchetypeV131.Counterattack: return Mathf.Clamp(4 + round / 40, 4, 6);
                case ObjectiveArchetypeV131.Annihilation: return encounter.EnemyCount + (encounter.BossRound ? 1 : 0);
                default: return Mathf.Max(1, encounter.EnemyCount);
            }
        }

        private static float TimeLimitFor(ObjectiveArchetypeV131 kind, int round)
        {
            float progress = (Mathf.Clamp(round, 1, 100) - 1f) / 99f;
            if (kind == ObjectiveArchetypeV131.EmitterHunt) return Mathf.Lerp(28f, 20f, progress);
            if (kind == ObjectiveArchetypeV131.Counterattack) return Mathf.Lerp(21f, 14f, progress);
            if (kind == ObjectiveArchetypeV131.ConvoyRescue) return MaxTimedObjective;
            return 0f;
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    /// <summary>Allocation-free objective state transition policy shared by runtime and packaged smoke.</summary>
    public static class ObjectiveRuntimeV131
    {
        public static ObjectiveRuntimeStateV131 Begin(ObjectivePlanV131 plan)
        {
            return new ObjectiveRuntimeStateV131
            {
                Plan = plan,
                Outcome = ObjectiveOutcomeV131.Active,
                CounterattackDeadline = -1f,
                Signature = unchecked(plan.Signature * 31 + 1)
            };
        }

        public static void NotifyEnemyDestroyed(ref ObjectiveRuntimeStateV131 state, EnemyKind kind, float elapsedSeconds)
        {
            if (state.Outcome != ObjectiveOutcomeV131.Active) return;
            state.TotalKills++;
            if (kind == state.Plan.PriorityKind) state.PriorityKills++;
            if (kind == EnemyKind.Supply) state.SupplyKills++;
            if (state.Plan.Kind == ObjectiveArchetypeV131.Counterattack && state.CounterattackDeadline < 0f)
                state.CounterattackDeadline = Mathf.Max(0f, elapsedSeconds) + state.Plan.TimeLimitSeconds;
            EvaluateImmediate(ref state, elapsedSeconds);
            Touch(ref state);
        }

        public static void NotifyEagleDamaged(ref ObjectiveRuntimeStateV131 state, int amount)
        {
            if (state.Outcome != ObjectiveOutcomeV131.Active || amount <= 0) return;
            state.EagleDamage += amount;
            if (state.Plan.Kind == ObjectiveArchetypeV131.SectorDefense && state.EagleDamage > state.Plan.MaxEagleDamage)
                state.Outcome = ObjectiveOutcomeV131.Failure;
            Touch(ref state);
        }

        public static void ObserveConvoy(ref ObjectiveRuntimeStateV131 state, bool active, bool resolved, bool success, float progress01)
        {
            if (state.Outcome != ObjectiveOutcomeV131.Active || state.Plan.Kind != ObjectiveArchetypeV131.ConvoyRescue) return;
            state.ConvoyObserved |= active || resolved;
            state.ConvoyResolved = resolved;
            state.ConvoySucceeded = resolved && success;
            state.ConvoyProgress01 = Mathf.Clamp01(progress01);
            if (resolved) state.Outcome = success ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
            Touch(ref state);
        }

        public static void Tick(ref ObjectiveRuntimeStateV131 state, float elapsedSeconds)
        {
            if (state.Outcome != ObjectiveOutcomeV131.Active) return;
            if (state.Plan.Kind == ObjectiveArchetypeV131.EmitterHunt && state.Plan.TimeLimitSeconds > 0f &&
                elapsedSeconds > state.Plan.TimeLimitSeconds && state.PriorityKills < state.Plan.TargetCount)
                state.Outcome = ObjectiveOutcomeV131.Failure;
            if (state.Plan.Kind == ObjectiveArchetypeV131.Counterattack && state.CounterattackDeadline > 0f &&
                elapsedSeconds > state.CounterattackDeadline && state.TotalKills < state.Plan.TargetCount)
                state.Outcome = ObjectiveOutcomeV131.Failure;
            Touch(ref state);
        }

        public static void FinalizeAtWaveClear(ref ObjectiveRuntimeStateV131 state)
        {
            if (state.Outcome != ObjectiveOutcomeV131.Active) return;
            switch (state.Plan.Kind)
            {
                case ObjectiveArchetypeV131.Annihilation:
                    state.Outcome = ObjectiveOutcomeV131.Success;
                    break;
                case ObjectiveArchetypeV131.SectorDefense:
                    state.Outcome = state.EagleDamage <= state.Plan.MaxEagleDamage ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
                    break;
                case ObjectiveArchetypeV131.CommandBreakthrough:
                case ObjectiveArchetypeV131.EmitterHunt:
                    state.Outcome = state.PriorityKills >= state.Plan.TargetCount ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
                    break;
                case ObjectiveArchetypeV131.SupplyInterception:
                    state.Outcome = state.SupplyKills >= state.Plan.TargetCount ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
                    break;
                case ObjectiveArchetypeV131.Counterattack:
                    state.Outcome = state.TotalKills >= state.Plan.TargetCount ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
                    break;
                case ObjectiveArchetypeV131.ConvoyRescue:
                    if (state.ConvoyResolved)
                        state.Outcome = state.ConvoySucceeded ? ObjectiveOutcomeV131.Success : ObjectiveOutcomeV131.Failure;
                    break;
            }
            Touch(ref state);
        }

        public static float Progress01(ObjectiveRuntimeStateV131 state)
        {
            switch (state.Plan.Kind)
            {
                case ObjectiveArchetypeV131.CommandBreakthrough:
                case ObjectiveArchetypeV131.EmitterHunt:
                    return Mathf.Clamp01(state.PriorityKills / (float)Mathf.Max(1, state.Plan.TargetCount));
                case ObjectiveArchetypeV131.SupplyInterception:
                    return Mathf.Clamp01(state.SupplyKills / (float)Mathf.Max(1, state.Plan.TargetCount));
                case ObjectiveArchetypeV131.ConvoyRescue:
                    return state.ConvoyProgress01;
                case ObjectiveArchetypeV131.SectorDefense:
                    return state.Outcome == ObjectiveOutcomeV131.Failure ? 0f : Mathf.Clamp01(1f - state.EagleDamage / (float)Mathf.Max(1, state.Plan.MaxEagleDamage + 1));
                default:
                    return Mathf.Clamp01(state.TotalKills / (float)Mathf.Max(1, state.Plan.TargetCount));
            }
        }

        private static void EvaluateImmediate(ref ObjectiveRuntimeStateV131 state, float elapsedSeconds)
        {
            if (state.Plan.Kind == ObjectiveArchetypeV131.CommandBreakthrough || state.Plan.Kind == ObjectiveArchetypeV131.EmitterHunt)
            {
                if (state.PriorityKills >= state.Plan.TargetCount) state.Outcome = ObjectiveOutcomeV131.Success;
            }
            else if (state.Plan.Kind == ObjectiveArchetypeV131.SupplyInterception)
            {
                if (state.SupplyKills >= state.Plan.TargetCount) state.Outcome = ObjectiveOutcomeV131.Success;
            }
            else if (state.Plan.Kind == ObjectiveArchetypeV131.Counterattack)
            {
                if (state.TotalKills >= state.Plan.TargetCount && state.CounterattackDeadline > 0f && elapsedSeconds <= state.CounterattackDeadline)
                    state.Outcome = ObjectiveOutcomeV131.Success;
            }
        }

        private static void Touch(ref ObjectiveRuntimeStateV131 state)
        {
            state.Signature = unchecked(state.Plan.Signature * 31 + (int)state.Outcome * 17 + state.TotalKills * 13 +
                state.PriorityKills * 11 + state.SupplyKills * 7 + state.EagleDamage * 5 + Mathf.RoundToInt(state.ConvoyProgress01 * 1000f));
        }
    }

    /// <summary>
    /// Runtime adapter for v13.1. It only observes canonical combat/convoy state and reports bounded directives
    /// back to TankGame. It never moves actors, spawns projectiles, applies damage or owns round transitions.
    /// </summary>
    public sealed class ObjectiveWarfareDirector : MonoBehaviour
    {
        public const float RefreshSeconds = 0.20f;
        public static ObjectiveWarfareDirector Instance { get; private set; }
        public static bool ConfigurationValid => ObjectivePlannerV131.ConfigurationValid && RefreshSeconds >= 0.15f && RefreshSeconds <= 0.35f;

        private TankGame _game;
        private ObjectiveRuntimeStateV131 _state;
        private ObjectiveRuntimeBudgetV131 _budget;
        private float _startedAt;
        private float _nextRefresh;
        private bool _rewardReported;
        private string _hudText = "OBJ STANDBY";

        public ObjectivePlanV131 CurrentPlan => _state.Plan;
        public ObjectiveRuntimeStateV131 CurrentState => _state;
        public ObjectiveRuntimeBudgetV131 CurrentRuntimeBudget => _budget;
        public string HudText => _hudText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            EnsureInstalled();
        }

        public static ObjectiveWarfareDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            ObjectiveWarfareDirector existing = FindAnyObjectByType<ObjectiveWarfareDirector>();
            if (existing != null) return existing;
            GameObject go = new GameObject("ObjectiveWarfareDirector_v13_1");
            DontDestroyOnLoad(go);
            return go.AddComponent<ObjectiveWarfareDirector>();
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

        public void BeginRound(TankGame game, EncounterPlan encounter, EncounterReadinessV130 readiness)
        {
            _game = game;
            ObjectivePlanV131 plan = ObjectivePlannerV131.PlanForRound(encounter.Round, encounter, readiness);
            _state = ObjectiveRuntimeV131.Begin(plan);
            _budget = ObjectivePlannerV131.RuntimeBudget(plan);
            _startedAt = Time.time;
            _nextRefresh = Time.time;
            _rewardReported = false;
            RefreshHud();
        }

        public void NotifyEnemyDestroyed(EnemyKind kind)
        {
            if (_state.Outcome != ObjectiveOutcomeV131.Active) return;
            ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref _state, kind, Elapsed());
            ReportResolutionIfNeeded();
            RefreshHud();
        }

        public void NotifyEagleDamaged(int amount)
        {
            if (_state.Outcome != ObjectiveOutcomeV131.Active) return;
            ObjectiveRuntimeV131.NotifyEagleDamaged(ref _state, amount);
            ReportResolutionIfNeeded();
            RefreshHud();
        }

        public static bool CanResolveRound(bool combatCleared)
        {
            if (!combatCleared) return false;
            ObjectiveWarfareDirector d = Instance;
            if (d == null || d._state.Outcome == ObjectiveOutcomeV131.Inactive) return true;
            d.RefreshRuntime(true);
            ObjectiveRuntimeV131.FinalizeAtWaveClear(ref d._state);
            d.ReportResolutionIfNeeded();
            d.RefreshHud();
            return d._state.Outcome != ObjectiveOutcomeV131.Active;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + RefreshSeconds;
            RefreshRuntime(false);
        }

        private void RefreshRuntime(bool forceConvoy)
        {
            if (_state.Outcome == ObjectiveOutcomeV131.Inactive) return;
            float elapsed = Elapsed();
            ObjectiveRuntimeV131.Tick(ref _state, elapsed);

            if (_state.Plan.Kind == ObjectiveArchetypeV131.ConvoyRescue &&
                (_state.Outcome == ObjectiveOutcomeV131.Active || forceConvoy))
            {
                ConvoyWarfareDirector convoy = FindAnyObjectByType<ConvoyWarfareDirector>();
                if (convoy != null && convoy.CurrentMission != ConvoyMissionKind.EnemyInterdiction)
                {
                    ObjectiveRuntimeV131.ObserveConvoy(ref _state, convoy.MissionActive, convoy.MissionResolved,
                        convoy.MissionSucceeded, convoy.RouteProgress01);
                }
                else if (elapsed > ObjectivePlannerV131.MaxTimedObjective && forceConvoy)
                {
                    ObjectiveRuntimeV131.ObserveConvoy(ref _state, false, true, false, 0f);
                }
            }

            ReportResolutionIfNeeded();
            RefreshHud();
        }

        private float Elapsed() => Mathf.Max(0f, Time.time - _startedAt);

        private void ReportResolutionIfNeeded()
        {
            if (_rewardReported || _state.Outcome == ObjectiveOutcomeV131.Active || _state.Outcome == ObjectiveOutcomeV131.Inactive) return;
            _rewardReported = true;
            bool success = _state.Outcome == ObjectiveOutcomeV131.Success;
            if (_game != null)
                _game.ApplyObjectiveResult(success, success ? _state.Plan.RewardScore : 0, _state.Plan.Kind.ToString());
        }

        private void RefreshHud()
        {
            ObjectivePlanV131 p = _state.Plan;
            float progress = ObjectiveRuntimeV131.Progress01(_state);
            string outcome = _state.Outcome == ObjectiveOutcomeV131.Active ? "ACTIVE" : _state.Outcome == ObjectiveOutcomeV131.Success ? "SUCCESS" : _state.Outcome == ObjectiveOutcomeV131.Failure ? "FAILED" : "STANDBY";
            string timer = string.Empty;
            if (_state.Outcome == ObjectiveOutcomeV131.Active && p.TimeLimitSeconds > 0f)
            {
                float deadline = p.Kind == ObjectiveArchetypeV131.Counterattack && _state.CounterattackDeadline > 0f
                    ? _state.CounterattackDeadline : p.TimeLimitSeconds;
                timer = " T" + Mathf.Max(0f, deadline - Elapsed()).ToString("00.0");
            }
            _hudText = "OBJ " + p.Kind + " " + outcome + " " + Mathf.RoundToInt(progress * 100f).ToString("00") + "%" + timer +
                       "   MUT " + p.Mutator + "   SIG " + p.Signature.ToString("X8");
        }
    }
}
