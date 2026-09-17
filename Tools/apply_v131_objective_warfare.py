#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one anchor, found {count}")
    return text.replace(old, new, 1)


OBJECTIVE_CS = r'''using System;
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

            // Compare against the fully resolved previous round rather than its raw slot. This keeps
            // anti-repeat correct even when the previous round itself was rotated or convoy-bound.
            ObjectiveArchetypeV131 previous = ResolveKind(round - 1, band);
            if (current != previous) return current;

            if (round % 10 == 0)
                return current == ObjectiveArchetypeV131.Annihilation ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;
            if (current == ObjectiveArchetypeV131.ConvoyRescue)
                return ObjectiveArchetypeV131.Counterattack;
            return NextGenericKind(current);
        }

        private static ObjectiveArchetypeV131 RawKind(int round, int band)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            if (r % 10 == 0)
                return ((r / 10) & 1) == 0 ? ObjectiveArchetypeV131.SectorDefense : ObjectiveArchetypeV131.Annihilation;

            // ConvoyRescue is reserved exclusively for a real friendly convoy round. Generic doctrine
            // rotates across the other six archetypes and therefore can never invent a convoy objective.
            if (ConvoyWarfareDirector.HasMissionForRound(r) &&
                ConvoyWarfareDirector.MissionForRound(r) != ConvoyMissionKind.EnemyInterdiction)
                return ObjectiveArchetypeV131.ConvoyRescue;

            int act = Mathf.Clamp((r - 1) / 20, 0, 4);
            int slot = PositiveMod(r * 11 + act * 3 + band * 5, 6);
            return GenericKindForSlot(slot);
        }

        private static ObjectiveArchetypeV131 GenericKindForSlot(int slot)
        {
            switch (PositiveMod(slot, 6))
            {
                case 0: return ObjectiveArchetypeV131.Annihilation;
                case 1: return ObjectiveArchetypeV131.SectorDefense;
                case 2: return ObjectiveArchetypeV131.CommandBreakthrough;
                case 3: return ObjectiveArchetypeV131.EmitterHunt;
                case 4: return ObjectiveArchetypeV131.SupplyInterception;
                default: return ObjectiveArchetypeV131.Counterattack;
            }
        }

        private static ObjectiveArchetypeV131 NextGenericKind(ObjectiveArchetypeV131 current)
        {
            switch (current)
            {
                case ObjectiveArchetypeV131.Annihilation: return ObjectiveArchetypeV131.SectorDefense;
                case ObjectiveArchetypeV131.SectorDefense: return ObjectiveArchetypeV131.CommandBreakthrough;
                case ObjectiveArchetypeV131.CommandBreakthrough: return ObjectiveArchetypeV131.EmitterHunt;
                case ObjectiveArchetypeV131.EmitterHunt: return ObjectiveArchetypeV131.SupplyInterception;
                case ObjectiveArchetypeV131.SupplyInterception: return ObjectiveArchetypeV131.Counterattack;
                default: return ObjectiveArchetypeV131.Annihilation;
            }
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
'''


