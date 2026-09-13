using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class DestructionReforgeCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-destruction-reforge-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("DestructionReforgeCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<DestructionReforgeCISmokeProbe>();
                return;
            }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            try
            {
                DestructionReforgeDirector director = FindAnyObjectByType<DestructionReforgeDirector>();
                if (director == null) { Fail("DestructionReforgeDirector missing"); yield break; }
                if (!DestructionReforgeDirector.ConfigurationValid) { Fail("Destruction reforge configuration invalid"); yield break; }
                if (!DestructionReforgeDirector.SingleWreckAuthority) { Fail("Single wreck authority flag missing"); yield break; }
                if (!DestructionReforgeDirector.UsesHealthAuthority || !DestructionReforgeDirector.UsesObstacleAuthority) { Fail("Authoritative integration flags invalid"); yield break; }
                if (!DestructionAuthorityMigration.LegacyAuthoritySuppressed) { Fail("Legacy wreck authority was not suppressed"); yield break; }
                if (DestructionReforgeDirector.FullWreckBudget > 28 || DestructionReforgeDirector.BalancedWreckBudget >= DestructionReforgeDirector.FullWreckBudget || DestructionReforgeDirector.SurvivalWreckBudget >= DestructionReforgeDirector.BalancedWreckBudget) { Fail("Wreck budgets invalid"); yield break; }
                if (DestructionReforgeDirector.FullDebrisBudget > 80) { Fail("Debris budget invalid"); yield break; }
                if (FindAnyObjectByType<CombatVfxReforgeDirector>() == null) { Fail("v6.8 combat VFX service missing"); yield break; }
                if (FindAnyObjectByType<VehicleMotionWeaponAnimationDirector>() == null) { Fail("v6.7 vehicle motion service missing"); yield break; }

                EnemyKind[] catalog = { EnemyKind.Basic, EnemyKind.Fast, EnemyKind.Sniper, EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Elite, EnemyKind.Boss };
                float previousScale = 0f;
                for (int i = 0; i < catalog.Length; i++)
                {
                    WreckProfile profile = DestructionReforgeDirector.Profile(catalog[i], false);
                    if (profile.Scale <= 0.70f || profile.TotalLifetime < 10f || profile.DebrisPieces < 4) { Fail("Invalid wreck profile for " + catalog[i]); yield break; }
                    if (catalog[i] == EnemyKind.Boss && profile.Scale <= previousScale) { Fail("Boss wreck profile not dominant"); yield break; }
                    previousScale = profile.Scale;
                }

                var healthProbe = new GameObject("DESTRUCTION_HEALTH_AUTHORITY_PROBE");
                Health health = healthProbe.AddComponent<Health>();
                health.Initialize(Team.Player, 10);
                if (!health.Damage(3, Team.Enemy) || health.Current != 7 || health.Maximum != 10) { Destroy(healthProbe); Fail("Health authority changed"); yield break; }
                Destroy(healthProbe);

                var obstacleProbe = new GameObject("DESTRUCTION_OBSTACLE_AUTHORITY_PROBE");
                Obstacle obstacle = obstacleProbe.AddComponent<Obstacle>();
                obstacle.Initialize(ObstacleKind.Brick, 3);
                if (!obstacle.Hit(1, obstacleProbe.transform.position) || obstacle.HitPoints != 2 || obstacle.MaximumHitPoints != 3) { Destroy(obstacleProbe); Fail("Obstacle authority changed"); yield break; }
                Destroy(obstacleProbe);

                string report = "v6.9 destruction reforge smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "profiles=" + DestructionReforgeDirector.WreckProfileCount +
                    " wreckBudgets=" + DestructionReforgeDirector.FullWreckBudget + "/" + DestructionReforgeDirector.BalancedWreckBudget + "/" + DestructionReforgeDirector.SurvivalWreckBudget +
                    " debrisBudgets=" + DestructionReforgeDirector.FullDebrisBudget + "/" + DestructionReforgeDirector.BalancedDebrisBudget + "/" + DestructionReforgeDirector.SurvivalDebrisBudget +
                    " legacySuppressed=" + DestructionAuthorityMigration.LegacyAuthoritySuppressed + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "DESTRUCTION_REFORGE_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "DESTRUCTION_REFORGE_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.9 destruction reforge smoke FAIL: " + reason);
            Application.Quit(69);
        }
    }
}
