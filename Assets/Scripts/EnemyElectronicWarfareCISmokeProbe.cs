using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22095)]
    public sealed class EnemyElectronicWarfareCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-ew-command-smoke")) return;
            var go = new GameObject("EnemyElectronicWarfareCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyElectronicWarfareCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasCounterplay = TacticalCounterplayDirector.Instance != null;
            bool hasAdaptive = AdaptiveFireControlDirector.Instance != null;
            bool hasNavigation = TacticalNavigationDirector.Instance != null;
            bool hasEw = EnemyElectronicWarfareDirector.Instance != null;
            bool config = EnemyElectronicWarfareDirector.ConfigurationValid;
            bool version = Application.version == "7.7.0-dev";

            if (hasGame && hasCounterplay && hasAdaptive && hasNavigation && hasEw && config && version)
            {
                EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
                WriteMarker(true,
                    $"game={hasGame} counterplay={hasCounterplay} adaptive={hasAdaptive} navigation={hasNavigation} ew={hasEw} config={config} " +
                    $"commandRound={EnemyElectronicWarfareDirector.MinimumCommandRound} commandHp={EnemyElectronicWarfareDirector.CommandHealthMultiplier:0.00} " +
                    $"superiority={EnemyElectronicWarfareDirector.TacticalSuperiorityDuration:0.0} smokeCadence={EnemyElectronicWarfareDirector.SmokeFlankCadence:0.00} maxSmokeResponders={EnemyElectronicWarfareDirector.MaxSmokeResponders} " +
                    $"decoyDuration={EnemyElectronicWarfareDirector.DecoyDuration:0.0} decoyCooldown={EnemyElectronicWarfareDirector.DecoyCooldown:0.0} detectDelay={EnemyElectronicWarfareDirector.CommandDecoyDetectionDelay:0.0} maxDecoyShots={EnemyElectronicWarfareDirector.MaxDecoyShotsPerBeat} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={hasGame} counterplay={hasCounterplay} adaptive={hasAdaptive} navigation={hasNavigation} ew={hasEw} config={config} version={Application.version}");
                Application.Quit(37);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "EW_COMMAND_PASS.txt" : "EW_COMMAND_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "EW command runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[EnemyElectronicWarfareCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
