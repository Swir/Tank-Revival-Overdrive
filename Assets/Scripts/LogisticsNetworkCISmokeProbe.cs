using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22106)]
    public sealed class LogisticsNetworkCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-logistics-network-smoke")) return;
            GameObject go = new GameObject("LogisticsNetworkCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<LogisticsNetworkCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool encounter = CampaignEncounterDirector.Instance != null;
            bool reserves = StrategicReserveAttritionDirector.Instance != null;
            bool logistics = LogisticsNetworkDirector.Instance != null;
            bool bridge = LogisticsNetworkDirector.BridgeAvailable;
            bool config = LogisticsNetworkDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.6.0-dev";

            if (game && encounter && reserves && logistics && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} encounter={encounter} reserves={reserves} logistics={logistics} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} encounter={encounter} reserves={reserves} logistics={logistics} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(46);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int scheduled = 0;
            int depots = 0;
            int convoys = 0;
            int hubs = 0;
            for (int round = 1; round <= 100; round++)
            {
                if (!LogisticsNetworkDirector.HasLogisticsForRound(round)) continue;
                if (round % 10 == 0)
                {
                    details = "logistics scheduled on boss round=" + round;
                    return false;
                }
                scheduled++;
                switch (LogisticsNetworkDirector.NodeKindForRound(round))
                {
                    case LogisticsNodeKind.SupplyDepot: depots++; break;
                    case LogisticsNodeKind.MobileConvoy: convoys++; break;
                    case LogisticsNodeKind.RepairHub: hubs++; break;
                }
                int hp = LogisticsNetworkDirector.HealthForRound(round);
                int reward = LogisticsNetworkDirector.RewardForRound(round);
                if (hp < LogisticsNetworkDirector.NodeHealthMin || hp > LogisticsNetworkDirector.NodeHealthMax || reward < LogisticsNetworkDirector.RewardMin || reward > LogisticsNetworkDirector.RewardMax)
                {
                    details = "node tuning bounds failed round=" + round;
                    return false;
                }
            }

            if (scheduled < 24 || depots == 0 || convoys == 0 || hubs == 0)
            {
                details = $"schedule coverage failed scheduled={scheduled} depots={depots} convoys={convoys} hubs={hubs}";
                return false;
            }

            StrategicReserveSnapshot baseline = new StrategicReserveSnapshot(6, 12, 5, 10, 4, 8);
            foreach (LogisticsNodeKind kind in Enum.GetValues(typeof(LogisticsNodeKind)))
            {
                StrategicReserveSnapshot destroyed = LogisticsNetworkDirector.ResolveReserveAfterNode(baseline, kind, true);
                StrategicReserveSnapshot survived = LogisticsNetworkDirector.ResolveReserveAfterNode(baseline, kind, false);
                StrategicReserveKind reserveKind = LogisticsNetworkDirector.ReserveKindForNode(kind);
                int d = ValueFor(destroyed, reserveKind);
                int s = ValueFor(survived, reserveKind);
                int b = ValueFor(baseline, reserveKind);
                if (!(d < b && s > b && d < s))
                {
                    details = $"reserve consequence ordering failed kind={kind} destroyed={d} baseline={b} survived={s}";
                    return false;
                }
            }

            RoundEncounterProfile baseProfile = CampaignEncounterDirector.Resolve(57);
            RoundEncounterProfile intact = LogisticsNetworkDirector.RefineEncounter(baseProfile, 0);
            RoundEncounterProfile broken = LogisticsNetworkDirector.RefineEncounter(baseProfile, LogisticsNetworkDirector.MaxSectorInterdiction);
            if (broken.HealthMultiplier > intact.HealthMultiplier + 0.001f || broken.FireSupportMultiplier > intact.FireSupportMultiplier + 0.001f || broken.StrikeCadence < intact.StrikeCadence - 0.001f)
            {
                details = "interdiction encounter ordering failed";
                return false;
            }

            RoundEncounterProfile boss = CampaignEncounterDirector.Resolve(60);
            RoundEncounterProfile bossBroken = LogisticsNetworkDirector.RefineEncounter(boss, LogisticsNetworkDirector.MaxSectorInterdiction);
            if (!bossBroken.BossRound)
            {
                details = "boss preservation failed";
                return false;
            }

            details = $"scheduled={scheduled} depots={depots} convoys={convoys} hubs={hubs} maxInterdiction={LogisticsNetworkDirector.MaxSectorInterdiction}";
            return true;
        }

        private static int ValueFor(StrategicReserveSnapshot snapshot, StrategicReserveKind kind)
        {
            switch (kind)
            {
                case StrategicReserveKind.Armor: return snapshot.Armor;
                case StrategicReserveKind.FireSupport: return snapshot.FireSupport;
                default: return snapshot.ElectronicWarfare;
            }
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "LOGISTICS_NETWORK_PASS.txt" : "LOGISTICS_NETWORK_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
