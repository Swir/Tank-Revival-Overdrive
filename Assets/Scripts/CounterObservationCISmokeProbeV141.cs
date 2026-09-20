using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20110)]
    public sealed class CounterObservationCISmokeProbeV141 : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v141-smoke")) return;
            if (FindAnyObjectByType<CounterObservationCISmokeProbeV141>() != null) return;
            GameObject go = new GameObject("CounterObservationCISmokeProbeV141");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterObservationCISmokeProbeV141>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true, "profiles=PASS evidence=PASS ranking=PASS disruption=PASS caps=PASS authority=PASS installation=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(41);
            }
        }

        private static void RunContracts()
        {
            Require(CounterObservationModelV141.ConfigurationValid, "configuration");
            Require(CounterObservationModelV141.PlannedRounds == 100, "100 rounds");
            Require(CounterObservationModelV141.MaxTrackedHostiles == 24, "hostile cap");
            Require(CounterObservationModelV141.MaxObserverCandidates == 6, "observer cap");

            int previousSignature = int.MinValue;
            float previousHold = 0f;
            float previousScale = 0f;
            for (int round = 1; round <= 100; round++)
            {
                CounterObservationProfileV141 profile = CounterObservationModelV141.ProfileForRound(round);
                Require(profile.Round == round, "profile round " + round);
                Require(profile.DesignationHoldSeconds >= CounterObservationModelV141.MinDesignationHoldSeconds &&
                        profile.DesignationHoldSeconds <= CounterObservationModelV141.MaxDesignationHoldSeconds, "designation hold " + round);
                Require(profile.DesignationLeaseSeconds >= CounterObservationModelV141.MinDesignationLeaseSeconds &&
                        profile.DesignationLeaseSeconds <= CounterObservationModelV141.MaxDesignationLeaseSeconds, "designation lease " + round);
                Require(profile.NetworkBreakSeconds >= CounterObservationModelV141.MinNetworkBreakSeconds &&
                        profile.NetworkBreakSeconds <= CounterObservationModelV141.MaxNetworkBreakSeconds, "network break " + round);
                Require(profile.CooldownSeconds >= CounterObservationModelV141.MinCooldownSeconds &&
                        profile.CooldownSeconds <= CounterObservationModelV141.MaxCooldownSeconds, "cooldown " + round);
                Require(profile.AcquisitionScale >= CounterObservationModelV141.MinAcquisitionScale &&
                        profile.AcquisitionScale <= CounterObservationModelV141.MaxAcquisitionScale, "acquisition scale " + round);
                Require(profile.Signature != previousSignature, "signature uniqueness " + round);
                Require(profile.DesignationHoldSeconds + 0.0001f >= previousHold, "late-round designation resilience " + round);
                Require(profile.AcquisitionScale + 0.0001f >= previousScale, "late-round disruption fairness " + round);
                previousSignature = profile.Signature;
                previousHold = profile.DesignationHoldSeconds;
                previousScale = profile.AcquisitionScale;
            }

            Require(CounterObservationModelV141.IsObserverKind(EnemyKind.Sniper), "sniper observer eligibility");
            Require(CounterObservationModelV141.IsObserverKind(EnemyKind.Siege), "siege observer eligibility");
            Require(CounterObservationModelV141.IsObserverKind(EnemyKind.Elite), "elite observer eligibility");
            Require(CounterObservationModelV141.IsObserverKind(EnemyKind.Supply), "supply observer eligibility");
            Require(!CounterObservationModelV141.IsObserverKind(EnemyKind.Fast), "fast observer exclusion");
            Require(!CounterObservationModelV141.IsObserverKind(EnemyKind.Boss), "boss observer exclusion");

            Require(!CounterObservationModelV141.HasDesignationEvidence(SensorContactStateV136.Unknown, true), "unknown cannot designate");
            Require(!CounterObservationModelV141.HasDesignationEvidence(SensorContactStateV136.Detected, true), "detected cannot designate");
            Require(!CounterObservationModelV141.HasDesignationEvidence(SensorContactStateV136.Tracked, false), "tracked requires finite sweep");
            Require(CounterObservationModelV141.HasDesignationEvidence(SensorContactStateV136.Tracked, true), "tracked sweep designation");
            Require(CounterObservationModelV141.HasDesignationEvidence(SensorContactStateV136.Verified, false), "verified designation");

            float strong = CounterObservationModelV141.CandidateScore(EnemyKind.Sniper, 0.92f, 4f);
            float weak = CounterObservationModelV141.CandidateScore(EnemyKind.Supply, 0.74f, 10f);
            Require(strong > weak && weak > 0f && strong <= 1f, "observer ranking bounds");

            CounterObservationProfileV141 early = CounterObservationModelV141.ProfileForRound(1);
            CounterObservationProfileV141 late = CounterObservationModelV141.ProfileForRound(100);
            Require(early.AcquisitionScale < 1f && late.AcquisitionScale < 1f, "finite acquisition suppression");
            Require(early.AcquisitionScale >= 0.40f && late.AcquisitionScale >= 0.40f, "no counter-battery immunity");
            Require(early.NetworkBreakSeconds <= 8f && late.NetworkBreakSeconds >= 5f, "bounded disruption duration");

            Require(CounterObservationDirectorV141.EnsureInstalled() != null, "director installation");
            Require(CounterBatteryDirectorV140.EnsureInstalled() != null, "counter-battery installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v14.1 contract failed: " + contract);
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
            string file = pass ? "V14_1_COUNTER_OBSERVATION_OK.txt" : "V14_1_COUNTER_OBSERVATION_FAIL.txt";
            string text =
                "Tank Revival: Orzel Overdrive\nCounter-observation warfare v14.1: " + (pass ? "PASS" : "FAIL") +
                "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion +
                "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[CounterObservationCISmokeProbeV141] " + text.Replace("\n", " | "));
        }
    }
}
