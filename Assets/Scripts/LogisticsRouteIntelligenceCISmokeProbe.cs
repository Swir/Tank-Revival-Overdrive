using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.2 logistics route intelligence, decoy and ambush contracts.</summary>
    public sealed class LogisticsRouteIntelligenceCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_2_ROUTE_INTEL_SMOKE_OK.txt";
        public const string FailMarker = "V12_2_ROUTE_INTEL_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v122-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<LogisticsRouteIntelligenceCISmokeProbe>() != null) return;
                GameObject go = new GameObject("LogisticsRouteIntelligenceCISmokeProbe_v12_2");
                DontDestroyOnLoad(go);
                go.AddComponent<LogisticsRouteIntelligenceCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!LogisticsRouteIntelligenceDirector.ConfigurationValid) { Fail("Route-intelligence configuration invalid"); return; }
                if (!LogisticsRouteIntelligenceDirector.BridgeAvailable) { Fail("Operational sustainment route bridge unavailable"); return; }
                if (!OperationalSustainmentDirector.ConfigurationValid) { Fail("v12.1 operational sustainment dependency invalid"); return; }
                if (!CombinedArmsMobileFrontDirector.ConfigurationValid) { Fail("v12.0 mobile-front dependency invalid"); return; }
                if (!ReactiveCoverBreachDirector.ConfigurationValid) { Fail("Reactive breach dependency invalid"); return; }
                if (!TerrainIntelligenceDirector.ConfigurationValid) { Fail("Terrain-intelligence dependency invalid"); return; }
                if (LogisticsRouteIntelligenceDirector.Instance == null) { Fail("Route-intelligence runtime director not installed"); return; }
                if (OperationalSustainmentDirector.Instance == null) { Fail("Operational sustainment runtime director not installed"); return; }
                if (LogisticsRouteIntelligenceDirector.RouteCount != 3 || LogisticsRouteIntelligenceDirector.MaxReroutes != 2 || LogisticsRouteIntelligenceDirector.MaxDecoys != 1)
                { Fail("Route/reroute/decoy bounds invalid"); return; }
                if (LogisticsRouteIntelligenceDirector.MaxAmbushActors > 3)
                { Fail("Ambush actor cap exceeds bounded contract"); return; }

                for (int baseLane = 0; baseLane < DynamicFrontlineTerritoryDirector.LaneCount; baseLane++)
                {
                    for (int planIndex = 0; planIndex < LogisticsRouteIntelligenceDirector.RouteCount; planIndex++)
                    {
                        LogisticsRoutePlan plan = (LogisticsRoutePlan)planIndex;
                        int lane = LogisticsRouteIntelligenceDirector.RouteLaneForPlan(baseLane, plan);
                        if (lane < 0 || lane >= DynamicFrontlineTerritoryDirector.LaneCount)
                        { Fail("Route plan escaped lane bounds"); return; }
                    }
                    LogisticsRoutePlan direct = LogisticsRoutePlan.Direct;
                    LogisticsRoutePlan alternate = LogisticsRouteIntelligenceDirector.AlternatePlan(direct, baseLane);
                    if (LogisticsRouteIntelligenceDirector.RouteLaneForPlan(baseLane, alternate) == LogisticsRouteIntelligenceDirector.RouteLaneForPlan(baseLane, direct))
                    { Fail("Alternate route failed to change lane for base lane " + baseLane); return; }
                }

                // Test the full deterministic (eligible round x possible active-front lane) route space.
                // The previous probe incorrectly correlated baseLane=round%3, which algebraically made
                // every sampled case a decoy even though live lane selection comes from the mobile front.
                int decoyCases = 0;
                int routeCases = 0;
                int eligibleRounds = 0;
                for (int round = OperationalSustainmentDirector.EarliestRound; round <= CombinedArmsMobileFrontDirector.LatestRound; round++)
                {
                    if (!OperationalSustainmentDirector.CanLaunchForRound(round)) continue;
                    eligibleRounds++;
                    for (int baseLane = 0; baseLane < DynamicFrontlineTerritoryDirector.LaneCount; baseLane++)
                    {
                        routeCases++;
                        LogisticsRoutePlan friendly = LogisticsRouteIntelligenceDirector.InitialPlanForRound(round, Team.Player, baseLane);
                        LogisticsRoutePlan enemy = LogisticsRouteIntelligenceDirector.InitialPlanForRound(round, Team.Enemy, baseLane);
                        if ((int)friendly < 0 || (int)friendly >= LogisticsRouteIntelligenceDirector.RouteCount || (int)enemy < 0 || (int)enemy >= LogisticsRouteIntelligenceDirector.RouteCount)
                        { Fail("Deterministic route plan outside catalog at round " + round + " lane " + baseLane); return; }
                        if (LogisticsRouteIntelligenceDirector.ShouldSpawnDecoy(round, baseLane)) decoyCases++;
                    }
                }
                if (eligibleRounds < 3 || routeCases < eligibleRounds * 3 || decoyCases < 1 || decoyCases >= routeCases)
                { Fail("Decoy route-space lacks bounded variation: eligible=" + eligibleRounds + " cases=" + routeCases + " decoy=" + decoyCases); return; }

                float closeThreat = LogisticsRouteIntelligenceDirector.ProximityThreat(1f);
                float midThreat = LogisticsRouteIntelligenceDirector.ProximityThreat(4f);
                float farThreat = LogisticsRouteIntelligenceDirector.ProximityThreat(LogisticsRouteIntelligenceDirector.ThreatRadius + 2f);
                if (!(closeThreat > midThreat && midThreat > farThreat) || farThreat > 0.001f)
                { Fail("Proximity threat scoring is not monotonic/bounded"); return; }
                if (LogisticsRouteIntelligenceDirector.BreachThreatContribution(Team.Player, Team.Enemy) <= 0f ||
                    LogisticsRouteIntelligenceDirector.BreachThreatContribution(Team.Enemy, Team.Enemy) >= 0f)
                { Fail("Breach threat ownership scoring invalid"); return; }

                if (!LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Fast, Team.Player) ||
                    !LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Elite, Team.Player) ||
                    !LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Sniper, Team.Player) ||
                    LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Heavy, Team.Player) ||
                    !LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Sniper, Team.Enemy) ||
                    LogisticsRouteIntelligenceDirector.IsRouteActorKind(EnemyKind.Fast, Team.Enemy))
                { Fail("Route screen/ambush class filter invalid"); return; }
                if (LogisticsRouteIntelligenceDirector.RouteRole(EnemyKind.Sniper, Team.Enemy) != SquadTacticalRole.Suppressor ||
                    LogisticsRouteIntelligenceDirector.RouteRole(EnemyKind.Fast, Team.Player) != SquadTacticalRole.Flanker)
                { Fail("Route actor role mapping invalid"); return; }

                int hp60 = LogisticsRouteIntelligenceDirector.DecoyHealthForRound(60);
                int hp99 = LogisticsRouteIntelligenceDirector.DecoyHealthForRound(99);
                if (hp60 < LogisticsRouteIntelligenceDirector.DecoyHealthMin || hp99 > LogisticsRouteIntelligenceDirector.DecoyHealthMax || hp99 < hp60)
                { Fail("Decoy health scaling invalid"); return; }

                // Physical decoy authority contract: ordinary Health + collision + kinematic body, no manifest/economy path.
                GameObject authority = new GameObject("V122_DECOY_AUTHORITY_PROBE");
                authority.AddComponent<BoxCollider2D>();
                Rigidbody2D body = authority.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                Health health = authority.AddComponent<Health>();
                health.Initialize(Team.Enemy, hp99);
                if (!health.Damage(1, Team.Player) || health.Current != hp99 - 1 || body.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(authority); Fail("Physical decoy Health/collision authority invalid"); return; }
                Destroy(authority);

                string report =
                    "v12.2 route intelligence smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "eligibleColumns=" + eligibleRounds + " routeCases=" + routeCases + " decoyCases=" + decoyCases + " routes=" + LogisticsRouteIntelligenceDirector.RouteCount + " rerouteCap=" + LogisticsRouteIntelligenceDirector.MaxReroutes + "\n" +
                    "ambushCap=" + LogisticsRouteIntelligenceDirector.MaxAmbushActors + " decoyCap=" + LogisticsRouteIntelligenceDirector.MaxDecoys + " hp60=" + hp60 + " hp99=" + hp99 + "\n" +
                    "threatNear=" + closeThreat.ToString("0.00") + " threatMid=" + midThreat.ToString("0.00") + " threatFar=" + farThreat.ToString("0.00") + "\n";
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
            Debug.LogError("[TankRevival] v12.2 route intelligence smoke FAIL: " + reason);
            Application.Quit(72);
        }
    }
}
