using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20100)]
    public sealed class CounterBatteryCISmokeProbeV140 : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v140-smoke")) return;
            if (FindAnyObjectByType<CounterBatteryCISmokeProbeV140>() != null) return;
            GameObject go = new GameObject("CounterBatteryCISmokeProbeV140");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterBatteryCISmokeProbeV140>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true,
                    "profiles=PASS exposure=PASS terrain=PASS sensor=PASS observers=PASS intent=PASS caps=PASS authority=PASS installation=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(40);
            }
        }

        private static void RunContracts()
        {
            Require(CounterBatteryModelV140.ConfigurationValid, "configuration");
            Require(CounterBatteryModelV140.PlannedRounds == 100, "100 rounds");
            Require(CounterBatteryModelV140.MaxTrackedHostiles == 24, "hostile cap");
            Require(CounterBatteryModelV140.MaxObservers == 6, "observer cap");
            Require(CounterBatteryModelV140.MaxShells == 4, "shell cap");

            int previousSignature = int.MinValue;
            int previousShells = 0;
            float previousThreshold = float.MaxValue;
            for (int round = 1; round <= 100; round++)
            {
                CounterBatteryProfileV140 profile = CounterBatteryModelV140.ProfileForRound(round);
                Require(profile.Round == round, "profile round " + round);
                Require(profile.ShellBudget >= 2 && profile.ShellBudget <= CounterBatteryModelV140.MaxShells, "shell budget " + round);
                Require(profile.LockThreshold >= 0.65f && profile.LockThreshold <= 0.82f, "lock threshold " + round);
                Require(profile.CooldownSeconds >= CounterBatteryModelV140.MinCooldownSeconds &&
                        profile.CooldownSeconds <= CounterBatteryModelV140.MaxCooldownSeconds, "cooldown " + round);
                Require(profile.AcquisitionScale >= 0.90f && profile.AcquisitionScale <= 1.18f, "acquisition scale " + round);
                Require(profile.Signature != previousSignature, "signature uniqueness " + round);
                Require(profile.ShellBudget >= previousShells, "shell monotonicity " + round);
                Require(profile.LockThreshold <= previousThreshold + 0.0001f, "threshold monotonicity " + round);
                previousSignature = profile.Signature;
                previousShells = profile.ShellBudget;
                previousThreshold = profile.LockThreshold;
            }

            float repeated = CounterBatteryModelV140.AddSupportSignature(0.20f, 0.25f);
            float relocated = CounterBatteryModelV140.AddSupportSignature(0.20f, CounterBatteryModelV140.BreakDistance + 0.25f);
            Require(repeated > relocated && relocated > 0.20f, "repeat-use signature pressure");
            float decayed = CounterBatteryModelV140.DecayExposure(repeated, 2f);
            Require(decayed < repeated && decayed >= 0f, "exposure decay");

            float crater = CounterBatteryModelV140.TerrainExposureScale(TacticalTerrainKind.Crater);
            float rubble = CounterBatteryModelV140.TerrainExposureScale(TacticalTerrainKind.Rubble);
            float open = CounterBatteryModelV140.TerrainExposureScale((TacticalTerrainKind)0);
            Require(crater < rubble && rubble < open, "terrain exposure ordering");

            float unknown = CounterBatteryModelV140.SensorCounterplayScale(SensorContactStateV136.Unknown, false);
            float tracked = CounterBatteryModelV140.SensorCounterplayScale(SensorContactStateV136.Tracked, false);
            float verified = CounterBatteryModelV140.SensorCounterplayScale(SensorContactStateV136.Verified, false);
            float verifiedSweep = CounterBatteryModelV140.SensorCounterplayScale(SensorContactStateV136.Verified, true);
            Require(verifiedSweep < verified && verified < tracked && tracked < unknown, "sensor counterplay ordering");

            float breakOpen = CounterBatteryModelV140.RelocationBreakDistance((TacticalTerrainKind)0, false);
            float breakCrater = CounterBatteryModelV140.RelocationBreakDistance(TacticalTerrainKind.Crater, false);
            float breakSweep = CounterBatteryModelV140.RelocationBreakDistance((TacticalTerrainKind)0, true);
            Require(breakCrater <= breakOpen && breakSweep < breakOpen, "relocation counterplay");
            Require(breakCrater >= CounterBatteryModelV140.MinimumPhysicalBreakDistance, "physical relocation floor");

            Require(CounterBatteryModelV140.ObserverWeight(EnemyKind.Sniper) > CounterBatteryModelV140.ObserverWeight(EnemyKind.Elite), "sniper observer priority");
            Require(CounterBatteryModelV140.ObserverWeight(EnemyKind.Siege) > CounterBatteryModelV140.ObserverWeight(EnemyKind.Supply), "siege observer priority");
            Require(CounterBatteryModelV140.ObserverWeight(EnemyKind.Fast) == 0f, "non-observer exclusion");

            CounterBatteryProfileV140 p80 = CounterBatteryModelV140.ProfileForRound(80);
            float noGain = CounterBatteryModelV140.AcquisitionGainPerSecond(
                CounterBatteryModelV140.SearchThreshold * 0.5f, 1f, p80);
            float lowGain = CounterBatteryModelV140.AcquisitionGainPerSecond(0.60f, 0.30f, p80);
            float highGain = CounterBatteryModelV140.AcquisitionGainPerSecond(0.90f, 1f, p80);
            Require(noGain == 0f && lowGain > 0f && highGain > lowGain, "acquisition pressure bounds");

            CounterBatteryStrikeIntentV140 intent = CounterBatteryModelV140.BuildIntent(new Vector2(2f, 1f), 2, p80);
            Require(intent.Ammo == AmmoType.Explosive, "explosive barrage ammo");
            Require(intent.Damage == 1, "bounded barrage damage");
            Require(intent.Speed > 0f && intent.Speed <= 12f, "bounded projectile speed");
            Require(intent.Direction.sqrMagnitude > 0.99f && intent.Direction.sqrMagnitude < 1.01f, "normalized direction");
            Require(intent.StrikeOrdinal == 2 && intent.RoundSignature == p80.Signature, "intent attribution");

            Require(CounterBatteryDirectorV140.EnsureInstalled() != null, "director installation");
            Require(CounterBatteryExecutionBridgeV140.EnsureInstalled() != null, "execution bridge installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v14.0 contract failed: " + contract);
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
            string file = pass ? "V14_0_COUNTER_BATTERY_OK.txt" : "V14_0_COUNTER_BATTERY_FAIL.txt";
            string text =
                "Tank Revival: Orzel Overdrive\nCounter-battery warfare v14.0: " + (pass ? "PASS" : "FAIL") +
                "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion +
                "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[CounterBatteryCISmokeProbeV140] " + text.Replace("\n", " | "));
        }
    }
}