SMOKE_CS = r'''using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for v13.1 objective doctrine, mutators and runtime transitions.</summary>
    public sealed class ObjectiveWarfareCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_1_OBJECTIVE_WARFARE_OK.txt";
        public const string FailMarker = "V13_1_OBJECTIVE_WARFARE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v131-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<ObjectiveWarfareCISmokeProbe>() != null) return;
                GameObject go = new GameObject("ObjectiveWarfareCISmokeProbe_v13_1");
                DontDestroyOnLoad(go);
                go.AddComponent<ObjectiveWarfareCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!ObjectivePlannerV131.ConfigurationValid || !ObjectiveWarfareDirector.ConfigurationValid)
                { Fail("v13.1 objective configuration invalid"); return; }
                if (ObjectiveWarfareDirector.Instance == null)
                { Fail("objective runtime director was not installed"); return; }

                EncounterReadinessV130 neutral = EncounterCrossSystemDoctrineV130.UniformSnapshot(0.50f, 0);
                EncounterReadinessV130 hostile = EncounterCrossSystemDoctrineV130.UniformSnapshot(0.10f, 6);
                EncounterReadinessV130 favorable = EncounterCrossSystemDoctrineV130.UniformSnapshot(0.90f, 6);
                bool[] archetypes = new bool[ObjectivePlannerV131.ArchetypeCount];
                bool[] mutators = new bool[ObjectivePlannerV131.MutatorCount];
                ObjectivePlanV131[] samples = new ObjectivePlanV131[ObjectivePlannerV131.ArchetypeCount];
                bool[] haveSample = new bool[ObjectivePlannerV131.ArchetypeCount];
                int signatureFold = 19;
                int forcedCompositionChecks = 0;
                ObjectiveArchetypeV131 previous = ObjectiveArchetypeV131.Annihilation;
                bool havePrevious = false;

                for (int round = 1; round <= ObjectivePlannerV131.PlannedRounds; round++)
                {
                    EncounterPlan encounter = EncounterPlannerV130.PlanForRound(round);
                    ObjectivePlanV131 plan = ObjectivePlannerV131.PlanForRound(round, encounter, neutral);
                    ObjectivePlanV131 repeated = ObjectivePlannerV131.PlanForRound(round, encounter, neutral);
                    if (plan.Round != round || plan.Signature != repeated.Signature)
                    { Fail("objective determinism mismatch at round " + round); return; }
                    if ((int)plan.Kind < 0 || (int)plan.Kind >= ObjectivePlannerV131.ArchetypeCount ||
                        (int)plan.Mutator < 0 || (int)plan.Mutator >= ObjectivePlannerV131.MutatorCount)
                    { Fail("objective or mutator catalog index invalid at round " + round); return; }
                    if (havePrevious && plan.Kind == previous)
                    { Fail("objective anti-repeat regressed at round " + round); return; }
                    havePrevious = true; previous = plan.Kind;

                    if (encounter.BossRound &&
                        (plan.Kind != ObjectiveArchetypeV131.Annihilation && plan.Kind != ObjectiveArchetypeV131.SectorDefense || plan.Mutator != BattlefieldMutatorV131.None))
                    { Fail("boss-safe objective scheduling regressed at round " + round); return; }
                    if (plan.Kind == ObjectiveArchetypeV131.ConvoyRescue &&
                        (!ConvoyWarfareDirector.HasMissionForRound(round) || ConvoyWarfareDirector.MissionForRound(round) == ConvoyMissionKind.EnemyInterdiction))
                    { Fail("convoy rescue scheduled without friendly convoy at round " + round); return; }

                    ObjectiveRuntimeBudgetV131 budget = ObjectivePlannerV131.RuntimeBudget(plan);
                    if (budget.ConcurrencyDelta < -1 || budget.ConcurrencyDelta > 1 ||
                        budget.SpawnIntervalScale < ObjectivePlannerV131.MinSpawnScale || budget.SpawnIntervalScale > ObjectivePlannerV131.MaxSpawnScale ||
                        budget.ForcedStride < 0 || budget.ForcedStride > 6)
                    { Fail("mutator budget escaped hard bounds at round " + round); return; }

                    EnemyKind fallback = EncounterPlannerV130.EnemyForSpawn(encounter, 0);
                    if (plan.Kind == ObjectiveArchetypeV131.CommandBreakthrough)
                    {
                        if (ObjectivePlannerV131.EnemyForObjectiveSpawn(plan, encounter, 0, fallback) != plan.PriorityKind)
                        { Fail("command objective does not guarantee command target"); return; }
                        forcedCompositionChecks++;
                    }
                    if (plan.Kind == ObjectiveArchetypeV131.EmitterHunt)
                    {
                        if (ObjectivePlannerV131.EnemyForObjectiveSpawn(plan, encounter, 1, fallback) != plan.PriorityKind)
                        { Fail("emitter objective does not guarantee signal carrier"); return; }
                        forcedCompositionChecks++;
                    }
                    if (plan.Kind == ObjectiveArchetypeV131.SupplyInterception)
                    {
                        if (ObjectivePlannerV131.EnemyForObjectiveSpawn(plan, encounter, 1, fallback) != EnemyKind.Supply ||
                            ObjectivePlannerV131.EnemyForObjectiveSpawn(plan, encounter, 4, fallback) != EnemyKind.Supply)
                        { Fail("supply objective does not guarantee two intercept targets"); return; }
                        forcedCompositionChecks++;
                    }

                    archetypes[(int)plan.Kind] = true;
                    mutators[(int)plan.Mutator] = true;
                    if (!haveSample[(int)plan.Kind]) { samples[(int)plan.Kind] = plan; haveSample[(int)plan.Kind] = true; }
                    signatureFold = unchecked(signatureFold * 31 + plan.Signature + budget.Signature);
                }

                int archetypeCount = 0, mutatorCount = 0;
                for (int i = 0; i < archetypes.Length; i++) if (archetypes[i]) archetypeCount++;
                for (int i = 0; i < mutators.Length; i++) if (mutators[i]) mutatorCount++;
                if (archetypeCount != ObjectivePlannerV131.ArchetypeCount)
                { Fail("all seven objective archetypes are not represented"); return; }
                if (mutatorCount < 6)
                { Fail("battlefield mutator coverage too narrow: " + mutatorCount); return; }
                if (forcedCompositionChecks < 3)
                { Fail("forced objective composition was not exercised"); return; }

                EncounterPlan crossEncounter = EncounterPlannerV130.PlanForRound(73);
                ObjectivePlanV131 neutralPlan = ObjectivePlannerV131.PlanForRound(73, crossEncounter, neutral);
                ObjectivePlanV131 hostilePlan = ObjectivePlannerV131.PlanForRound(73, crossEncounter, hostile);
                ObjectivePlanV131 favorablePlan = ObjectivePlannerV131.PlanForRound(73, crossEncounter, favorable);
                if (neutralPlan.CrossSystemBand != 1 || hostilePlan.CrossSystemBand != 0 || favorablePlan.CrossSystemBand != 2 ||
                    neutralPlan.Signature == hostilePlan.Signature || neutralPlan.Signature == favorablePlan.Signature)
                { Fail("cross-system readiness does not influence deterministic objective doctrine"); return; }

                // Pure state-machine transition coverage for every objective archetype.
                ObjectiveRuntimeStateV131 command = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.CommandBreakthrough]);
                ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref command, command.Plan.PriorityKind, 2f);
                if (command.Outcome != ObjectiveOutcomeV131.Success) { Fail("command breakthrough success transition failed"); return; }

                ObjectiveRuntimeStateV131 emitter = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.EmitterHunt]);
                ObjectiveRuntimeV131.Tick(ref emitter, emitter.Plan.TimeLimitSeconds + 0.1f);
                if (emitter.Outcome != ObjectiveOutcomeV131.Failure) { Fail("emitter timeout failure transition failed"); return; }

                ObjectiveRuntimeStateV131 supply = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.SupplyInterception]);
                ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref supply, EnemyKind.Supply, 2f);
                ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref supply, EnemyKind.Supply, 3f);
                if (supply.Outcome != ObjectiveOutcomeV131.Success) { Fail("supply intercept success transition failed"); return; }

                ObjectiveRuntimeStateV131 defense = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.SectorDefense]);
                ObjectiveRuntimeV131.NotifyEagleDamaged(ref defense, defense.Plan.MaxEagleDamage + 1);
                if (defense.Outcome != ObjectiveOutcomeV131.Failure) { Fail("sector defense damage budget failure transition failed"); return; }

                ObjectiveRuntimeStateV131 counter = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.Counterattack]);
                for (int i = 0; i < counter.Plan.TargetCount; i++) ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref counter, EnemyKind.Basic, 1f + i);
                if (counter.Outcome != ObjectiveOutcomeV131.Success) { Fail("counterattack success transition failed"); return; }
                ObjectiveRuntimeStateV131 counterFail = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.Counterattack]);
                ObjectiveRuntimeV131.NotifyEnemyDestroyed(ref counterFail, EnemyKind.Basic, 1f);
                ObjectiveRuntimeV131.Tick(ref counterFail, counterFail.CounterattackDeadline + 0.1f);
                if (counterFail.Outcome != ObjectiveOutcomeV131.Failure) { Fail("counterattack timeout failure transition failed"); return; }

                ObjectiveRuntimeStateV131 convoy = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.ConvoyRescue]);
                ObjectiveRuntimeV131.ObserveConvoy(ref convoy, true, false, false, 0.55f);
                if (ObjectiveRuntimeV131.Progress01(convoy) < 0.54f) { Fail("convoy progress bridge failed"); return; }
                ObjectiveRuntimeV131.ObserveConvoy(ref convoy, false, true, true, 1f);
                if (convoy.Outcome != ObjectiveOutcomeV131.Success) { Fail("convoy rescue success transition failed"); return; }

                ObjectiveRuntimeStateV131 annihilation = ObjectiveRuntimeV131.Begin(samples[(int)ObjectiveArchetypeV131.Annihilation]);
                ObjectiveRuntimeV131.FinalizeAtWaveClear(ref annihilation);
                if (annihilation.Outcome != ObjectiveOutcomeV131.Success) { Fail("annihilation wave-clear transition failed"); return; }

                string report =
                    "v13.1 deterministic objective warfare + battlefield mutators smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "plans=100 archetypes=" + archetypeCount + " mutators=" + mutatorCount + " forcedChecks=" + forcedCompositionChecks + " signatureFold=" + signatureFold + "\n" +
                    "crossSystemBands=" + hostilePlan.CrossSystemBand + "/" + neutralPlan.CrossSystemBand + "/" + favorablePlan.CrossSystemBand +
                    " runtimeTransitions=command/emitter/supply/defense/counter/convoy/annihilation\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v13.1 objective warfare smoke FAIL: " + reason);
            Application.Quit(79);
        }
    }
}
'''


