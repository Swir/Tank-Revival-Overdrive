using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class CareerRecordsCISmokeProbe : MonoBehaviour
    {
        private const string Flag = "-career-records-smoke";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool enabled = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                {
                    enabled = true;
                    break;
                }
            }
            if (!enabled) return;

            GameObject go = new GameObject("CareerRecordsCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CareerRecordsCISmokeProbe>();
        }

        private void Start()
        {
            try
            {
                string reason;
                if (!CareerRecordsDirector.ValidateCatalog(out reason))
                {
                    Fail("catalog validation failed: " + reason);
                    return;
                }

                if (CareerRecordsDirector.AchievementCatalogSize < 10)
                {
                    Fail("achievement catalog unexpectedly small");
                    return;
                }

                if (!CareerRecordsDirector.RunPersistenceSelfTest(out reason))
                {
                    Fail("persistence validation failed: " + reason);
                    return;
                }

                CareerRecordsDirector director = FindAnyObjectByType<CareerRecordsDirector>();
                PlayerProfileDirector profile = FindAnyObjectByType<PlayerProfileDirector>();
                TankGame game = FindAnyObjectByType<TankGame>();
                if (director == null)
                {
                    Fail("CareerRecordsDirector missing from runtime");
                    return;
                }
                if (profile == null)
                {
                    Fail("PlayerProfileDirector missing from runtime");
                    return;
                }
                if (game == null)
                {
                    Fail("TankGame missing from runtime");
                    return;
                }

                CareerRecordData data = director.Data;
                if (data == null || data.schemaVersion < 1 || data.highestRoundObserved < 1)
                {
                    Fail("career data invalid after runtime load");
                    return;
                }

                string report =
                    "v5.4 career records smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "catalog=" + CareerRecordsDirector.AchievementCatalogSize + "\n" +
                    "profileFurthest=" + profile.FurthestRound + "\n" +
                    "careerHighest=" + data.highestRoundObserved + "\n" +
                    "persistence=PASS\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CAREER_RECORDS_PASS.txt"), report);
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
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CAREER_RECORDS_FAIL.txt"), reason);
            }
            catch (Exception) { }
            Debug.LogError("[TankRevival] v5.4 career records smoke FAIL: " + reason);
            Application.Quit(31);
        }
    }
}
