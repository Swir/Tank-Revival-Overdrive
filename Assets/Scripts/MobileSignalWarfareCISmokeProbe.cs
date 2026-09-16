using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.4 mobile signal warfare and counter-recon raid contracts.</summary>
    public sealed class MobileSignalWarfareCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_4_MOBILE_SIGNAL_SMOKE_OK.txt";
        public const string FailMarker = "V12_4_MOBILE_SIGNAL_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v124-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<MobileSignalWarfareCISmokeProbe>() != null) return;
                GameObject go = new GameObject("MobileSignalWarfareCISmokeProbe_v12_4");
                DontDestroyOnLoad(go);
                go.AddComponent<MobileSignalWarfareCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!MobileSignalWarfareDirector.ConfigurationValid) { Fail("Mobile signal warfare configuration invalid"); return; }
                if (!ReconElectronicWarfareDirector.ConfigurationValid || !ReconElectronicWarfareDirector.ExternalWarfareBridgeValid) { Fail("v12.3 recon/EW bridge invalid"); return; }
                if (!LogisticsRouteIntelligenceDirector.ConfigurationValid) { Fail("v12.2 route intelligence dependency invalid"); return; }
                if (!OperationalSustainmentDirector.ConfigurationValid) { Fail("v12.1 operational sustainment dependency invalid"); return; }
                if (!CombinedArmsMobileFrontDirector.ConfigurationValid) { Fail("v12.0 mobile-front dependency invalid"); return; }
                if (MobileSignalWarfareDirector.Instance == null) { Fail("Mobile signal warfare runtime director not installed"); return; }
                if (ReconElectronicWarfareDirector.Instance == null) { Fail("Recon/EW runtime director not installed"); return; }

                if (MobileSignalWarfareDirector.MaxMobileJammers != 1 || MobileSignalWarfareDirector.MaxRaidActors > 3 || MobileSignalWarfareDirector.MaxRaidActors < 2)
                { Fail("Mobile-jammer/raid actor caps invalid"); return; }
                if (MobileSignalWarfareDirector.RelaySuppressionSeconds >= MobileSignalWarfareDirector.RelaySabotageCooldown)
                { Fail("Relay sabotage cooldown must allow a finite recovery gap"); return; }

                float suppressionLow = ReconElectronicWarfareDirector.ClampSuppressionSeconds(-100f);
                float suppressionHigh = ReconElectronicWarfareDirector.ClampSuppressionSeconds(100f);
                float counterLow = ReconElectronicWarfareDirector.ClampCounterJamSeconds(-100f);
                float counterHigh = ReconElectronicWarfareDirector.ClampCounterJamSeconds(100f);
                if (Mathf.Abs(suppressionLow - ReconElectronicWarfareDirector.ExternalSuppressionMinSeconds) > 0.001f ||
                    Mathf.Abs(suppressionHigh - ReconElectronicWarfareDirector.ExternalSuppressionMaxSeconds) > 0.001f ||
                    Mathf.Abs(counterLow - ReconElectronicWarfareDirector.CounterJamMinSeconds) > 0.001f ||
                    Mathf.Abs(counterHigh - ReconElectronicWarfareDirector.CounterJamMaxSeconds) > 0.001f)
                { Fail("Recon external suppression/counter-jam clamp bridge invalid"); return; }

                float q0 = MobileSignalWarfareDirector.InterceptQualityForDistance(0f);
                float qMid = MobileSignalWarfareDirector.InterceptQualityForDistance(MobileSignalWarfareDirector.InterceptRadius * 0.5f);
                float qEdge = MobileSignalWarfareDirector.InterceptQualityForDistance(MobileSignalWarfareDirector.InterceptRadius);
                float qFar = MobileSignalWarfareDirector.InterceptQualityForDistance(MobileSignalWarfareDirector.InterceptRadius * 2f);
                if (!(q0 > qMid && qMid > qEdge) || q0 > 1.001f || qEdge > 0.001f || qFar > 0.001f)
                { Fail("Transmission intercept quality is not monotonic/bounded"); return; }

                int anchorCases = 0;
                float[] progressCases = { 0f, 0.25f, 0.50f, 0.75f, 1f };
                int[] roundCases = { 60, 72, 84, 99 };
                for (int lane = 0; lane < DynamicFrontlineTerritoryDirector.LaneCount; lane++)
                {
                    for (int p = 0; p < progressCases.Length; p++)
                    {
                        for (int r = 0; r < roundCases.Length; r++)
                        {
                            Vector2 anchor = MobileSignalWarfareDirector.MobileJammerAnchor(lane, progressCases[p], roundCases[r]);
                            if (Mathf.Abs(anchor.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f || anchor.y < -4.26f || anchor.y > 4.36f)
                            { Fail("Mobile jammer anchor escaped bounded battlefield space"); return; }
                            anchorCases++;
                        }
                    }
                }
                if (anchorCases < 50) { Fail("Mobile jammer lane/progress coverage too small"); return; }

                int hp60 = MobileSignalWarfareDirector.MobileJammerHealthForRound(60);
                int hp99 = MobileSignalWarfareDirector.MobileJammerHealthForRound(99);
                if (hp60 < MobileSignalWarfareDirector.MobileJammerHealthMin || hp99 > MobileSignalWarfareDirector.MobileJammerHealthMax || hp99 < hp60)
                { Fail("Mobile jammer health scaling invalid"); return; }

                if (!MobileSignalWarfareDirector.IsRaidKind(EnemyKind.Fast) ||
                    !MobileSignalWarfareDirector.IsRaidKind(EnemyKind.Elite) ||
                    !MobileSignalWarfareDirector.IsRaidKind(EnemyKind.Sniper) ||
                    MobileSignalWarfareDirector.IsRaidKind(EnemyKind.Heavy) ||
                    MobileSignalWarfareDirector.RaidRole(EnemyKind.Fast) != SquadTacticalRole.Flanker ||
                    MobileSignalWarfareDirector.RaidRole(EnemyKind.Sniper) != SquadTacticalRole.Suppressor ||
                    MobileSignalWarfareDirector.RaidRole(EnemyKind.Elite) != SquadTacticalRole.Hunter)
                { Fail("Counter-recon raid class/role mapping invalid"); return; }

                // Physical mobile-jammer authority contract: ordinary Health + collider + kinematic body.
                GameObject authority = new GameObject("V124_MOBILE_JAMMER_AUTHORITY_PROBE");
                authority.AddComponent<BoxCollider2D>();
                Rigidbody2D body = authority.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                Health health = authority.AddComponent<Health>();
                health.Initialize(Team.Enemy, hp99);
                if (!health.Damage(1, Team.Player) || health.Current != hp99 - 1 || body.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(authority); Fail("Mobile jammer physical Health/collision authority invalid"); return; }
                Destroy(authority);

                float jammedSignal = ReconElectronicWarfareDirector.SignalQualityForState(0, true, ReconElectronicWarfareDirector.MaxRelayNodes);
                float clearSignal = ReconElectronicWarfareDirector.SignalQualityForState(0, false, ReconElectronicWarfareDirector.MaxRelayNodes);
                if (clearSignal <= jammedSignal) { Fail("Counter-jam clear-signal quality must exceed jammed quality"); return; }

                string report =
                    "v12.4 mobile signal warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "mobileJammerCap=" + MobileSignalWarfareDirector.MaxMobileJammers + " raidCap=" + MobileSignalWarfareDirector.MaxRaidActors + " anchorCases=" + anchorCases + "\n" +
                    "interceptQ0=" + q0.ToString("0.00") + " interceptQMid=" + qMid.ToString("0.00") + " interceptQEdge=" + qEdge.ToString("0.00") + "\n" +
                    "hp60=" + hp60 + " hp99=" + hp99 + " suppressionClamp=" + suppressionLow.ToString("0.0") + "-" + suppressionHigh.ToString("0.0") + " counterJamClamp=" + counterLow.ToString("0.0") + "-" + counterHigh.ToString("0.0") + "\n";
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
            Debug.LogError("[TankRevival] v12.4 mobile signal warfare smoke FAIL: " + reason);
            Application.Quit(74);
        }
    }
}
