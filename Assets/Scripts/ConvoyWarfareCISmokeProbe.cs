using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    public sealed class ConvoyWarfareCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-convoy-warfare-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                var go = new GameObject("ConvoyWarfareCISmokeProbe");
                DontDestroyOnLoad(go);
                go.AddComponent<ConvoyWarfareCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (FindAnyObjectByType<ConvoyWarfareDirector>() == null) { Fail("ConvoyWarfareDirector missing"); return; }
                if (!ConvoyWarfareDirector.ConfigurationValid) { Fail("Convoy configuration outside bounded values"); return; }
                if (ConvoyWarfareDirector.MissionCount != 3) { Fail("Convoy mission catalog must contain exactly 3 entries"); return; }

                if (!ConvoyWarfareDirector.HasMissionForRound(22)) { Fail("Round 22 should schedule convoy warfare"); return; }
                if (!ConvoyWarfareDirector.HasMissionForRound(28)) { Fail("Round 28 should schedule convoy warfare"); return; }
                if (ConvoyWarfareDirector.HasMissionForRound(40)) { Fail("Boss round must not schedule convoy warfare"); return; }
                if (ConvoyWarfareDirector.HasMissionForRound(64)) { Fail("v6.1 multi-stage round must not overlap convoy warfare"); return; }
                if (!ConvoyWarfareDirector.HasMissionForRound(94)) { Fail("Late campaign should still schedule convoy warfare"); return; }

                if (ConvoyWarfareDirector.MissionForRound(22) != ConvoyMissionKind.EagleEscort) { Fail("Round 22 mission rotation mismatch"); return; }
                if (ConvoyWarfareDirector.MissionForRound(28) != ConvoyMissionKind.EnemyInterdiction) { Fail("Round 28 mission rotation mismatch"); return; }
                if (ConvoyWarfareDirector.MissionForRound(34) != ConvoyMissionKind.MobileResupply) { Fail("Round 34 mission rotation mismatch"); return; }

                int reward22 = ConvoyWarfareDirector.RewardForRound(22);
                int reward100 = ConvoyWarfareDirector.RewardForRound(100);
                if (reward22 < ConvoyWarfareDirector.RewardMin || reward100 > ConvoyWarfareDirector.RewardMax || reward100 < reward22) { Fail("Reward curve outside bounds"); return; }

                float route22 = ConvoyWarfareDirector.RouteSecondsForRound(22);
                float route100 = ConvoyWarfareDirector.RouteSecondsForRound(100);
                if (route22 < ConvoyWarfareDirector.RouteSecondsMin - 0.01f || route100 > ConvoyWarfareDirector.RouteSecondsMax + 0.01f || route100 <= route22) { Fail("Route duration curve outside bounds"); return; }

                int friendly = ConvoyWarfareDirector.ConvoyHealthForRound(100, ConvoyMissionKind.EagleEscort);
                int enemy = ConvoyWarfareDirector.ConvoyHealthForRound(100, ConvoyMissionKind.EnemyInterdiction);
                if (friendly < ConvoyWarfareDirector.FriendlyHealthMin || friendly > ConvoyWarfareDirector.FriendlyHealthMax) { Fail("Friendly convoy HP outside bounds"); return; }
                if (enemy < ConvoyWarfareDirector.EnemyHealthMin || enemy > ConvoyWarfareDirector.EnemyHealthMax) { Fail("Enemy convoy HP outside bounds"); return; }

                var healthProbe = new GameObject("CONVOY_HEALTH_AUTHORITY_PROBE");
                Health health = healthProbe.AddComponent<Health>();
                health.Initialize(Team.Player, 9);
                if (!health.Damage(2, Team.Enemy) || health.Current != 7) { Destroy(healthProbe); Fail("Authoritative Health damage path failed"); return; }
                Destroy(healthProbe);

                string report = "v6.2 convoy warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "missions=" + ConvoyWarfareDirector.MissionCount +
                    " reward22=" + reward22 +
                    " reward100=" + reward100 +
                    " route22=" + route22.ToString("0.0") +
                    " route100=" + route100.ToString("0.0") +
                    " friendlyHP100=" + friendly +
                    " enemyHP100=" + enemy + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CONVOY_WARFARE_PASS.txt"), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "CONVOY_WARFARE_FAIL.txt"), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v6.2 convoy warfare smoke FAIL: " + reason);
            Application.Quit(62);
        }
    }
}
