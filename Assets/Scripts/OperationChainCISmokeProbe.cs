using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22103)]
    public sealed class OperationChainCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-operation-chain-smoke")) return;
            GameObject go = new GameObject("OperationChainCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<OperationChainCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool encounter = CampaignEncounterDirector.Instance != null;
            bool pacing = CampaignPacingDirector.Instance != null;
            bool sectors = SectorIdentityDirector.Instance != null;
            bool chains = OperationChainDirector.Instance != null;
            bool bridge = OperationChainDirector.BridgeAvailable;
            bool config = OperationChainDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.3.0-dev";

            if (game && encounter && pacing && sectors && chains && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} encounter={encounter} pacing={pacing} sectors={sectors} chains={chains} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} encounter={encounter} pacing={pacing} sectors={sectors} chains={chains} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(42);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int chainRounds = 0;
            int successes = 0;
            int failures = 0;

            for (int round = 1; round <= 100; round++)
            {
                bool expected = round % 10 >= OperationChainDirector.FirstSectorRound && round % 10 < OperationChainDirector.FirstSectorRound + OperationChainDirector.ChainLength;
                if (round % 10 == 0) expected = false;
                bool actual = OperationChainDirector.IsChainRound(round);
                if (actual != expected)
                {
                    details = $"schedule mismatch round={round} actual={actual} expected={expected}";
                    return false;
                }

                if (!actual) continue;
                chainRounds++;
                OperationChainProfile profile = OperationChainDirector.Resolve(round);
                if (profile.Round != round || profile.Sector != (round - 1) / 10 || string.IsNullOrEmpty(profile.Codename) || string.IsNullOrEmpty(profile.Objective))
                {
                    details = $"profile invalid round={round}";
                    return false;
                }

                RoundEncounterProfile baseProfile = CampaignPacingDirector.RefineEncounter(CampaignEncounterDirector.Resolve(round), CampaignPacingDirector.Resolve(round));
                SectorIdentityProfile sector = SectorIdentityDirector.ResolveSector((round - 1) / 10, EncounterDeck.Spearhead);
                RoundEncounterProfile sectorProfile = SectorIdentityDirector.RefineEncounter(baseProfile, sector);
                RoundEncounterProfile good = OperationChainDirector.RefineEncounter(sectorProfile, OperationChainOutcome.Success);
                RoundEncounterProfile bad = OperationChainDirector.RefineEncounter(sectorProfile, OperationChainOutcome.Failure);

                if (good.Round != round || bad.Round != round || good.HealthMultiplier > sectorProfile.HealthMultiplier + 0.001f || bad.HealthMultiplier < sectorProfile.HealthMultiplier - 0.001f)
                {
                    details = $"consequence ordering failed round={round}";
                    return false;
                }
                if (good.HealthMultiplier < 0.82f || bad.HealthMultiplier > 1.48f || good.FireSupportMultiplier < 0.78f || bad.FireSupportMultiplier > 1.58f)
                {
                    details = $"consequence bounds failed round={round}";
                    return false;
                }
            }

            OperationChainOutcome clear = OperationChainDirector.EvaluateStage(1f, 0.94f, 0.72f, true);
            OperationChainOutcome setback = OperationChainDirector.EvaluateStage(1f, 0.70f, 0.50f, true);
            OperationChainOutcome playerLost = OperationChainDirector.EvaluateStage(1f, 0.95f, 0f, false);
            if (clear != OperationChainOutcome.Success || setback != OperationChainOutcome.Failure || playerLost != OperationChainOutcome.Failure)
            {
                details = $"outcome rules failed clear={clear} setback={setback} playerLost={playerLost}";
                return false;
            }
            successes++;
            failures += 2;

            int[] bosses = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            for (int i = 0; i < bosses.Length; i++)
            {
                int round = bosses[i];
                RoundEncounterProfile boss = CampaignEncounterDirector.Resolve(round);
                RoundEncounterProfile refined = OperationChainDirector.RefineEncounter(boss, OperationChainOutcome.Failure);
                if (!refined.BossRound || refined.HealthMultiplier != boss.HealthMultiplier || refined.Codename != boss.Codename)
                {
                    details = $"boss preservation failed round={round}";
                    return false;
                }
            }

            if (chainRounds != 30)
            {
                details = $"chain round count={chainRounds} expected=30";
                return false;
            }

            details = $"chainRounds={chainRounds} successes={successes} failures={failures} reward={OperationChainDirector.CompletionReward}";
            return true;
        }

        private static bool HasArgument(string needle)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], needle, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string message)
        {
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "OPERATION_CHAIN_PASS.txt" : "OPERATION_CHAIN_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
