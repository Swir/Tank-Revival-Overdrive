using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22150)]
    public sealed class SiegeLogisticsFireControlCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-siege-logistics-smoke")) return;
            GameObject go = new GameObject("SiegeLogisticsFireControlCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<SiegeLogisticsFireControlCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool siege = SiegeLineWarfareDirector.Instance != null;
            bool logistics = SiegeLogisticsFireControlDirector.Instance != null;
            bool config = SiegeLogisticsFireControlDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.4.0-dev";

            if (game && siege && logistics && config && matrix && version)
            {
                WriteMarker(true, $"game={game} siege={siege} logistics={logistics} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} siege={siege} logistics={logistics} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(54);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(29) ||
                !SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(33) ||
                SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(40) ||
                SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(100))
            {
                details = "schedule failed";
                return false;
            }

            int early = SiegeLogisticsFireControlDirector.RelocationBudgetForRound(33);
            int late = SiegeLogisticsFireControlDirector.RelocationBudgetForRound(75);
            if (early != 1 || late != 2 || late > SiegeLogisticsFireControlDirector.MaxRelocationsPerBattery)
            {
                details = $"relocation bounds failed {early}/{late}";
                return false;
            }

            float full = SiegeLogisticsFireControlDirector.FireControlMultiplier(true, true);
            float noSpotter = SiegeLogisticsFireControlDirector.FireControlMultiplier(true, false);
            float noSupply = SiegeLogisticsFireControlDirector.FireControlMultiplier(false, true);
            float denied = SiegeLogisticsFireControlDirector.FireControlMultiplier(false, false);
            if (!(full > noSpotter && noSpotter > denied && full > noSupply && noSupply > denied && denied >= 0.5f))
            {
                details = $"fire-control ordering failed {full:F2}/{noSpotter:F2}/{noSupply:F2}/{denied:F2}";
                return false;
            }

            int eligible = 0;
            for (int round = 1; round <= 100; round++) if (SiegeLogisticsFireControlDirector.IsLogisticsSiegeRound(round)) eligible++;
            if (eligible < 15 || eligible > 25)
            {
                details = "eligible count failed " + eligible;
                return false;
            }

            if (SiegeLogisticsFireControlDirector.SupplyHealth < 5 || SiegeLogisticsFireControlDirector.SupplyMoveSpeed > 0.5f ||
                SiegeLogisticsFireControlDirector.MaxSpotters != 1 || SiegeLogisticsFireControlDirector.MaxSupplyNodes != 1 ||
                SiegeLogisticsFireControlDirector.RelocationCooldown < 6f)
            {
                details = "safety bounds failed";
                return false;
            }

            details = $"eligible={eligible} relocate={early}/{late} fire={full:F2}/{noSpotter:F2}/{noSupply:F2}/{denied:F2}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "SIEGE_LOGISTICS_PASS.txt" : "SIEGE_LOGISTICS_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}