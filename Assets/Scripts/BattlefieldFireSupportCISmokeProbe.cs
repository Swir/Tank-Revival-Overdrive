using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(20090)]
    public sealed class BattlefieldFireSupportCISmokeProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-tr-v139-smoke")) return;
            if (FindAnyObjectByType<BattlefieldFireSupportCISmokeProbe>() != null) return;
            GameObject go = new GameObject("BattlefieldFireSupportCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldFireSupportCISmokeProbe>();
        }

        private void Start()
        {
            try
            {
                RunContracts();
                WriteMarker(true,
                    "profiles=PASS charge=PASS sensor=PASS reactions=PASS intent=PASS caps=PASS authority=PASS installation=PASS");
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                WriteMarker(false, ex.GetType().Name + ": " + ex.Message);
                Application.Quit(39);
            }
        }

        private static void RunContracts()
        {
            Require(BattlefieldFireSupportModelV139.ConfigurationValid, "configuration");
            Require(BattlefieldFireSupportModelV139.PlannedRounds == 100, "100 rounds");
            Require(BattlefieldFireSupportModelV139.MaxTrackedHostiles == 24, "hostile cap");
            Require(BattlefieldFireSupportModelV139.MaxTelegraphs == 6, "telegraph cap");
            Require(BattlefieldFireSupportModelV139.MaxStrikes == 6, "strike cap");

            int previous = int.MinValue;
            for (int round = 1; round <= 100; round++)
            {
                FireSupportProfileV139 profile = BattlefieldFireSupportModelV139.ProfileForRound(round);
                Require(profile.Round == round, "profile round " + round);
                Require(profile.StrikeBudget >= 3 && profile.StrikeBudget <= 6, "strike budget " + round);
                Require(profile.CooldownSeconds >= 15f && profile.CooldownSeconds <= 22f, "cooldown " + round);
                Require(profile.Signature != previous, "signature uniqueness " + round);
                previous = profile.Signature;
            }

            float charge = 0f;
            for (int i = 0; i < 40; i++)
                charge = BattlefieldFireSupportModelV139.AdvanceCharge(
                    charge,
                    BattlefieldSuppressionModelV138.MaxPressure,
                    1f,
                    BattlefieldFireSupportModelV139.SampleCadenceSeconds);
            Require(charge >= BattlefieldFireSupportModelV139.ReadyCharge, "charge reaches ready");
            float decayed = BattlefieldFireSupportModelV139.AdvanceCharge(
                0.75f, 0f, 0f, BattlefieldFireSupportModelV139.SampleCadenceSeconds);
            Require(decayed < 0.75f && decayed >= 0f, "charge decay");

            float detected = BattlefieldFireSupportModelV139.SensorOpportunity(SensorContactStateV136.Detected, 0.35f);
            float tracked = BattlefieldFireSupportModelV139.SensorOpportunity(SensorContactStateV136.Tracked, 0.60f);
            float verified = BattlefieldFireSupportModelV139.SensorOpportunity(SensorContactStateV136.Verified, 0.85f);
            Require(detected > 0f && tracked > detected && verified > tracked, "sensor ordering");
            Require(BattlefieldFireSupportModelV139.SensorOpportunity(SensorContactStateV136.Unknown, 1f) == 0f, "unknown contact denial");

            Require(BattlefieldFireSupportModelV139.ReactionFor(EnemyKind.Fast) == FireSupportReactionV139.Evade, "fast evade");
            Require(BattlefieldFireSupportModelV139.ReactionFor(EnemyKind.Heavy) == FireSupportReactionV139.Brace, "heavy brace");
            Require(BattlefieldFireSupportModelV139.ReactionFor(EnemyKind.Sniper) == FireSupportReactionV139.Disperse, "sniper disperse");
            Require(BattlefieldFireSupportModelV139.ReactionFor(EnemyKind.Siege) == FireSupportReactionV139.Hold, "siege hold");
            Require(BattlefieldFireSupportModelV139.ReactionFor(EnemyKind.Boss) == FireSupportReactionV139.Brace, "boss brace");

            FireSupportProfileV139 p80 = BattlefieldFireSupportModelV139.ProfileForRound(80);
            float scoreDetected = BattlefieldFireSupportModelV139.TargetScore(
                EnemyKind.Sniper, 70f, 1.20f, 1f, SensorContactStateV136.Detected, 0.35f, 2, p80);
            float scoreVerified = BattlefieldFireSupportModelV139.TargetScore(
                EnemyKind.Sniper, 70f, 1.20f, 1f, SensorContactStateV136.Verified, 0.85f, 2, p80);
            float scoreUnknown = BattlefieldFireSupportModelV139.TargetScore(
                EnemyKind.Sniper, 70f, 1.20f, 1f, SensorContactStateV136.Unknown, 1f, 2, p80);
            Require(scoreVerified > scoreDetected, "verified contact priority");
            Require(float.IsNegativeInfinity(scoreUnknown), "unknown contact rejected");

            FireSupportStrikeIntentV139 intent = BattlefieldFireSupportModelV139.BuildIntent(
                EnemyKind.Sniper, new Vector2(2f, 1f), 2, 80, p80.Signature);
            Require(intent.Ammo == AmmoType.Explosive, "explosive support ammo");
            Require(intent.Damage >= 1 && intent.Damage <= 2, "bounded damage");
            Require(intent.Speed > 0f && intent.Speed <= 12f, "bounded projectile speed");
            Require(intent.Direction.sqrMagnitude > 0.99f && intent.Direction.sqrMagnitude < 1.01f, "normalized direction");
            Require(intent.Role == FireSupportRoleV139.Sniper, "intent role");
            Require(intent.Reaction == FireSupportReactionV139.Disperse, "intent reaction");

            Require(BattlefieldFireSupportDirector.EnsureInstalled() != null, "director installation");
            Require(BattlefieldFireSupportExecutionBridgeV139.EnsureInstalled() != null, "execution bridge installation");
        }

        private static void Require(bool value, string contract)
        {
            if (!value) throw new InvalidOperationException("v13.9 contract failed: " + contract);
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
            string file = pass ? "V13_9_FIRE_SUPPORT_OK.txt" : "V13_9_FIRE_SUPPORT_FAIL.txt";
            string text =
                "Tank Revival: Orzel Overdrive\nFire-support command v13.9: " + (pass ? "PASS" : "FAIL") +
                "\nVersion: " + Application.version +
                "\nUnity: " + Application.unityVersion +
                "\nDetails: " + details + "\n";
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), file), text);
            Debug.Log("[BattlefieldFireSupportCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}
