using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22060)]
    public sealed class TacticalNavigationCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tactical-navigation-smoke")) return;
            var go = new GameObject("TacticalNavigationCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalNavigationCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasSquad = EnemySquadTacticsDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasRegroup = TacticalRegroupDirector.Instance != null;
            bool hasRegistry = RuntimeBattleRegistry.HealthSnapshot != null && RuntimeBattleRegistry.EnemySnapshot != null;
            bool navigationConfig = TacticalNavigationDirector.ConfigurationValid;
            bool regroupConfig = TacticalRegroupDirector.ConfigurationValid;
            bool version = Application.version == "7.3.0-dev";

            if (hasGame && hasSquad && hasNavigation && hasRegroup && hasRegistry && navigationConfig && regroupConfig && version)
            {
                WriteMarker(true, $"game={hasGame} squad={hasSquad} navigation={hasNavigation} regroup={hasRegroup} registry={hasRegistry} navConfig={navigationConfig} regroupConfig={regroupConfig} cadence={TacticalNavigationDirector.DecisionCadence} probe={TacticalNavigationDirector.ProbeDistance} separation={TacticalNavigationDirector.SeparationRadius} antiStall={TacticalNavigationDirector.AntiStallSeconds} maxEnemies={TacticalNavigationDirector.MaxManagedEnemies} fallbackHp={TacticalRegroupDirector.FallbackHealthRatio} isolation={TacticalRegroupDirector.IsolationDistance} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} squad={hasSquad} navigation={hasNavigation} regroup={hasRegroup} registry={hasRegistry} navConfig={navigationConfig} regroupConfig={regroupConfig} version={Application.version}");
                Application.Quit(33);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "TACTICAL_NAV_PASS.txt" : "TACTICAL_NAV_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Tactical navigation runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[TacticalNavigationCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
