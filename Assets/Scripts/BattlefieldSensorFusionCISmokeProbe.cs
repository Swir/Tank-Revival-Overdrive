using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v13.6 battlefield sensor fusion and contact warfare.</summary>
    public sealed class BattlefieldSensorFusionCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_6_SENSOR_FUSION_OK.txt";
        public const string FailMarker = "V13_6_SENSOR_FUSION_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v136-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<BattlefieldSensorFusionCISmokeProbe>() != null) return;
                GameObject go = new GameObject("BattlefieldSensorFusionCISmokeProbe_v13_6");
                DontDestroyOnLoad(go);
                go.AddComponent<BattlefieldSensorFusionCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!BattlefieldSensorFusionPlannerV136.ConfigurationValid) { Fail("sensor planner configuration invalid"); return; }
                if (!BattlefieldWeatherPlannerV135.ConfigurationValid) { Fail("v13.5 weather dependency invalid"); return; }
                if (!ReconElectronicWarfareDirector.ConfigurationValid) { Fail("v12.3 Recon/EW dependency invalid"); return; }

                BattlefieldSensorFusionDirector runtime = BattlefieldSensorFusionDirector.EnsureInstalled();
                if (runtime == null || BattlefieldSensorFusionDirector.Instance == null) { Fail("sensor runtime director not installed"); return; }

                if (BattlefieldSensorFusionPlannerV136.MaxTrackedContacts != 24 ||
                    BattlefieldSensorFusionPlannerV136.MaxWorldMarkers > 8 ||
                    BattlefieldSensorFusionPlannerV136.SweepCooldownSeconds < 10f ||
                    BattlefieldSensorFusionPlannerV136.SweepDurationSeconds > 3.5f ||
                    BattlefieldSensorFusionPlannerV136.SweepRadius > 13f)
                { Fail("contact/sweep caps outside contract"); return; }

                int plans = 0;
                int profileCoverage = 0;
                int verifiedSamples = 0;
                int trackedSamples = 0;
                int detectedSamples = 0;
                int unknownSamples = 0;
                int previousSignature = 0;
                bool havePrevious = false;

                for (int round = 1; round <= BattlefieldSensorFusionPlannerV136.PlannedRounds; round++)
                {
                    BattlefieldWeatherPlanV135 weather = BattlefieldWeatherPlannerV135.PlanForRound(round, round * 7919 + 134);
                    profileCoverage |= 1 << (int)weather.Kind;

                    for (int k = 0; k < 8; k++)
                    {
                        EnemyKind kind = (EnemyKind)k;
                        float distance = 2.0f + ((round + k * 3) % 16);
                        TacticalTerrainKind terrain = (TacticalTerrainKind)((round + k) % 5);
                        float recon = 0.20f + ((round + k) % 7) * 0.11f;
                        bool reconActive = ((round + k) & 1) == 0;
                        bool sweepActive = (round % 11 == 0) && distance <= BattlefieldSensorFusionPlannerV136.SweepRadius;

                        SensorContactSampleV136 a = BattlefieldSensorFusionPlannerV136.Sample(
                            round, kind, k, distance, weather.VisibilityScale, terrain, recon, reconActive, sweepActive);
                        SensorContactSampleV136 b = BattlefieldSensorFusionPlannerV136.Sample(
                            round, kind, k, distance, weather.VisibilityScale, terrain, recon, reconActive, sweepActive);

                        if (a.Signature != b.Signature || Mathf.Abs(a.Confidence - b.Confidence) > 0.00001f || a.State != b.State)
                        { Fail("sensor sample is not deterministic"); return; }
                        if (a.Confidence < 0f || a.Confidence > 1f)
                        { Fail("sensor confidence escaped [0,1]"); return; }
                        if (a.WeatherVisibility < BattlefieldSensorFusionPlannerV136.MinWeatherVisibility ||
                            a.TerrainVisibility < BattlefieldSensorFusionPlannerV136.MinTerrainVisibility ||
                            a.ReconQuality < BattlefieldSensorFusionPlannerV136.MinReconQuality)
                        { Fail("sensor fairness floor violated"); return; }
                        if (havePrevious && a.Signature == previousSignature && round > 1)
                        { Fail("adjacent deterministic signatures unexpectedly repeated"); return; }
                        previousSignature = a.Signature;
                        havePrevious = true;

                        switch (a.State)
                        {
                            case SensorContactStateV136.Verified: verifiedSamples++; break;
                            case SensorContactStateV136.Tracked: trackedSamples++; break;
                            case SensorContactStateV136.Detected: detectedSamples++; break;
                            default: unknownSamples++; break;
                        }
                        plans++;
                    }
                }

                int allWeatherMask = (1 << 5) - 1;
                if (plans != 800 || profileCoverage != allWeatherMask)
                { Fail("100-round sensor/weather coverage incomplete"); return; }

                float near = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Basic, 2.0f, 1f, TacticalTerrainKind.Clear, 0.8f, true, false);
                float mid = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Basic, 8.0f, 1f, TacticalTerrainKind.Clear, 0.8f, true, false);
                float far = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Basic, 16.0f, 1f, TacticalTerrainKind.Clear, 0.8f, true, false);
                if (!(near > mid && mid > far))
                { Fail("distance confidence is not monotonic"); return; }

                float clear = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Sniper, 9f, 1f, TacticalTerrainKind.Clear, 0.5f, true, false);
                float storm = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Sniper, 9f, BattlefieldWeatherPlannerV135.MinVisibilityScale, TacticalTerrainKind.Clear, 0.5f, true, false);
                float crater = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Sniper, 9f, 1f, TacticalTerrainKind.Crater, 0.5f, true, false);
                if (!(clear > storm && clear > crater))
                { Fail("weather/terrain visibility coupling is not monotonic"); return; }

                float lowRecon = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, 8f, 0.82f, TacticalTerrainKind.Rubble, 0.18f, true, false);
                float highRecon = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, 8f, 0.82f, TacticalTerrainKind.Rubble, 1f, true, false);
                if (!(highRecon > lowRecon))
                { Fail("Recon/EW quality does not improve contact confidence"); return; }

                float noSweep = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, 8f, 0.70f, TacticalTerrainKind.Rubble, 0.3f, true, false);
                float sweepConfidence = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, 8f, 0.70f, TacticalTerrainKind.Rubble, 0.3f, true, true);
                float outOfRangeSweep = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, BattlefieldSensorFusionPlannerV136.SweepRadius + 0.5f, 0.70f, TacticalTerrainKind.Rubble, 0.3f, true, true);
                float outOfRangeBase = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Fast, BattlefieldSensorFusionPlannerV136.SweepRadius + 0.5f, 0.70f, TacticalTerrainKind.Rubble, 0.3f, true, false);
                if (!(sweepConfidence > noSweep) || Mathf.Abs(outOfRangeSweep - outOfRangeBase) > 0.00001f)
                { Fail("active sweep range/boost contract invalid"); return; }

                float closeFloor = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Sniper, 1.0f, BattlefieldWeatherPlannerV135.MinVisibilityScale, TacticalTerrainKind.Crater, 0.18f, true, false);
                if (closeFloor < BattlefieldSensorFusionPlannerV136.TrackingThreshold)
                { Fail("close-range anti-blindness floor invalid"); return; }

                float bossFloor = BattlefieldSensorFusionPlannerV136.Confidence(
                    EnemyKind.Boss, 18f, BattlefieldWeatherPlannerV135.MinVisibilityScale, TacticalTerrainKind.Crater, 0.18f, true, false);
                if (bossFloor < BattlefieldSensorFusionPlannerV136.DetectionThreshold)
                { Fail("boss detection floor invalid"); return; }

                if (BattlefieldSensorFusionPlannerV136.StateForConfidence(BattlefieldSensorFusionPlannerV136.DetectionThreshold - 0.01f) != SensorContactStateV136.Unknown ||
                    BattlefieldSensorFusionPlannerV136.StateForConfidence(BattlefieldSensorFusionPlannerV136.DetectionThreshold + 0.01f) != SensorContactStateV136.Detected ||
                    BattlefieldSensorFusionPlannerV136.StateForConfidence(BattlefieldSensorFusionPlannerV136.TrackingThreshold + 0.01f) != SensorContactStateV136.Tracked ||
                    BattlefieldSensorFusionPlannerV136.StateForConfidence(BattlefieldSensorFusionPlannerV136.VerificationThreshold + 0.01f) != SensorContactStateV136.Verified)
                { Fail("contact threshold mapping invalid"); return; }

                string report =
                    "v13.6 battlefield sensor fusion smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "samples=" + plans + " weatherMask=" + profileCoverage +
                    " unknown=" + unknownSamples + " detected=" + detectedSamples +
                    " tracked=" + trackedSamples + " verified=" + verifiedSamples + "\n" +
                    "distanceNear=" + near.ToString("0.000") + " mid=" + mid.ToString("0.000") + " far=" + far.ToString("0.000") + "\n" +
                    "weatherClear=" + clear.ToString("0.000") + " storm=" + storm.ToString("0.000") +
                    " reconLow=" + lowRecon.ToString("0.000") + " reconHigh=" + highRecon.ToString("0.000") + "\n" +
                    "sweepBase=" + noSweep.ToString("0.000") + " sweepBoosted=" + sweepConfidence.ToString("0.000") +
                    " closeFloor=" + closeFloor.ToString("0.000") + " bossFloor=" + bossFloor.ToString("0.000") + "\n";
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v13.6 sensor fusion smoke FAIL: " + reason);
            Application.Quit(76);
        }
    }
}
