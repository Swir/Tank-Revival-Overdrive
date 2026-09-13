using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22050)]
    public sealed class EnemySquadTacticsCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-squad-ai-smoke")) return;
            var go = new GameObject("EnemySquadTacticsCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemySquadTacticsCISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasSquad = EnemySquadTacticsDirector.Instance != null;
            bool hasBossCommand = BossCommandTacticsDirector.Instance != null;
            bool hasRegistry = RuntimeBattleRegistry.HealthSnapshot != null && RuntimeBattleRegistry.EnemySnapshot != null;
            bool squadConfig = EnemySquadTacticsDirector.ConfigurationValid;
            bool bossConfig = BossCommandTacticsDirector.ConfigurationValid;
            bool version = Application.version == "7.2.0-dev";

            if (hasGame && hasSquad && hasBossCommand && hasRegistry && squadConfig && bossConfig && version)
            {
                WriteMarker(true, $"game={hasGame} squad={hasSquad} bossCommand={hasBossCommand} registry={hasRegistry} squadConfig={squadConfig} bossConfig={bossConfig} roles={EnemySquadTacticsDirector.RoleCount} shotBudget={EnemySquadTacticsDirector.MaxCoordinatedShotsPerBeat} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} squad={hasSquad} bossCommand={hasBossCommand} registry={hasRegistry} squadConfig={squadConfig} bossConfig={bossConfig} version={Application.version}");
                Application.Quit(32);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "SQUAD_AI_PASS.txt" : "SQUAD_AI_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Enemy squad AI runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[EnemySquadTacticsCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
