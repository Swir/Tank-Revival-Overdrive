using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Packaged-EXE qualification for v11.9. The probe validates the bounded engineering policy,
    /// resolvable/re-openable breach lifecycle and unchanged Obstacle/Health authority contracts.
    /// </summary>
    public sealed class CombatEngineeringCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V119_COMBAT_ENGINEERING_SMOKE_OK.txt";
        public const string FailMarker = "V119_COMBAT_ENGINEERING_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-v119-combat-engineering-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<CombatEngineeringCISmokeProbe>() != null) return;
                GameObject go = new GameObject("CombatEngineeringCISmokeProbe_v11_9");
                DontDestroyOnLoad(go);
                go.AddComponent<CombatEngineeringCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!ReactiveCoverBreachDirector.ConfigurationValid) { Fail("Reactive breach memory configuration invalid"); return; }
                if (!CombatEngineeringCounterBreachDirector.ConfigurationValid) { Fail("Combat engineering configuration invalid"); return; }
                if (!AdaptivePlatoonManeuverDirector.ConfigurationValid) { Fail("Adaptive platoon maneuver configuration invalid"); return; }
                if (CombatEngineeringCounterBreachDirector.MaxTrackedSectors > ReactiveCoverBreachDirector.MaxRecentBreaches) { Fail("Engineering sector memory exceeds breach ring budget"); return; }
                if (CombatEngineeringCounterBreachDirector.MaxActiveAssets > CombatEngineeringCounterBreachDirector.MaxTrackedSectors) { Fail("Active engineering assets exceed tracked sector budget"); return; }
                if (CombatEngineeringCounterBreachDirector.MaxActiveBarriers + CombatEngineeringCounterBreachDirector.MaxActiveMines < CombatEngineeringCounterBreachDirector.MaxActiveAssets) { Fail("Per-type asset caps cannot satisfy global cap"); return; }
                if (CombatEngineeringCounterBreachDirector.SectorCooldown > ReactiveCoverBreachDirector.RecentBreachSeconds) { Fail("Sector cooldown outlives tactical breach memory"); return; }

                if (CombatEngineeringCounterBreachDirector.BarrierKindForRound(30, Team.Player) != ObstacleKind.Brick) { Fail("Early Orzelek counter-breach barrier tier invalid"); return; }
                if (CombatEngineeringCounterBreachDirector.BarrierKindForRound(80, Team.Player) != ObstacleKind.Steel) { Fail("Late Orzelek counter-breach barrier tier invalid"); return; }
                if (CombatEngineeringCounterBreachDirector.BarrierKindForRound(60, Team.Enemy) != ObstacleKind.Brick) { Fail("Enemy counter-breach tech curve escalates too early"); return; }
                if (CombatEngineeringCounterBreachDirector.BarrierKindForRound(80, Team.Enemy) != ObstacleKind.Steel) { Fail("Late enemy counter-breach barrier tier invalid"); return; }
                if (!CombatEngineeringCounterBreachDirector.CanEnemyCounterBreachRound(36) || CombatEngineeringCounterBreachDirector.CanEnemyCounterBreachRound(35)) { Fail("Enemy counter-breach round gate invalid"); return; }
                if (CombatEngineeringCounterBreachDirector.MineDamageForRound(20) >= CombatEngineeringCounterBreachDirector.MineDamageForRound(80)) { Fail("Denial charge progression invalid"); return; }

                int steelBasic = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Basic, 2, false);
                int steelHe = Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Explosive, 2, false);
                if (steelBasic != 0 || steelHe <= 0) { Fail("Counter-breach Steel no longer obeys existing ammo-aware Obstacle authority"); return; }

                Vector2 testPosition = new Vector2(41.25f, -37.5f);
                ReactiveCoverBreachDirector.ReportBreach(testPosition, Team.Enemy, AmmoType.Explosive, ObstacleKind.Steel);
                int firstSequence = ReactiveCoverBreachDirector.Sequence;
                if (!ReactiveCoverBreachDirector.TryFindRecentBreachNear(testPosition + Vector2.right, Team.Enemy, 2f, out ReactiveCoverBreachDirector.BreachSnapshot first) || first.Sequence != firstSequence)
                {
                    Fail("Engineering query cannot consume fresh hostile breach");
                    return;
                }
                if (!ReactiveCoverBreachDirector.ResolveBreach(first.Sequence) || ReactiveCoverBreachDirector.IsBreachActive(first.Sequence))
                {
                    Fail("Closed breach remains tactically active");
                    return;
                }

                // Destroying a replacement Obstacle would call ReportBreach. Re-publish directly here to
                // validate that a new sequence becomes available rather than reviving the stale snapshot.
                ReactiveCoverBreachDirector.ReportBreach(testPosition, Team.Enemy, AmmoType.Plasma, ObstacleKind.Steel);
                int reopenedSequence = ReactiveCoverBreachDirector.Sequence;
                if (reopenedSequence <= firstSequence || !ReactiveCoverBreachDirector.IsBreachActive(reopenedSequence))
                {
                    Fail("Re-breached sector did not publish a fresh tactical sequence");
                    return;
                }
                if (!ReactiveCoverBreachDirector.TryFindRecentBreachNear(testPosition, Team.Enemy, 1f, out ReactiveCoverBreachDirector.BreachSnapshot reopened) || reopened.Sequence != reopenedSequence)
                {
                    Fail("Engineering query cannot reacquire genuinely reopened gap");
                    return;
                }

                GameObject obstacleAuthority = new GameObject("V119_OBSTACLE_AUTHORITY_PROBE");
                Obstacle obstacle = obstacleAuthority.AddComponent<Obstacle>();
                int barrierHp = CombatEngineeringCounterBreachDirector.BarrierHitPointsForRound(80, Team.Player);
                obstacle.Initialize(ObstacleKind.Steel, barrierHp);
                if (obstacle.Kind != ObstacleKind.Steel || obstacle.HitPoints != barrierHp || obstacle.MaximumHitPoints != barrierHp)
                {
                    Destroy(obstacleAuthority);
                    Fail("Temporary barrier does not use authoritative Obstacle integrity");
                    return;
                }
                Destroy(obstacleAuthority);

                GameObject healthAuthority = new GameObject("V119_HEALTH_AUTHORITY_PROBE");
                Health health = healthAuthority.AddComponent<Health>();
                health.Initialize(Team.Player, 9);
                if (!health.Damage(2, Team.Enemy) || health.Current != 7 || health.Maximum != 9)
                {
                    Destroy(healthAuthority);
                    Fail("Vehicle Health authority changed");
                    return;
                }
                Destroy(healthAuthority);

                string report =
                    "v11.9 combat engineering + counter-breach smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "assets=" + CombatEngineeringCounterBreachDirector.MaxActiveAssets + " barriers=" + CombatEngineeringCounterBreachDirector.MaxActiveBarriers + " mines=" + CombatEngineeringCounterBreachDirector.MaxActiveMines + " sectors=" + CombatEngineeringCounterBreachDirector.MaxTrackedSectors + "\n" +
                    "friendlyRound=" + CombatEngineeringCounterBreachDirector.FriendlyResponseRound + " enemyRound=" + CombatEngineeringCounterBreachDirector.EnemyCounterBreachRound + " cooldown=" + CombatEngineeringCounterBreachDirector.SectorCooldown + "\n" +
                    "closedSeq=" + firstSequence + " reopenedSeq=" + reopenedSequence + " resolvedTotal=" + ReactiveCoverBreachDirector.ResolvedCount + "\n" +
                    "lateBarrierHP=" + barrierHp + " steelBasic=" + steelBasic + " steelHE=" + steelHe + "\n";
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
            Debug.LogError("[TankRevival] v11.9 combat engineering smoke FAIL: " + reason);
            Application.Quit(69);
        }
    }
}
