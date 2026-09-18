using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for v13.5 deterministic weather, visibility and fairness contracts.</summary>
    public sealed class BattlefieldWeatherCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_5_BATTLEFIELD_WEATHER_OK.txt";
        public const string FailMarker = "V13_5_BATTLEFIELD_WEATHER_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v135-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<BattlefieldWeatherCISmokeProbe>() != null) return;
                GameObject go = new GameObject("BattlefieldWeatherCISmokeProbe_v13_5");
                DontDestroyOnLoad(go);
                go.AddComponent<BattlefieldWeatherCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!BattlefieldWeatherPlannerV135.ConfigurationValid || !BattlefieldWeatherDirector.ConfigurationValid)
                { Fail("v13.5 battlefield weather configuration invalid"); return; }

                BattlefieldWeatherDirector runtime = BattlefieldWeatherDirector.EnsureInstalled();
                if (runtime == null || BattlefieldWeatherDirector.Instance == null)
                { Fail("battlefield weather runtime director was not installed"); return; }

                if (BattlefieldWeatherPlannerV135.PlannedRounds != 100 ||
                    BattlefieldWeatherPlannerV135.ProfileCount != 5 ||
                    BattlefieldWeatherPlannerV135.MaxPresentationStreaks > 24)
                { Fail("hard weather budgets regressed"); return; }

                int[] coverage = new int[BattlefieldWeatherPlannerV135.ProfileCount];
                int signatureFold = 135;
                BattlefieldWeatherKindV135 previous = (BattlefieldWeatherKindV135)(-1);
                int severeRound = -1;
                int severeTerrainSignature = 0;

                for (int round = 1; round <= 100; round++)
                {
                    int terrainSignature = unchecked(round * 7919 + 134);
                    BattlefieldWeatherPlanV135 a = BattlefieldWeatherPlannerV135.PlanForRound(round, terrainSignature);
                    BattlefieldWeatherPlanV135 b = BattlefieldWeatherPlannerV135.PlanForRound(round, terrainSignature);

                    if (a.Round != round || a.Signature != b.Signature || a.Kind != b.Kind ||
                        Mathf.Abs(a.VisibilityScale - b.VisibilityScale) > 0.0001f ||
                        Mathf.Abs(a.TractionScale - b.TractionScale) > 0.0001f ||
                        Mathf.Abs(a.PlayerSpreadScale - b.PlayerSpreadScale) > 0.0001f ||
                        Mathf.Abs(a.EnemySpreadScale - b.EnemySpreadScale) > 0.0001f ||
                        Mathf.Abs(a.EnemyReloadScale - b.EnemyReloadScale) > 0.0001f ||
                        a.StreakBudget != b.StreakBudget)
                    { Fail("weather determinism mismatch round=" + round); return; }

                    int index = (int)a.Kind;
                    if (index < 0 || index >= coverage.Length)
                    { Fail("invalid weather kind round=" + round); return; }
                    coverage[index]++;

                    if (round > 1 && a.Kind == previous)
                    { Fail("adjacent weather profile repeated round=" + round); return; }
                    previous = a.Kind;

                    if (a.VisibilityScale < BattlefieldWeatherPlannerV135.MinVisibilityScale || a.VisibilityScale > 1f ||
                        a.TractionScale < BattlefieldWeatherPlannerV135.MinTractionScale || a.TractionScale > 1f ||
                        a.PlayerSpreadScale < 1f || a.PlayerSpreadScale > BattlefieldWeatherPlannerV135.MaxPlayerSpreadScale ||
                        a.EnemySpreadScale < 1f || a.EnemySpreadScale > BattlefieldWeatherPlannerV135.MaxEnemySpreadScale ||
                        a.EnemyReloadScale < 1f || a.EnemyReloadScale > BattlefieldWeatherPlannerV135.MaxEnemyReloadScale ||
                        a.StreakBudget < 0 || a.StreakBudget > BattlefieldWeatherPlannerV135.MaxPresentationStreaks)
                    { Fail("weather fairness/presentation bounds escaped round=" + round); return; }

                    if (a.Kind == BattlefieldWeatherKindV135.Storm && severeRound < 0)
                    {
                        severeRound = round;
                        severeTerrainSignature = terrainSignature;
                    }

                    signatureFold = unchecked(signatureFold * 31 + a.Signature);
                }

                for (int i = 0; i < coverage.Length; i++)
                    if (coverage[i] < 18)
                    { Fail("weather profile coverage too low index=" + i + " count=" + coverage[i]); return; }

                if (severeRound < 0)
                { Fail("storm profile missing from 100-round planner"); return; }

                runtime.ApplyDeterministicPlan(severeRound, severeTerrainSignature);
                BattlefieldWeatherPlanV135 severe = runtime.CurrentPlan;
                if (severe.Kind != BattlefieldWeatherKindV135.Storm ||
                    severe.VisibilityScale >= 0.80f || severe.TractionScale >= 0.92f ||
                    severe.EnemySpreadScale <= severe.PlayerSpreadScale)
                { Fail("storm profile lost material tactical differentiation"); return; }

                float basicSpread = BattlefieldWeatherDirector.PlayerSpreadScale(AmmoType.Basic);
                float apSpread = BattlefieldWeatherDirector.PlayerSpreadScale(AmmoType.ArmorPiercing);
                float plasmaSpread = BattlefieldWeatherDirector.PlayerSpreadScale(AmmoType.Plasma);
                if (apSpread > basicSpread || plasmaSpread > basicSpread || apSpread < 1f || plasmaSpread < 1f)
                { Fail("precision-ammo weather counterplay regressed"); return; }

                float playerMobility = BattlefieldWeatherDirector.MobilityScale(Team.Player, Vector2.zero);
                float enemyMobility = BattlefieldWeatherDirector.MobilityScale(Team.Enemy, Vector2.zero);
                float sniperSpread = BattlefieldWeatherDirector.EnemySpreadScale(EnemyKind.Sniper);
                float bossSpread = BattlefieldWeatherDirector.EnemySpreadScale(EnemyKind.Boss);
                float enemyReload = BattlefieldWeatherDirector.EnemyReloadScale(EnemyKind.Heavy);
                if (playerMobility < 0.76f || playerMobility > 1.02f ||
                    enemyMobility < 0.76f || enemyMobility > 1.02f ||
                    sniperSpread < 1f || sniperSpread > BattlefieldWeatherPlannerV135.MaxEnemySpreadScale ||
                    bossSpread < 1f || bossSpread > BattlefieldWeatherPlannerV135.MaxEnemySpreadScale ||
                    enemyReload < 1f || enemyReload > BattlefieldWeatherPlannerV135.MaxEnemyReloadScale)
                { Fail("runtime weather multipliers escaped hard bounds"); return; }

                runtime.ApplyDeterministicPlan(65, unchecked(65 * 7919 + 134));
                if (runtime.CurrentPlan.Round != 65 || string.IsNullOrEmpty(BattlefieldWeatherDirector.CurrentTelemetry))
                { Fail("runtime weather plan/telemetry did not update"); return; }

                string report =
                    "v13.5 battlefield weather + visibility smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "rounds=100 profiles=" + BattlefieldWeatherPlannerV135.ProfileCount +
                    " maxStreaks=" + BattlefieldWeatherPlannerV135.MaxPresentationStreaks +
                    " signatureFold=" + signatureFold + "\n" +
                    "stormRound=" + severeRound +
                    " playerMobility=" + playerMobility.ToString("0.000") +
                    " enemyMobility=" + enemyMobility.ToString("0.000") +
                    " basicSpread=" + basicSpread.ToString("0.000") +
                    " apSpread=" + apSpread.ToString("0.000") +
                    " sniperSpread=" + sniperSpread.ToString("0.000") + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); }
            catch (Exception) { }
            Debug.LogError("[TankRevival] v13.5 battlefield weather smoke FAIL: " + reason);
            Application.Quit(83);
        }
    }
}
