using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Non-interactive runtime probe used only when the packaged Windows build is launched with
    /// -demo-ci-smoke. It verifies that the real standalone player boots far enough to create the
    /// authoritative TankGame and the player-facing demo shell, then writes a marker for CI.
    /// Normal players never see or execute this path.
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class DemoCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 12f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-demo-ci-smoke")) return;
            var go = new GameObject("DemoCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoCISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasShell = DemoExperienceDirector.HasPlayerFacingShell;
            bool hasStability = FindAnyObjectByType<RuntimeStabilityDirector>() != null;

            if (hasGame && hasShell && hasStability)
            {
                WriteMarker(true, $"game={hasGame} shell={hasShell} stability={hasStability}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} shell={hasShell} stability={hasStability}");
                Application.Quit(23);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "DEMO_SMOKE_PASS.txt" : "DEMO_SMOKE_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[DemoCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
