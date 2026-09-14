using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class PlayerCounterOrderCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "COUNTER_ORDERS_PASS.txt";
        private const string FailFile = "COUNTER_ORDERS_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-counter-orders-smoke")) return;
            GameObject go = new GameObject("PlayerCounterOrderCISmokeProbe_v10_5");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerCounterOrderCISmokeProbe>();
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
                PlayerCounterOrderDirector counter = PlayerCounterOrderDirector.Instance;
                HighCommandDeceptionWarDirector deception = HighCommandDeceptionWarDirector.Instance;
                CommandNetworkHuntDirector intel = CommandNetworkHuntDirector.Instance;
                DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
                if (game == null || counter == null || deception == null || intel == null || frontline == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateRoundWindows();
                ValidateOrders();
                ValidateIntegration();

                File.WriteAllText(PassFile,
                    "v10.5 player counter-orders packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "caps=window:" + PlayerCounterOrderDirector.DecisionWindow +
                    ",beats:" + PlayerCounterOrderDirector.MaxResponseBeats +
                    ",shells:" + PlayerCounterOrderDirector.MaxSupportShellsPerBeat +
                    ",repair:" + PlayerCounterOrderDirector.MaxBlockRepairPerOperation + "\n" +
                    "integration=v10.4 deception + v7.8 SIGINT/recon + v9.0 frontline + authoritative Health/Projectile/economy\n");
                Debug.Log("[CI] v10.5 player counter-orders smoke PASS");
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
            if (Application.version != "10.5.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!PlayerCounterOrderDirector.ConfigurationValid)
                throw new InvalidOperationException("PlayerCounterOrderDirector.ConfigurationValid=false");
            if (!HighCommandDeceptionWarDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.4 deception configuration drifted");
            if (!CommandNetworkHuntDirector.ConfigurationValid)
                throw new InvalidOperationException("v7.8 intelligence configuration drifted");
            if (!DynamicFrontlineTerritoryDirector.ConfigurationValid)
                throw new InvalidOperationException("v9.0 frontline configuration drifted");
        }

        private static void ValidateRoundWindows()
        {
            int eligible = 0;
            for (int round = 1; round <= 100; round++)
            {
                bool expected = HighCommandDeceptionWarDirector.ResolvePhase(round) == DeceptionWarPhase.MainEffort;
                bool actual = PlayerCounterOrderDirector.IsDecisionRound(round);
                if (expected != actual)
                    throw new InvalidOperationException("counter-order window mismatch at round " + round);
                if (actual)
                {
                    eligible++;
                    if (round % 10 == 0 || (round - 1) % 10 >= 8)
                        throw new InvalidOperationException("counter-order leaked into boss/pre-boss boundary at round " + round);
                }
            }
            if (eligible < 10)
                throw new InvalidOperationException("counter-order campaign coverage unexpectedly low: " + eligible);
        }

        private static void ValidateOrders()
        {
            if (PlayerCounterOrderDirector.ResolveDefaultOrder(0.30f) != PlayerCounterOrder.Block)
                throw new InvalidOperationException("low Eagle health should default to BLOCK");
            if (PlayerCounterOrderDirector.ResolveDefaultOrder(0.60f) != PlayerCounterOrder.DeepStrike)
                throw new InvalidOperationException("mid Eagle health should default to DEEP STRIKE");
            if (PlayerCounterOrderDirector.ResolveDefaultOrder(0.90f) != PlayerCounterOrder.Counterattack)
                throw new InvalidOperationException("high Eagle health should default to COUNTERATTACK");

            int block = PlayerCounterOrderDirector.ResolveCompletionReward(PlayerCounterOrder.Block, true);
            int counter = PlayerCounterOrderDirector.ResolveCompletionReward(PlayerCounterOrder.Counterattack, true);
            int deep = PlayerCounterOrderDirector.ResolveCompletionReward(PlayerCounterOrder.DeepStrike, true);
            if (block <= 0 || counter <= block || deep <= 0 || counter > PlayerCounterOrderDirector.MaxOrderRewardBonds)
                throw new InvalidOperationException("counter-order reward bounds drifted");
            if (PlayerCounterOrderDirector.ResolveCompletionReward(PlayerCounterOrder.DeepStrike, false) != 0)
                throw new InvalidOperationException("no-target response must not award bonds");
        }

        private static void ValidateIntegration()
        {
            if (DynamicFrontlineTerritoryDirector.LaneCount != 3)
                throw new InvalidOperationException("frontline lane contract drifted");
            if (CommandNetworkHuntDirector.SigintCooldown <= 0f || CommandNetworkHuntDirector.ReconDuration <= 0f)
                throw new InvalidOperationException("SIGINT/recon contract unavailable");
            if (PlayerCounterOrderDirector.MaxSupportShellsPerBeat > HighCommandDeceptionWarDirector.MaxSupportShells)
                throw new InvalidOperationException("friendly response shell budget exceeds v10.4 operation ceiling");
            if (PlayerCounterOrderDirector.MaxResponseBeats > 2)
                throw new InvalidOperationException("counter-order response beats must remain bounded");
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
            Debug.LogError("[CI] v10.5 player counter-orders smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
