using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class BattlefieldPresentationOverdriveCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-visual-overdrive-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("BattlefieldPresentationOverdriveCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<BattlefieldPresentationOverdriveCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() == null) { Fail("BattlefieldPresentationOverdriveDirector missing"); return; }
                if (!BattlefieldPresentationOverdriveDirector.ConfigurationValid) { Fail("Visual-overdrive configuration invalid"); return; }
                if (FindAnyObjectByType<CombatFX3DDirector>() == null) { Fail("CombatFX3DDirector missing"); return; }
                if (FindAnyObjectByType<BattlefieldSmoke3DDirector>() == null) { Fail("BattlefieldSmoke3DDirector missing"); return; }
                if (FindAnyObjectByType<CombatReadabilityDirector>() == null) { Fail("CombatReadabilityDirector missing"); return; }

                if (BattlefieldPresentationOverdriveDirector.CriticalHealthRatio >= BattlefieldPresentationOverdriveDirector.DistressedHealthRatio) { Fail("Health presentation thresholds inverted"); return; }
                if (BattlefieldPresentationOverdriveDirector.MaxThreatMarkers > 24) { Fail("Threat marker budget too high"); return; }

                var probe = new GameObject("VISUAL_OVERDRIVE_HEALTH_AUTHORITY_PROBE");
                Health health = probe.AddComponent<Health>();
                health.Initialize(Team.Player, 10);
                if (!health.Damage(3, Team.Enemy) || health.Current != 7 || health.Maximum != 10) { Destroy(probe); Fail("Authoritative Health path changed"); return; }
                health.Heal(1);
                if (health.Current != 8) { Destroy(probe); Fail("Authoritative Health healing changed"); return; }
                Destroy(probe);

                string report = "v6.5 battlefield presentation smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "distressed=" + BattlefieldPresentationOverdriveDirector.DistressedHealthRatio +
                    " critical=" + BattlefieldPresentationOverdriveDirector.CriticalHealthRatio +
                    " markerBudget=" + BattlefieldPresentationOverdriveDirector.MaxThreatMarkers +
                    " scan=" + BattlefieldPresentationOverdriveDirector.ScanIntervalSeconds + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "VISUAL_OVERDRIVE_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "VISUAL_OVERDRIVE_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.5 visual overdrive smoke FAIL: " + reason);
            Application.Quit(65);
        }
    }
}
