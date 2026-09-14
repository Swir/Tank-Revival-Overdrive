using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class HighCommandFinaleCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "HIGH_COMMAND_FINALE_PASS.txt";
        private const string FailFile = "HIGH_COMMAND_FINALE_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-high-command-finale-smoke")) return;
            GameObject go = new GameObject("HighCommandFinaleCISmokeProbe_v10_8");
            DontDestroyOnLoad(go);
            go.AddComponent<HighCommandFinaleCISmokeProbe>();
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
                HighCommandFinaleDirector finale = HighCommandFinaleDirector.Instance;
                WarStateEndgameDirector endgame = WarStateEndgameDirector.Instance;
                if (game == null || finale == null || endgame == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("required v10.8/v10.7 runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateObjectiveMapping();
                ValidateHqModel();
                ValidateEpilogues();
                ValidateRoundSafety();

                File.WriteAllText(PassFile,
                    "v10.8 High Command finale packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "objective_window=" + HighCommandFinaleDirector.ObjectiveStartRound + "-" + HighCommandFinaleDirector.ObjectiveEndRound + "\n" +
                    "hq_hp=" + HighCommandFinaleDirector.AdvantageHqHealth + "/" + HighCommandFinaleDirector.ContestedHqHealth + "/" + HighCommandFinaleDirector.CrisisHqHealth + "\n" +
                    "caps=support:" + HighCommandFinaleDirector.MaxSupportShellsPerRound +
                    ",enemy_pressure:" + HighCommandFinaleDirector.MaxEnemyPressureShellsPerRound +
                    ",relocations:" + HighCommandFinaleDirector.MaxHqRelocations +
                    ",reward:" + HighCommandFinaleDirector.MaxFinaleRewardBonds + "\n" +
                    "integration=v10.7 war state + physical Health/collision HQ + boss-safe round 100 + epilogue\n");
                Debug.Log("[CI] v10.8 High Command finale smoke PASS");
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
            if (Application.version != "10.8.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!HighCommandFinaleDirector.ConfigurationValid)
                throw new InvalidOperationException("HighCommandFinaleDirector.ConfigurationValid=false");
            if (!WarStateEndgameDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.7 endgame configuration drifted");
        }

        private static void ValidateObjectiveMapping()
        {
            if (HighCommandFinaleDirector.ObjectiveForState(FinalWarState.Advantage) != FinalObjectiveType.HqAssault)
                throw new InvalidOperationException("ADVANTAGE must map to HQ ASSAULT");
            if (HighCommandFinaleDirector.ObjectiveForState(FinalWarState.Contested) != FinalObjectiveType.CommandIsolation)
                throw new InvalidOperationException("CONTESTED must map to COMMAND ISOLATION");
            if (HighCommandFinaleDirector.ObjectiveForState(FinalWarState.Crisis) != FinalObjectiveType.EvacuationDenial)
                throw new InvalidOperationException("CRISIS must map to EVACUATION DENIAL");
        }

        private static void ValidateHqModel()
        {
            int advantage = HighCommandFinaleDirector.HqHealthForState(FinalWarState.Advantage);
            int contested = HighCommandFinaleDirector.HqHealthForState(FinalWarState.Contested);
            int crisis = HighCommandFinaleDirector.HqHealthForState(FinalWarState.Crisis);
            if (!(advantage < contested && contested < crisis))
                throw new InvalidOperationException("HQ health ordering must be ADVANTAGE < CONTESTED < CRISIS");
            if (!HighCommandFinaleDirector.IsHqIsolated(10, 20) || HighCommandFinaleDirector.IsHqIsolated(11, 20))
                throw new InvalidOperationException("50% command-isolation threshold drifted");
            if (HighCommandFinaleDirector.RewardForObjective(FinalObjectiveType.HqAssault, FinalWarState.Advantage) > HighCommandFinaleDirector.MaxFinaleRewardBonds)
                throw new InvalidOperationException("finale reward exceeds hard cap");
        }

        private static void ValidateEpilogues()
        {
            if (HighCommandFinaleDirector.ResolveEpilogueOutcome(true, true, FinalWarState.Advantage) != CampaignEpilogueOutcome.DecisiveVictory)
                throw new InvalidOperationException("successful ADVANTAGE finale must resolve DECISIVE VICTORY");
            if (HighCommandFinaleDirector.ResolveEpilogueOutcome(true, false, FinalWarState.Contested) != CampaignEpilogueOutcome.CommandEscaped)
                throw new InvalidOperationException("won campaign with failed contested objective must record COMMAND ESCAPED");
            if (HighCommandFinaleDirector.ResolveEpilogueOutcome(false, false, FinalWarState.Crisis) != CampaignEpilogueOutcome.Defeat)
                throw new InvalidOperationException("lost CRISIS finale must resolve DEFEAT");
        }

        private static void ValidateRoundSafety()
        {
            int active = 0;
            for (int round = 1; round <= 100; round++)
            {
                bool objective = HighCommandFinaleDirector.IsFinalObjectiveRound(round);
                if (objective) active++;
                if (round < 97 && objective)
                    throw new InvalidOperationException("High Command finale leaked before round 97");
                if (round == 100 && objective)
                    throw new InvalidOperationException("round 100 boss must not be part of physical-HQ objective window");
            }
            if (active != 3)
                throw new InvalidOperationException("expected three physical-HQ objective rounds, got " + active);
            if (HighCommandFinaleDirector.MaxSupportShellsPerRound > WarStateEndgameDirector.MaxSupportShellsPerRound)
                throw new InvalidOperationException("v10.8 support budget exceeds v10.7 ceiling");
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
            Debug.LogError("[CI] v10.8 High Command finale smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
