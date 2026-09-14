using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class HighCommandDeceptionWarCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "DECEPTION_WAR_PASS.txt";
        private const string FailFile = "DECEPTION_WAR_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-deception-war-smoke")) return;
            GameObject go = new GameObject("HighCommandDeceptionWarCISmokeProbe_v10_4");
            DontDestroyOnLoad(go);
            go.AddComponent<HighCommandDeceptionWarCISmokeProbe>();
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
                HighCommandDeceptionWarDirector deception = HighCommandDeceptionWarDirector.Instance;
                AdaptiveEnemyHighCommandDirector high = AdaptiveEnemyHighCommandDirector.Instance;
                DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
                StrategicReserveAttritionDirector reserves = StrategicReserveAttritionDirector.Instance;
                CommandNetworkHuntDirector intel = CommandNetworkHuntDirector.Instance;
                if (game == null || deception == null || high == null || frontline == null || reserves == null || intel == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateAxisPlans();
                ValidateReserveCaps();
                ValidateRoundWindows();
                ValidateIntegration();

                File.WriteAllText(PassFile,
                    "v10.4 high command deception war packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "caps=commit:" + HighCommandDeceptionWarDirector.MaxCommittedCombatants +
                    ",feint:" + HighCommandDeceptionWarDirector.MaxFeintCombatants +
                    ",shells:" + HighCommandDeceptionWarDirector.MaxSupportShells + "\n" +
                    "window=offsets:" + HighCommandDeceptionWarDirector.PlanningOffset + "-" + HighCommandDeceptionWarDirector.AssaultEndOffset + "\n" +
                    "integration=v10.3 high command + v9.0 frontline + v8.5 reserves + v7.8 SIGINT/recon\n");
                Debug.Log("[CI] v10.4 high command deception war smoke PASS");
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
            if (Application.version != "10.4.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!HighCommandDeceptionWarDirector.ConfigurationValid)
                throw new InvalidOperationException("HighCommandDeceptionWarDirector.ConfigurationValid=false");
            if (!AdaptiveEnemyHighCommandDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.3 high command configuration drifted");
            if (!DynamicFrontlineTerritoryDirector.ConfigurationValid)
                throw new InvalidOperationException("v9.0 frontline configuration drifted");
            if (!StrategicReserveAttritionDirector.ConfigurationValid)
                throw new InvalidOperationException("v8.5 reserve configuration drifted");
            if (!CommandNetworkHuntDirector.ConfigurationValid)
                throw new InvalidOperationException("v7.8 intelligence configuration drifted");
        }

        private static void ValidateAxisPlans()
        {
            EnemyCounterDoctrine[] doctrines = {
                EnemyCounterDoctrine.ArmorTrap,
                EnemyCounterDoctrine.DispersedLogistics,
                EnemyCounterDoctrine.SiegeBreach
            };
            for (int sector = 4; sector < 10; sector++)
            {
                int round = sector * 10 + 6;
                for (int i = 0; i < doctrines.Length; i++)
                {
                    HighCommandDeceptionWarDirector.ResolveAxes(round, doctrines[i], out DeceptionAxis main, out DeceptionAxis feint);
                    if (main == feint) throw new InvalidOperationException("main and feint axes collided");
                    if ((int)main < 0 || (int)main >= DynamicFrontlineTerritoryDirector.LaneCount)
                        throw new InvalidOperationException("main axis outside frontline lanes");
                    if ((int)feint < 0 || (int)feint >= DynamicFrontlineTerritoryDirector.LaneCount)
                        throw new InvalidOperationException("feint axis outside frontline lanes");
                }
            }
        }

        private static void ValidateReserveCaps()
        {
            StrategicReserveSnapshot full = new StrategicReserveSnapshot(12, 12, 10, 10, 8, 8);
            StrategicReserveSnapshot medium = new StrategicReserveSnapshot(6, 12, 5, 10, 4, 8);
            StrategicReserveSnapshot critical = new StrategicReserveSnapshot(1, 12, 1, 10, 1, 8);

            if (HighCommandDeceptionWarDirector.ResolveReserveCommitmentCap(full, EnemyCounterDoctrine.ArmorTrap) != 4)
                throw new InvalidOperationException("full armor reserve should permit four-unit commitment");
            if (HighCommandDeceptionWarDirector.ResolveReserveCommitmentCap(medium, EnemyCounterDoctrine.SiegeBreach) != 3)
                throw new InvalidOperationException("medium fire reserve should permit three-unit commitment");
            if (HighCommandDeceptionWarDirector.ResolveReserveCommitmentCap(critical, EnemyCounterDoctrine.DispersedLogistics) != 2)
                throw new InvalidOperationException("critical EW reserve must retain bounded two-axis minimum");

            if (HighCommandDeceptionWarDirector.ResolveEffectiveMainCap(4, false) != 3 ||
                HighCommandDeceptionWarDirector.ResolveEffectiveMainCap(4, true) != 2)
                throw new InvalidOperationException("confirmed intelligence must degrade main effort by one unit");

            if (HighCommandDeceptionWarDirector.ResolveSupportShellCap(full, false) != 2 ||
                HighCommandDeceptionWarDirector.ResolveSupportShellCap(full, true) != 1 ||
                HighCommandDeceptionWarDirector.ResolveSupportShellCap(critical, false) != 0)
                throw new InvalidOperationException("support-fire reserve/intelligence caps drifted");
        }

        private static void ValidateRoundWindows()
        {
            for (int round = 1; round <= 100; round++)
            {
                bool expected = false;
                if (round >= HighCommandDeceptionWarDirector.MinimumOperationRound && round % 10 != 0)
                {
                    int offset = (round - 1) % 10;
                    expected = offset >= HighCommandDeceptionWarDirector.PlanningOffset && offset <= HighCommandDeceptionWarDirector.AssaultEndOffset;
                }
                if (HighCommandDeceptionWarDirector.HasOperationForRound(round) != expected)
                    throw new InvalidOperationException("deception-war window mismatch at round " + round);

                DeceptionWarPhase phase = HighCommandDeceptionWarDirector.ResolvePhase(round);
                if (!expected && phase != DeceptionWarPhase.None)
                    throw new InvalidOperationException("inactive round returned active phase at " + round);
            }

            if (HighCommandDeceptionWarDirector.ResolvePhase(46) != DeceptionWarPhase.Masking)
                throw new InvalidOperationException("round 46 should be masking phase");
            if (HighCommandDeceptionWarDirector.ResolvePhase(47) != DeceptionWarPhase.MainEffort ||
                HighCommandDeceptionWarDirector.ResolvePhase(48) != DeceptionWarPhase.MainEffort)
                throw new InvalidOperationException("rounds 47-48 should be main-effort phase");
            if (HighCommandDeceptionWarDirector.HasOperationForRound(49) || HighCommandDeceptionWarDirector.HasOperationForRound(50))
                throw new InvalidOperationException("pre-boss/end-sector rounds must remain isolated");
        }

        private static void ValidateIntegration()
        {
            if (DynamicFrontlineTerritoryDirector.LaneCount != 3)
                throw new InvalidOperationException("frontline lane contract drifted");
            if (StrategicReserveAttritionDirector.SectorCount != 10)
                throw new InvalidOperationException("reserve sector contract drifted");
            if (CommandNetworkHuntDirector.SigintCooldown <= 0f || CommandNetworkHuntDirector.ReconDuration <= 0f)
                throw new InvalidOperationException("SIGINT/recon contract unavailable");
            if (AdaptiveEnemyHighCommandDirector.MaxRetaskedCombatants > HighCommandDeceptionWarDirector.MaxCommittedCombatants)
                throw new InvalidOperationException("v10.4 commitment cap must not exceed existing v10.3 tactical ceiling");
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
            Debug.LogError("[CI] v10.4 deception war smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
