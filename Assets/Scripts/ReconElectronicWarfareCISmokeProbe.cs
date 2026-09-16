using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.3 reconnaissance relays, electronic jamming and counter-logistics contracts.</summary>
    public sealed class ReconElectronicWarfareCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_3_RECON_EW_SMOKE_OK.txt";
        public const string FailMarker = "V12_3_RECON_EW_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v123-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<ReconElectronicWarfareCISmokeProbe>() != null) return;
                GameObject go = new GameObject("ReconElectronicWarfareCISmokeProbe_v12_3");
                DontDestroyOnLoad(go);
                go.AddComponent<ReconElectronicWarfareCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!ReconElectronicWarfareDirector.ConfigurationValid) { Fail("Recon/EW configuration invalid"); return; }
                if (!ReconElectronicWarfareDirector.BridgeAvailable) { Fail("v12.2 route-intelligence bridge unavailable"); return; }
                if (!LogisticsRouteIntelligenceDirector.ConfigurationValid) { Fail("v12.2 route-intelligence dependency invalid"); return; }
                if (!OperationalSustainmentDirector.ConfigurationValid) { Fail("v12.1 operational sustainment dependency invalid"); return; }
                if (!CombinedArmsMobileFrontDirector.ConfigurationValid) { Fail("v12.0 mobile-front dependency invalid"); return; }
                if (ReconElectronicWarfareDirector.Instance == null) { Fail("Recon/EW runtime director not installed"); return; }
                if (LogisticsRouteIntelligenceDirector.Instance == null) { Fail("Route-intelligence runtime director not installed"); return; }

                if (ReconElectronicWarfareDirector.MaxRelayNodes != 2 || ReconElectronicWarfareDirector.MaxJammers != 1 || ReconElectronicWarfareDirector.MaxGuardActors > 3)
                { Fail("Relay/jammer/guard caps invalid"); return; }
                if (ReconElectronicWarfareDirector.RequiredPacketsForVerified != ReconElectronicWarfareDirector.MaxRelayNodes)
                { Fail("Verified intelligence threshold must require the full bounded relay network"); return; }

                if (ReconElectronicWarfareDirector.IntelForPackets(0, true) != RouteIntelState.Unknown ||
                    ReconElectronicWarfareDirector.IntelForPackets(1, true) != RouteIntelState.Contact ||
                    ReconElectronicWarfareDirector.IntelForPackets(2, true) != RouteIntelState.Verified ||
                    ReconElectronicWarfareDirector.IntelForPackets(0, false) != RouteIntelState.Contact ||
                    ReconElectronicWarfareDirector.IntelForPackets(1, false) != RouteIntelState.Contact ||
                    ReconElectronicWarfareDirector.IntelForPackets(2, false) != RouteIntelState.Verified)
                { Fail("Intel packet/jammer threshold mapping invalid"); return; }

                float jammed0 = ReconElectronicWarfareDirector.SignalQualityForState(0, true, 2);
                float jammed1 = ReconElectronicWarfareDirector.SignalQualityForState(1, true, 2);
                float jammed2 = ReconElectronicWarfareDirector.SignalQualityForState(2, true, 2);
                float clear0 = ReconElectronicWarfareDirector.SignalQualityForState(0, false, 2);
                if (!(jammed0 < jammed1 && jammed1 < jammed2 && clear0 > jammed0) || jammed2 > 1.001f)
                { Fail("Signal-quality curve is not monotonic/bounded"); return; }

                int laneCases = 0;
                int distinctRelayCases = 0;
                for (int routeLane = 0; routeLane < DynamicFrontlineTerritoryDirector.LaneCount; routeLane++)
                {
                    int relayA = ReconElectronicWarfareDirector.RelayLaneForIndex(routeLane, 0);
                    int relayB = ReconElectronicWarfareDirector.RelayLaneForIndex(routeLane, 1);
                    if (relayA < 0 || relayA >= DynamicFrontlineTerritoryDirector.LaneCount || relayB < 0 || relayB >= DynamicFrontlineTerritoryDirector.LaneCount)
                    { Fail("Relay lane placement escaped lane bounds"); return; }
                    if (relayA != relayB) distinctRelayCases++;

                    for (int round = OperationalSustainmentDirector.EarliestRound; round <= CombinedArmsMobileFrontDirector.LatestRound; round += 3)
                    {
                        int jammerLane = ReconElectronicWarfareDirector.JammerLaneForRoute(routeLane, round);
                        if (jammerLane < 0 || jammerLane >= DynamicFrontlineTerritoryDirector.LaneCount || jammerLane == routeLane)
                        { Fail("Jammer lane failed to remain bounded and off the real route lane"); return; }
                        Vector2 relayPos = ReconElectronicWarfareDirector.RelayAnchor(relayA, 0);
                        Vector2 jammerPos = ReconElectronicWarfareDirector.JammerAnchor(jammerLane, round);
                        if (Mathf.Abs(relayPos.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f || Mathf.Abs(jammerPos.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f)
                        { Fail("Recon/EW anchor escaped arena X bounds"); return; }
                        laneCases++;
                    }
                }
                if (laneCases < 18 || distinctRelayCases < 1)
                { Fail("Deterministic recon/EW lane-space coverage too small"); return; }

                if (!ReconElectronicWarfareDirector.IsEWGuardKind(EnemyKind.Fast) ||
                    !ReconElectronicWarfareDirector.IsEWGuardKind(EnemyKind.Elite) ||
                    !ReconElectronicWarfareDirector.IsEWGuardKind(EnemyKind.Sniper) ||
                    ReconElectronicWarfareDirector.IsEWGuardKind(EnemyKind.Heavy) ||
                    ReconElectronicWarfareDirector.EWGuardRole(EnemyKind.Sniper) != SquadTacticalRole.Suppressor ||
                    ReconElectronicWarfareDirector.EWGuardRole(EnemyKind.Fast) != SquadTacticalRole.Flanker ||
                    ReconElectronicWarfareDirector.EWGuardRole(EnemyKind.Elite) != SquadTacticalRole.Escort)
                { Fail("EW guard class/role mapping invalid"); return; }

                int relay60 = ReconElectronicWarfareDirector.RelayHealthForRound(60);
                int relay99 = ReconElectronicWarfareDirector.RelayHealthForRound(99);
                int jammer60 = ReconElectronicWarfareDirector.JammerHealthForRound(60);
                int jammer99 = ReconElectronicWarfareDirector.JammerHealthForRound(99);
                if (relay60 < ReconElectronicWarfareDirector.RelayHealthMin || relay99 > ReconElectronicWarfareDirector.RelayHealthMax || relay99 < relay60)
                { Fail("Relay health scaling invalid"); return; }
                if (jammer60 < ReconElectronicWarfareDirector.JammerHealthMin || jammer99 > ReconElectronicWarfareDirector.JammerHealthMax || jammer99 < jammer60)
                { Fail("Jammer health scaling invalid"); return; }

                // Physical infrastructure authority contract: ordinary Health + collider + kinematic body.
                GameObject relayAuthority = new GameObject("V123_RELAY_AUTHORITY_PROBE");
                relayAuthority.AddComponent<BoxCollider2D>();
                Rigidbody2D relayBody = relayAuthority.AddComponent<Rigidbody2D>();
                relayBody.bodyType = RigidbodyType2D.Kinematic;
                Health relayHealth = relayAuthority.AddComponent<Health>();
                relayHealth.Initialize(Team.Player, relay99);
                if (!relayHealth.Damage(1, Team.Enemy) || relayHealth.Current != relay99 - 1 || relayBody.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(relayAuthority); Fail("Relay physical Health/collision authority invalid"); return; }
                Destroy(relayAuthority);

                GameObject jammerAuthority = new GameObject("V123_JAMMER_AUTHORITY_PROBE");
                jammerAuthority.AddComponent<BoxCollider2D>();
                Rigidbody2D jammerBody = jammerAuthority.AddComponent<Rigidbody2D>();
                jammerBody.bodyType = RigidbodyType2D.Kinematic;
                Health jammerHealth = jammerAuthority.AddComponent<Health>();
                jammerHealth.Initialize(Team.Enemy, jammer99);
                if (!jammerHealth.Damage(1, Team.Player) || jammerHealth.Current != jammer99 - 1 || jammerBody.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(jammerAuthority); Fail("Jammer physical Health/collision authority invalid"); return; }
                Destroy(jammerAuthority);

                string report =
                    "v12.3 recon/electronic-warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "relays=" + ReconElectronicWarfareDirector.MaxRelayNodes + " jammerCap=" + ReconElectronicWarfareDirector.MaxJammers + " guardCap=" + ReconElectronicWarfareDirector.MaxGuardActors + " laneCases=" + laneCases + "\n" +
                    "signalJammed0=" + jammed0.ToString("0.00") + " signalJammed1=" + jammed1.ToString("0.00") + " signalJammed2=" + jammed2.ToString("0.00") + " signalClear0=" + clear0.ToString("0.00") + "\n" +
                    "relayHP60=" + relay60 + " relayHP99=" + relay99 + " jammerHP60=" + jammer60 + " jammerHP99=" + jammer99 + "\n";
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
            Debug.LogError("[TankRevival] v12.3 recon/EW smoke FAIL: " + reason);
            Application.Quit(73);
        }
    }
}
