using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22090)]
    public sealed class TacticalCounterplayCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tactical-counterplay-smoke")) return;
            var go = new GameObject("TacticalCounterplayCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalCounterplayCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasAdaptive = AdaptiveFireControlDirector.Instance != null;
            bool hasSquad = EnemySquadTacticsDirector.Instance != null;
            bool hasBoss = BossCommandTacticsDirector.Instance != null;
            bool hasCounterplay = TacticalCounterplayDirector.Instance != null;
            bool config = TacticalCounterplayDirector.ConfigurationValid;
            bool version = Application.version == "7.6.0-dev";

            if (hasGame && hasAdaptive && hasSquad && hasBoss && hasCounterplay && config && version)
            {
                TacticalCounterplayDirector counterplay = TacticalCounterplayDirector.Instance;
                WriteMarker(true,
                    $"game={hasGame} adaptive={hasAdaptive} squad={hasSquad} boss={hasBoss} counterplay={hasCounterplay} config={config} " +
                    $"smokeRadius={TacticalCounterplayDirector.SmokeRadius} smokeDuration={TacticalCounterplayDirector.SmokeDuration} smokeCooldown={TacticalCounterplayDirector.SmokeCooldown} " +
                    $"ecmDuration={TacticalCounterplayDirector.EcmDuration} ecmCooldown={TacticalCounterplayDirector.EcmCooldown} empJam={TacticalCounterplayDirector.EmpJamDuration} maxSmoke={TacticalCounterplayDirector.MaxSmokeZones} " +
                    $"zones={counterplay.ActiveSmokeZones} empInterrupts={counterplay.EmpInterrupts} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false,
                    $"game={hasGame} adaptive={hasAdaptive} squad={hasSquad} boss={hasBoss} counterplay={hasCounterplay} config={config} version={Application.version}");
                Application.Quit(36);
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "TACTICAL_COUNTERPLAY_PASS.txt" : "TACTICAL_COUNTERPLAY_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Tactical counterplay runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[TacticalCounterplayCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
