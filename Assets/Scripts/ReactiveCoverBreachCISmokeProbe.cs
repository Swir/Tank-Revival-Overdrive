using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Packaged-EXE qualification for v11.8 reactive cover. It validates pure structural profiles,
    /// fixed-cap breach memory, maneuver integration prerequisites and the unchanged Health authority.
    /// </summary>
    public sealed class ReactiveCoverBreachCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V118_REACTIVE_COVER_SMOKE_OK.txt";
        public const string FailMarker = "V118_REACTIVE_COVER_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-v118-reactive-cover-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<ReactiveCoverBreachCISmokeProbe>() != null) return;
                GameObject go = new GameObject("ReactiveCoverBreachCISmokeProbe_v11_8");
                DontDestroyOnLoad(go);
                go.AddComponent<ReactiveCoverBreachCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!ReactiveCoverBreachDirector.ConfigurationValid) { Fail("Reactive cover breach memory configuration invalid"); return; }
                if (!AdaptivePlatoonManeuverDirector.ConfigurationValid) { Fail("Adaptive maneuver configuration invalid"); return; }
                if (!SiegeLineWarfareDirector.ConfigurationValid) { Fail("Siege artillery configuration invalid"); return; }
                if (ReactiveCoverBreachDirector.MaxRecentBreaches > AdaptivePlatoonManeuverDirector.MaxTrackedActors) { Fail("Breach memory exceeds bounded platoon actor budget"); return; }

                int steelBasic = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Basic, 2, false);
                int steelEmp = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.EMP, 2, false);
                int steelAp = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.ArmorPiercing, 2, false);
                int steelHe = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Explosive, 2, false);
                int steelHeSplash = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Explosive, 2, true);
                int steelPlasma = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Plasma, 2, false);
                int brickBasic = Obstacle.StructuralDamageFor(ObstacleKind.Brick, AmmoType.Basic, 2, false);

                if (steelBasic != 0 || steelEmp != 0) { Fail("Ordinary/EMP fire unexpectedly damages fortified Steel"); return; }
                if (steelAp <= 0 || steelHe <= steelAp || steelPlasma <= steelAp) { Fail("Heavy ordnance hierarchy invalid for Steel"); return; }
                if (steelHeSplash <= 0 || steelHeSplash >= steelHe) { Fail("HE splash attenuation invalid"); return; }
                if (brickBasic <= 0) { Fail("Basic ammunition must remain effective against Brick"); return; }

                int startSequence = ReactiveCoverBreachDirector.Sequence;
                for (int i = 0; i < ReactiveCoverBreachDirector.MaxRecentBreaches + 4; i++)
                {
                    ReactiveCoverBreachDirector.ReportBreach(new Vector2(0.15f * i, 0.05f * (i & 1)), Team.Enemy, i % 2 == 0 ? AmmoType.Explosive : AmmoType.ArmorPiercing, i % 3 == 0 ? ObstacleKind.Steel : ObstacleKind.Brick);
                }
                if (ReactiveCoverBreachDirector.Sequence - startSequence != ReactiveCoverBreachDirector.MaxRecentBreaches + 4) { Fail("Breach sequence accounting invalid"); return; }
                if (ReactiveCoverBreachDirector.RecentCount != ReactiveCoverBreachDirector.MaxRecentBreaches) { Fail("Breach ring buffer is not capped exactly"); return; }
                if (!ReactiveCoverBreachDirector.TryFindBestBreach(Vector2.zero, new Vector2(5f, 0f), Team.Enemy, out ReactiveCoverBreachDirector.BreachSnapshot breach) || !breach.Valid)
                {
                    Fail("Recent tactical breach cannot be consumed by maneuver layer");
                    return;
                }

                GameObject authorityProbe = new GameObject("V118_HEALTH_AUTHORITY_PROBE");
                Health health = authorityProbe.AddComponent<Health>();
                health.Initialize(Team.Player, 10);
                if (!health.Damage(3, Team.Enemy) || health.Current != 7 || health.Maximum != 10)
                {
                    Destroy(authorityProbe);
                    Fail("Authoritative Health path changed");
                    return;
                }
                Destroy(authorityProbe);

                string report =
                    "v11.8 reactive cover breakthrough smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "steel basic=" + steelBasic + " AP=" + steelAp + " HE=" + steelHe + " HE-splash=" + steelHeSplash + " plasma=" + steelPlasma + "\n" +
                    "breachCap=" + ReactiveCoverBreachDirector.MaxRecentBreaches + " ttl=" + ReactiveCoverBreachDirector.RecentBreachSeconds + " exploitRange=" + ReactiveCoverBreachDirector.ManeuverExploitRange + "\n" +
                    "selectedBreachSeq=" + breach.Sequence + " ordnance=" + breach.Ordnance + " kind=" + breach.CoverKind + "\n";
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
            Debug.LogError("[TankRevival] v11.8 reactive cover smoke FAIL: " + reason);
            Application.Quit(68);
        }
    }
}
