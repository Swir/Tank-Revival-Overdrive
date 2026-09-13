using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CombatReadabilityCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-combat-readability-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("CombatReadabilityCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<CombatReadabilityCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                CombatReadabilityDirector director = FindAnyObjectByType<CombatReadabilityDirector>();
                if (director == null) { Fail("CombatReadabilityDirector missing"); return; }
                if (!CombatReadabilityDirector.ConfigurationValid) { Fail("Adaptive HUD configuration invalid"); return; }
                if (!CombatReadabilityDirector.LegacyPanelSuppressionEnabled) { Fail("Legacy tactical panel suppression must be enabled by default"); return; }
                if (CombatReadabilityDirector.DensityModeCount != Enum.GetValues(typeof(CombatHudDensity)).Length) { Fail("HUD density catalog mismatch"); return; }
                if (CombatReadabilityDirector.MinimalLineBudget >= CombatReadabilityDirector.FocusLineBudget || CombatReadabilityDirector.FocusLineBudget >= CombatReadabilityDirector.StandardLineBudget) { Fail("HUD density line budgets are not strictly bounded"); return; }
                if (CombatReadabilityDirector.SuppressedLegacyDirectorCount != 6) { Fail("Expected six consolidated tactical legacy panels"); return; }

                if (FindAnyObjectByType<OrzelekFortressDirector>() == null) { Fail("OrzelekFortressDirector missing"); return; }
                if (FindAnyObjectByType<EnemyCommandNetworkDirector>() == null) { Fail("EnemyCommandNetworkDirector missing"); return; }
                if (FindAnyObjectByType<DynamicBattlefieldDirector>() == null) { Fail("DynamicBattlefieldDirector missing"); return; }
                if (FindAnyObjectByType<MultiStageOperationDirector>() == null) { Fail("MultiStageOperationDirector missing"); return; }
                if (FindAnyObjectByType<ConvoyWarfareDirector>() == null) { Fail("ConvoyWarfareDirector missing"); return; }
                if (FindAnyObjectByType<CombinedArmsDirector>() == null) { Fail("CombinedArmsDirector missing"); return; }

                var healthProbe = new GameObject("READABILITY_HEALTH_AUTHORITY_PROBE");
                Health health = healthProbe.AddComponent<Health>();
                health.Initialize(Team.Player, 7);
                if (!health.Damage(2, Team.Enemy) || health.Current != 5) { Destroy(healthProbe); Fail("Authoritative Health path changed by HUD milestone"); return; }
                Destroy(healthProbe);

                string report = "v6.4 combat readability smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "densityModes=" + CombatReadabilityDirector.DensityModeCount +
                    " budgets=" + CombatReadabilityDirector.MinimalLineBudget + "/" + CombatReadabilityDirector.FocusLineBudget + "/" + CombatReadabilityDirector.StandardLineBudget +
                    " suppressedLegacyPanels=" + CombatReadabilityDirector.SuppressedLegacyDirectorCount +
                    " suppressionEnabled=" + CombatReadabilityDirector.LegacyPanelSuppressionEnabled + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_READABILITY_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_READABILITY_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.4 combat readability smoke FAIL: " + reason);
            Application.Quit(64);
        }
    }
}
