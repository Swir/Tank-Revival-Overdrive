using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class EnemyCommandNetworkCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-command-network-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("EnemyCommandNetworkCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<EnemyCommandNetworkCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<EnemyCommandNetworkDirector>() == null) { Fail("EnemyCommandNetworkDirector missing"); return; }
                if (!EnemyCommandNetworkDirector.ConfigurationValid) { Fail("Command-network configuration outside bounded values"); return; }
                if (EnemyCommandNetworkDirector.RoleCount != 3) { Fail("Command role catalog must contain exactly 3 roles"); return; }
                if (EnemyCommandNetworkDirector.EarliestRound < 15 || EnemyCommandNetworkDirector.EarliestRound > 25) { Fail("Earliest coordinated siege round outside expected band"); return; }
                if (EnemyCommandNetworkDirector.TelegraphSeconds < 4f || EnemyCommandNetworkDirector.TelegraphSeconds > 8f) { Fail("Telegraph window outside counterplay bounds"); return; }
                if (EnemyCommandNetworkDirector.MaxAssignedUnits > 5 || EnemyCommandNetworkDirector.SiegeVolleyCount > 4) { Fail("Command cell pressure exceeds bounded cap"); return; }
                if (!EnemyCommandNetworkDirector.ShieldCountersStrike(20f, 10f)) { Fail("AEGIS interception rule did not recognize active shield"); return; }
                if (EnemyCommandNetworkDirector.ShieldCountersStrike(10f, 10f)) { Fail("Expired shield incorrectly counters siege strike"); return; }

                string report = "v5.9 command network smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "roles=" + EnemyCommandNetworkDirector.RoleCount +
                    " telegraph=" + EnemyCommandNetworkDirector.TelegraphSeconds +
                    " cooldown=" + EnemyCommandNetworkDirector.OperationCooldown +
                    " assignedCap=" + EnemyCommandNetworkDirector.MaxAssignedUnits +
                    " volley=" + EnemyCommandNetworkDirector.SiegeVolleyCount +
                    " earliestRound=" + EnemyCommandNetworkDirector.EarliestRound + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMMAND_NETWORK_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMMAND_NETWORK_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v5.9 command network smoke FAIL: " + reason);
            Application.Quit(59);
        }
    }
}
