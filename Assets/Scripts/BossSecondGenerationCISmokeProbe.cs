using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class BossSecondGenerationCISmokeProbe : MonoBehaviour
    {
        private const string Flag = "-boss-generation-smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("BossSecondGenerationCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<BossSecondGenerationCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<BossSecondGenerationBootstrap>() == null)
                {
                    Fail("BossSecondGenerationBootstrap missing from runtime");
                    return;
                }
                if (BossSecondGenerationDirector.DoctrineCount != 4)
                {
                    Fail("Second-generation doctrine catalog must contain four doctrines");
                    return;
                }
                if (BossSecondGenerationDirector.RetaliationThreshold < 55 || BossSecondGenerationDirector.RetaliationThreshold > 75)
                {
                    Fail("Module retaliation threshold outside safe range");
                    return;
                }
                if (BossSecondGenerationDirector.CriticalRetaliationThreshold < 20 || BossSecondGenerationDirector.CriticalRetaliationThreshold >= BossSecondGenerationDirector.RetaliationThreshold)
                {
                    Fail("Critical module retaliation threshold invalid");
                    return;
                }
                if (BossSecondGenerationDirector.CoreVulnerabilitySeconds < 0.8f || BossSecondGenerationDirector.CoreVulnerabilitySeconds > 1.8f)
                {
                    Fail("Core vulnerability window outside bounded range");
                    return;
                }

                string report = "v5.7 boss generation smoke: PASS\nVersion: " + Application.version + "\n";
                int[] rounds = { 10, 20, 30, 40, 70, 100 };
                for (int i = 0; i < rounds.Length; i++)
                {
                    int round = rounds[i];
                    BossDoctrine doctrine = BossSecondGenerationDirector.DoctrineForRound(round);
                    for (int phase = 1; phase <= 4; phase++)
                    {
                        float cadence = BossSecondGenerationDirector.CommandCadence(doctrine, round, phase);
                        float telegraph = BossSecondGenerationDirector.TelegraphSeconds(doctrine, phase);
                        AmmoType ammo = BossSecondGenerationDirector.DoctrineAmmo(doctrine, phase);
                        if (cadence < 3.2f || cadence > 6.9f)
                        {
                            Fail("Doctrine cadence out of bounds: " + doctrine + " r" + round + " p" + phase + " = " + cadence);
                            return;
                        }
                        if (telegraph < 0.55f || telegraph > 1.15f)
                        {
                            Fail("Doctrine telegraph out of bounds: " + doctrine + " p" + phase + " = " + telegraph);
                            return;
                        }
                        report += "r" + round + " p" + phase + " " + doctrine + " cadence=" + cadence.ToString("0.00") + " telegraph=" + telegraph.ToString("0.00") + " ammo=" + ammo + "\n";
                    }
                }

                TankModule[] modules = { TankModule.Engine, TankModule.Tracks, TankModule.Gun, TankModule.AmmoRack };
                for (int i = 0; i < modules.Length; i++)
                {
                    AmmoType normal = BossSecondGenerationDirector.ReactionAmmo(modules[i], false);
                    AmmoType critical = BossSecondGenerationDirector.ReactionAmmo(modules[i], true);
                    if (normal == AmmoType.Basic)
                    {
                        Fail("Module reaction missing authored ammo for " + modules[i]);
                        return;
                    }
                    report += modules[i] + " normal=" + normal + " critical=" + critical + "\n";
                }

                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "BOSS_GENERATION_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "BOSS_GENERATION_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v5.7 boss generation smoke FAIL: " + reason);
            Application.Quit(57);
        }
    }
}
