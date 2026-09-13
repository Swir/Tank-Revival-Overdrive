using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class MultiStageOperationCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-multi-stage-operation-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("MultiStageOperationCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<MultiStageOperationCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<MultiStageOperationDirector>() == null) { Fail("MultiStageOperationDirector missing"); return; }
                if (!MultiStageOperationDirector.ConfigurationValid) { Fail("Operation configuration outside bounded values"); return; }
                if (MultiStageOperationDirector.DoctrineCount != 3) { Fail("Doctrine catalog must contain exactly 3 entries"); return; }
                if (MultiStageOperationDirector.PlayablePhaseCount != 3) { Fail("Playable operation phase count must be 3"); return; }
                if (!MultiStageOperationDirector.HasOperationForRound(16)) { Fail("Round 16 should schedule an operation"); return; }
                if (MultiStageOperationDirector.HasOperationForRound(20)) { Fail("Boss round must not schedule a standard operation"); return; }
                if (!MultiStageOperationDirector.HasOperationForRound(96)) { Fail("Late campaign should still schedule operations"); return; }
                if (MultiStageOperationDirector.RewardForRound(1) < MultiStageOperationDirector.MinReward || MultiStageOperationDirector.RewardForRound(100) > MultiStageOperationDirector.MaxReward) { Fail("Reward bounds invalid"); return; }
                float one = MultiStageOperationDirector.ContestDelta(1f, 1);
                float capped = MultiStageOperationDirector.ContestDelta(1f, 99);
                if (one <= 0f || capped <= one || capped > MultiStageOperationDirector.MaxContesters * MultiStageOperationDirector.EnemyContestRate + 0.01f) { Fail("Contest pressure bounds invalid"); return; }

                string report = "v6.1 multi-stage operation smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "doctrines=" + MultiStageOperationDirector.DoctrineCount +
                    " phases=" + MultiStageOperationDirector.PlayablePhaseCount +
                    " capture=" + MultiStageOperationDirector.CaptureSeconds +
                    " hold=" + MultiStageOperationDirector.FinalHoldSeconds +
                    " contestRate=" + MultiStageOperationDirector.EnemyContestRate +
                    " reward100=" + MultiStageOperationDirector.RewardForRound(100) + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "MULTI_STAGE_OPERATION_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "MULTI_STAGE_OPERATION_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.1 multi-stage operation smoke FAIL: " + reason);
            Application.Quit(61);
        }
    }
}
