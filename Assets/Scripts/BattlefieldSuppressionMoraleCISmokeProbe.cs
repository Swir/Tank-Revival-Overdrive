using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20080)]
    public sealed class BattlefieldSuppressionMoraleCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v138-smoke")) return;
            if (FindAnyObjectByType<BattlefieldSuppressionMoraleCISmokeProbe>() != null) return;
            GameObject go = new GameObject("BattlefieldSuppressionMoraleCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldSuppressionMoraleCISmokeProbe>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true, "thresholds=PASS hysteresis=PASS anti-permabroken=PASS ammo-counterplay=PASS density=PASS bounds=PASS authority=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(38);
            }
        }

        private static void RunContracts()
        {
            Require(BattlefieldSuppressionModelV138.ConfigurationValid, "configuration");
            Require(BattlefieldSuppressionModelV138.MaxTrackedEnemies == 24, "tracked cap");
            Require(BattlefieldSuppressionModelV138.MaxHudMarkers <= 8, "HUD cap");

            BattlefieldMoraleStateV138 s = BattlefieldMoraleStateV138.Steady;
            s = BattlefieldSuppressionModelV138.ResolveState(s, 25f, 2f);
            Require(s == BattlefieldMoraleStateV138.Pressed, "steady->pressed");
            s = BattlefieldSuppressionModelV138.ResolveState(s, 50f, 2f);
            Require(s == BattlefieldMoraleStateV138.Suppressed, "pressed->suppressed");
            s = BattlefieldSuppressionModelV138.ResolveState(s, 80f, 2f);
            Require(s == BattlefieldMoraleStateV138.Broken, "suppressed->broken");
            s = BattlefieldSuppressionModelV138.ResolveState(s, 80f, BattlefieldSuppressionModelV138.MaxBrokenSeconds + .1f);
            Require(s == BattlefieldMoraleStateV138.Recovering, "max broken anti-lock");
            s = BattlefieldSuppressionModelV138.ResolveState(s, 5f, BattlefieldSuppressionModelV138.RecoveryFloorSeconds + .1f);
            Require(s == BattlefieldMoraleStateV138.Steady, "recovery->steady");

            float basic = BattlefieldSuppressionModelV138.ImpactPressure(AmmoType.Basic, EnemyKind.Basic, 2);
            float ap = BattlefieldSuppressionModelV138.ImpactPressure(AmmoType.ArmorPiercing, EnemyKind.Basic, 2);
            float plasma = BattlefieldSuppressionModelV138.ImpactPressure(AmmoType.Plasma, EnemyKind.Basic, 2);
            float boss = BattlefieldSuppressionModelV138.ImpactPressure(AmmoType.Plasma, EnemyKind.Boss, 2);
            Require(ap > basic && plasma > ap, "AP/Plasma counterplay ordering");
            Require(boss < plasma && boss >= 4f, "boss resistant not immune");

            float normalMiss = BattlefieldSuppressionModelV138.NearMissPressure(AmmoType.Basic, EnemyKind.Basic, LateRoundPressureBandV137.Normal);
            float criticalMiss = BattlefieldSuppressionModelV138.NearMissPressure(AmmoType.Basic, EnemyKind.Basic, LateRoundPressureBandV137.Critical);
            Require(criticalMiss < normalMiss, "late-round density anti-cheap-pressure budget");

            SuppressionIntentV138 broken = BattlefieldSuppressionModelV138.Intent(BattlefieldMoraleStateV138.Broken, 80f);
            Require(broken.MovementScale >= .80f && broken.MovementScale <= 1f, "bounded movement");
            Require(broken.ReloadScale >= 1f && broken.ReloadScale <= 1.30f, "bounded reload");
            Require(broken.SpreadScale >= 1f && broken.SpreadScale <= 1.50f, "bounded spread");
            Require(BattlefieldSuppressionMoraleDirector.EnsureInstalled() != null, "runtime director installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v13.8 contract failed: " + contract);
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
            string file = pass ? "V13_8_SUPPRESSION_MORALE_OK.txt" : "V13_8_SUPPRESSION_MORALE_FAIL.txt";
            string text = "Tank Revival: Orzel Overdrive\nBattlefield suppression/morale v13.8: " + (pass ? "PASS" : "FAIL") +
                          "\nVersion: " + Application.version + "\nUnity: " + Application.unityVersion + "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[BattlefieldSuppressionMoraleCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
