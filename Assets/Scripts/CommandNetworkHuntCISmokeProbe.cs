using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22096)]
    public sealed class CommandNetworkHuntCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-command-network-hunt-smoke")) return;
            var go = new GameObject("CommandNetworkHuntCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CommandNetworkHuntCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasEw = EnemyElectronicWarfareDirector.Instance != null;
            bool hasCounterplay = TacticalCounterplayDirector.Instance != null;
            bool hasAdaptive = AdaptiveFireControlDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasHunt = CommandNetworkHuntDirector.Instance != null;
            bool config = CommandNetworkHuntDirector.ConfigurationValid;
            bool version = Application.version == "7.8.0-dev";

            if (hasGame && hasEw && hasCounterplay && hasAdaptive && hasNavigation && hasHunt && config && version)
            {
                WriteMarker(true,
                    $"game={hasGame} ew={hasEw} counterplay={hasCounterplay} adaptive={hasAdaptive} navigation={hasNavigation} hunt={hasHunt} config={config} " +
                    $"relayRound={CommandNetworkHuntDirector.MinimumRelayRound} maxRelays={CommandNetworkHuntDirector.MaxRelayNodes} relayHp={CommandNetworkHuntDirector.RelayHealthMultiplier:0.00} " +
                    $"sigintCd={CommandNetworkHuntDirector.SigintCooldown:0.0} reveal={CommandNetworkHuntDirector.SigintBaseRevealDuration:0.0}+{CommandNetworkHuntDirector.SigintWeakNetworkBonus:0.0} " +
                    $"reconCd={CommandNetworkHuntDirector.ReconCooldown:0.0} reconDuration={CommandNetworkHuntDirector.ReconDuration:0.0} sweep={CommandNetworkHuntDirector.ReconSweepCadence:0.00} " +
                    $"relayDisrupt={CommandNetworkHuntDirector.RelayDisruptionDuration:0.00} fullBreak={CommandNetworkHuntDirector.FullNetworkBreakDuration:0.0} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} ew={hasEw} counterplay={hasCounterplay} adaptive={hasAdaptive} navigation={hasNavigation} hunt={hasHunt} config={config} version={Application.version}");
                Application.Quit(38);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "COMMAND_NETWORK_HUNT_PASS.txt" : "COMMAND_NETWORK_HUNT_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Command network hunt runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[CommandNetworkHuntCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
