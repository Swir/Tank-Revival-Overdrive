using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CounterOffensiveCampaignCISmokeProbe : MonoBehaviour
    {
        private const string PassFile = "COUNTER_OFFENSIVE_PASS.txt";
        private const string FailFile = "COUNTER_OFFENSIVE_FAIL.txt";
        private float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArg("-counter-offensive-smoke")) return;
            GameObject go = new GameObject("CounterOffensiveCampaignCISmokeProbe_v10_6");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterOffensiveCampaignCISmokeProbe>();
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
                CounterOffensiveCampaignDirector campaign = CounterOffensiveCampaignDirector.Instance;
                PlayerCounterOrderDirector counter = PlayerCounterOrderDirector.Instance;
                HighCommandDeceptionWarDirector deception = HighCommandDeceptionWarDirector.Instance;
                DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
                if (game == null || campaign == null || counter == null || deception == null || frontline == null)
                {
                    if (Time.realtimeSinceStartup < _deadline) return;
                    Fail("required v10.6/v10.5/v10.4 runtime directors not installed");
                    return;
                }

                ValidateConfiguration();
                ValidateOutcomeModel();
                ValidateCarryoverWindows();
                ValidateIntegration();

                File.WriteAllText(PassFile,
                    "v10.6 counter-offensive campaign packaged smoke PASS\n" +
                    "version=" + Application.version + "\n" +
                    "caps=assessment:" + CounterOffensiveCampaignDirector.AssessmentDelay +
                    ",momentum:" + CounterOffensiveCampaignDirector.MinOperationalMomentum + ".." + CounterOffensiveCampaignDirector.MaxOperationalMomentum +
                    ",intel:" + CounterOffensiveCampaignDirector.MaxCapturedIntel +
                    ",planRounds:" + CounterOffensiveCampaignDirector.MaxPlanRounds +
                    ",shells:" + CounterOffensiveCampaignDirector.MaxSupportShellsPerRound + "\n" +
                    "integration=v10.5 counter-orders + v10.4 deception + authoritative Health/Projectile/frontline/War Bonds\n");
                Debug.Log("[CI] v10.6 counter-offensive campaign smoke PASS");
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
            if (Application.version != "10.6.0-dev")
                throw new InvalidOperationException("unexpected Application.version: " + Application.version);
            if (!CounterOffensiveCampaignDirector.ConfigurationValid)
                throw new InvalidOperationException("CounterOffensiveCampaignDirector.ConfigurationValid=false");
            if (!PlayerCounterOrderDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.5 counter-order configuration drifted");
            if (!HighCommandDeceptionWarDirector.ConfigurationValid)
                throw new InvalidOperationException("v10.4 deception configuration drifted");
            if (!DynamicFrontlineTerritoryDirector.ConfigurationValid)
                throw new InvalidOperationException("frontline configuration drifted");
        }

        private static void ValidateOutcomeModel()
        {
            if (CounterOffensiveCampaignDirector.EvaluateOutcome(PlayerCounterOrder.Block, 0.70f, 0.64f, 0, 0, true) != CounterOrderOutcome.Success)
                throw new InvalidOperationException("BLOCK hold should resolve SUCCESS");
            if (CounterOffensiveCampaignDirector.EvaluateOutcome(PlayerCounterOrder.Counterattack, 0.80f, 0.80f, 1, 1, true) != CounterOrderOutcome.Success)
                throw new InvalidOperationException("COUNTERATTACK high-value kill should resolve SUCCESS");
            if (CounterOffensiveCampaignDirector.EvaluateOutcome(PlayerCounterOrder.DeepStrike, 0.80f, 0.80f, 0, 1, true) != CounterOrderOutcome.Stalemate)
                throw new InvalidOperationException("DEEP STRIKE low-value-only result should resolve STALEMATE");
            if (CounterOffensiveCampaignDirector.EvaluateOutcome(PlayerCounterOrder.DeepStrike, 0.80f, 0.0f, 3, 5, false) != CounterOrderOutcome.Failure)
                throw new InvalidOperationException("Eagle loss must resolve FAILURE");

            if (CounterOffensiveCampaignDirector.ResolveNextSectorPlan(PlayerCounterOrder.Block, CounterOrderOutcome.Success) != CounterOffensivePlan.LocalOffensive)
                throw new InvalidOperationException("BLOCK success plan mismatch");
            if (CounterOffensiveCampaignDirector.ResolveNextSectorPlan(PlayerCounterOrder.Counterattack, CounterOrderOutcome.Success) != CounterOffensivePlan.FeintExploit)
                throw new InvalidOperationException("COUNTERATTACK success plan mismatch");
            if (CounterOffensiveCampaignDirector.ResolveNextSectorPlan(PlayerCounterOrder.DeepStrike, CounterOrderOutcome.Success) != CounterOffensivePlan.CommandCollapse)
                throw new InvalidOperationException("DEEP STRIKE success plan mismatch");
            if (CounterOffensiveCampaignDirector.ResolveNextSectorPlan(PlayerCounterOrder.Block, CounterOrderOutcome.Failure) != CounterOffensivePlan.EnemyRecovery)
                throw new InvalidOperationException("failure recovery plan mismatch");
        }

        private static void ValidateCarryoverWindows()
        {
            int planRounds = 0;
            for (int round = 1; round <= 100; round++)
            {
                bool active = CounterOffensiveCampaignDirector.IsPlanRound(round);
                if (active)
                {
                    planRounds++;
                    int offset = (round - 1) % 10;
                    if (offset >= CounterOffensiveCampaignDirector.MaxPlanRounds || round % 10 == 0)
                        throw new InvalidOperationException("carryover leaked outside sector opening at round " + round);
                }
                int sector = CounterOffensiveCampaignDirector.SectorForRound(round);
                if (sector < 1 || sector > 10)
                    throw new InvalidOperationException("sector mapping out of range at round " + round);
            }
            if (planRounds != 10 * CounterOffensiveCampaignDirector.MaxPlanRounds)
                throw new InvalidOperationException("unexpected total carryover coverage: " + planRounds);
        }

        private static void ValidateIntegration()
        {
            if (CounterOffensiveCampaignDirector.MaxSupportShellsPerRound > PlayerCounterOrderDirector.MaxSupportShellsPerBeat)
                throw new InvalidOperationException("v10.6 support budget exceeds v10.5 response ceiling");
            if (CounterOffensiveCampaignDirector.MaxCapturedIntel > 2)
                throw new InvalidOperationException("captured intelligence must remain hard-capped");
            if (CounterOffensiveCampaignDirector.MomentumDelta(CounterOrderOutcome.Success) != 1 ||
                CounterOffensiveCampaignDirector.MomentumDelta(CounterOrderOutcome.Failure) != -1 ||
                CounterOffensiveCampaignDirector.MomentumDelta(CounterOrderOutcome.Stalemate) != 0)
                throw new InvalidOperationException("operational momentum deltas drifted");
            if (DynamicFrontlineTerritoryDirector.LaneCount != 3)
                throw new InvalidOperationException("frontline lane authority drifted");
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
            Debug.LogError("[CI] v10.6 counter-offensive campaign smoke FAIL: " + message);
            Application.Quit(2);
            enabled = false;
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
