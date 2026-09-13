using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(21000)]
    public sealed class Demo2CISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-demo2-ci-smoke")) return;
            var go = new GameObject("Demo2CISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<Demo2CISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasLegacyShell = DemoExperienceDirector.HasPlayerFacingShell;
            bool hasDemo2 = Demo2PlayerExperienceDirector.Instance != null;
            bool hasReadability = CombatReadabilityDirector.Instance != null;
            bool hasStability = FindAnyObjectByType<RuntimeStabilityDirector>() != null;
            bool config = Demo2PlayerExperienceDirector.ConfigurationValid && CombatReadabilityDirector.ConfigurationValid;
            bool version = Application.version == "7.0.0-rc1";

            if (hasGame && hasLegacyShell && hasDemo2 && hasReadability && hasStability && config && version)
            {
                WriteMarker(true, $"game={hasGame} shell={hasLegacyShell} demo2={hasDemo2} hud={hasReadability} stability={hasStability} config={config} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} shell={hasLegacyShell} demo2={hasDemo2} hud={hasReadability} stability={hasStability} config={config} version={Application.version}");
                Application.Quit(27);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "DEMO2_SMOKE_PASS.txt" : "DEMO2_SMOKE_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Demo 2 runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[Demo2CISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
