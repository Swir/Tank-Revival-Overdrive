using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class AceCommanderCISmokeProbe : MonoBehaviour
    {
        private const string Flag = "-ace-commander-smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("AceCommanderCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<AceCommanderCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<AceCommanderDirector>() == null) { Fail("AceCommanderDirector missing from runtime"); return; }
                if (AceCommanderDirector.ArchetypeCount != 4) { Fail("Ace archetype catalog must contain four archetypes"); return; }
                if (!AceCommanderDirector.IsAceRound(12) || !AceCommanderDirector.IsAceRound(35) || AceCommanderDirector.IsAceRound(3)) { Fail("Ace round schedule invalid"); return; }

                var dummy = new GameObject("ACE_VALIDATION_HEALTH");
                Health health = dummy.AddComponent<Health>();
                health.Initialize(Team.Enemy, 10);
                int mutated = AceCommanderDirector.ApplyHealthMutationForValidation(health, AceArchetype.SiegeMarshal, 75);
                if (mutated <= 10 || mutated > 17) { Fail("Ace health mutation outside bounded range: " + mutated); return; }

                string report = "v5.6 ace commander smoke: PASS\nVersion: " + Application.version + "\n";
                for (int i = 0; i < AceCommanderDirector.ArchetypeCount; i++)
                {
                    AceArchetype a = (AceArchetype)i;
                    float hp = AceCommanderDirector.HealthMultiplier(a, 75);
                    float cadence = AceCommanderDirector.SpecialCadence(a, 75);
                    int bounty = AceCommanderDirector.BountyFor(a, 75);
                    if (hp < 1.15f || hp > 1.64f || cadence < 3.4f || cadence > 7.4f || bounty < 8 || bounty > 24)
                    { Fail("Archetype tuning bounds invalid for " + a); return; }
                    report += a + " hp=" + hp.ToString("0.00") + " cadence=" + cadence.ToString("0.00") + " bounty=" + bounty + " ammo=" + AceCommanderDirector.SpecialAmmo(a) + "\n";
                }

                Destroy(dummy);
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ACE_COMMANDER_PASS.txt"), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex) { Fail(ex.GetType().Name + ": " + ex.Message); }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ACE_COMMANDER_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v5.6 ace commander smoke FAIL: " + reason);
            Application.Quit(56);
        }
    }
}
