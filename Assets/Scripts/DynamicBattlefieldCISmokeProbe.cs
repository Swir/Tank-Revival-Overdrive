using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class DynamicBattlefieldCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-dynamic-battlefield-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("DynamicBattlefieldCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<DynamicBattlefieldCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<DynamicBattlefieldDirector>() == null) { Fail("DynamicBattlefieldDirector missing"); return; }
                if (!DynamicBattlefieldDirector.ConfigurationValid) { Fail("Dynamic battlefield configuration outside bounded values"); return; }
                if (DynamicBattlefieldDirector.ObjectiveCount != 3) { Fail("Objective catalog must contain exactly 3 objective types"); return; }
                if (DynamicBattlefieldDirector.HazardCount != 2) { Fail("Hazard catalog must contain exactly 2 hazard types"); return; }
                if (!DynamicBattlefieldDirector.HasObjectiveForRound(6)) { Fail("Round 6 should carry a dynamic objective"); return; }
                if (DynamicBattlefieldDirector.HasObjectiveForRound(10)) { Fail("Boss round 10 must not carry a standard dynamic objective"); return; }
                if (!DynamicBattlefieldDirector.HasObjectiveForRound(99)) { Fail("Late campaign should still schedule dynamic objectives"); return; }
                if (DynamicBattlefieldDirector.RewardForRound(1) < 5 || DynamicBattlefieldDirector.RewardForRound(100) > 13) { Fail("War Bond objective rewards outside bounded range"); return; }
                if (DynamicBattlefieldDirector.DemolitionHealthForRound(1) < 4 || DynamicBattlefieldDirector.DemolitionHealthForRound(100) > 10) { Fail("Demolition target endurance outside bounded range"); return; }

                BattlefieldObjectiveKind a = DynamicBattlefieldDirector.ObjectiveForRound(6);
                BattlefieldObjectiveKind b = DynamicBattlefieldDirector.ObjectiveForRound(9);
                BattlefieldObjectiveKind c = DynamicBattlefieldDirector.ObjectiveForRound(12);
                if (a == b || b == c || a == c) { Fail("Representative objective rotation is not varied"); return; }

                string report = "v6.0 dynamic battlefield smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "objectives=" + DynamicBattlefieldDirector.ObjectiveCount +
                    " hazards=" + DynamicBattlefieldDirector.HazardCount +
                    " telegraph=" + DynamicBattlefieldDirector.ArtilleryTelegraphSeconds +
                    " radius=" + DynamicBattlefieldDirector.ArtilleryRadius +
                    " cadence=" + DynamicBattlefieldDirector.HazardCadence +
                    " maxMinefields=" + DynamicBattlefieldDirector.MaxMinefields + "\n" +
                    "rotation=" + a + "," + b + "," + c + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "DYNAMIC_BATTLEFIELD_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "DYNAMIC_BATTLEFIELD_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.0 dynamic battlefield smoke FAIL: " + reason);
            Application.Quit(60);
        }
    }
}
