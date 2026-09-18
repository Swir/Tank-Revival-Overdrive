using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20070)]
    public sealed class LateRoundPerformanceCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v137-smoke")) return;
            if (FindAnyObjectByType<LateRoundPerformanceCISmokeProbe>() != null) return;
            GameObject go = new GameObject("LateRoundPerformanceCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<LateRoundPerformanceCISmokeProbe>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true, "planner=PASS budgets=PASS hysteresis-config=PASS pool-policy=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(37);
            }
        }

        private static void RunContracts()
        {
            if (!LateRoundPerformancePlannerV137.ConfigurationValid)
                throw new InvalidOperationException("v13.7 configuration invalid");

            LateRoundPerformanceProfileV137 normal = LateRoundPerformancePlannerV137.Plan(
                20, 8, 16, 1, WarfarePerformanceGovernor.BudgetTier.Full);
            LateRoundPerformanceProfileV137 dense = LateRoundPerformancePlannerV137.Plan(
                85, 24, 42, 6, WarfarePerformanceGovernor.BudgetTier.Full);
            LateRoundPerformanceProfileV137 critical = LateRoundPerformancePlannerV137.Plan(
                100, 36, 60, 10, WarfarePerformanceGovernor.BudgetTier.Full);
            LateRoundPerformanceProfileV137 governorCritical = LateRoundPerformancePlannerV137.Plan(
                10, 2, 5, 0, WarfarePerformanceGovernor.BudgetTier.Survival);

            Require(normal.Band == LateRoundPressureBandV137.Normal, "normal classification");
            Require(dense.Band == LateRoundPressureBandV137.Dense, "dense classification");
            Require(critical.Band == LateRoundPressureBandV137.Critical, "critical classification");
            Require(governorCritical.Band == LateRoundPressureBandV137.Critical, "governor floor classification");

            Require(normal.TrailTokens > dense.TrailTokens && dense.TrailTokens > critical.TrailTokens, "trail budget monotonicity");
            Require(normal.MicroTokens > dense.MicroTokens && dense.MicroTokens > critical.MicroTokens, "micro budget monotonicity");
            Require(normal.TacticalTokens > dense.TacticalTokens && dense.TacticalTokens > critical.TacticalTokens, "tactical budget monotonicity");
            Require(normal.ExplosionSparkCap > dense.ExplosionSparkCap && dense.ExplosionSparkCap > critical.ExplosionSparkCap, "spark cap monotonicity");
            Require(normal.ExplosionSmokeCap > dense.ExplosionSmokeCap && dense.ExplosionSmokeCap > critical.ExplosionSmokeCap, "smoke cap monotonicity");
            Require(normal.PresentationDensity > dense.PresentationDensity && dense.PresentationDensity > critical.PresentationDensity, "density monotonicity");

            Require(critical.TrailTokens >= 2 && critical.MicroTokens >= 3 && critical.TacticalTokens >= 4, "critical presentation floors");
            Require(ProjectilePool.ValidateIntegrity(out string reason), "projectile pool integrity: " + reason);
            Require(LateRoundPerformanceDirector.EnsureInstalled() != null, "runtime director installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v13.7 contract failed: " + contract);
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string details)
        {
            string file = pass ? "V13_7_LATE_ROUND_PERFORMANCE_OK.txt" : "V13_7_LATE_ROUND_PERFORMANCE_FAIL.txt";
            string text = "Tank Revival: Orzel Overdrive\nLate-round performance v13.7: " + (pass ? "PASS" : "FAIL") +
                          "\nVersion: " + Application.version + "\nUnity: " + Application.unityVersion + "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[LateRoundPerformanceCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
