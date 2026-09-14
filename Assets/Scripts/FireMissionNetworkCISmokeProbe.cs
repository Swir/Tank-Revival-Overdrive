using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22160)]
    public sealed class FireMissionNetworkCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-fire-mission-network-smoke")) return;
            GameObject go = new GameObject("FireMissionNetworkCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FireMissionNetworkCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool hunt = CommandNetworkHuntDirector.Instance != null;
            bool siege = SiegeLineWarfareDirector.Instance != null;
            bool logistics = SiegeLogisticsFireControlDirector.Instance != null;
            bool network = FireMissionNetworkDirector.Instance != null;
            bool config = FireMissionNetworkDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.5.0-dev";

            if (game && hunt && siege && logistics && network && config && matrix && version)
            {
                WriteMarker(true, $"game={game} hunt={hunt} siege={siege} logistics={logistics} network={network} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} hunt={hunt} siege={siege} logistics={logistics} network={network} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(55);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (FireMissionNetworkDirector.IsFireMissionRound(35) || !FireMissionNetworkDirector.IsFireMissionRound(39) || FireMissionNetworkDirector.IsFireMissionRound(40) || FireMissionNetworkDirector.IsFireMissionRound(100))
            {
                details = "schedule failed";
                return false;
            }

            int earlyDecoys = FireMissionNetworkDirector.DecoyCountForRound(39);
            int lateDecoys = FireMissionNetworkDirector.DecoyCountForRound(75);
            if (earlyDecoys != 1 || lateDecoys != 2 || lateDecoys > FireMissionNetworkDirector.MaxDecoys)
            {
                details = $"decoy bounds failed {earlyDecoys}/{lateDecoys}";
                return false;
            }

            float fullEnemy = FireMissionNetworkDirector.LockDuration(true, true);
            float noSupply = FireMissionNetworkDirector.LockDuration(false, true);
            float noSpotter = FireMissionNetworkDirector.LockDuration(true, false);
            float degraded = FireMissionNetworkDirector.LockDuration(false, false);
            if (!(fullEnemy < noSupply && fullEnemy < noSpotter && degraded >= noSupply && degraded >= noSpotter && fullEnemy >= 4f && degraded <= 7f))
            {
                details = $"lock ordering failed {fullEnemy:F2}/{noSupply:F2}/{noSpotter:F2}/{degraded:F2}";
                return false;
            }

            int eligible = 0;
            for (int round = 1; round <= 100; round++) if (FireMissionNetworkDirector.IsFireMissionRound(round)) eligible++;
            if (eligible < 12 || eligible > 24)
            {
                details = "eligible count failed " + eligible;
                return false;
            }

            if (FireMissionNetworkDirector.MaxCounterSurveillanceRedeploys != 1 || FireMissionNetworkDirector.DecoyRedeployDelay < 3f || FireMissionNetworkDirector.CounterSurveillancePenaltySeconds > 2f || FireMissionNetworkDirector.EarliestRound < CommandNetworkHuntDirector.MinimumRelayRound)
            {
                details = "safety bounds failed";
                return false;
            }

            details = $"eligible={eligible} decoys={earlyDecoys}/{lateDecoys} lock={fullEnemy:F2}/{noSupply:F2}/{noSpotter:F2}/{degraded:F2}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "FIRE_MISSION_NETWORK_PASS.txt" : "FIRE_MISSION_NETWORK_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
