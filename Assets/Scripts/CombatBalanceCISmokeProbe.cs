using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CombatBalanceCISmokeProbe : MonoBehaviour
    {
        private const string Flag = "-combat-balance-smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("CombatBalanceCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<CombatBalanceCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!CombatBalanceTuning.Validate(out string reason))
                {
                    Fail(reason);
                    return;
                }

                if (FindAnyObjectByType<CombatBalanceDirector>() == null)
                {
                    Fail("CombatBalanceDirector missing from runtime");
                    return;
                }

                int[] rounds = { 1, 25, 50, 75, 100 };
                string report = "v5.5 combat balance smoke: PASS\nVersion: " + Application.version + "\n";
                for (int i = 0; i < rounds.Length; i++)
                {
                    int r = rounds[i];
                    report += "R" + r + " band=" + CombatBalanceTuning.BandName(r)
                        + " basic=" + CombatBalanceTuning.EnemyHealthMultiplier(r, EnemyKind.Basic).ToString("0.000")
                        + " siege=" + CombatBalanceTuning.EnemyHealthMultiplier(r, EnemyKind.Siege).ToString("0.000")
                        + " boss=" + CombatBalanceTuning.EnemyHealthMultiplier(r, EnemyKind.Boss).ToString("0.000")
                        + " reliefThreshold=" + CombatBalanceTuning.PressureThresholdForRelief(r) + "\n";
                }

                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_BALANCE_PASS.txt"), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBAT_BALANCE_FAIL.txt"), reason); }
            catch (Exception) { }
            Debug.LogError("[TankRevival] v5.5 combat balance smoke FAIL: " + reason);
            Application.Quit(41);
        }
    }
}
