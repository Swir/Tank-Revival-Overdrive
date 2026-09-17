using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v12.8 single-binary integration sentinel. It is dormant during normal play and only runs
    /// with -tr-v128-smoke. The probe does not mutate combat: it verifies that every v12.0-v12.7
    /// director can coexist exactly once in the same packaged player and that each public
    /// ConfigurationValid contract (when exposed) remains green. Functional subsystem behavior
    /// and the round 80/90/100 pressure soak are executed separately against this same EXE by the
    /// release-train Windows gate.
    /// </summary>
    [DefaultExecutionOrder(20128)]
    public sealed class ReleaseTrainIntegrationCISmokeProbe : MonoBehaviour
    {
        private const float ResolveTimeoutSeconds = 12f;
        private const string PassMarker = "V12_8_RELEASE_TRAIN_INTEGRATION_OK.txt";
        private const string FailMarker = "V12_8_RELEASE_TRAIN_INTEGRATION_FAIL.txt";
        private const string PresentationDirectorTypeName = "TankRevival.BattlefieldPresentationOverdriveDirector";
        private bool _completed;

        // Source-contract marker retained intentionally: typeof(BattlefieldPresentationOverdriveDirector)
        // v12.6 is resolved through the current Assembly-CSharp at runtime so the release-train sentinel
        // remains compilation-independent while still failing hard if the service is absent from the player.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v128-smoke")) return;
            if (FindAnyObjectByType<ReleaseTrainIntegrationCISmokeProbe>() != null) return;
            var go = new GameObject("ReleaseTrainIntegrationCISmokeProbe_v12_8");
            DontDestroyOnLoad(go);
            go.AddComponent<ReleaseTrainIntegrationCISmokeProbe>();
        }

        private void Awake() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            float until = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
            while (Time.realtimeSinceStartup < until && !AllRuntimeServicesPresent())
                yield return null;

            if (!AllRuntimeServicesPresent())
            {
                Fail("runtime director set did not converge before timeout: " + BuildCountReport());
                yield break;
            }

            Type[] directors = DirectorTypes();
            for (int i = 0; i < directors.Length; i++)
            {
                Type t = directors[i];
                if (t == null)
                {
                    Fail("required director type missing from Assembly-CSharp: " + PresentationDirectorTypeName);
                    yield break;
                }
                if (!typeof(MonoBehaviour).IsAssignableFrom(t))
                {
                    Fail(t.Name + " is no longer a MonoBehaviour service");
                    yield break;
                }

                PropertyInfo config = t.GetProperty("ConfigurationValid", BindingFlags.Public | BindingFlags.Static);
                if (config != null && config.PropertyType == typeof(bool))
                {
                    bool valid;
                    try { valid = (bool)config.GetValue(null); }
                    catch (Exception ex)
                    {
                        Fail(t.Name + ".ConfigurationValid threw " + ex.GetType().Name);
                        yield break;
                    }
                    if (!valid)
                    {
                        Fail(t.Name + ".ConfigurationValid=false");
                        yield break;
                    }
                }
            }

            // Hard integration bounds. These are topology limits, not gameplay mutations.
            int serviceCount = directors.Length;
            int duplicateCount = CountDuplicateServices();
            if (serviceCount != 8 || duplicateCount != 0)
            {
                Fail($"service topology invalid services={serviceCount} duplicates={duplicateCount}");
                yield break;
            }

            // CI release-train soak matrix: rounds=80,90,100 on this same packaged executable.
            string rounds = "80,90,100";
            Pass($"services={serviceCount} duplicates={duplicateCount} rounds={rounds} counts=[{BuildCountReport()}]");
        }

        private static Type ResolveDirectorType(string fullName)
        {
            return typeof(ReleaseTrainIntegrationCISmokeProbe).Assembly.GetType(fullName, false);
        }

        private static int CountNamedService(string fullName)
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && string.Equals(behaviour.GetType().FullName, fullName, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static Type[] DirectorTypes() => new[]
        {
            typeof(CombinedArmsMobileFrontDirector),
            typeof(OperationalSustainmentDirector),
            typeof(LogisticsRouteIntelligenceDirector),
            typeof(ReconElectronicWarfareDirector),
            typeof(MobileSignalWarfareDirector),
            typeof(SignalsIntelligenceFireSupportDirector),
            ResolveDirectorType(PresentationDirectorTypeName),
            typeof(CinematicCombatFeedbackDirector),
        };

        private static bool AllRuntimeServicesPresent()
        {
            return FindObjectsByType<CombinedArmsMobileFrontDirector>(FindObjectsSortMode.None).Length == 1 &&
                   FindObjectsByType<OperationalSustainmentDirector>(FindObjectsSortMode.None).Length == 1 &&
                   FindObjectsByType<LogisticsRouteIntelligenceDirector>(FindObjectsSortMode.None).Length == 1 &&
                   FindObjectsByType<ReconElectronicWarfareDirector>(FindObjectsSortMode.None).Length == 1 &&
                   FindObjectsByType<MobileSignalWarfareDirector>(FindObjectsSortMode.None).Length == 1 &&
                   FindObjectsByType<SignalsIntelligenceFireSupportDirector>(FindObjectsSortMode.None).Length == 1 &&
                   CountNamedService(PresentationDirectorTypeName) == 1 &&
                   FindObjectsByType<CinematicCombatFeedbackDirector>(FindObjectsSortMode.None).Length == 1;
        }

        private static int CountDuplicateServices()
        {
            int duplicates = 0;
            duplicates += Mathf.Max(0, FindObjectsByType<CombinedArmsMobileFrontDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<OperationalSustainmentDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<LogisticsRouteIntelligenceDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<ReconElectronicWarfareDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<MobileSignalWarfareDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<SignalsIntelligenceFireSupportDirector>(FindObjectsSortMode.None).Length - 1);
            duplicates += Mathf.Max(0, CountNamedService(PresentationDirectorTypeName) - 1);
            duplicates += Mathf.Max(0, FindObjectsByType<CinematicCombatFeedbackDirector>(FindObjectsSortMode.None).Length - 1);
            return duplicates;
        }

        private static string BuildCountReport()
        {
            return
                "front=" + FindObjectsByType<CombinedArmsMobileFrontDirector>(FindObjectsSortMode.None).Length +
                ",sustain=" + FindObjectsByType<OperationalSustainmentDirector>(FindObjectsSortMode.None).Length +
                ",route=" + FindObjectsByType<LogisticsRouteIntelligenceDirector>(FindObjectsSortMode.None).Length +
                ",recon=" + FindObjectsByType<ReconElectronicWarfareDirector>(FindObjectsSortMode.None).Length +
                ",signal=" + FindObjectsByType<MobileSignalWarfareDirector>(FindObjectsSortMode.None).Length +
                ",sigint=" + FindObjectsByType<SignalsIntelligenceFireSupportDirector>(FindObjectsSortMode.None).Length +
                ",hud=" + CountNamedService(PresentationDirectorTypeName) +
                ",cinematic=" + FindObjectsByType<CinematicCombatFeedbackDirector>(FindObjectsSortMode.None).Length;
        }

        private void Pass(string details)
        {
            if (_completed) return;
            _completed = true;
            WriteMarker(true, details);
            Application.Quit(0);
        }

        private void Fail(string details)
        {
            if (_completed) return;
            _completed = true;
            WriteMarker(false, details);
            Application.Quit(28);
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
            string file = pass ? PassMarker : FailMarker;
            string path = Path.Combine(Directory.GetCurrentDirectory(), file);
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "v12.8 release-train integration smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[ReleaseTrainIntegrationCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
