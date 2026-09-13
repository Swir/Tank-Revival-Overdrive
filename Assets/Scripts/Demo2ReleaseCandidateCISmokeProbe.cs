using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22100)]
    public sealed class Demo2ReleaseCandidateCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 18f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-demo2-rc-smoke")) return;
            var go = new GameObject("Demo2ReleaseCandidateCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<Demo2ReleaseCandidateCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool frontend = FindAnyObjectByType<FrontendHudArtDirector>() != null;
            bool squad = EnemySquadTacticsDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool terrain = TerrainIntelligenceDirector.Instance != null;
            bool adaptive = AdaptiveFireControlDirector.Instance != null;
            bool counterplay = TacticalCounterplayDirector.Instance != null;
            bool ew = EnemyElectronicWarfareDirector.Instance != null;
            bool hunt = CommandNetworkHuntDirector.Instance != null;
            bool hq = MobileHQWarfareDirector.Instance != null;
            bool integration = Demo2FullCampaignIntegrationDirector.Instance != null;
            bool config = TacticalCounterplayDirector.ConfigurationValid && MobileHQWarfareDirector.ConfigurationValid && Demo2FullCampaignIntegrationDirector.ConfigurationValid;
            bool version = Application.version == "8.0.0-rc1";

            if (game && frontend && squad && navigation && terrain && adaptive && counterplay && ew && hunt && hq && integration && config && version)
            {
                WriteMarker(true, $"game={game} frontend={frontend} squad={squad} navigation={navigation} terrain={terrain} adaptive={adaptive} counterplay={counterplay} ew={ew} hunt={hunt} mobileHq={hq} integration={integration} config={config} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} frontend={frontend} squad={squad} navigation={navigation} terrain={terrain} adaptive={adaptive} counterplay={counterplay} ew={ew} hunt={hunt} mobileHq={hq} integration={integration} config={config} version={Application.version}");
                Application.Quit(40);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "DEMO2_RC_PASS.txt" : "DEMO2_RC_FAIL.txt");
            string text = "Tank Revival: Orzel Overdrive\nDemo 2 RC integration smoke: " + (pass ? "PASS" : "FAIL") + "\nVersion: " + Application.version + "\nUnity: " + Application.unityVersion + "\nDetails: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[Demo2ReleaseCandidateCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
