using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22000)]
    public sealed class FrontendHudArtCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-frontend-hud-smoke")) return;
            var go = new GameObject("FrontendHudArtCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FrontendHudArtCISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasFrontend = FrontendHudArtDirector.Instance != null;
            bool hasReadability = CombatReadabilityDirector.Instance != null;
            bool hasDemo2 = Demo2PlayerExperienceDirector.Instance != null;
            bool hasStability = FindAnyObjectByType<RuntimeStabilityDirector>() != null;
            bool ammoCatalog = AmmoDatabase.AmmoTypeCount == FrontendHudArtDirector.AmmoChipCount && AmmoDatabase.AmmoTypeCount == 7;
            bool config = FrontendHudArtDirector.ConfigurationValid && CombatReadabilityDirector.ConfigurationValid;
            bool version = Application.version == "7.1.0-dev";

            if (hasGame && hasFrontend && hasReadability && hasDemo2 && hasStability && ammoCatalog && config && version)
            {
                WriteMarker(true, $"game={hasGame} frontend={hasFrontend} readability={hasReadability} demo2={hasDemo2} stability={hasStability} ammo7={ammoCatalog} config={config} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} frontend={hasFrontend} readability={hasReadability} demo2={hasDemo2} stability={hasStability} ammo7={ammoCatalog} config={config} version={Application.version}");
                Application.Quit(31);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "FRONTEND_HUD_PASS.txt" : "FRONTEND_HUD_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Frontend/HUD runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[FrontendHudArtCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
