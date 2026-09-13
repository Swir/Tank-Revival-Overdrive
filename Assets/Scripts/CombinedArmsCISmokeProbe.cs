using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CombinedArmsCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-combined-arms-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("CombinedArmsCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<CombinedArmsCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<CombinedArmsDirector>() == null) { Fail("CombinedArmsDirector missing"); return; }
                if (!CombinedArmsDirector.ConfigurationValid) { Fail("Combined arms configuration outside bounded values or TankGame spawn bridge missing"); return; }

                if (!CombinedArmsDirector.HasOperationForRound(26)) { Fail("Round 26 should schedule combined-arms operation"); return; }
                if (!CombinedArmsDirector.HasOperationForRound(34)) { Fail("Round 34 should schedule combined-arms response during convoy warfare"); return; }
                if (CombinedArmsDirector.HasOperationForRound(50)) { Fail("Boss round must not schedule combined-arms operation"); return; }
                if (!CombinedArmsDirector.HasOperationForRound(98)) { Fail("Late campaign should still schedule combined-arms operation"); return; }

                if (CombinedArmsDirector.DoctrineForRound(26) != ReinforcementDoctrine.EscortScreen) { Fail("Round 26 doctrine rotation mismatch"); return; }
                if (CombinedArmsDirector.DoctrineForRound(34) != ReinforcementDoctrine.HunterKiller) { Fail("Round 34 doctrine rotation mismatch"); return; }
                if (CombinedArmsDirector.DoctrineForRound(42) != ReinforcementDoctrine.SiegeRelief) { Fail("Round 42 doctrine rotation mismatch"); return; }

                for (int r = 26; r <= 100; r += 8)
                {
                    if (r % 10 == 0) continue;
                    int hp = CombinedArmsDirector.CommandPostHealthForRound(r);
                    int reward = CombinedArmsDirector.RewardForRound(r);
                    int waves = CombinedArmsDirector.WaveCountForRound(r);
                    int size = CombinedArmsDirector.WaveSizeForRound(r);
                    if (hp < CombinedArmsDirector.CommandPostHealthMin || hp > CombinedArmsDirector.CommandPostHealthMax) { Fail("Command post HP outside bounds"); return; }
                    if (reward < CombinedArmsDirector.RewardMin || reward > CombinedArmsDirector.RewardMax) { Fail("Reward outside bounds"); return; }
                    if (waves < CombinedArmsDirector.ReinforcementWavesMin || waves > CombinedArmsDirector.ReinforcementWavesMax) { Fail("Wave count outside bounds"); return; }
                    if (size < CombinedArmsDirector.ReinforcementWaveSizeMin || size > CombinedArmsDirector.ReinforcementWaveSizeMax) { Fail("Wave size outside bounds"); return; }
                }

                foreach (ReinforcementDoctrine doctrine in Enum.GetValues(typeof(ReinforcementDoctrine)))
                {
                    EnemyKind[] composition = CombinedArmsDirector.CompositionForRound(82, doctrine);
                    if (composition == null || composition.Length < 3 || composition.Length > 4) { Fail("Doctrine composition size invalid: " + doctrine); return; }
                    for (int i = 0; i < composition.Length; i++)
                    {
                        if (composition[i] == EnemyKind.Boss || composition[i] == EnemyKind.Supply) { Fail("Doctrine composition includes reserved class: " + doctrine); return; }
                    }
                }

                var healthProbe = new GameObject("COMBINED_ARMS_HEALTH_AUTHORITY_PROBE");
                Health health = healthProbe.AddComponent<Health>();
                health.Initialize(Team.Enemy, 8);
                if (!health.Damage(3, Team.Player) || health.Current != 5) { Destroy(healthProbe); Fail("Authoritative Health damage path failed"); return; }
                Destroy(healthProbe);

                string report = "v6.3 combined arms smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "round26=" + CombinedArmsDirector.DoctrineForRound(26) +
                    " round34=" + CombinedArmsDirector.DoctrineForRound(34) +
                    " round42=" + CombinedArmsDirector.DoctrineForRound(42) +
                    " hp98=" + CombinedArmsDirector.CommandPostHealthForRound(98) +
                    " waves98=" + CombinedArmsDirector.WaveCountForRound(98) +
                    " size98=" + CombinedArmsDirector.WaveSizeForRound(98) +
                    " reward98=" + CombinedArmsDirector.RewardForRound(98) +
                    " maxLive=" + CombinedArmsDirector.MaxLiveEnemyPressure +
                    " supportCharges=" + CombinedArmsDirector.MaxSupportCharges + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBINED_ARMS_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "COMBINED_ARMS_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.3 combined arms smoke FAIL: " + reason);
            Application.Quit(63);
        }
    }
}
