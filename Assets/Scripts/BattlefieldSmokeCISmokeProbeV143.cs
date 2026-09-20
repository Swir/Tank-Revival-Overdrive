using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20130)]
    public sealed class BattlefieldSmokeCISmokeProbeV143 : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v143-smoke")) return;
            if (FindAnyObjectByType<BattlefieldSmokeCISmokeProbeV143>() != null) return;
            GameObject go = new GameObject("BattlefieldSmokeCISmokeProbeV143");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldSmokeCISmokeProbeV143>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(
                    true,
                    "bounds=PASS weather=PASS tradeoff=PASS break-contact=PASS presentation=PASS authority=PASS installation=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(43);
            }
        }

        private static void RunContracts()
        {
            Require(BattlefieldSmokeScreenModelV143.ConfigurationValid, "smoke configuration");
            Require(BattlefieldSmokePresentationV143.ConfigurationValid, "presentation configuration");
            Require(BattlefieldSmokeScreenModelV143.PlannedRounds == 100, "100 round contract");
            Require(BattlefieldSmokeScreenModelV143.MaxActiveSmokeZones == 1, "single active smoke zone");
            Require(BattlefieldSmokeScreenModelV143.MaxSmokeChargesPerRound == 2, "finite smoke charges");

            float storm = BattlefieldSmokeScreenModelV143.WeatherPersistenceScale(BattlefieldWeatherKindV135.Storm);
            float rain = BattlefieldSmokeScreenModelV143.WeatherPersistenceScale(BattlefieldWeatherKindV135.Rain);
            float clear = BattlefieldSmokeScreenModelV143.WeatherPersistenceScale(BattlefieldWeatherKindV135.Clear);
            float mist = BattlefieldSmokeScreenModelV143.WeatherPersistenceScale(BattlefieldWeatherKindV135.Mist);
            float snow = BattlefieldSmokeScreenModelV143.WeatherPersistenceScale(BattlefieldWeatherKindV135.Snow);
            Require(storm < rain && rain < clear && clear < mist && mist <= snow, "weather dispersion ordering");
            Require(storm >= BattlefieldSmokeScreenModelV143.MinWeatherPersistenceScale, "weather minimum");
            Require(snow <= BattlefieldSmokeScreenModelV143.MaxWeatherPersistenceScale, "weather maximum");

            float weak = BattlefieldSmokeScreenModelV143.MinContextStrength01;
            float strong = BattlefieldSmokeScreenModelV143.MaxContextStrength01;
            float weakExposure = BattlefieldSmokeScreenModelV143.ExposureScale(weak);
            float strongExposure = BattlefieldSmokeScreenModelV143.ExposureScale(strong);
            float weakAcquisition = BattlefieldSmokeScreenModelV143.AcquisitionScale(weak);
            float strongAcquisition = BattlefieldSmokeScreenModelV143.AcquisitionScale(strong);
            float weakSensors = BattlefieldSmokeScreenModelV143.SensorThroughputScale(weak);
            float strongSensors = BattlefieldSmokeScreenModelV143.SensorThroughputScale(strong);

            Require(strongExposure <= weakExposure && strongExposure >= BattlefieldSmokeScreenModelV143.MinCounterBatteryExposureScale,
                "counter-battery exposure tradeoff bounds");
            Require(strongAcquisition <= weakAcquisition && strongAcquisition >= BattlefieldSmokeScreenModelV143.MinObserverAcquisitionScale,
                "observer acquisition tradeoff bounds");
            Require(strongSensors <= weakSensors && strongSensors >= BattlefieldSmokeScreenModelV143.MinPlayerSensorThroughputScale,
                "player sensor throughput retains non-zero floor");

            float weakBreak = BattlefieldSmokeScreenModelV143.BreakContactScale(weak);
            float strongBreak = BattlefieldSmokeScreenModelV143.BreakContactScale(strong);
            Require(strongBreak <= weakBreak, "strong smoke improves break-contact distance");
            Require(strongBreak >= BattlefieldSmokeScreenModelV143.MinBreakContactDistanceScale &&
                    weakBreak <= BattlefieldSmokeScreenModelV143.MaxBreakContactDistanceScale,
                    "break-contact hard bounds");

            float lowResilience = BattlefieldSmokeScreenModelV143.ContextStrength01(default, 0f);
            float highResilience = BattlefieldSmokeScreenModelV143.ContextStrength01(default, 1f);
            Require(lowResilience >= highResilience, "observer resilience cannot strengthen smoke");

            Require(BattlefieldSmokePresentationV143.MaxPresentationCues == 2, "fixed presentation cap");
            Require(BattlefieldSmokeScreenDirectorV143.EnsureInstalled() != null, "smoke director installation");
            Require(BattlefieldSmokePresentationV143.EnsureInstalled() != null, "smoke presentation installation");
            Require(CounterBatteryDirectorV140.EnsureInstalled() != null, "counter-battery installation");
            Require(CounterObservationDirectorV141.EnsureInstalled() != null, "counter-observation installation");
            Require(CounterReconDeceptionDirectorV142.EnsureInstalled() != null, "deception installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v14.3 contract failed: " + contract);
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string details)
        {
            string file = pass ? "V14_3_SMOKE_OK.txt" : "V14_3_SMOKE_FAIL.txt";
            string text =
                "Tank Revival: Orzel Overdrive\nSmoke / break-contact warfare v14.3: " + (pass ? "PASS" : "FAIL") +
                "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion +
                "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[BattlefieldSmokeCISmokeProbeV143] " + text.Replace("\n", " | "));
        }
    }
}
