using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for v13.2 bounded combat history and adaptive enemy command.</summary>
    public sealed class AdaptiveEnemyCommandCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_2_ADAPTIVE_COMMAND_OK.txt";
        public const string FailMarker = "V13_2_ADAPTIVE_COMMAND_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v132-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<AdaptiveEnemyCommandCISmokeProbe>() != null) return;
                GameObject go = new GameObject("AdaptiveEnemyCommandCISmokeProbe_v13_2");
                DontDestroyOnLoad(go);
                go.AddComponent<AdaptiveEnemyCommandCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!AdaptiveEnemyCommandPlannerV132.ConfigurationValid || !AdaptiveEnemyCommandDirector.ConfigurationValid)
                { Fail("v13.2 adaptive-command configuration invalid"); return; }
                if (AdaptiveEnemyCommandDirector.Instance == null)
                { Fail("adaptive enemy command runtime director was not installed"); return; }

                EncounterReadinessV130 neutralReadiness = EncounterCrossSystemDoctrineV130.UniformSnapshot(0.50f, 0);
                CombatHistorySnapshotV132 neutral = BuildHistory(4, 5, 2, 0, 0, 0, true).Snapshot();
                CombatHistorySnapshotV132 offensive = BuildHistory(4, 12, 6, 0, 0, 0, true).Snapshot();
                CombatHistorySnapshotV132 supplyHeavy = BuildHistory(4, 6, 1, 3, 0, 0, true).Snapshot();
                CombatHistorySnapshotV132 distressed = BuildHistory(4, 2, 0, 0, 2, 3, false).Snapshot();

                EncounterPlan sampleEncounter = EncounterPlannerV130.PlanForRound(73);
                ObjectivePlanV131 sampleObjective = ObjectivePlannerV131.PlanForRound(73, sampleEncounter, neutralReadiness);
                AdaptiveCommandPlanV132 offensePlan = AdaptiveEnemyCommandPlannerV132.Resolve(73, sampleEncounter, sampleObjective, offensive, AdaptiveCommandDoctrineV132.Balanced, 0);
                AdaptiveCommandPlanV132 supplyPlan = AdaptiveEnemyCommandPlannerV132.Resolve(73, sampleEncounter, sampleObjective, supplyHeavy, AdaptiveCommandDoctrineV132.Balanced, 0);
                AdaptiveCommandPlanV132 distressPlan = AdaptiveEnemyCommandPlannerV132.Resolve(73, sampleEncounter, sampleObjective, distressed, AdaptiveCommandDoctrineV132.Balanced, 0);
                if (offensePlan.Doctrine != AdaptiveCommandDoctrineV132.HunterKiller)
                { Fail("high offensive pressure did not resolve to HunterKiller"); return; }
                if (supplyPlan.Doctrine != AdaptiveCommandDoctrineV132.Interdiction)
                { Fail("repeated supply interception did not resolve to Interdiction"); return; }
                if (distressPlan.Doctrine != AdaptiveCommandDoctrineV132.RecoveryWindow || distressPlan.ConcurrencyDelta != -1 || distressPlan.SpawnIntervalScale < 1f || distressPlan.ForcedStride != 0)
                { Fail("distress recovery doctrine violated anti-snowball contract"); return; }

                CombatHistoryBufferV132 cap = new CombatHistoryBufferV132();
                for (int i = 0; i < 12; i++)
                    cap.Push(new CombatRoundTelemetryV132 { Round = i + 1, TotalKills = 4 + i, SpecialistKills = 2, ObjectiveResolved = true, ObjectiveSucceeded = (i & 1) == 0 });
                CombatHistorySnapshotV132 cappedA = cap.Snapshot();
                CombatHistorySnapshotV132 cappedB = cap.Snapshot();
                if (cap.Count != CombatHistoryBufferV132.Capacity || cappedA.Count != CombatHistoryBufferV132.Capacity || cappedA.Signature != cappedB.Signature)
                { Fail("fixed history capacity/determinism regressed"); return; }

                AdaptiveCommandDoctrineV132 previousDoctrine = AdaptiveCommandDoctrineV132.Balanced;
                int previousRun = 0;
                int signatureFold = 31;
                int recoveryCount = 0;
                int forcedChecks = 0;
                for (int round = 1; round <= 100; round++)
                {
                    EncounterPlan encounter = EncounterPlannerV130.PlanForRound(round);
                    ObjectivePlanV131 objective = ObjectivePlannerV131.PlanForRound(round, encounter, neutralReadiness);
                    CombatHistorySnapshotV132 history = round % 17 == 0 ? distressed : round % 11 == 0 ? offensive : neutral;
                    AdaptiveCommandPlanV132 plan = AdaptiveEnemyCommandPlannerV132.Resolve(round, encounter, objective, history, previousDoctrine, previousRun);
                    AdaptiveCommandPlanV132 repeat = AdaptiveEnemyCommandPlannerV132.Resolve(round, encounter, objective, history, previousDoctrine, previousRun);
                    if (plan.Signature != repeat.Signature || plan.Doctrine != repeat.Doctrine)
                    { Fail("adaptive command determinism mismatch at round " + round); return; }
                    if ((int)plan.Doctrine < 0 || (int)plan.Doctrine >= AdaptiveEnemyCommandPlannerV132.DoctrineCount ||
                        plan.DoctrineRunLength < 1 || plan.DoctrineRunLength > AdaptiveEnemyCommandPlannerV132.MaximumRepeatRounds)
                    { Fail("doctrine catalog/run-length invariant failed at round " + round); return; }
                    if (plan.ConcurrencyDelta < -1 || plan.ConcurrencyDelta > 1 ||
                        plan.SpawnIntervalScale < AdaptiveEnemyCommandPlannerV132.MinSpawnScale || plan.SpawnIntervalScale > AdaptiveEnemyCommandPlannerV132.MaxSpawnScale ||
                        plan.MovementScale < AdaptiveEnemyCommandPlannerV132.MinMovementScale || plan.MovementScale > AdaptiveEnemyCommandPlannerV132.MaxMovementScale ||
                        plan.ReloadScale < AdaptiveEnemyCommandPlannerV132.MinReloadScale || plan.ReloadScale > AdaptiveEnemyCommandPlannerV132.MaxReloadScale ||
                        plan.SpreadScale < AdaptiveEnemyCommandPlannerV132.MinSpreadScale || plan.SpreadScale > AdaptiveEnemyCommandPlannerV132.MaxSpreadScale)
                    { Fail("adaptive command budget escaped hard bounds at round " + round); return; }
                    if (plan.ForcedStride > 0 && plan.ForcedStride < AdaptiveEnemyCommandPlannerV132.MinForcedStride)
                    { Fail("specialist injection stride escaped lower bound at round " + round); return; }
                    if (plan.Doctrine == AdaptiveCommandDoctrineV132.RecoveryWindow) recoveryCount++;

                    EnemyKind preservedSupply = AdaptiveEnemyCommandPlannerV132.EnemyForSpawn(plan, 10, EnemyKind.Supply, false);
                    EnemyKind preservedBoss = AdaptiveEnemyCommandPlannerV132.EnemyForSpawn(plan, 10, EnemyKind.Boss, true);
                    if (preservedSupply != EnemyKind.Supply || preservedBoss != EnemyKind.Boss)
                    { Fail("adaptive composition overwrote canonical Supply/Boss authority"); return; }
                    if (plan.ForcedStride >= AdaptiveEnemyCommandPlannerV132.MinForcedStride && !encounter.BossRound)
                    {
                        EnemyKind forced = AdaptiveEnemyCommandPlannerV132.EnemyForSpawn(plan, plan.ForcedStride, EnemyKind.Basic, false);
                        if (forced != plan.ForcedKind) { Fail("bounded specialist directive was not applied"); return; }
                        forcedChecks++;
                    }

                    for (int kindValue = 0; kindValue <= (int)EnemyKind.Boss; kindValue++)
                    {
                        EnemyCommandPostureV132 posture = AdaptiveEnemyCommandPlannerV132.PostureFor(plan, (EnemyKind)kindValue);
                        if (posture.MovementScale < AdaptiveEnemyCommandPlannerV132.MinMovementScale || posture.MovementScale > AdaptiveEnemyCommandPlannerV132.MaxMovementScale ||
                            posture.ReloadScale < AdaptiveEnemyCommandPlannerV132.MinReloadScale || posture.ReloadScale > AdaptiveEnemyCommandPlannerV132.MaxReloadScale ||
                            posture.SpreadScale < AdaptiveEnemyCommandPlannerV132.MinSpreadScale || posture.SpreadScale > AdaptiveEnemyCommandPlannerV132.MaxSpreadScale)
                        { Fail("AI posture escaped hard multiplier bounds at round " + round); return; }
                    }

                    if (plan.Doctrine == previousDoctrine) previousRun = plan.DoctrineRunLength;
                    else { previousDoctrine = plan.Doctrine; previousRun = 1; }
                    signatureFold = unchecked(signatureFold * 31 + plan.Signature);
                }

                if (recoveryCount < 1 || forcedChecks < 10)
                { Fail("adaptive doctrine coverage too narrow"); return; }

                string report =
                    "v13.2 adaptive enemy command + counter-doctrine warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "historyCapacity=" + CombatHistoryBufferV132.Capacity + " doctrineCount=" + AdaptiveEnemyCommandPlannerV132.DoctrineCount +
                    " recoveryPlans=" + recoveryCount + " forcedChecks=" + forcedChecks + " signatureFold=" + signatureFold + "\n" +
                    "offense=" + offensePlan.Doctrine + " supply=" + supplyPlan.Doctrine + " distress=" + distressPlan.Doctrine +
                    " distress=" + Mathf.RoundToInt(distressed.Distress01 * 100f) + "% offensePressure=" + Mathf.RoundToInt(offensive.OffensivePressure01 * 100f) + "%\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static CombatHistoryBufferV132 BuildHistory(int count, int kills, int specialists, int supply, int playerLosses, int eagleDamage, bool objectiveSuccess)
        {
            CombatHistoryBufferV132 buffer = new CombatHistoryBufferV132();
            for (int i = 0; i < count; i++)
            {
                buffer.Push(new CombatRoundTelemetryV132
                {
                    Round = 40 + i,
                    TotalKills = kills,
                    SpecialistKills = specialists,
                    SupplyKills = supply,
                    PlayerLosses = playerLosses,
                    EagleDamage = eagleDamage,
                    ObjectiveResolved = true,
                    ObjectiveSucceeded = objectiveSuccess
                });
            }
            return buffer;
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v13.2 adaptive command smoke FAIL: " + reason);
            Application.Quit(80);
        }
    }
}
