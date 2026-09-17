using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for deterministic 100-round encounter planning and boss phase warfare.</summary>
    public sealed class EncounterWarfareCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_0_ENCOUNTER_WARFARE_OK.txt";
        public const string FailMarker = "V13_0_ENCOUNTER_WARFARE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v130-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<EncounterWarfareCISmokeProbe>() != null) return;
                GameObject go = new GameObject("EncounterWarfareCISmokeProbe_v13_0");
                DontDestroyOnLoad(go);
                go.AddComponent<EncounterWarfareCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!EncounterPlannerV130.ConfigurationValid) { Fail("encounter planner configuration invalid"); return; }
                if (!EncounterWarfareDirector.ConfigurationValid) { Fail("encounter director configuration invalid"); return; }
                if (EncounterWarfareDirector.Instance == null) { Fail("encounter director was not installed at runtime"); return; }
                if (BossPhaseWarfareV130.MaxPhases != 4) { Fail("boss phase hard cap regressed"); return; }

                EncounterPlan[] plans = new EncounterPlan[EncounterPlannerV130.PlannedRounds];
                EncounterPlannerV130.BuildAll(plans);
                bool[] doctrines = new bool[8];
                int bossRounds = 0;
                int signatureFold = 17;
                int minEnemies = int.MaxValue;
                int maxEnemies = int.MinValue;
                int minAlive = int.MaxValue;
                int maxAlive = int.MinValue;
                float minInterval = float.MaxValue;
                float maxPressure = 0f;

                for (int i = 0; i < plans.Length; i++)
                {
                    int round = i + 1;
                    EncounterPlan plan = plans[i];
                    EncounterPlan repeated = EncounterPlannerV130.PlanForRound(round);
                    if (plan.Round != round || plan.Signature != repeated.Signature)
                    { Fail("round plan determinism/signature mismatch at round " + round); return; }
                    if (plan.BossRound != (round % 10 == 0))
                    { Fail("boss cadence mismatch at round " + round); return; }
                    if (plan.EnemyCount < 6 || plan.EnemyCount > EncounterPlannerV130.MaxEnemyBudget ||
                        plan.MaxAlive < 4 || plan.MaxAlive > EncounterPlannerV130.MaxConcurrentEnemies ||
                        plan.SpawnInterval < EncounterPlannerV130.MinSpawnInterval - 0.001f ||
                        plan.SpawnInterval > EncounterPlannerV130.MaxSpawnInterval + 0.001f)
                    { Fail("encounter budget exceeded at round " + round); return; }
                    if (plan.EaglePressure < 0f || plan.EaglePressure > 1f || plan.RoutePressure < 0f || plan.RoutePressure > 1f ||
                        plan.LogisticsPressure < 0f || plan.LogisticsPressure > 1f || plan.EwPressure < 0f || plan.EwPressure > 1f ||
                        plan.SigintWindow < 0.34f || plan.SigintWindow > 0.92f)
                    { Fail("pressure envelope invalid at round " + round); return; }
                    if ((int)plan.Act != Mathf.Clamp((round - 1) / 20, 0, 4))
                    { Fail("five-act progression mismatch at round " + round); return; }
                    if (plan.BossRound)
                    {
                        bossRounds++;
                        if (plan.Doctrine != EncounterDoctrine.BossGauntlet || plan.PrimaryEnemy != EnemyKind.Boss || plan.BossPhaseCount < 3 || plan.BossPhaseCount > 4)
                        { Fail("boss plan contract invalid at round " + round); return; }
                    }
                    else if (plan.BossPhaseCount != 0)
                    { Fail("non-boss round has boss phases at round " + round); return; }

                    doctrines[(int)plan.Doctrine] = true;
                    signatureFold = unchecked(signatureFold * 31 + plan.Signature);
                    minEnemies = Mathf.Min(minEnemies, plan.EnemyCount);
                    maxEnemies = Mathf.Max(maxEnemies, plan.EnemyCount);
                    minAlive = Mathf.Min(minAlive, plan.MaxAlive);
                    maxAlive = Mathf.Max(maxAlive, plan.MaxAlive);
                    minInterval = Mathf.Min(minInterval, plan.SpawnInterval);
                    maxPressure = Mathf.Max(maxPressure, plan.EaglePressure);
                }

                // Live-consumption contracts: deterministic wave composition and monotonic Orzelek fortification.
                int spawnFold = 23;
                int supplySpawns = 0;
                for (int r = 0; r < plans.Length; r++)
                {
                    EncounterPlan plan = plans[r];
                    for (int ordinal = 0; ordinal < plan.EnemyCount; ordinal++)
                    {
                        EnemyKind a = EncounterPlannerV130.EnemyForSpawn(plan, ordinal);
                        EnemyKind b = EncounterPlannerV130.EnemyForSpawn(plan, ordinal);
                        if (a != b) { Fail("spawn composition is not deterministic at round " + plan.Round); return; }
                        if (a == EnemyKind.Boss) { Fail("support wave leaked boss authority at round " + plan.Round); return; }
                        if (a == EnemyKind.Supply) supplySpawns++;
                        spawnFold = unchecked(spawnFold * 31 + (int)a);
                    }
                }
                if (supplySpawns <= 0) { Fail("deterministic campaign composition contains no supply units"); return; }

                EagleDefensePlanV130 earlyDefense = EncounterPlannerV130.EagleDefenseFor(plans[0]);
                EagleDefensePlanV130 midDefense = EncounterPlannerV130.EagleDefenseFor(plans[49]);
                EagleDefensePlanV130 lateDefense = EncounterPlannerV130.EagleDefenseFor(plans[99]);
                if (earlyDefense.Tier < 1 || lateDefense.Tier > 3 ||
                    earlyDefense.Tier > midDefense.Tier || midDefense.Tier > lateDefense.Tier ||
                    earlyDefense.WallHitPoints > midDefense.WallHitPoints || midDefense.WallHitPoints > lateDefense.WallHitPoints ||
                    earlyDefense.CrownHitPoints > midDefense.CrownHitPoints || midDefense.CrownHitPoints > lateDefense.CrownHitPoints ||
                    !lateDefense.SteelSides || lateDefense.SteelCrown || !lateDefense.DestructibleBreachLane ||
                    lateDefense.CrownHitPoints < lateDefense.WallHitPoints || lateDefense.CrownHitPoints > 5)
                { Fail("Orzelek fortification policy lost bounded destructible center breach lane"); return; }

                if (!EncounterCrossSystemDoctrineV130.ConfigurationValid)
                { Fail("cross-system doctrine configuration invalid"); return; }
                EncounterPlan pressurePlan = plans[79];
                EncounterRuntimeBudgetV130 neutralBudget = EncounterCrossSystemDoctrineV130.Resolve(pressurePlan, EncounterCrossSystemDoctrineV130.UniformSnapshot(0.50f, 0));
                EncounterRuntimeBudgetV130 hostileBudget = EncounterCrossSystemDoctrineV130.Resolve(pressurePlan, EncounterCrossSystemDoctrineV130.UniformSnapshot(0.10f, 6));
                EncounterRuntimeBudgetV130 favorableBudget = EncounterCrossSystemDoctrineV130.Resolve(pressurePlan, EncounterCrossSystemDoctrineV130.UniformSnapshot(0.90f, 6));
                if (neutralBudget.ConcurrencyDelta != 0 || neutralBudget.MaxAlive != pressurePlan.MaxAlive ||
                    hostileBudget.ConcurrencyDelta != 1 || favorableBudget.ConcurrencyDelta != -1 ||
                    hostileBudget.MaxAlive > EncounterPlannerV130.MaxConcurrentEnemies || favorableBudget.MaxAlive < 4 ||
                    hostileBudget.SpawnInterval < EncounterPlannerV130.MinSpawnInterval ||
                    favorableBudget.SpawnInterval > EncounterPlannerV130.MaxSpawnInterval ||
                    hostileBudget.SpawnInterval >= neutralBudget.SpawnInterval || favorableBudget.SpawnInterval <= neutralBudget.SpawnInterval)
                { Fail("cross-system pressure/relief budget is not bounded and directional"); return; }

                if (bossRounds != 10) { Fail("expected exactly ten boss rounds, got " + bossRounds); return; }
                int doctrineCount = 0;
                for (int i = 0; i < doctrines.Length; i++) if (doctrines[i]) doctrineCount++;
                if (doctrineCount != 8) { Fail("all eight encounter doctrines are not represented"); return; }

                for (int block = 0; block < 10; block++)
                {
                    bool[] blockDoctrines = new bool[8];
                    int unique = 0;
                    for (int r = block * 10; r < block * 10 + 10; r++) blockDoctrines[(int)plans[r].Doctrine] = true;
                    for (int d = 0; d < blockDoctrines.Length; d++) if (blockDoctrines[d]) unique++;
                    if (unique < 5) { Fail("10-round block lacks doctrine variety: block " + (block + 1)); return; }
                }

                if (BossPhaseWarfareV130.PhaseCountForRound(10) != 3 || BossPhaseWarfareV130.PhaseCountForRound(50) != 4 || BossPhaseWarfareV130.PhaseCountForRound(100) != 4)
                { Fail("boss phase-count progression invalid"); return; }
                if (BossPhaseWarfareV130.ResolvePhase(10, 1f, false, false) != 1 ||
                    BossPhaseWarfareV130.ResolvePhase(10, 0.69f, false, false) != 2 ||
                    BossPhaseWarfareV130.ResolvePhase(10, 0.41f, false, false) != 3 ||
                    BossPhaseWarfareV130.ResolvePhase(100, 0.17f, false, false) != 4)
                { Fail("health-driven boss phase thresholds invalid"); return; }
                if (BossPhaseWarfareV130.ResolvePhase(100, 0.80f, true, false) != 2 ||
                    BossPhaseWarfareV130.ResolvePhase(100, 0.39f, false, true) != 4)
                { Fail("component-casualty boss phase escalation invalid"); return; }
                if (!(BossPhaseWarfareV130.CadenceScale(1) > BossPhaseWarfareV130.CadenceScale(2) &&
                      BossPhaseWarfareV130.CadenceScale(2) > BossPhaseWarfareV130.CadenceScale(3) &&
                      BossPhaseWarfareV130.CadenceScale(3) > BossPhaseWarfareV130.CadenceScale(4)))
                { Fail("boss cadence escalation is not monotonic"); return; }

                string report =
                    "v13.0 deterministic encounter planner + boss phase warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "plans=" + plans.Length + " bossRounds=" + bossRounds + " doctrines=" + doctrineCount + " signatureFold=" + signatureFold + "\n" +
                    "enemyBudget=" + minEnemies + ".." + maxEnemies + " concurrent=" + minAlive + ".." + maxAlive +
                    " minSpawnInterval=" + minInterval.ToString("0.000") + " maxEaglePressure=" + maxPressure.ToString("0.000") + "\n" +
                    "bossPhases=3..4 maxTrackedRounds=" + EncounterWarfareDirector.MaxTrackedRounds + " maxTrackedBosses=" + EncounterWarfareDirector.MaxTrackedBosses + "\n" +
                    "spawnFold=" + spawnFold + " supplySpawns=" + supplySpawns + " fortificationTiers=" + earlyDefense.Tier + "/" + midDefense.Tier + "/" + lateDefense.Tier + " breachLane=" + lateDefense.DestructibleBreachLane + "\n" +
                    "crossSystem=6 channels hostileDelta=" + hostileBudget.ConcurrencyDelta + " favorableDelta=" + favorableBudget.ConcurrencyDelta + " neutralInterval=" + neutralBudget.SpawnInterval.ToString("0.000") + "\n";
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
            Debug.LogError("[TankRevival] v13.0 encounter warfare smoke FAIL: " + reason);
            Application.Quit(78);
        }
    }
}
