using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22130)]
    public sealed class FortificationNetworkCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-fortification-network-smoke")) return;
            GameObject go = new GameObject("FortificationNetworkCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FortificationNetworkCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool frontline = DynamicFrontlineTerritoryDirector.Instance != null;
            bool strongpoint = StrongpointTerritoryWarfareDirector.Instance != null;
            bool network = FortificationNetworkDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool config = FortificationNetworkDirector.ConfigurationValid;
            bool bridge = FortificationNetworkDirector.BridgeAvailable;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.2.0-dev";

            if (game && frontline && strongpoint && network && navigation && config && bridge && matrix && version)
            {
                WriteMarker(true, $"game={game} frontline={frontline} strongpoint={strongpoint} network={network} navigation={navigation} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} frontline={frontline} strongpoint={strongpoint} network={network} navigation={navigation} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(52);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (FortificationNetworkDirector.CanNetworkRound(19) || !FortificationNetworkDirector.CanNetworkRound(21) || FortificationNetworkDirector.CanNetworkRound(30) || !FortificationNetworkDirector.CanNetworkRound(99) || FortificationNetworkDirector.CanNetworkRound(100))
            {
                details = "network schedule failed";
                return false;
            }

            int early = FortificationNetworkDirector.BreakthroughSizeForRound(20);
            int late = FortificationNetworkDirector.BreakthroughSizeForRound(95);
            if (early != 2 || late < 4 || late > FortificationNetworkDirector.MaxBreakthroughActors)
            {
                details = $"breakthrough curve failed {early}/{late}";
                return false;
            }

            if (FortificationNetworkDirector.MaxAuxNodes != 2 || FortificationNetworkDirector.ArtilleryHealth < 5 || FortificationNetworkDirector.RepairHealth < 6 ||
                FortificationNetworkDirector.ArtilleryInterval < 4f || FortificationNetworkDirector.RepairInterval < 7f ||
                FortificationNetworkDirector.MaxRepairPulses > 3 || FortificationNetworkDirector.MaxBreakthroughShots > 3 ||
                FortificationNetworkDirector.NodeLossPenalty > 8f)
            {
                details = "safety bounds failed";
                return false;
            }

            int eligible = 0;
            for (int round = 1; round <= 100; round++) if (FortificationNetworkDirector.CanNetworkRound(round)) eligible++;
            if (eligible < 70 || eligible > 75)
            {
                details = "eligible round count failed " + eligible;
                return false;
            }

            details = $"eligible={eligible} breakthrough={early}/{late} artillery={FortificationNetworkDirector.ArtilleryInterval:F1}s repair={FortificationNetworkDirector.RepairInterval:F1}s actors={FortificationNetworkDirector.MaxBreakthroughActors} shots={FortificationNetworkDirector.MaxBreakthroughShots}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "FORTIFICATION_NETWORK_PASS.txt" : "FORTIFICATION_NETWORK_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
