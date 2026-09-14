using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22200)]
    public sealed class CombinedArmsCampaignCommandCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-campaign-command-smoke")) return;
            GameObject go = new GameObject("CombinedArmsCampaignCommandCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CombinedArmsCampaignCommandCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool command = CombinedArmsCampaignCommandDirector.Instance != null;
            bool logistics = FindAnyObjectByType<LogisticsNetworkDirector>() != null;
            bool supply = FindAnyObjectByType<SupplyRouteWarfareDirector>() != null;
            bool frontline = FindAnyObjectByType<DynamicFrontlineTerritoryDirector>() != null;
            bool fortifications = FindAnyObjectByType<FortificationNetworkDirector>() != null;
            bool fireMission = FireMissionNetworkDirector.Instance != null;
            bool config = CombinedArmsCampaignCommandDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool integrations = CombinedArmsCampaignCommandDirector.CoreIntegrationTypesAvailable();
            bool version = Application.version == "10.0.0-dev";

            if (game && command && logistics && supply && frontline && fortifications && fireMission && config && matrix && integrations && version)
            {
                WriteMarker(true, $"game={game} command={command} logistics={logistics} supply={supply} frontline={frontline} fortifications={fortifications} fireMission={fireMission} config={config} matrix={matrix} integrations={integrations} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} command={command} logistics={logistics} supply={supply} frontline={frontline} fortifications={fortifications} fireMission={fireMission} config={config} matrix={matrix} integrations={integrations} {details} version={Application.version}");
                Application.Quit(60);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int[] expected = { 41, 55, 62, 69, 76, 83, 97 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(expected[i]))
                {
                    details = "expected command operation missing at round " + expected[i];
                    return false;
                }
            }

            int[] blocked = { 1, 40, 48, 50, 90, 100 };
            for (int i = 0; i < blocked.Length; i++)
            {
                if (CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(blocked[i]))
                {
                    details = "blocked round scheduled " + blocked[i];
                    return false;
                }
            }

            int eligible = CombinedArmsCampaignCommandDirector.EligibleOperationCount();
            if (eligible != expected.Length)
            {
                details = "eligible count failed " + eligible;
                return false;
            }

            int win = 0;
            for (int i = 0; i < 10; i++) win = CombinedArmsCampaignCommandDirector.NextMomentum(win, true);
            int loss = 0;
            for (int i = 0; i < 10; i++) loss = CombinedArmsCampaignCommandDirector.NextMomentum(loss, false);
            if (win != CombinedArmsCampaignCommandDirector.MomentumMax || loss != CombinedArmsCampaignCommandDirector.MomentumMin)
            {
                details = $"momentum clamp failed {win}/{loss}";
                return false;
            }

            int lowPressureWaves = CombinedArmsCampaignCommandDirector.ResponseWaveCount(2);
            int highPressureWaves = CombinedArmsCampaignCommandDirector.ResponseWaveCount(-1);
            int friendlySize = CombinedArmsCampaignCommandDirector.ResponseWaveSize(83, 2);
            int enemySize = CombinedArmsCampaignCommandDirector.ResponseWaveSize(83, -2);
            if (lowPressureWaves != 2 || highPressureWaves != 3 || friendlySize < 2 || enemySize > 4 || enemySize <= friendlySize)
            {
                details = $"pressure bounds failed waves={lowPressureWaves}/{highPressureWaves} size={friendlySize}/{enemySize}";
                return false;
            }

            int healthyRelay = CombinedArmsCampaignCommandDirector.RelayHealthForRound(83, 2);
            int pressuredRelay = CombinedArmsCampaignCommandDirector.RelayHealthForRound(83, -2);
            int rewardNeutral = CombinedArmsCampaignCommandDirector.RewardForRound(83, 0);
            int rewardMomentum = CombinedArmsCampaignCommandDirector.RewardForRound(83, 3);
            int supportNeutral = CombinedArmsCampaignCommandDirector.FriendlySupportShells(0);
            int supportMomentum = CombinedArmsCampaignCommandDirector.FriendlySupportShells(3);
            if (pressuredRelay < healthyRelay || pressuredRelay > CombinedArmsCampaignCommandDirector.RelayHealthMax || rewardMomentum <= rewardNeutral || rewardMomentum > CombinedArmsCampaignCommandDirector.RewardMax || supportNeutral != 2 || supportMomentum > CombinedArmsCampaignCommandDirector.MaxFriendlySupportShells || supportMomentum <= supportNeutral)
            {
                details = $"momentum effects failed relay={healthyRelay}/{pressuredRelay} reward={rewardNeutral}/{rewardMomentum} support={supportNeutral}/{supportMomentum}";
                return false;
            }

            details = $"eligible={eligible} momentum={loss}..{win} waves={lowPressureWaves}/{highPressureWaves} sizes={friendlySize}/{enemySize} relay={healthyRelay}/{pressuredRelay} reward={rewardNeutral}/{rewardMomentum} support={supportNeutral}/{supportMomentum}";
            return true;
        }

        private static bool HasArgument(string needle)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], needle, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string message)
        {
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "CAMPAIGN_COMMAND_PASS.txt" : "CAMPAIGN_COMMAND_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
