using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Packaged-EXE runtime qualification for v11.7 presentation integration.
    /// The probe validates bounded/read-only presentation contracts and the authoritative Health path.
    /// </summary>
    public sealed class AdaptiveAssaultPresentationCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V117_PRESENTATION_SMOKE_OK.txt";
        public const string FailMarker = "V117_PRESENTATION_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-v117-presentation-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<AdaptiveAssaultPresentationCISmokeProbe>() != null) return;
                var go = new GameObject("AdaptiveAssaultPresentationCISmokeProbe_v11_7");
                DontDestroyOnLoad(go);
                go.AddComponent<AdaptiveAssaultPresentationCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!AdaptivePlatoonManeuverDirector.ConfigurationValid) { Fail("Adaptive maneuver configuration invalid"); return; }
                if (!AdaptiveAssaultPresentationDirector.ConfigurationValid) { Fail("v11.7 presentation configuration invalid"); return; }
                if (AdaptiveAssaultPresentationDirector.MaxManeuverCues > AdaptivePlatoonManeuverDirector.MaxTrackedActors) { Fail("Presentation cue cap exceeds maneuver actor cap"); return; }
                if (AdaptiveAssaultPresentationDirector.CueHoldSeconds <= AdaptiveAssaultPresentationDirector.ScanIntervalSeconds) { Fail("Cue hold must bridge scan cadence"); return; }
                if (TacticalCombatHudDirector.PanelHeight < 90f || TacticalCombatHudDirector.PanelHeight > 120f) { Fail("Tactical HUD panel height outside readable bounds"); return; }
                if (FindAnyObjectByType<AdaptiveAssaultPresentationDirector>() == null) { Fail("AdaptiveAssaultPresentationDirector missing"); return; }
                if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() == null) { Fail("BattlefieldPresentationOverdriveDirector missing"); return; }
                if (FindAnyObjectByType<CombatFX3DDirector>() == null) { Fail("CombatFX3DDirector missing"); return; }
                if (FindAnyObjectByType<TacticalCombatHudDirector>() == null) { Fail("TacticalCombatHudDirector missing"); return; }

                AdaptivePlatoonManeuverDirector.ManeuverPresentationSnapshot snapshot =
                    AdaptivePlatoonManeuverDirector.ReadPresentationSnapshot(70);
                if (snapshot.Phase01 < 0f || snapshot.Phase01 >= 1f) { Fail("Maneuver phase normalization invalid"); return; }
                if (snapshot.CasualtyPressure01 < 0f || snapshot.CasualtyPressure01 > 1f) { Fail("Casualty pressure outside bounds"); return; }
                if (!MassBattleFxBudget.TryConsumeTacticalCue(true)) { Fail("Priority tactical cue budget rejected"); return; }

                var authorityProbe = new GameObject("V117_HEALTH_AUTHORITY_PROBE");
                Health health = authorityProbe.AddComponent<Health>();
                health.Initialize(Team.Player, 12);
                if (!health.Damage(4, Team.Enemy) || health.Current != 8 || health.Maximum != 12)
                {
                    Destroy(authorityProbe);
                    Fail("Authoritative Health damage path changed");
                    return;
                }
                health.Heal(2);
                if (health.Current != 10)
                {
                    Destroy(authorityProbe);
                    Fail("Authoritative Health healing path changed");
                    return;
                }
                Destroy(authorityProbe);

                string report =
                    "v11.7 adaptive assault presentation smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "cueCap=" + AdaptiveAssaultPresentationDirector.MaxManeuverCues +
                    " scan=" + AdaptiveAssaultPresentationDirector.ScanIntervalSeconds +
                    " hold=" + AdaptiveAssaultPresentationDirector.CueHoldSeconds +
                    " panel=" + TacticalCombatHudDirector.PanelHeight + "\n" +
                    "phase=" + snapshot.PhaseIndex +
                    " casualtyPressure=" + snapshot.CasualtyPressure01 +
                    " doctrine=" + snapshot.Doctrine + "\n";
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
            Debug.LogError("[TankRevival] v11.7 presentation smoke FAIL: " + reason);
            Application.Quit(67);
        }
    }
}
