using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class TacticalCombatReforgeCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "TACTICAL_COMBAT_REFORGE_PASS.txt";
        private const string FailFile = "TACTICAL_COMBAT_REFORGE_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-tactical-combat-reforge-smoke")) return;
            GameObject go = new GameObject("TacticalCombatReforgeCISmokeProbe_v11_0");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalCombatReforgeCISmokeProbe>();
        }

        private void Awake()
        {
            SafeDelete(PassFile);
            SafeDelete(FailFile);
            _deadline = Time.realtimeSinceStartup + 14f;
        }

        private void Update()
        {
            try
            {
                TankGame game = FindAnyObjectByType<TankGame>();
                TacticalCombatReforgeDirector reforge = TacticalCombatReforgeDirector.Instance;
                TacticalNavigationDirector navigation = TacticalNavigationDirector.Instance;
                EnemySquadTacticsDirector squad = EnemySquadTacticsDirector.Instance;
                TacticalRegroupDirector regroup = TacticalRegroupDirector.Instance;
                if (game == null || reforge == null || navigation == null || squad == null || regroup == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("required tactical runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateRoleModel();
                ValidateSuppressionModel();
                ValidateLeaderModel();
                ValidateAuthorityBounds();

                File.WriteAllText(PassFile,
                    "v11.0 Tactical Combat Reforge packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "platoons=" + TacticalCombatReforgeDirector.MaxPlatoons +
                    ",members=" + TacticalCombatReforgeDirector.MaxMembersPerPlatoon +
                    ",managed=" + TacticalCombatReforgeDirector.MaxManagedCombatants + "\n" +
                    "suppression=threshold:" + TacticalCombatReforgeDirector.SuppressionThreshold +
                    ",decay:" + TacticalCombatReforgeDirector.SuppressionDecayPerSecond +
                    ",relocate:" + TacticalCombatReforgeDirector.RelocationSeconds + "\n" +
                    "integration=EnemySquadTactics + TacticalNavigationAgent + TerrainIntelligence + TacticalRegroup + HighCommand arbitration\n");
                Debug.Log("[CI] v11.0 Tactical Combat Reforge smoke PASS");
                Application.Quit(0);
                enabled = false;
            }
            catch (Exception ex)
            {
                Fail(ex.ToString());
            }
        }

        private static void ValidateConfiguration()
        {
            if (Application.version != "11.0.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!TacticalCombatReforgeDirector.ConfigurationValid)
                throw new InvalidOperationException("TacticalCombatReforgeDirector.ConfigurationValid=false");
            if (!TacticalNavigationDirector.ConfigurationValid || !EnemySquadTacticsDirector.ConfigurationValid || !TacticalRegroupDirector.ConfigurationValid)
                throw new InvalidOperationException("existing tactical configuration drifted");
            if (TacticalCombatReforgeDirector.MaxManagedCombatants != TacticalCombatReforgeDirector.MaxPlatoons * TacticalCombatReforgeDirector.MaxMembersPerPlatoon)
                throw new InvalidOperationException("managed combatant cap mismatch");
        }

        private static void ValidateRoleModel()
        {
            if (TacticalCombatReforgeDirector.RoleForKind(EnemyKind.Siege, 0, false, 60) != SquadTacticalRole.Breaker)
                throw new InvalidOperationException("Siege must remain Breaker");
            if (TacticalCombatReforgeDirector.RoleForKind(EnemyKind.Sniper, 1, false, 60) != SquadTacticalRole.Suppressor)
                throw new InvalidOperationException("Sniper must remain Suppressor");
            if (TacticalCombatReforgeDirector.RoleForKind(EnemyKind.Fast, 0, false, 60) != SquadTacticalRole.Flanker)
                throw new InvalidOperationException("Fast slot 0 must flank");
            if (TacticalCombatReforgeDirector.RoleForKind(EnemyKind.Heavy, 0, true, 60) != SquadTacticalRole.Vanguard)
                throw new InvalidOperationException("Heavy leader must anchor as Vanguard");
            if (TacticalCombatReforgeDirector.LeaderPriority(EnemyKind.Elite) <= TacticalCombatReforgeDirector.LeaderPriority(EnemyKind.Heavy))
                throw new InvalidOperationException("Elite must outrank Heavy for platoon command");
        }

        private static void ValidateSuppressionModel()
        {
            float oneHit = TacticalCombatReforgeDirector.SuppressionAfterDamage(0f, 1);
            if (oneHit < TacticalCombatReforgeDirector.SuppressionThreshold)
                throw new InvalidOperationException("one real damage event should trigger bounded suppression response");
            if (!TacticalCombatReforgeDirector.IsSuppressed(oneHit, 0.2f))
                throw new InvalidOperationException("fresh damage must be recognized as suppression");
            float decayed = TacticalCombatReforgeDirector.SuppressionAfterDecay(oneHit, 3.0f);
            if (decayed >= oneHit || decayed < 0f)
                throw new InvalidOperationException("suppression decay invalid");
            if (TacticalCombatReforgeDirector.SuppressionAfterDamage(99f, 99) > TacticalCombatReforgeDirector.SuppressionMaximum)
                throw new InvalidOperationException("suppression exceeds hard maximum");
            if (TacticalCombatReforgeDirector.IsSuppressed(oneHit, TacticalCombatReforgeDirector.RelocationSeconds + 0.1f))
                throw new InvalidOperationException("relocation window must expire");
        }

        private static void ValidateLeaderModel()
        {
            if (TacticalCombatReforgeDirector.LeaderPriority(EnemyKind.Supply) != 0 || TacticalCombatReforgeDirector.LeaderPriority(EnemyKind.Boss) != 0)
                throw new InvalidOperationException("Supply/Boss must not become tactical platoon leaders");
            if (TacticalCombatReforgeDirector.LeaderLossRegroupSeconds < 1.5f || TacticalCombatReforgeDirector.LeaderLossRegroupSeconds > 3.5f)
                throw new InvalidOperationException("leader-loss regroup window outside hard bounds");
        }

        private static void ValidateAuthorityBounds()
        {
            if (TacticalCombatReforgeDirector.MaxManagedCombatants > TacticalNavigationDirector.MaxManagedEnemies)
                throw new InvalidOperationException("v11.0 combatant cap exceeds existing navigation authority");
            if (TacticalCombatReforgeDirector.CommandCadence < TacticalNavigationDirector.DecisionCadence * 0.75f)
                throw new InvalidOperationException("v11.0 command cadence is too aggressive");
            if (TacticalCombatReforgeDirector.SuppressionMaximum > 3.0f)
                throw new InvalidOperationException("suppression maximum drifted");
        }

        private static bool HasArg(string arg)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void Fail(string message)
        {
            try { File.WriteAllText(FailFile, message); } catch { }
            Debug.LogError("[CI] v11.0 Tactical Combat Reforge smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
