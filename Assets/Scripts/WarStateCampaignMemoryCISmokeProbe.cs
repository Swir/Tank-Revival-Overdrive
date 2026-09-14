using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22104)]
    public sealed class WarStateCampaignMemoryCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-war-state-smoke")) return;
            GameObject go = new GameObject("WarStateCampaignMemoryCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<WarStateCampaignMemoryCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool encounter = CampaignEncounterDirector.Instance != null;
            bool chains = OperationChainDirector.Instance != null;
            bool memory = WarStateCampaignMemoryDirector.Instance != null;
            bool bridge = WarStateCampaignMemoryDirector.BridgeAvailable;
            bool config = WarStateCampaignMemoryDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.4.0-dev";

            if (game && encounter && chains && memory && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} encounter={encounter} chains={chains} memory={memory} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} encounter={encounter} chains={chains} memory={memory} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(44);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            SectorWarState[] results = new SectorWarState[WarStateCampaignMemoryDirector.SectorCount];
            for (int i = 0; i < results.Length; i++)
                results[i] = i % 2 == 0 ? SectorWarState.Victory : SectorWarState.Defeat;

            int inherited = 0;
            int current = 0;
            int bossChecks = 0;
            for (int round = 1; round <= 100; round++)
            {
                int sector = WarStateCampaignMemoryDirector.SectorForRound(round);
                int sectorRound = WarStateCampaignMemoryDirector.SectorRound(round);
                SectorWarState state = WarStateCampaignMemoryDirector.ResolveMemoryForRound(round, results);
                SectorWarState expected = sectorRound >= WarStateCampaignMemoryDirector.OperationResolutionSectorRound
                    ? results[sector]
                    : (sector > 0 ? results[sector - 1] : SectorWarState.Unresolved);
                if (state != expected)
                {
                    details = $"memory mismatch round={round} actual={state} expected={expected}";
                    return false;
                }
                if (state != SectorWarState.Unresolved)
                {
                    if (sectorRound >= WarStateCampaignMemoryDirector.OperationResolutionSectorRound) current++;
                    else inherited++;
                }

                if (state == SectorWarState.Unresolved) continue;
                RoundEncounterProfile baseline = CampaignEncounterDirector.Resolve(round);
                int momentum = state == SectorWarState.Victory ? 6 : -6;
                RoundEncounterProfile refined = WarStateCampaignMemoryDirector.RefineEncounter(baseline, state, momentum);
                if (refined.Round != baseline.Round || refined.BossRound != baseline.BossRound)
                {
                    details = $"profile identity mismatch round={round}";
                    return false;
                }
                if (refined.HealthMultiplier < 0.80f || refined.HealthMultiplier > 1.52f || refined.FireSupportMultiplier < 0.76f || refined.FireSupportMultiplier > 1.62f)
                {
                    details = $"bounds failed round={round} hp={refined.HealthMultiplier:0.000} support={refined.FireSupportMultiplier:0.000}";
                    return false;
                }
                if (state == SectorWarState.Victory && refined.HealthMultiplier > baseline.HealthMultiplier + 0.001f)
                {
                    details = $"victory failed to reduce endurance round={round}";
                    return false;
                }
                if (state == SectorWarState.Defeat && refined.HealthMultiplier < baseline.HealthMultiplier - 0.001f)
                {
                    details = $"defeat failed to increase endurance round={round}";
                    return false;
                }
                if (baseline.BossRound) bossChecks++;
            }

            if (WarStateCampaignMemoryDirector.ClampMomentum(99) != WarStateCampaignMemoryDirector.MomentumMaximum ||
                WarStateCampaignMemoryDirector.ClampMomentum(-99) != WarStateCampaignMemoryDirector.MomentumMinimum ||
                WarStateCampaignMemoryDirector.ClampMomentum(3) != 3)
            {
                details = "momentum clamp failed";
                return false;
            }

            RoundEncounterProfile boss = CampaignEncounterDirector.Resolve(100);
            RoundEncounterProfile wonBoss = WarStateCampaignMemoryDirector.RefineEncounter(boss, SectorWarState.Victory, 6);
            RoundEncounterProfile lostBoss = WarStateCampaignMemoryDirector.RefineEncounter(boss, SectorWarState.Defeat, -6);
            if (!wonBoss.BossRound || !lostBoss.BossRound || wonBoss.HealthMultiplier >= lostBoss.HealthMultiplier)
            {
                details = "boss consequence ordering failed";
                return false;
            }

            details = $"sectors={results.Length} inheritedRounds={inherited} currentRounds={current} bossChecks={bossChecks} momentum=[{WarStateCampaignMemoryDirector.MomentumMinimum},{WarStateCampaignMemoryDirector.MomentumMaximum}] entryRepair={WarStateCampaignMemoryDirector.VictoryEagleRepair} bonds={WarStateCampaignMemoryDirector.VictoryEntryBondReward}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "WAR_STATE_PASS.txt" : "WAR_STATE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
