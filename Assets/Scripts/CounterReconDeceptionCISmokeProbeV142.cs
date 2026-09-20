using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20120)]
    public sealed class CounterReconDeceptionCISmokeProbeV142 : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v142-smoke")) return;
            if (FindAnyObjectByType<CounterReconDeceptionCISmokeProbeV142>() != null) return;
            GameObject go = new GameObject("CounterReconDeceptionCISmokeProbeV142");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterReconDeceptionCISmokeProbeV142>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true, "rounds=PASS decoys=PASS emcon=PASS adaptation=PASS reacquisition=PASS resilience=PASS authority=PASS installation=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(42);
            }
        }

        private static void RunContracts()
        {
            Require(CounterReconDeceptionModelV142.ConfigurationValid, "configuration");
            Require(CounterReconDeceptionModelV142.PlannedRounds == 100, "100 rounds");
            Require(CounterReconDeceptionModelV142.MaxDecoyChargesPerRound == 2, "finite decoy charges");
            Require(CounterReconDeceptionModelV142.DecoyLifetimeSeconds > 0f &&
                    CounterReconDeceptionModelV142.DecoyCooldownSeconds >= CounterReconDeceptionModelV142.DecoyLifetimeSeconds,
                    "finite decoy lifetime/cooldown");
            Require(CounterReconDeceptionModelV142.EmconLifetimeSeconds > 0f &&
                    CounterReconDeceptionModelV142.EmconCooldownSeconds > CounterReconDeceptionModelV142.EmconLifetimeSeconds,
                    "finite EMCON lifetime/cooldown");
            Require(CounterReconDeceptionModelV142.EmconExposureScale > 0f &&
                    CounterReconDeceptionModelV142.EmconAcquisitionScale > 0f,
                    "EMCON retains non-zero hostile exposure/acquisition");
            Require(CounterReconDeceptionModelV142.MinReacquisitionAcquisitionScale > 0f,
                    "reacquisition never grants scripted immunity");
            Require(CounterReconDeceptionModelV142.MinDecoyCredibility01 > 0f,
                    "adaptation never hard-disables deception");

            float previousBaseDelay = float.MaxValue;
            int previousOffsetHash = int.MinValue;
            for (int round = 1; round <= 100; round++)
            {
                float baseDelay = CounterReconDeceptionModelV142.ReacquisitionDelayForRound(round);
                Require(baseDelay >= CounterReconDeceptionModelV142.MinReacquisitionDelaySeconds &&
                        baseDelay <= CounterReconDeceptionModelV142.MaxReacquisitionDelaySeconds,
                        "reacquisition delay bounds " + round);
                Require(baseDelay <= previousBaseDelay + 0.0001f, "late-round reacquisition monotonicity " + round);
                previousBaseDelay = baseDelay;

                float lowResilience = CounterReconDeceptionModelV142.ReacquisitionDelayForContext(round, 0f, 0f);
                float highResilience = CounterReconDeceptionModelV142.ReacquisitionDelayForContext(
                    round, 1f, CounterReconDeceptionModelV142.MaxSuspicion01);
                Require(lowResilience >= CounterReconDeceptionModelV142.MinReacquisitionDelaySeconds &&
                        lowResilience <= CounterReconDeceptionModelV142.MaxReacquisitionDelaySeconds,
                        "low-resilience context delay " + round);
                Require(highResilience >= CounterReconDeceptionModelV142.MinReacquisitionDelaySeconds &&
                        highResilience <= CounterReconDeceptionModelV142.MaxReacquisitionDelaySeconds,
                        "high-resilience context delay " + round);
                Require(highResilience <= lowResilience + 0.0001f,
                        "disciplined observers reacquire no slower " + round);

                Vector2 a = CounterReconDeceptionModelV142.DecoyOffset(round, 1);
                Vector2 b = CounterReconDeceptionModelV142.DecoyOffset(round, 1);
                Require(a == b, "deterministic decoy offset " + round);
                Require(Mathf.Abs(a.x) >= CounterReconDeceptionModelV142.MinDecoyOffset - 0.001f &&
                        Mathf.Abs(a.x) <= CounterReconDeceptionModelV142.MaxDecoyOffset + 0.001f,
                        "decoy offset bounds " + round);
                int offsetHash = Mathf.RoundToInt(a.x * 1000f) * 397 ^ Mathf.RoundToInt(a.y * 1000f);
                if (round > 1) Require(offsetHash != previousOffsetHash, "adjacent decoy signature variation " + round);
                previousOffsetHash = offsetHash;
            }

            float suspicion = 0f;
            float previousCredibility = CounterReconDeceptionModelV142.MaxDecoyCredibility01;
            for (int use = 0; use < 6; use++)
            {
                float credibility = CounterReconDeceptionModelV142.DecoyCredibility01(suspicion, 0.55f);
                Require(credibility >= CounterReconDeceptionModelV142.MinDecoyCredibility01 &&
                        credibility <= CounterReconDeceptionModelV142.MaxDecoyCredibility01,
                        "decoy credibility bounds " + use);
                Require(credibility <= previousCredibility + 0.0001f, "deception-break adaptation " + use);
                previousCredibility = credibility;
                float next = CounterReconDeceptionModelV142.SuspicionAfterDecoy(suspicion);
                Require(next >= suspicion && next <= CounterReconDeceptionModelV142.MaxSuspicion01,
                        "bounded deterministic suspicion " + use);
                suspicion = next;
            }

            float lowExposure = CounterReconDeceptionModelV142.EmconExposureForResilience(0f);
            float highExposure = CounterReconDeceptionModelV142.EmconExposureForResilience(1f);
            Require(lowExposure > 0f && highExposure > 0f && highExposure >= lowExposure,
                    "resilience-aware EMCON exposure floor");
            float lowAcquisition = CounterReconDeceptionModelV142.ReacquisitionAcquisitionForResilience(0f);
            float highAcquisition = CounterReconDeceptionModelV142.ReacquisitionAcquisitionForResilience(1f);
            Require(lowAcquisition > 0f && highAcquisition > 0f && highAcquisition >= lowAcquisition,
                    "resilience-aware reacquisition floor");

            Require(CounterBatteryDirectorV140.EnsureInstalled() != null, "counter-battery authority installation");
            Require(CounterObservationDirectorV141.EnsureInstalled() != null, "counter-observation installation");
            Require(CounterReconDeceptionDirectorV142.EnsureInstalled() != null, "deception director installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v14.2 contract failed: " + contract);
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
            string file = pass ? "V14_2_DECEPTION_OK.txt" : "V14_2_DECEPTION_FAIL.txt";
            string text =
                "Tank Revival: Orzel Overdrive\nDeception / EMCON warfare v14.2: " + (pass ? "PASS" : "FAIL") +
                "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion +
                "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[CounterReconDeceptionCISmokeProbeV142] " + text.Replace("\n", " | "));
        }
    }
}