CHANGELOG = '''# Tank Revival: Orzeł Overdrive — v13.1 Development Changelog

## Dynamic Objective Warfare & Battlefield Mutators

- Adds a deterministic 100-round objective planner with seven playable archetypes: Annihilation, Sector Defense, Command Breakthrough, Emitter Hunt, Supply Interception, Convoy Rescue and Counterattack.
- Keeps the qualified v13.0 Encounter Planner as campaign-pressure authority while objective doctrine consumes the same bounded read-only operational readiness snapshot.
- Adds bounded battlefield mutators that can only adjust live concurrency by ±1, scale spawn cadence inside 0.90–1.12, or deterministically substitute limited support/specialist spawns.
- Objective-specific composition guarantees priority command/signal/supply targets without introducing a second spawner; TankGame still performs every actual enemy spawn.
- Adds a pure objective state machine for success/failure/progress transitions, including Eagle damage budgets, timed counterattacks, emitter deadlines and convoy outcome bridging.
- Integrates friendly v6.2 convoy missions through new read-only ConvoyWarfareDirector telemetry instead of duplicating convoy movement, Health or reward authority.
- TankGame reports objective outcome bonuses through its existing score path; objective systems never award a new currency, heal hidden HP, move actors, spawn projectiles or apply damage.
- Tactical HUD gains a compact cached objective/mutator/signature line with a 0.20 s runtime refresh budget.
- Packaged v13.1 smoke validates all 100 plans, all seven archetypes, anti-repeat scheduling, boss-safe doctrine, mutator hard bounds, cross-system readiness bands and pure runtime state transitions.

## Qualification policy

v13.1 remains IN DEVELOPMENT until one exact Windows x64 candidate passes the dedicated packaged-EXE gate plus v13.0, v12.9, v12.8 and round 80/90/100 regressions. ROADMAP checkboxes remain open until that exact SHA is qualified.
'''

