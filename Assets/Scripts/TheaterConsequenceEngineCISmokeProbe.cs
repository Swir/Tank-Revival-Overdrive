using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22230)]
    public sealed class TheaterConsequenceEngineCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-theater-consequence-smoke")) return;
            GameObject go = new GameObject("TheaterConsequenceEngineCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<TheaterConsequenceEngineCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool engine = TheaterConsequenceEngineDirector.Instance != null;
            bool memory = WarStateCampaignMemoryDirector.Instance != null;
            bool orders = TheaterOrderDirector.Instance != null;
            bool command = CombinedArmsCampaignCommandDirector.Instance != null;
            bool config = TheaterConsequenceEngineDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "10.2.0-dev";

            if (game && engine && memory && orders && command && config && matrix && version)
            {
                WriteMarker(true, $"game={game} engine={engine} memory={memory} orders={orders} command={command} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} engine={engine} memory={memory} orders={orders} command={command} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(62);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (TheaterConsequenceEngineDirector.SectorCount != 10)
            {
                details = "sector count invalid";
                return false;
            }

            for (int sector = 0; sector < 10; sector++)
            {
                int first = sector * 10 + 1;
                int last = first + 9;
                if (TheaterConsequenceEngineDirector.ExpectedSectorForRound(first) != sector ||
                    TheaterConsequenceEngineDirector.ExpectedSectorForRound(last) != sector)
                {
                    details = "sector mapping failed at " + sector;
                    return false;
                }
                if (TheaterConsequenceEngineDirector.IsConsequenceActiveForRound(last))
                {
                    details = "boss/final sector round received consequence at " + last;
                    return false;
                }
                for (int offset = 0; offset < TheaterConsequenceEngineDirector.ConsequenceRounds; offset++)
                {
                    if (!TheaterConsequenceEngineDirector.IsConsequenceActiveForRound(first + offset))
                    {
                        details = "opening consequence window failed sector=" + sector + " offset=" + offset;
                        return false;
                    }
                }
            }

            if (TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.Assault, SectorWarState.Defeat, -2) != TheaterSectorDoctrine.Breakthrough ||
                TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.Interdiction, SectorWarState.Victory, 2) != TheaterSectorDoctrine.SupplyStarved ||
                TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.Fortify, SectorWarState.Victory, 3) != TheaterSectorDoctrine.PreparedDefense)
            {
                details = "explicit theater-order branching failed";
                return false;
            }

            if (TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.None, SectorWarState.Victory, 0) != TheaterSectorDoctrine.Breakthrough ||
                TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.None, SectorWarState.Defeat, 0) != TheaterSectorDoctrine.PreparedDefense ||
                TheaterConsequenceEngineDirector.ResolveDoctrine(TheaterOrderKind.None, SectorWarState.Unresolved, 0) != TheaterSectorDoctrine.SupplyStarved)
            {
                details = "campaign-memory fallback branching failed";
                return false;
            }

            int positive = 0;
            int negative = 0;
            for (int i = 0; i < 12; i++)
            {
                positive = TheaterConsequenceEngineDirector.NextInitiative(positive, SectorWarState.Victory, 3);
                negative = TheaterConsequenceEngineDirector.NextInitiative(negative, SectorWarState.Defeat, -2);
            }
            if (positive != TheaterConsequenceEngineDirector.InitiativeMax || negative != TheaterConsequenceEngineDirector.InitiativeMin)
            {
                details = $"initiative caps failed {positive}/{negative}";
                return false;
            }

            int earlyShells = TheaterConsequenceEngineDirector.BreakthroughShellCount(51, 0);
            int lateShells = TheaterConsequenceEngineDirector.BreakthroughShellCount(81, 2);
            if (earlyShells != 1 || lateShells != 2 || lateShells > TheaterConsequenceEngineDirector.MaxBreakthroughShellsPerRound)
            {
                details = $"breakthrough shell bounds failed {earlyShells}/{lateShells}";
                return false;
            }

            if (!TheaterOrderDirector.HasDecisionForRound(42) || !CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(83) || !WarStateCampaignMemoryDirector.ConfigurationValid)
            {
                details = "required v8.4/v10.0/v10.1 integration unavailable";
                return false;
            }

            details = $"sectors=10 window={TheaterConsequenceEngineDirector.ConsequenceRounds} initiative={negative}..{positive} shells={earlyShells}/{lateShells}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "THEATER_CONSEQUENCE_PASS.txt" : "THEATER_CONSEQUENCE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
