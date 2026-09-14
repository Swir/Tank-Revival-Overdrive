using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22220)]
    public sealed class TheaterOrderCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-theater-orders-smoke")) return;
            GameObject go = new GameObject("TheaterOrderCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<TheaterOrderCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool orders = TheaterOrderDirector.Instance != null;
            bool command = CombinedArmsCampaignCommandDirector.Instance != null;
            bool logistics = LogisticsNetworkDirector.Instance != null;
            bool config = TheaterOrderDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "10.1.0-dev";

            if (game && orders && command && logistics && config && matrix && version)
            {
                WriteMarker(true, $"game={game} orders={orders} command={command} logistics={logistics} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} orders={orders} command={command} logistics={logistics} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(61);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int[] expected = { 42, 56, 70, 84, 98 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!TheaterOrderDirector.HasDecisionForRound(expected[i]))
                {
                    details = "expected decision missing at round " + expected[i];
                    return false;
                }
            }

            int[] blocked = { 1, 40, 41, 50, 69, 80, 90, 100 };
            for (int i = 0; i < blocked.Length; i++)
            {
                if (TheaterOrderDirector.HasDecisionForRound(blocked[i]))
                {
                    details = "blocked decision scheduled at round " + blocked[i];
                    return false;
                }
            }

            int count = TheaterOrderDirector.EligibleDecisionCount();
            if (count != expected.Length)
            {
                details = "decision count failed " + count;
                return false;
            }

            int through42 = TheaterOrderDirector.ActiveUntilForDecisionRound(42);
            int through98 = TheaterOrderDirector.ActiveUntilForDecisionRound(98);
            if (through42 != 46 || through98 != 100)
            {
                details = $"order duration failed through={through42}/{through98}";
                return false;
            }

            if (!TheaterOrderDirector.IsOrderActiveForRound(TheaterOrderKind.Assault, 44, through42) ||
                TheaterOrderDirector.IsOrderActiveForRound(TheaterOrderKind.None, 44, through42) ||
                TheaterOrderDirector.IsOrderActiveForRound(TheaterOrderKind.Assault, 47, through42))
            {
                details = "active-window semantics failed";
                return false;
            }

            int earlyShells = TheaterOrderDirector.AssaultShellCount(42);
            int lateShells = TheaterOrderDirector.AssaultShellCount(84);
            if (earlyShells != 1 || lateShells != 2 || lateShells > TheaterOrderDirector.MaxAssaultShellsPerRound)
            {
                details = $"assault shell bounds failed {earlyShells}/{lateShells}";
                return false;
            }

            if (TheaterOrderDirector.DefaultOrderForMomentum(-2) != TheaterOrderKind.Fortify ||
                TheaterOrderDirector.DefaultOrderForMomentum(0) != TheaterOrderKind.Interdiction ||
                TheaterOrderDirector.DefaultOrderForMomentum(3) != TheaterOrderKind.Assault)
            {
                details = "adaptive default doctrine failed";
                return false;
            }

            int assaultSupport = TheaterOrderDirector.CommandSupportBonus(TheaterOrderKind.Assault);
            int interdictionPressure = TheaterOrderDirector.CommandRelayPressureBonus(TheaterOrderKind.Interdiction);
            int fortifyReward = TheaterOrderDirector.CommandRewardBonus(TheaterOrderKind.Fortify);
            int noneReward = TheaterOrderDirector.CommandRewardBonus(TheaterOrderKind.None);
            if (assaultSupport != 1 || interdictionPressure != 1 || fortifyReward != TheaterOrderDirector.OperationRewardBonus || noneReward != 0)
            {
                details = $"command integration bounds failed support={assaultSupport} pressure={interdictionPressure} reward={fortifyReward}/{noneReward}";
                return false;
            }

            if (!CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(83) || !LogisticsNetworkDirector.HasLogisticsForRound(87))
            {
                details = "required v10.0/logistics integration schedule unavailable";
                return false;
            }

            details = $"decisions={count} through={through42}/{through98} assault={earlyShells}/{lateShells} integration={assaultSupport}/{interdictionPressure}/{fortifyReward}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "THEATER_ORDERS_PASS.txt" : "THEATER_ORDERS_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
