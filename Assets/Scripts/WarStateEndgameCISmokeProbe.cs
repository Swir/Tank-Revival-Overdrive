using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class WarStateEndgameCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "ENDGAME_REFORGE_PASS.txt";
        private const string FailFile = "ENDGAME_REFORGE_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-endgame-reforge-smoke")) return;
            GameObject go = new GameObject("WarStateEndgameCISmokeProbe_v10_7");
            DontDestroyOnLoad(go);
            go.AddComponent<WarStateEndgameCISmokeProbe>();
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
                WarStateEndgameDirector endgame = WarStateEndgameDirector.Instance;
                CounterOffensiveCampaignDirector counter = CounterOffensiveCampaignDirector.Instance;
                if (game == null || endgame == null || counter == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("required v10.7/v10.6 runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateWarStateModel();
                ValidateEndgameSchedule();
                ValidatePlanMapping();

                File.WriteAllText(PassFile,
                    "v10.7 war-state endgame packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "window=" + WarStateEndgameDirector.EndgameStartRound + "-" + WarStateEndgameDirector.FinalRound + "\n" +
                    "caps=shells:" + WarStateEndgameDirector.MaxSupportShellsPerRound +
                    ",recovery:" + WarStateEndgameDirector.MaxRecoveryPerRound +
                    ",reward:" + WarStateEndgameDirector.MaxEndgameRewardBonds + "\n" +
                    "integration=v10.6 counter-offensive state + authoritative TankGame/Projectile/Health/Orzelek/War Bonds\n");
                Debug.Log("[CI] v10.7 war-state endgame smoke PASS");
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
            if (Application.version != "10.7.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!WarStateEndgameDirector.ConfigurationValid)
                throw new InvalidOperationException("WarStateEndgameDirector.ConfigurationValid=false");
            if (!CounterOffensiveCampaignDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.6 counter-offensive configuration drifted");
        }

        private static void ValidateWarStateModel()
        {
            if (WarStateEndgameDirector.ResolveWarState(2, 1, CounterOrderOutcome.Success) != FinalWarState.Advantage)
                throw new InvalidOperationException("positive campaign state must resolve ADVANTAGE");
            if (WarStateEndgameDirector.ResolveWarState(-2, 0, CounterOrderOutcome.Failure) != FinalWarState.Crisis)
                throw new InvalidOperationException("failed campaign state must resolve CRISIS");
            if (WarStateEndgameDirector.ResolveWarState(0, 0, CounterOrderOutcome.Stalemate) != FinalWarState.Contested)
                throw new InvalidOperationException("neutral campaign state must resolve CONTESTED");
        }

        private static void ValidateEndgameSchedule()
        {
            int active = 0;
            for (int round = 1; round <= 100; round++)
            {
                bool isEndgame = WarStateEndgameDirector.IsEndgameRound(round);
                if (isEndgame) active++;
                if (round < 90 && isEndgame)
                    throw new InvalidOperationException("endgame leaked before round 90");
            }
            if (active != 11)
                throw new InvalidOperationException("expected 11 endgame rounds, got " + active);
            if (WarStateEndgameDirector.PhaseForRound(90) != EndgamePhase.Intelligence ||
                WarStateEndgameDirector.PhaseForRound(94) != EndgamePhase.Interdiction ||
                WarStateEndgameDirector.PhaseForRound(98) != EndgamePhase.Breakthrough ||
                WarStateEndgameDirector.PhaseForRound(100) != EndgamePhase.FinalBattle)
                throw new InvalidOperationException("endgame phase mapping drifted");
        }

        private static void ValidatePlanMapping()
        {
            if (WarStateEndgameDirector.ResolveEndgamePlan(FinalWarState.Advantage, 1, CounterOrderOutcome.Success) != EndgamePlan.CommandCollapse)
                throw new InvalidOperationException("advantage + intel must map to COMMAND COLLAPSE");
            if (WarStateEndgameDirector.ResolveEndgamePlan(FinalWarState.Advantage, 0, CounterOrderOutcome.Success) != EndgamePlan.BreakthroughPursuit)
                throw new InvalidOperationException("advantage without intel must map to BREAKTHROUGH PURSUIT");
            if (WarStateEndgameDirector.ResolveEndgamePlan(FinalWarState.Crisis, 2, CounterOrderOutcome.Success) != EndgamePlan.DesperateDefense)
                throw new InvalidOperationException("crisis must map to DESPERATE DEFENSE");
            if (WarStateEndgameDirector.MaxSupportShellsPerRound > CounterOffensiveCampaignDirector.MaxSupportShellsPerRound)
                throw new InvalidOperationException("v10.7 support budget exceeds v10.6 ceiling");
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
            Debug.LogError("[CI] v10.7 war-state endgame smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
