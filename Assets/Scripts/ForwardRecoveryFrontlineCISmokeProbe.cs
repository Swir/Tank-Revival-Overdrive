using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22109)]
    public sealed class ForwardRecoveryFrontlineCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-forward-recovery-smoke")) return;
            GameObject go = new GameObject("ForwardRecoveryFrontlineCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<ForwardRecoveryFrontlineCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool salvage = BattlefieldSalvageDirector.Instance != null;
            bool recovery = ForwardRecoveryFrontlineDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool bridge = ForwardRecoveryFrontlineDirector.BridgeAvailable;
            bool config = ForwardRecoveryFrontlineDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.9.0-dev";

            if (game && salvage && recovery && navigation && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} salvage={salvage} recovery={recovery} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} salvage={salvage} recovery={recovery} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(49);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            if (!ForwardRecoveryFrontlineDirector.CanCommit(ForwardRecoveryFrontlineDirector.CommitmentRange - 0.05f, false, false, 3f) ||
                ForwardRecoveryFrontlineDirector.CanCommit(ForwardRecoveryFrontlineDirector.CommitmentRange + 0.05f, false, false, 3f) ||
                ForwardRecoveryFrontlineDirector.CanCommit(0.5f, true, false, 3f) ||
                ForwardRecoveryFrontlineDirector.CanCommit(0.5f, false, true, 3f) ||
                ForwardRecoveryFrontlineDirector.CanCommit(0.5f, false, false, 0f))
            {
                details = "commit bounds failed";
                return false;
            }

            int convoyEarly = ForwardRecoveryFrontlineDirector.ConvoyHealthForRound(10);
            int convoyLate = ForwardRecoveryFrontlineDirector.ConvoyHealthForRound(100);
            int baseEarly = ForwardRecoveryFrontlineDirector.BaseHealthForRound(10);
            int baseLate = ForwardRecoveryFrontlineDirector.BaseHealthForRound(100);
            if (convoyEarly < ForwardRecoveryFrontlineDirector.ConvoyHealthMin || convoyLate > ForwardRecoveryFrontlineDirector.ConvoyHealthMax || convoyEarly >= convoyLate ||
                baseEarly < ForwardRecoveryFrontlineDirector.BaseHealthMin || baseLate > ForwardRecoveryFrontlineDirector.BaseHealthMax || baseEarly >= baseLate)
            {
                details = $"health progression failed convoy={convoyEarly}/{convoyLate} base={baseEarly}/{baseLate}";
                return false;
            }

            int early = ForwardRecoveryFrontlineDirector.CounterUnitsForRound(20);
            int mid = ForwardRecoveryFrontlineDirector.CounterUnitsForRound(50);
            int late = ForwardRecoveryFrontlineDirector.CounterUnitsForRound(85);
            if (early != 2 || mid != 3 || late != ForwardRecoveryFrontlineDirector.MaxCounterUnits)
            {
                details = $"counter progression failed {early}/{mid}/{late}";
                return false;
            }

            if (ForwardRecoveryFrontlineDirector.SupplyAmmoForReserve(StrategicReserveKind.Armor) != AmmoType.ArmorPiercing ||
                ForwardRecoveryFrontlineDirector.SupplyAmmoForReserve(StrategicReserveKind.FireSupport) != AmmoType.Explosive ||
                ForwardRecoveryFrontlineDirector.SupplyAmmoForReserve(StrategicReserveKind.ElectronicWarfare) != AmmoType.EMP)
            {
                details = "reserve-to-ammo mapping failed";
                return false;
            }

            int normal = ForwardRecoveryFrontlineDirector.CompletionReward(false);
            int logistics = ForwardRecoveryFrontlineDirector.CompletionReward(true);
            if (normal != ForwardRecoveryFrontlineDirector.CompletionBondReward || logistics != ForwardRecoveryFrontlineDirector.CompletionLogisticsBondReward || normal >= logistics)
            {
                details = $"reward ordering failed {normal}/{logistics}";
                return false;
            }

            details = $"commit={ForwardRecoveryFrontlineDirector.CommitmentRange:F2} convoyHP={convoyEarly}/{convoyLate} baseHP={baseEarly}/{baseLate} counter={early}/{mid}/{late} hold={ForwardRecoveryFrontlineDirector.HoldDuration:F1}s support={ForwardRecoveryFrontlineDirector.SupportDuration:F1}s pulses={ForwardRecoveryFrontlineDirector.MaxSupportPulses}";
            return true;
        }

        private static bool HasArgument(string needle)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], needle, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void WriteMarker(bool pass, string message)
        {
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "FORWARD_RECOVERY_PASS.txt" : "FORWARD_RECOVERY_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
