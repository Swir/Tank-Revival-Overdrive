using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class OrzelekFortressCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-fortress-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("OrzelekFortressCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<OrzelekFortressCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<OrzelekFortressDirector>() == null) { Fail("OrzelekFortressDirector missing"); return; }
                if (!OrzelekFortressDirector.ConfigurationValid) { Fail("Fortress configuration outside safe bounds"); return; }
                if (OrzelekFortressDirector.ShieldCost >= OrzelekFortressDirector.CounterBatteryCost) { Fail("Defense cost ordering invalid"); return; }
                if (OrzelekFortressDirector.ShieldCooldown < 10f || OrzelekFortressDirector.RepairCooldown > 25f) { Fail("Cooldown bounds invalid"); return; }

                const string key = "TankRevival.WarBonds";
                int original = PlayerPrefs.GetInt(key, 0);
                PlayerPrefs.SetInt(key, 60);
                PlayerPrefs.Save();
                if (!OrzelekFortressDirector.ProbeSpend(OrzelekFortressDirector.ShieldCost)) { Restore(key, original); Fail("War Bond spend probe failed"); return; }
                int expected = 60 - OrzelekFortressDirector.ShieldCost;
                if (WarEconomyDirector.CurrentBonds != expected) { Restore(key, original); Fail("War Bond balance did not decrease correctly"); return; }
                Restore(key, original);

                string report = "v5.8 fortress smoke: PASS\nVersion: " + Application.version + "\nshield=" + OrzelekFortressDirector.ShieldCost + "/" + OrzelekFortressDirector.ShieldCooldown + " repair=" + OrzelekFortressDirector.RepairCost + "/" + OrzelekFortressDirector.RepairCooldown + " counter=" + OrzelekFortressDirector.CounterBatteryCost + "/" + OrzelekFortressDirector.CounterBatteryCooldown + " targets=" + OrzelekFortressDirector.CounterBatteryTargets + " radius=" + OrzelekFortressDirector.CounterBatteryRadius + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "FORTRESS_PASS.txt"), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex) { Fail(ex.GetType().Name + ": " + ex.Message); }
        }

        private static void Restore(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "FORTRESS_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v5.8 fortress smoke FAIL: " + reason);
            Application.Quit(58);
        }
    }
}
