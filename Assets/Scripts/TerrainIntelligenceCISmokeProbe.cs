using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22070)]
    public sealed class TerrainIntelligenceCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-terrain-intelligence-smoke")) return;
            var go = new GameObject("TerrainIntelligenceCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<TerrainIntelligenceCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasSquad = EnemySquadTacticsDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasTerrain = TerrainIntelligenceDirector.Instance != null;
            bool hasRegistry = RuntimeBattleRegistry.HealthSnapshot != null && RuntimeBattleRegistry.EnemySnapshot != null;
            bool navigationConfig = TacticalNavigationDirector.ConfigurationValid;
            bool terrainConfig = TerrainIntelligenceDirector.ConfigurationValid;
            bool version = Application.version == "7.4.0-dev";

            if (hasGame && hasSquad && hasNavigation && hasTerrain && hasRegistry && navigationConfig && terrainConfig && version)
            {
                TerrainIntelligenceDirector terrain = TerrainIntelligenceDirector.Instance;
                WriteMarker(true,
                    $"game={hasGame} squad={hasSquad} navigation={hasNavigation} terrain={hasTerrain} registry={hasRegistry} " +
                    $"navConfig={navigationConfig} terrainConfig={terrainConfig} obstacles={terrain.CachedObstacleCount} coverCandidates={terrain.CoverCandidateCount} " +
                    $"breachCandidates={terrain.BreachCandidateCount} scan={TerrainIntelligenceDirector.ScanCadence} coverRadius={TerrainIntelligenceDirector.CoverSearchRadius} " +
                    $"breachWidth={TerrainIntelligenceDirector.BreachCorridorWidth} breachRange={TerrainIntelligenceDirector.BreachRange} " +
                    $"breachCadence={TerrainIntelligenceDirector.MinimumBreachCadence} maxObstacles={TerrainIntelligenceDirector.MaxCachedObstacles} " +
                    $"maxBreachActors={TerrainIntelligenceDirector.MaxBreachActorsPerBeat} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false,
                    $"game={hasGame} squad={hasSquad} navigation={hasNavigation} terrain={hasTerrain} registry={hasRegistry} " +
                    $"navConfig={navigationConfig} terrainConfig={terrainConfig} version={Application.version}");
                Application.Quit(34);
            }
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string details)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "TERRAIN_INTELLIGENCE_PASS.txt" : "TERRAIN_INTELLIGENCE_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Terrain intelligence runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[TerrainIntelligenceCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
