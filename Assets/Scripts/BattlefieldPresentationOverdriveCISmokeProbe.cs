using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.6 unified tactical presentation contracts.</summary>
    public sealed class BattlefieldPresentationOverdriveCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_6_BATTLEFIELD_PRESENTATION_SMOKE_OK.txt";
        public const string FailMarker = "V12_6_BATTLEFIELD_PRESENTATION_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v126-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<BattlefieldPresentationOverdriveCISmokeProbe>() != null) return;
                GameObject go = new GameObject("BattlefieldPresentationOverdriveCISmokeProbe_v12_6");
                DontDestroyOnLoad(go);
                go.AddComponent<BattlefieldPresentationOverdriveCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!BattlefieldPresentationOverdriveDirector.ConfigurationValid) { Fail("v12.6 presentation configuration invalid"); return; }
                if (BattlefieldPresentationOverdriveDirector.Instance == null) { Fail("v12.6 runtime presentation director not installed"); return; }
                if (!SignalsIntelligenceFireSupportDirector.ConfigurationValid || !MobileSignalWarfareDirector.ConfigurationValid ||
                    !ReconElectronicWarfareDirector.ConfigurationValid || !LogisticsRouteIntelligenceDirector.ConfigurationValid ||
                    !OperationalSustainmentDirector.ConfigurationValid || !CombinedArmsMobileFrontDirector.ConfigurationValid)
                { Fail("v12.0-v12.5 presentation source dependency invalid"); return; }

                TacticalPresentationBudget light = BattlefieldPresentationOverdriveDirector.ComputeBudget(20, 1, false);
                TacticalPresentationBudget medium = BattlefieldPresentationOverdriveDirector.ComputeBudget(65, 5, false);
                TacticalPresentationBudget dense = BattlefieldPresentationOverdriveDirector.ComputeBudget(99, 9, false);
                TacticalPresentationBudget critical = BattlefieldPresentationOverdriveDirector.ComputeBudget(99, 9, true);

                if (light.MaxTelegraphs != BattlefieldPresentationOverdriveDirector.MaxTelegraphs || light.MaxAlerts != BattlefieldPresentationOverdriveDirector.MaxAlerts)
                { Fail("Full-detail budget does not expose configured presentation capacity"); return; }
                if (!(light.MaxTelegraphs >= medium.MaxTelegraphs && medium.MaxTelegraphs >= dense.MaxTelegraphs) ||
                    !(light.RingSegments >= medium.RingSegments && medium.RingSegments >= dense.RingSegments) ||
                    !(light.RefreshSeconds <= medium.RefreshSeconds && medium.RefreshSeconds <= dense.RefreshSeconds))
                { Fail("Presentation budget is not monotonically cheaper under late-wave pressure"); return; }
                if (dense.MaxTelegraphs < BattlefieldPresentationOverdriveDirector.MinTelegraphs || dense.RingSegments < BattlefieldPresentationOverdriveDirector.MinRingSegments ||
                    critical.MaxAlerts < BattlefieldPresentationOverdriveDirector.MinAlerts)
                { Fail("Dense-battle budget dropped below readability floor"); return; }

                int criticalScore = BattlefieldPresentationOverdriveDirector.PriorityScore(TacticalPresentationSeverity.Critical, TacticalPresentationChannel.FireSupport, true);
                int warningScore = BattlefieldPresentationOverdriveDirector.PriorityScore(TacticalPresentationSeverity.Warning, TacticalPresentationChannel.Recon, true);
                int objectiveScore = BattlefieldPresentationOverdriveDirector.PriorityScore(TacticalPresentationSeverity.Objective, TacticalPresentationChannel.Front, false);
                int routineScore = BattlefieldPresentationOverdriveDirector.PriorityScore(TacticalPresentationSeverity.Routine, TacticalPresentationChannel.Sustainment, false);
                if (!(criticalScore > warningScore && warningScore > objectiveScore && objectiveScore > routineScore))
                { Fail("Context priority ordering invalid"); return; }

                if (BattlefieldPresentationOverdriveDirector.ClampTelegraphCount(99, light) != BattlefieldPresentationOverdriveDirector.MaxTelegraphs ||
                    BattlefieldPresentationOverdriveDirector.ClampTelegraphCount(99, dense) != dense.MaxTelegraphs ||
                    BattlefieldPresentationOverdriveDirector.ClampTelegraphCount(-2, dense) != 0)
                { Fail("World-space telegraph cap invalid"); return; }

                float now = 100f;
                if (!BattlefieldPresentationOverdriveDirector.IsSnapshotFresh(now - dense.RefreshSeconds, now, dense.RefreshSeconds) ||
                    BattlefieldPresentationOverdriveDirector.IsSnapshotFresh(now - 1.2f, now, dense.RefreshSeconds))
                { Fail("Snapshot freshness window invalid"); return; }

                Vector2 friendlyStart = BattlefieldPresentationOverdriveDirector.FrontObjectivePosition(1, 0f, false);
                Vector2 friendlyGoal = BattlefieldPresentationOverdriveDirector.FrontObjectivePosition(1, 1f, false);
                Vector2 enemyStart = BattlefieldPresentationOverdriveDirector.FrontObjectivePosition(1, 0f, true);
                Vector2 enemyGoal = BattlefieldPresentationOverdriveDirector.FrontObjectivePosition(1, 1f, true);
                if (!(friendlyStart.y < friendlyGoal.y && enemyStart.y > enemyGoal.y))
                { Fail("Front objective projection orientation invalid"); return; }

                float minRadius = float.MaxValue;
                float maxRadius = 0f;
                int ringCases = 0;
                Vector2 center = new Vector2(1.25f, -0.75f);
                for (int segments = BattlefieldPresentationOverdriveDirector.MinRingSegments; segments <= BattlefieldPresentationOverdriveDirector.MaxRingSegments; segments += 4)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        Vector2 point = BattlefieldPresentationOverdriveDirector.RingPoint(center, 1.4f, i, segments);
                        float radius = Vector2.Distance(center, point);
                        minRadius = Mathf.Min(minRadius, radius);
                        maxRadius = Mathf.Max(maxRadius, radius);
                        ringCases++;
                        if (Mathf.Abs(radius - 1.4f) > 0.01f) { Fail("2.5D ring geometry escaped radius tolerance"); return; }
                    }
                }
                if (ringCases < 60) { Fail("Insufficient telegraph geometry coverage"); return; }

                TacticalPresentationSnapshot synthetic = new TacticalPresentationSnapshot
                {
                    Round = 99,
                    FrontActive = true,
                    FrontKind = MobileFrontOperationKind.EnemyBreakthrough,
                    FrontProgress = 0.80f,
                    SustainmentActive = true,
                    RouteActive = true,
                    ReconActive = true,
                    MobileSignalActive = true,
                    SigintActive = true,
                    SpoofRisk = true,
                    DecoyActive = true,
                    SuppressedRelays = 1,
                    RaidCount = 2,
                    FireWindow = true,
                    PendingShells = 3
                };
                int pressure = BattlefieldPresentationOverdriveDirector.ComputePresentationPressure(synthetic);
                if (pressure < 7 || pressure > 10) { Fail("Representative late-wave pressure did not enter dense presentation tier"); return; }

                string report =
                    "v12.6 battlefield presentation smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "budgetLight=" + light.MaxTelegraphs + "/" + light.RingSegments + "/" + light.RefreshSeconds.ToString("0.000") +
                    " budgetMedium=" + medium.MaxTelegraphs + "/" + medium.RingSegments + "/" + medium.RefreshSeconds.ToString("0.000") +
                    " budgetDense=" + dense.MaxTelegraphs + "/" + dense.RingSegments + "/" + dense.RefreshSeconds.ToString("0.000") + "\n" +
                    "priority=" + criticalScore + ">" + warningScore + ">" + objectiveScore + ">" + routineScore +
                    " pressure99=" + pressure + " ringCases=" + ringCases + " radius=" + minRadius.ToString("0.00") + "-" + maxRadius.ToString("0.00") + "\n" +
                    "telegraphCap=" + BattlefieldPresentationOverdriveDirector.MaxTelegraphs + " alertCap=" + BattlefieldPresentationOverdriveDirector.MaxAlerts + "\n";
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
            Debug.LogError("[TankRevival] v12.6 battlefield presentation smoke FAIL: " + reason);
            Application.Quit(76);
        }
    }
}
