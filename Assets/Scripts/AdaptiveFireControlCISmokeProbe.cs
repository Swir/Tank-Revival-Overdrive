using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22080)]
    public sealed class AdaptiveFireControlCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-adaptive-fire-control-smoke")) return;
            var go = new GameObject("AdaptiveFireControlCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveFireControlCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasSquad = EnemySquadTacticsDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasTerrain = TerrainIntelligenceDirector.Instance != null;
            bool hasAdaptive = AdaptiveFireControlDirector.Instance != null;
            bool hasRegistry = RuntimeBattleRegistry.HealthSnapshot != null && RuntimeBattleRegistry.EnemySnapshot != null;
            bool config = AdaptiveFireControlDirector.ConfigurationValid;
            bool version = Application.version == "7.5.0-dev";

            if (hasGame && hasSquad && hasNavigation && hasTerrain && hasAdaptive && hasRegistry && config && version)
            {
                AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
                WriteMarker(true,
                    $"game={hasGame} squad={hasSquad} navigation={hasNavigation} terrain={hasTerrain} adaptive={hasAdaptive} registry={hasRegistry} config={config} " +
                    $"phase={adaptive.ActivePhase} lanes={adaptive.ActiveSuppressionLanes} managed={adaptive.LastManagedActors} shots={adaptive.LastSequenceShots} " +
                    $"decision={AdaptiveFireControlDirector.DecisionCadence} minSequence={AdaptiveFireControlDirector.MinimumSequenceCadence} maxSequence={AdaptiveFireControlDirector.MaximumSequenceCadence} " +
                    $"prediction={AdaptiveFireControlDirector.MaximumPredictionSeconds} laneWidth={AdaptiveFireControlDirector.SuppressionLaneHalfWidth} maxLanes={AdaptiveFireControlDirector.MaxSuppressionLanes} " +
                    $"maxShots={AdaptiveFireControlDirector.MaxShotsPerSequenceBeat} maxActors={AdaptiveFireControlDirector.MaxManagedActors} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false,
                    $"game={hasGame} squad={hasSquad} navigation={hasNavigation} terrain={hasTerrain} adaptive={hasAdaptive} registry={hasRegistry} config={config} version={Application.version}");
                Application.Quit(35);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "ADAPTIVE_FIRE_CONTROL_PASS.txt" : "ADAPTIVE_FIRE_CONTROL_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Adaptive fire control runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[AdaptiveFireControlCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
