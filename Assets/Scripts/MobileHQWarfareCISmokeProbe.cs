using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22097)]
    public sealed class MobileHQWarfareCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-mobile-hq-smoke")) return;
            var go = new GameObject("MobileHQWarfareCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileHQWarfareCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasEw = EnemyElectronicWarfareDirector.Instance != null;
            bool hasHunt = CommandNetworkHuntDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasAdaptive = AdaptiveFireControlDirector.Instance != null;
            bool hasMobileHq = MobileHQWarfareDirector.Instance != null;
            bool config = MobileHQWarfareDirector.ConfigurationValid;
            bool version = Application.version == "7.9.0-dev";

            if (hasGame && hasEw && hasHunt && hasNavigation && hasAdaptive && hasMobileHq && config && version)
            {
                WriteMarker(true,
                    $"game={hasGame} ew={hasEw} hunt={hasHunt} navigation={hasNavigation} adaptive={hasAdaptive} mobileHq={hasMobileHq} config={config} " +
                    $"minRound={MobileHQWarfareDirector.MinimumOperationRound} cadence={MobileHQWarfareDirector.OperationRoundCadence} hqHp={MobileHQWarfareDirector.MobileHQHealthMultiplier:0.00} " +
                    $"relocate={MobileHQWarfareDirector.MobileHQRelocationCadence:0.0} escortCap={MobileHQWarfareDirector.MaxEscortOrders} shots={MobileHQWarfareDirector.MaxCounterattackShots} " +
                    $"succession={MobileHQWarfareDirector.EmergencySuccessionDelay:0.0} successorHp={MobileHQWarfareDirector.SuccessorHealthMultiplier:0.00} collapse={MobileHQWarfareDirector.CommandCollapseDuration:0.0} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} ew={hasEw} hunt={hasHunt} navigation={hasNavigation} adaptive={hasAdaptive} mobileHq={hasMobileHq} config={config} version={Application.version}");
                Application.Quit(39);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "MOBILE_HQ_PASS.txt" : "MOBILE_HQ_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Mobile HQ warfare runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[MobileHQWarfareCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
