using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22107)]
    public sealed class SupplyRouteWarfareCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-supply-routes-smoke")) return;
            GameObject go = new GameObject("SupplyRouteWarfareCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<SupplyRouteWarfareCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool logistics = LogisticsNetworkDirector.Instance != null;
            bool routes = SupplyRouteWarfareDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool bridge = SupplyRouteWarfareDirector.BridgeAvailable;
            bool config = SupplyRouteWarfareDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.7.0-dev";

            if (game && logistics && routes && navigation && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} logistics={logistics} routes={routes} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} logistics={logistics} routes={routes} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(47);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int scheduled = 0;
            int earlyEscorts = 0;
            int midEscorts = 0;
            int lateEscorts = 0;
            for (int round = 1; round <= 100; round++)
            {
                int escorts = SupplyRouteWarfareDirector.EscortCountForRound(round);
                bool logistics = LogisticsNetworkDirector.HasLogisticsForRound(round);
                if (!logistics)
                {
                    if (escorts != 0)
                    {
                        details = "escort scheduled without logistics round=" + round;
                        return false;
                    }
                    continue;
                }

                scheduled++;
                if (escorts < 2 || escorts > SupplyRouteWarfareDirector.MaxEscorts)
                {
                    details = "escort bound failed round=" + round + " escorts=" + escorts;
                    return false;
                }
                if (round < 30) earlyEscorts = Mathf.Max(earlyEscorts, escorts);
                else if (round < 60) midEscorts = Mathf.Max(midEscorts, escorts);
                else lateEscorts = Mathf.Max(lateEscorts, escorts);
            }

            if (scheduled < 24 || earlyEscorts != 2 || midEscorts != 3 || lateEscorts != SupplyRouteWarfareDirector.MaxEscorts)
            {
                details = $"escort progression failed scheduled={scheduled} early={earlyEscorts} mid={midEscorts} late={lateEscorts}";
                return false;
            }

            int maxHp = LogisticsNetworkDirector.NodeHealthMax;
            int threshold = Mathf.CeilToInt(maxHp * SupplyRouteWarfareDirector.RerouteDamageFraction);
            if (SupplyRouteWarfareDirector.ShouldReroute(LogisticsNodeKind.SupplyDepot, threshold + 2, maxHp, 0, SupplyRouteWarfareDirector.RerouteCooldown + 1f))
            {
                details = "static depot accepted reroute";
                return false;
            }
            if (!SupplyRouteWarfareDirector.ShouldReroute(LogisticsNodeKind.MobileConvoy, threshold, maxHp, 0, SupplyRouteWarfareDirector.RerouteCooldown + 0.1f))
            {
                details = "damaged convoy rejected valid reroute";
                return false;
            }
            if (SupplyRouteWarfareDirector.ShouldReroute(LogisticsNodeKind.MobileConvoy, threshold, maxHp, SupplyRouteWarfareDirector.MaxReroutesPerConvoy, SupplyRouteWarfareDirector.RerouteCooldown + 1f))
            {
                details = "reroute cap failed";
                return false;
            }
            if (SupplyRouteWarfareDirector.ShouldReroute(LogisticsNodeKind.MobileConvoy, threshold, maxHp, 0, SupplyRouteWarfareDirector.RerouteCooldown - 0.1f))
            {
                details = "reroute cooldown failed";
                return false;
            }

            if (!SupplyRouteWarfareDirector.CanRepairNode(5, 10, 0, SupplyRouteWarfareDirector.RepairRange - 0.1f) ||
                SupplyRouteWarfareDirector.CanRepairNode(10, 10, 0, 1f) ||
                SupplyRouteWarfareDirector.CanRepairNode(5, 10, SupplyRouteWarfareDirector.MaxRepairsPerNode, 1f) ||
                SupplyRouteWarfareDirector.CanRepairNode(5, 10, 0, SupplyRouteWarfareDirector.RepairRange + 0.2f))
            {
                details = "repair safety bounds failed";
                return false;
            }

            foreach (LogisticsNodeKind kind in Enum.GetValues(typeof(LogisticsNodeKind)))
            {
                int repair = SupplyRouteWarfareDirector.CapturedSupplyRepair(kind);
                if (repair != 1)
                {
                    details = "captured supply repair mapping failed kind=" + kind;
                    return false;
                }
            }

            details = $"scheduled={scheduled} escortProgression={earlyEscorts}/{midEscorts}/{lateEscorts} rerouteCap={SupplyRouteWarfareDirector.MaxReroutesPerConvoy} repairs={SupplyRouteWarfareDirector.MaxRepairsPerNode} captureBonds={SupplyRouteWarfareDirector.CaptureBondReward}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "SUPPLY_ROUTES_PASS.txt" : "SUPPLY_ROUTES_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