META_OBJECTIVE = '''fileFormatVersion: 2
guid: b43d6e9e07e54a15a1f131da8a9c7a31
'''
META_SMOKE = '''fileFormatVersion: 2
guid: e7b0df7e8e8b4c3999f3c8f0f2e0d131
'''


def write_new_files() -> None:
    (ROOT / "Assets/Scripts/ObjectiveWarfareV131.cs").write_text(OBJECTIVE_CS, encoding="utf-8")
    (ROOT / "Assets/Scripts/ObjectiveWarfareV131.cs.meta").write_text(META_OBJECTIVE, encoding="utf-8")
    (ROOT / "Assets/Scripts/ObjectiveWarfareCISmokeProbe.cs").write_text(SMOKE_CS, encoding="utf-8")
    (ROOT / "Assets/Scripts/ObjectiveWarfareCISmokeProbe.cs.meta").write_text(META_SMOKE, encoding="utf-8")
    (ROOT / "CHANGELOG_v13.1.md").write_text(CHANGELOG, encoding="utf-8")
    (ROOT / "VERSION").write_text("v13.1.0-dev\n", encoding="utf-8")


def patch_convoy() -> None:
    path = ROOT / "Assets/Scripts/ConvoyWarfareDirector.cs"
    s = path.read_text(encoding="utf-8")
    anchor = '''        private GUIStyle _warning;\n\n        public static int MissionCount => Enum.GetValues(typeof(ConvoyMissionKind)).Length;\n'''
    replacement = '''        private GUIStyle _warning;\n\n        // v13.1 read-only bridge. Objective warfare may observe the existing convoy mission,\n        // but ConvoyWarfareDirector remains the sole route/movement/Health/reward authority.\n        public bool MissionActive => HasMissionForRound(_round) && _convoyHealth != null && !_missionResolved;\n        public bool MissionResolved => _missionResolved;\n        public bool MissionSucceeded => _missionResolved && _threat == ConvoyThreatState.Complete;\n        public ConvoyMissionKind CurrentMission => _kind;\n        public float RouteProgress01 => _routeDuration > 0f ? Mathf.Clamp01(_routeElapsed / _routeDuration) : 0f;\n        public int CurrentConvoyHealth => _convoyHealth != null ? _convoyHealth.Current : 0;\n\n        public static int MissionCount => Enum.GetValues(typeof(ConvoyMissionKind)).Length;\n'''
    s = replace_once(s, anchor, replacement, "convoy read-only objective bridge")
    path.write_text(s, encoding="utf-8")


