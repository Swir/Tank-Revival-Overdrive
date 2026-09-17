using System;
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
