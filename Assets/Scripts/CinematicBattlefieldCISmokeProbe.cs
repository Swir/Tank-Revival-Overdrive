using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CinematicBattlefieldCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-cinematic-battlefield-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("CinematicBattlefieldCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<CinematicBattlefieldCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                CinematicBattlefieldDirector director = FindAnyObjectByType<CinematicBattlefieldDirector>();
                if (director == null) { Fail("CinematicBattlefieldDirector missing"); return; }
                if (!CinematicBattlefieldDirector.ConfigurationValid) { Fail("Cinematic battlefield configuration invalid"); return; }
                if (CinematicBattlefieldDirector.SectorCount != 10) { Fail("Sector atmosphere catalog must cover all 10 campaign sectors"); return; }
                if (CinematicBattlefieldDirector.MaxCameraOffset > 0.5f || CinematicBattlefieldDirector.MaxCameraZoomDelta > 0.35f) { Fail("Camera comfort bounds exceeded"); return; }
                if (CinematicBattlefieldDirector.MaxTrackMarks > 64 || CinematicBattlefieldDirector.MaxImpactScars > 48 || CinematicBattlefieldDirector.MaxDebris > 32) { Fail("Aftermath budget too high"); return; }
                if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() == null) { Fail("v6.5 visual overdrive service missing"); return; }
                if (FindAnyObjectByType<CombatReadabilityDirector>() == null) { Fail("v6.4 combat readability service missing"); return; }

                var probe = new GameObject("CINEMATIC_HEALTH_AUTHORITY_PROBE");
                Health health = probe.AddComponent<Health>();
                health.Initialize(Team.Player, 12);
                if (!health.Damage(4, Team.Enemy) || health.Current != 8 || health.Maximum != 12) { Destroy(probe); Fail("Authoritative Health damage path changed"); return; }
                health.Heal(2);
                if (health.Current != 10) { Destroy(probe); Fail("Authoritative Health healing path changed"); return; }
                Destroy(probe);

                string report = "v6.6 cinematic battlefield smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "sectors=" + CinematicBattlefieldDirector.SectorCount +
                    " cameraOffset=" + CinematicBattlefieldDirector.MaxCameraOffset +
                    " zoomDelta=" + CinematicBattlefieldDirector.MaxCameraZoomDelta +
                    " tracks=" + CinematicBattlefieldDirector.MaxTrackMarks +
                    " scars=" + CinematicBattlefieldDirector.MaxImpactScars +
                    " debris=" + CinematicBattlefieldDirector.MaxDebris + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CINEMATIC_BATTLEFIELD_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CINEMATIC_BATTLEFIELD_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.6 cinematic battlefield smoke FAIL: " + reason);
            Application.Quit(66);
        }
    }
}
