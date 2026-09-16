using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.5 SIGINT fire support and deception contracts.</summary>
    public sealed class SignalsIntelligenceFireSupportCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_5_SIGINT_FIRE_SUPPORT_SMOKE_OK.txt";
        public const string FailMarker = "V12_5_SIGINT_FIRE_SUPPORT_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v125-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<SignalsIntelligenceFireSupportCISmokeProbe>() != null) return;
                GameObject go = new GameObject("SignalsIntelligenceFireSupportCISmokeProbe_v12_5");
                DontDestroyOnLoad(go);
                go.AddComponent<SignalsIntelligenceFireSupportCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!SignalsIntelligenceFireSupportDirector.ConfigurationValid) { Fail("v12.5 SIGINT/fire-support configuration invalid"); return; }
                if (!MobileSignalWarfareDirector.ConfigurationValid) { Fail("v12.4 mobile signal dependency invalid"); return; }
                if (!ReconElectronicWarfareDirector.ConfigurationValid || !ReconElectronicWarfareDirector.ExternalWarfareBridgeValid) { Fail("v12.3 recon/EW dependency invalid"); return; }
                if (!LogisticsRouteIntelligenceDirector.ConfigurationValid || !OperationalSustainmentDirector.ConfigurationValid || !CombinedArmsMobileFrontDirector.ConfigurationValid)
                { Fail("v12.0-v12.2 operational dependencies invalid"); return; }
                if (SignalsIntelligenceFireSupportDirector.Instance == null) { Fail("v12.5 runtime director not installed"); return; }

                if (SignalsIntelligenceFireSupportDirector.MaxEmitters != 2 || SignalsIntelligenceFireSupportDirector.MaxGuardActors != 3)
                { Fail("Emitter/guard caps invalid"); return; }
                if (SignalsIntelligenceFireSupportDirector.MaxFireMissionsPerOperation * SignalsIntelligenceFireSupportDirector.ShellsPerMission > 6)
                { Fail("Fire-support shell cap exceeds bounded budget"); return; }

                float coincident = SignalsIntelligenceFireSupportDirector.TriangulationQualityForGeometry(new Vector2(-2f, -2f), new Vector2(-2f, -2f), new Vector2(0f, 2f));
                float narrow = SignalsIntelligenceFireSupportDirector.TriangulationQualityForGeometry(new Vector2(-0.65f, -3f), new Vector2(0.65f, -3f), new Vector2(0f, 2f));
                float strong = SignalsIntelligenceFireSupportDirector.TriangulationQualityForGeometry(new Vector2(-4.5f, -2.6f), new Vector2(4.5f, -2.6f), new Vector2(0f, 2f));
                float edgeLeft = SignalsIntelligenceFireSupportDirector.TriangulationQualityForGeometry(new Vector2(-0.42f, ReconElectronicWarfareDirector.RelayY), new Vector2(0.42f, ReconElectronicWarfareDirector.RelayY + 0.55f), new Vector2(-5.82f, 4.15f));
                float edgeRight = SignalsIntelligenceFireSupportDirector.TriangulationQualityForGeometry(new Vector2(-0.42f, ReconElectronicWarfareDirector.RelayY), new Vector2(0.42f, ReconElectronicWarfareDirector.RelayY + 0.55f), new Vector2(5.82f, 4.15f));
                if (coincident > 0.001f || narrow < 0f || strong > 1.001f || strong <= narrow || strong < SignalsIntelligenceFireSupportDirector.MinTriangulationQuality)
                { Fail("Triangulation geometry is not bounded/discriminating"); return; }
                if (edgeLeft < SignalsIntelligenceFireSupportDirector.MinTriangulationQuality || edgeRight < SignalsIntelligenceFireSupportDirector.MinTriangulationQuality)
                { Fail("Edge-route relay geometry cannot achieve a playable firing solution"); return; }

                float qNear = SignalsIntelligenceFireSupportDirector.VerificationQualityForDistance(0f);
                float qMid = SignalsIntelligenceFireSupportDirector.VerificationQualityForDistance(SignalsIntelligenceFireSupportDirector.VerificationRadius * 0.5f);
                float qEdge = SignalsIntelligenceFireSupportDirector.VerificationQualityForDistance(SignalsIntelligenceFireSupportDirector.VerificationRadius);
                if (!(qNear > qMid && qMid > qEdge) || qNear > 1.001f || qEdge > 0.001f)
                { Fail("Emitter verification quality is not monotonic/bounded"); return; }

                int anchorCases = 0;
                float[] progressCases = { 0f, 0.25f, 0.50f, 0.75f, 1f };
                int[] rounds = { 72, 80, 90, 99 };
                for (int lane = 0; lane < DynamicFrontlineTerritoryDirector.LaneCount; lane++)
                {
                    for (int p = 0; p < progressCases.Length; p++)
                    {
                        for (int r = 0; r < rounds.Length; r++)
                        {
                            Vector2 trueAnchor = SignalsIntelligenceFireSupportDirector.EmitterAnchor(lane, progressCases[p], rounds[r], false);
                            Vector2 decoyAnchor = SignalsIntelligenceFireSupportDirector.EmitterAnchor(SignalsIntelligenceFireSupportDirector.AlternateLane(lane, rounds[r]), progressCases[p], rounds[r], true);
                            if (Mathf.Abs(trueAnchor.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f || trueAnchor.y < -4.21f || trueAnchor.y > 4.26f ||
                                Mathf.Abs(decoyAnchor.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f || decoyAnchor.y < -4.21f || decoyAnchor.y > 4.26f)
                            { Fail("Emitter anchor escaped bounded battlefield space"); return; }
                            anchorCases += 2;
                        }
                    }
                }
                if (anchorCases < 100) { Fail("Emitter anchor coverage too small"); return; }

                int hp72 = SignalsIntelligenceFireSupportDirector.EmitterHealthForRound(72);
                int hp99 = SignalsIntelligenceFireSupportDirector.EmitterHealthForRound(99);
                if (hp72 < SignalsIntelligenceFireSupportDirector.EmitterHealthMin || hp99 > SignalsIntelligenceFireSupportDirector.EmitterHealthMax || hp99 < hp72)
                { Fail("Emitter health scaling invalid"); return; }

                float maxScatter = 0f;
                int scatterCases = 0;
                for (int mission = 1; mission <= SignalsIntelligenceFireSupportDirector.MaxFireMissionsPerOperation; mission++)
                {
                    for (int shell = 0; shell < SignalsIntelligenceFireSupportDirector.ShellsPerMission; shell++)
                    {
                        Vector2 scatter = SignalsIntelligenceFireSupportDirector.FireMissionScatter(99, mission, shell);
                        maxScatter = Mathf.Max(maxScatter, scatter.magnitude);
                        if (scatter.magnitude > SignalsIntelligenceFireSupportDirector.FireSupportScatterRadius + 0.01f)
                        { Fail("Fire mission scatter exceeded cap"); return; }
                        scatterCases++;
                    }
                }
                if (scatterCases != SignalsIntelligenceFireSupportDirector.MaxFireMissionsPerOperation * SignalsIntelligenceFireSupportDirector.ShellsPerMission)
                { Fail("Fire mission scatter case count invalid"); return; }

                if (!SignalsIntelligenceFireSupportDirector.IsGuardKind(EnemyKind.Fast) ||
                    !SignalsIntelligenceFireSupportDirector.IsGuardKind(EnemyKind.Elite) ||
                    !SignalsIntelligenceFireSupportDirector.IsGuardKind(EnemyKind.Sniper) ||
                    SignalsIntelligenceFireSupportDirector.IsGuardKind(EnemyKind.Heavy) ||
                    SignalsIntelligenceFireSupportDirector.GuardRole(EnemyKind.Fast) != SquadTacticalRole.Flanker ||
                    SignalsIntelligenceFireSupportDirector.GuardRole(EnemyKind.Sniper) != SquadTacticalRole.Suppressor ||
                    SignalsIntelligenceFireSupportDirector.GuardRole(EnemyKind.Elite) != SquadTacticalRole.Hunter)
                { Fail("Counter-SIGINT guard class/role mapping invalid"); return; }

                GameObject authority = new GameObject("V125_EMITTER_AUTHORITY_PROBE");
                BoxCollider2D collider = authority.AddComponent<BoxCollider2D>();
                Rigidbody2D body = authority.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                Health health = authority.AddComponent<Health>();
                health.Initialize(Team.Enemy, hp99);
                bool damaged = health.Damage(1, Team.Player);
                if (!damaged || health.Current != hp99 - 1 || collider == null || body.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(authority); Fail("Physical emitter Health/collision authority invalid"); return; }
                Destroy(authority);

                string report =
                    "v12.5 SIGINT fire-support smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "emitters=" + SignalsIntelligenceFireSupportDirector.MaxEmitters + " guards=" + SignalsIntelligenceFireSupportDirector.MaxGuardActors + " maxSupportShells=" + (SignalsIntelligenceFireSupportDirector.MaxFireMissionsPerOperation * SignalsIntelligenceFireSupportDirector.ShellsPerMission) + "\n" +
                    "triCoincident=" + coincident.ToString("0.00") + " triNarrow=" + narrow.ToString("0.00") + " triStrong=" + strong.ToString("0.00") + " edgeLeft=" + edgeLeft.ToString("0.00") + " edgeRight=" + edgeRight.ToString("0.00") + " anchorCases=" + anchorCases + "\n" +
                    "hp72=" + hp72 + " hp99=" + hp99 + " scatterCases=" + scatterCases + " maxScatter=" + maxScatter.ToString("0.00") + "\n";
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
            Debug.LogError("[TankRevival] v12.5 SIGINT fire-support smoke FAIL: " + reason);
            Application.Quit(75);
        }
    }
}
