using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22120)]
    public sealed class StrongpointTerritoryWarfareCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-strongpoint-warfare-smoke")) return;
            GameObject go = new GameObject("StrongpointTerritoryWarfareCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<StrongpointTerritoryWarfareCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool frontline = DynamicFrontlineTerritoryDirector.Instance != null;
            bool strongpoints = StrongpointTerritoryWarfareDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool economy = FindAnyObjectByType<WarEconomyDirector>() != null;
            bool config = StrongpointTerritoryWarfareDirector.ConfigurationValid;
            bool bridge = StrongpointTerritoryWarfareDirector.BridgeAvailable;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "9.1.0-dev";

            if (game && frontline && strongpoints && navigation && economy && config && bridge && matrix && version)
            {
                WriteMarker(true, $"game={game} frontline={frontline} strongpoints={strongpoints} navigation={navigation} economy={economy} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} frontline={frontline} strongpoints={strongpoints} navigation={navigation} economy={economy} config={config} bridge={bridge} matrix={matrix} {details} version={Application.version}");
                Application.Quit(51);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (StrongpointTerritoryWarfareDirector.CanFortifyRound(10) || !StrongpointTerritoryWarfareDirector.CanFortifyRound(15) || StrongpointTerritoryWarfareDirector.CanFortifyRound(20) || !StrongpointTerritoryWarfareDirector.CanFortifyRound(99) || StrongpointTerritoryWarfareDirector.CanFortifyRound(100))
            {
                details = "fortification schedule failed";
                return false;
            }

            int earlyCost = StrongpointTerritoryWarfareDirector.BuildCostForRound(15);
            int lateCost = StrongpointTerritoryWarfareDirector.BuildCostForRound(95);
            if (earlyCost < 16 || earlyCost > 20 || lateCost < earlyCost || lateCost > earlyCost + 2)
            {
                details = $"build cost bounds failed {earlyCost}/{lateCost}";
                return false;
            }

            int earlyHp = StrongpointTerritoryWarfareDirector.StrongpointHealthForRound(15);
            int lateHp = StrongpointTerritoryWarfareDirector.StrongpointHealthForRound(95);
            if (earlyHp < StrongpointTerritoryWarfareDirector.BaseStrongpointHealth || lateHp <= earlyHp || lateHp >= StrongpointTerritoryWarfareDirector.MaxStrongpointHealth)
            {
                details = $"health curve failed {earlyHp}/{lateHp}";
                return false;
            }

            int earlyAttack = StrongpointTerritoryWarfareDirector.CounterattackSizeForRound(15);
            int lateAttack = StrongpointTerritoryWarfareDirector.CounterattackSizeForRound(95);
            if (earlyAttack != 2 || lateAttack < 4 || lateAttack > StrongpointTerritoryWarfareDirector.MaxCounterattackActors)
            {
                details = $"counterattack budget failed {earlyAttack}/{lateAttack}";
                return false;
            }

            if (StrongpointTerritoryWarfareDirector.ControlBuildBoost <= 0f || StrongpointTerritoryWarfareDirector.ControlBuildBoost > 12f ||
                StrongpointTerritoryWarfareDirector.ControlReinforceBoost <= 0f || StrongpointTerritoryWarfareDirector.ControlReinforceBoost >= StrongpointTerritoryWarfareDirector.ControlBuildBoost ||
                StrongpointTerritoryWarfareDirector.ControlLossOnDestroyed <= 0f || StrongpointTerritoryWarfareDirector.ControlLossOnDestroyed > 15f ||
                StrongpointTerritoryWarfareDirector.MaxVolleyShots > 2 || StrongpointTerritoryWarfareDirector.MaxSupportPulses > 3)
            {
                details = "control/support safety bounds failed";
                return false;
            }

            int eligible = 0;
            for (int round = 1; round <= 100; round++) if (StrongpointTerritoryWarfareDirector.CanFortifyRound(round)) eligible++;
            if (eligible < 70 || eligible > 80)
            {
                details = "eligible round count failed " + eligible;
                return false;
            }

            details = $"eligible={eligible} cost={earlyCost}/{lateCost} hp={earlyHp}/{lateHp} counterattack={earlyAttack}/{lateAttack} buildBoost={StrongpointTerritoryWarfareDirector.ControlBuildBoost:F0} loss={StrongpointTerritoryWarfareDirector.ControlLossOnDestroyed:F0}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "STRONGPOINT_WARFARE_PASS.txt" : "STRONGPOINT_WARFARE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
