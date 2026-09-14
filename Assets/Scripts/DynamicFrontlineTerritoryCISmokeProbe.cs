using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22110)]
    public sealed class DynamicFrontlineTerritoryCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-dynamic-frontline-smoke")) return;
            GameObject go = new GameObject("DynamicFrontlineTerritoryCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<DynamicFrontlineTerritoryCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool frontline = DynamicFrontlineTerritoryDirector.Instance != null;
            bool dynamicBattlefield = FindAnyObjectByType<DynamicBattlefieldDirector>() != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool recovery = ForwardRecoveryFrontlineDirector.Instance != null;
            bool config = DynamicFrontlineTerritoryDirector.ConfigurationValid;
            bool bridge = DynamicFrontlineTerritoryDirector.BridgeAvailable;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.0.0-dev";

            if (game && frontline && dynamicBattlefield && navigation && recovery && config && bridge && matrix && version)
            {
                WriteMarker(true, $"game={game} frontline={frontline} battlefield={dynamicBattlefield} navigation={navigation} recovery={recovery} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} frontline={frontline} battlefield={dynamicBattlefield} navigation={navigation} recovery={recovery} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(50);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (DynamicFrontlineTerritoryDirector.LaneCount != 3)
            {
                details = "lane count failed";
                return false;
            }

            Vector2 west = DynamicFrontlineTerritoryDirector.LanePosition(0);
            Vector2 center = DynamicFrontlineTerritoryDirector.LanePosition(1);
            Vector2 east = DynamicFrontlineTerritoryDirector.LanePosition(2);
            if (!(west.x < center.x && center.x < east.x) || Mathf.Abs(west.y - east.y) > 0.01f)
            {
                details = $"lane layout failed {west}/{center}/{east}";
                return false;
            }

            if (DynamicFrontlineTerritoryDirector.StateForScore(85f) != FrontlineControlState.Friendly ||
                DynamicFrontlineTerritoryDirector.StateForScore(50f) != FrontlineControlState.Contested ||
                DynamicFrontlineTerritoryDirector.StateForScore(15f) != FrontlineControlState.Enemy)
            {
                details = "control thresholds failed";
                return false;
            }

            int scheduled = 0;
            for (int round = 1; round <= 100; round++)
            {
                bool active = DynamicFrontlineTerritoryDirector.HasOperationForRound(round);
                if (active)
                {
                    scheduled++;
                    if (round < DynamicFrontlineTerritoryDirector.EarliestOperationRound || round % DynamicFrontlineTerritoryDirector.OperationInterval != 0 || round % 10 == 0 || round % DynamicBattlefieldDirector.ObjectiveInterval != 0)
                    {
                        details = "operation schedule overlap failed at round " + round;
                        return false;
                    }
                }
            }
            if (scheduled < 10 || scheduled > 16)
            {
                details = "operation count out of bounds " + scheduled;
                return false;
            }

            float carriedFriendly = DynamicFrontlineTerritoryDirector.CarryScore(100f);
            float carriedEnemy = DynamicFrontlineTerritoryDirector.CarryScore(0f);
            if (carriedFriendly <= 70f || carriedFriendly >= 90f || carriedEnemy >= 30f || carriedEnemy <= 10f)
            {
                details = $"carry bounds failed {carriedFriendly:F1}/{carriedEnemy:F1}";
                return false;
            }

            int earlyReward = DynamicFrontlineTerritoryDirector.RewardForRound(12);
            int lateReward = DynamicFrontlineTerritoryDirector.RewardForRound(96);
            if (earlyReward < 8 || lateReward > 13 || earlyReward >= lateReward)
            {
                details = $"reward bounds failed {earlyReward}/{lateReward}";
                return false;
            }

            details = $"lanes={west.x:F1}/{center.x:F1}/{east.x:F1} scheduled={scheduled} carry={carriedFriendly:F1}/{carriedEnemy:F1} reward={earlyReward}/{lateReward} duration={DynamicFrontlineTerritoryDirector.OperationDuration:F0}s";
            return true;
        }

        private static bool HasArgument(string needle)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (string.Equals(args[i], needle, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string message)
        {
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "DYNAMIC_FRONTLINE_PASS.txt" : "DYNAMIC_FRONTLINE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