def patch_tank_game() -> None:
    path = ROOT / "Assets/Scripts/TankGame.cs"
    s = path.read_text(encoding="utf-8")

    fields = '''        private EncounterPlan _encounterPlan;\n        private EncounterRuntimeBudgetV130 _encounterRuntimeBudget;\n        private float _nextEncounterDoctrineRefresh;\n'''
    fields_new = '''        private EncounterPlan _encounterPlan;\n        private EncounterRuntimeBudgetV130 _encounterRuntimeBudget;\n        private ObjectivePlanV131 _objectivePlan;\n        private ObjectiveRuntimeBudgetV131 _objectiveRuntimeBudget;\n        private float _nextEncounterDoctrineRefresh;\n'''
    s = replace_once(s, fields, fields_new, "TankGame objective fields")

    props = '''        public EncounterPlan CurrentEncounterPlan => _encounterPlan;\n        public EncounterRuntimeBudgetV130 CurrentEncounterRuntimeBudget => _encounterRuntimeBudget;\n'''
    props_new = '''        public EncounterPlan CurrentEncounterPlan => _encounterPlan;\n        public EncounterRuntimeBudgetV130 CurrentEncounterRuntimeBudget => _encounterRuntimeBudget;\n        public ObjectivePlanV131 CurrentObjectivePlan => _objectivePlan;\n        public ObjectiveRuntimeBudgetV131 CurrentObjectiveRuntimeBudget => _objectiveRuntimeBudget;\n'''
    s = replace_once(s, props, props_new, "TankGame objective properties")

    clear = '''            if (_enemiesToSpawn <= 0 && _aliveEnemies <= 0 && !_bossPending)\n            {\n                _roundClearAt = Time.time + 1.9f;\n'''
    clear_new = '''            bool combatCleared = _enemiesToSpawn <= 0 && _aliveEnemies <= 0 && !_bossPending;\n            if (combatCleared && ObjectiveWarfareDirector.CanResolveRound(combatCleared))\n            {\n                _roundClearAt = Time.time + 1.9f;\n'''
    s = replace_once(s, clear, clear_new, "TankGame objective round-resolution gate")

    begin = '''            _encounterPlan = EncounterPlannerV130.PlanForRound(round);\n            _enemiesToSpawn = _encounterPlan.EnemyCount;\n            _encounterRuntimeBudget = EncounterCrossSystemDoctrineV130.Resolve(_encounterPlan, EncounterCrossSystemDoctrineV130.CaptureReadOnly());\n            _maxAlive = _encounterRuntimeBudget.MaxAlive;\n            _bossPending = _encounterPlan.BossRound;\n            _spawnOrdinal = 0;\n            _nextEncounterDoctrineRefresh = Time.time + EncounterCrossSystemDoctrineV130.RefreshSeconds;\n            _nextSpawn = Time.time + _encounterRuntimeBudget.SpawnInterval;\n'''
    begin_new = '''            _encounterPlan = EncounterPlannerV130.PlanForRound(round);\n            _enemiesToSpawn = _encounterPlan.EnemyCount;\n            EncounterReadinessV130 readiness = EncounterCrossSystemDoctrineV130.CaptureReadOnly();\n            _encounterRuntimeBudget = EncounterCrossSystemDoctrineV130.Resolve(_encounterPlan, readiness);\n            ObjectiveWarfareDirector objectiveDirector = ObjectiveWarfareDirector.EnsureInstalled();\n            objectiveDirector.BeginRound(this, _encounterPlan, readiness);\n            _objectivePlan = objectiveDirector.CurrentPlan;\n            _objectiveRuntimeBudget = objectiveDirector.CurrentRuntimeBudget;\n            _maxAlive = ResolveActiveMaxAlive();\n            _bossPending = _encounterPlan.BossRound;\n            _spawnOrdinal = 0;\n            _nextEncounterDoctrineRefresh = Time.time + EncounterCrossSystemDoctrineV130.RefreshSeconds;\n            _nextSpawn = Time.time + ResolveActiveSpawnInterval();\n'''
    s = replace_once(s, begin, begin_new, "TankGame objective BeginRound integration")

    refresh = '''        private void RefreshEncounterDoctrineBudget()\n        {\n            if (Time.time < _nextEncounterDoctrineRefresh) return;\n            _nextEncounterDoctrineRefresh = Time.time + EncounterCrossSystemDoctrineV130.RefreshSeconds;\n            EncounterReadinessV130 readiness = EncounterCrossSystemDoctrineV130.CaptureReadOnly();\n            _encounterRuntimeBudget = EncounterCrossSystemDoctrineV130.Resolve(_encounterPlan, readiness);\n            _maxAlive = _encounterRuntimeBudget.MaxAlive;\n        }\n\n        private void HandleSpawning()\n'''
    refresh_new = '''        private void RefreshEncounterDoctrineBudget()\n        {\n            if (Time.time < _nextEncounterDoctrineRefresh) return;\n            _nextEncounterDoctrineRefresh = Time.time + EncounterCrossSystemDoctrineV130.RefreshSeconds;\n            EncounterReadinessV130 readiness = EncounterCrossSystemDoctrineV130.CaptureReadOnly();\n            _encounterRuntimeBudget = EncounterCrossSystemDoctrineV130.Resolve(_encounterPlan, readiness);\n            _maxAlive = ResolveActiveMaxAlive();\n        }\n\n        private int ResolveActiveMaxAlive()\n        {\n            return Mathf.Clamp(_encounterRuntimeBudget.MaxAlive + _objectiveRuntimeBudget.ConcurrencyDelta, 4, EncounterPlannerV130.MaxConcurrentEnemies);\n        }\n\n        private float ResolveActiveSpawnInterval()\n        {\n            float scaled = _encounterRuntimeBudget.SpawnInterval * _objectiveRuntimeBudget.SpawnIntervalScale;\n            return Mathf.Clamp(scaled, EncounterPlannerV130.MinSpawnInterval, EncounterPlannerV130.MaxSpawnInterval);\n        }\n\n        private void HandleSpawning()\n'''
    s = replace_once(s, refresh, refresh_new, "TankGame objective runtime budget helpers")

    spawn_kind = '''                EnemyKind kind = EncounterPlannerV130.EnemyForSpawn(_encounterPlan, _spawnOrdinal);\n'''
    spawn_kind_new = '''                EnemyKind baseKind = EncounterPlannerV130.EnemyForSpawn(_encounterPlan, _spawnOrdinal);\n                EnemyKind kind = ObjectivePlannerV131.EnemyForObjectiveSpawn(_objectivePlan, _encounterPlan, _spawnOrdinal, baseKind);\n'''
    s = replace_once(s, spawn_kind, spawn_kind_new, "TankGame objective composition")

    spawn_interval = '''                _nextSpawn = Time.time + Mathf.Max(EncounterPlannerV130.MinSpawnInterval, _encounterRuntimeBudget.SpawnInterval);\n'''
    spawn_interval_new = '''                _nextSpawn = Time.time + ResolveActiveSpawnInterval();\n'''
    s = replace_once(s, spawn_interval, spawn_interval_new, "TankGame objective spawn cadence")

    eagle = '''            _eagleHp = eagle.Current;\n            KickCamera(0.20f, 0.12f);\n'''
    eagle_new = '''            _eagleHp = eagle.Current;\n            ObjectiveWarfareDirector.Instance?.NotifyEagleDamaged(amount);\n            KickCamera(0.20f, 0.12f);\n'''
    s = replace_once(s, eagle, eagle_new, "TankGame Eagle objective telemetry")

    destroyed = '''        public void OnEnemyDestroyed(EnemyTank enemy, Vector3 position, EnemyKind kind)\n        {\n            _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);\n'''
    destroyed_new = '''        public void OnEnemyDestroyed(EnemyTank enemy, Vector3 position, EnemyKind kind)\n        {\n            _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);\n            ObjectiveWarfareDirector.Instance?.NotifyEnemyDestroyed(kind);\n'''
    s = replace_once(s, destroyed, destroyed_new, "TankGame enemy objective telemetry")

    kick = '''        public void KickCamera(float duration, float amount)\n'''
    apply_result = '''        public void ApplyObjectiveResult(bool success, int scoreBonus, string label)\n        {\n            if (_state != GameState.Playing) return;\n            if (success && scoreBonus > 0) _score += Mathf.Clamp(scoreBonus, 0, 2500);\n            ShowToast(success ? $"OBJECTIVE COMPLETE // {label} // +{scoreBonus:N0}" : $"OBJECTIVE FAILED // {label}", 1.75f);\n            BattleAudio.PlayGlobal(success ? SoundCue.RoundClear : SoundCue.EnemyShot, success ? 0.36f : 0.24f, success ? 0.04f : -0.08f);\n        }\n\n        public void KickCamera(float duration, float amount)\n'''
    s = replace_once(s, kick, apply_result, "TankGame canonical objective reward path")

    hud_box = '''            GUI.Box(new Rect(14f, 12f, 480f, 183f), string.Empty);\n'''
    hud_box_new = '''            GUI.Box(new Rect(14f, 12f, 560f, 208f), string.Empty);\n'''
    s = replace_once(s, hud_box, hud_box_new, "TankGame objective HUD box")

    hud_line = '''            GUI.Label(new Rect(28f, 153f, 455f, 25f), $"XSYS {_encounterRuntimeBudget.ActiveChannels}/6   READY {Mathf.RoundToInt(_encounterRuntimeBudget.Readiness * 100f):00}%   CONC {_encounterRuntimeBudget.ConcurrencyDelta:+#;-#;0}   RT {_encounterRuntimeBudget.Signature:X8}", _smallStyle);\n\n            if (_player != null)\n'''
    hud_line_new = '''            GUI.Label(new Rect(28f, 153f, 530f, 25f), $"XSYS {_encounterRuntimeBudget.ActiveChannels}/6   READY {Mathf.RoundToInt(_encounterRuntimeBudget.Readiness * 100f):00}%   CONC {_encounterRuntimeBudget.ConcurrencyDelta:+#;-#;0}   RT {_encounterRuntimeBudget.Signature:X8}", _smallStyle);\n            string objectiveHud = ObjectiveWarfareDirector.Instance != null ? ObjectiveWarfareDirector.Instance.HudText : "OBJ STANDBY";\n            GUI.Label(new Rect(28f, 178f, 530f, 25f), objectiveHud, _smallStyle);\n\n            if (_player != null)\n'''
    s = replace_once(s, hud_line, hud_line_new, "TankGame objective HUD telemetry")

    path.write_text(s, encoding="utf-8")


def main() -> None:
    write_new_files()
    patch_convoy()
    patch_tank_game()
    print("v13.1 objective warfare gameplay package applied")


if __name__ == "__main__":
    main()
