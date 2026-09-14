using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22140)]
    public sealed class SiegeLineWarfareCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-siege-line-smoke")) return;
            GameObject go = new GameObject("SiegeLineWarfareCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<SiegeLineWarfareCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool frontline = DynamicFrontlineTerritoryDirector.Instance != null;
            bool strongpoint = StrongpointTerritoryWarfareDirector.Instance != null;
            bool network = FortificationNetworkDirector.Instance != null;
            bool siege = SiegeLineWarfareDirector.Instance != null;
            bool config = SiegeLineWarfareDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.3.0-dev";

            if (game && frontline && strongpoint && network && siege && config && matrix && version)
            {
                WriteMarker(true, $"game={game} frontline={frontline} strongpoint={strongpoint} network={network} siege={siege} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} frontline={frontline} strongpoint={strongpoint} network={network} siege={siege} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(53);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (SiegeLineWarfareDirector.IsSiegeRound(23) || !SiegeLineWarfareDirector.IsSiegeRound(24) || SiegeLineWarfareDirector.IsSiegeRound(30) || SiegeLineWarfareDirector.IsSiegeRound(100))
            {
                details = "siege schedule failed";
                return false;
            }

            int early = SiegeLineWarfareDirector.BatteryCountForRound(24);
            int late = SiegeLineWarfareDirector.BatteryCountForRound(84);
            int earlyTeam = SiegeLineWarfareDirector.BreachTeamSizeForRound(24);
            int lateTeam = SiegeLineWarfareDirector.BreachTeamSizeForRound(96);
            if (early != 1 || late != 2 || earlyTeam != 2 || lateTeam < 4 || lateTeam > SiegeLineWarfareDirector.MaxBreachActors)
            {
                details = $"pressure curve failed batteries={early}/{late} teams={earlyTeam}/{lateTeam}";
                return false;
            }

            if (SiegeLineWarfareDirector.MaxBatteries != 2 || SiegeLineWarfareDirector.BatteryHealth < 5 ||
                SiegeLineWarfareDirector.BatteryFireInterval < 4f || SiegeLineWarfareDirector.CounterBatteryInterval < 3f ||
                SiegeLineWarfareDirector.MaxBatteryShots > 2 || SiegeLineWarfareDirector.MaxCounterBatteryShots != 1 ||
                SiegeLineWarfareDirector.BreachSuppressionSeconds > 10f)
            {
                details = "safety bounds failed";
                return false;
            }

            int eligible = 0;
            for (int round = 1; round <= 100; round++) if (SiegeLineWarfareDirector.IsSiegeRound(round)) eligible++;
            if (eligible < 20 || eligible > 30)
            {
                details = "eligible round count failed " + eligible;
                return false;
            }

            details = $"eligible={eligible} batteries={early}/{late} breach={earlyTeam}/{lateTeam} volley={SiegeLineWarfareDirector.MaxBatteryShots} counter={SiegeLineWarfareDirector.MaxCounterBatteryShots}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "SIEGE_LINE_PASS.txt" : "SIEGE_LINE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
