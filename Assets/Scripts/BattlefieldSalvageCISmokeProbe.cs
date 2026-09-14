using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22108)]
    public sealed class BattlefieldSalvageCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-battlefield-salvage-smoke")) return;
            GameObject go = new GameObject("BattlefieldSalvageCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldSalvageCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool logistics = LogisticsNetworkDirector.Instance != null;
            bool routes = SupplyRouteWarfareDirector.Instance != null;
            bool salvage = BattlefieldSalvageDirector.Instance != null;
            bool navigation = TacticalNavigationDirector.Instance != null;
            bool bridge = BattlefieldSalvageDirector.BridgeAvailable;
            bool config = BattlefieldSalvageDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.8.0-dev";

            if (game && logistics && routes && salvage && navigation && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} logistics={logistics} routes={routes} salvage={salvage} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} logistics={logistics} routes={routes} salvage={salvage} navigation={navigation} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(48);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            EnemyKind[] eligible = { EnemyKind.Heavy, EnemyKind.Sniper, EnemyKind.Siege, EnemyKind.Elite };
            for (int i = 0; i < eligible.Length; i++)
            {
                EnemyKind kind = eligible[i];
                if (!BattlefieldSalvageDirector.IsSalvageEligible(kind))
                {
                    details = "eligible enemy rejected kind=" + kind;
                    return false;
                }
                StrategicReserveKind reserve = BattlefieldSalvageDirector.ReserveKindForEnemy(kind);
                AmmoType ammo = BattlefieldSalvageDirector.FieldAmmoForReserve(reserve);
                if (ammo == AmmoType.Basic)
                {
                    details = "field ammo mapped to basic kind=" + kind;
                    return false;
                }
            }

            EnemyKind[] ineligible = { EnemyKind.Basic, EnemyKind.Fast, EnemyKind.Supply, EnemyKind.Boss };
            for (int i = 0; i < ineligible.Length; i++)
            {
                if (BattlefieldSalvageDirector.IsSalvageEligible(ineligible[i]))
                {
                    details = "ineligible enemy accepted kind=" + ineligible[i];
                    return false;
                }
            }

            if (BattlefieldSalvageDirector.FieldAmmoForReserve(StrategicReserveKind.Armor) != AmmoType.ArmorPiercing ||
                BattlefieldSalvageDirector.FieldAmmoForReserve(StrategicReserveKind.FireSupport) != AmmoType.Explosive ||
                BattlefieldSalvageDirector.FieldAmmoForReserve(StrategicReserveKind.ElectronicWarfare) != AmmoType.EMP)
            {
                details = "reserve-to-ammo mapping failed";
                return false;
            }

            int early = BattlefieldSalvageDirector.CounterRecoveryCountForRound(20);
            int mid = BattlefieldSalvageDirector.CounterRecoveryCountForRound(45);
            int late = BattlefieldSalvageDirector.CounterRecoveryCountForRound(80);
            if (early != 1 || mid != 2 || late != BattlefieldSalvageDirector.MaxCounterRecoveryUnits)
            {
                details = $"counter-recovery progression failed {early}/{mid}/{late}";
                return false;
            }

            if (!BattlefieldSalvageDirector.CanPlayerRecover(BattlefieldSalvageDirector.InteractionRange - 0.05f, false, 2f) ||
                BattlefieldSalvageDirector.CanPlayerRecover(BattlefieldSalvageDirector.InteractionRange + 0.05f, false, 2f) ||
                BattlefieldSalvageDirector.CanPlayerRecover(0.2f, true, 2f) ||
                BattlefieldSalvageDirector.CanPlayerRecover(0.2f, false, 0f))
            {
                details = "player recovery bounds failed";
                return false;
            }

            if (!BattlefieldSalvageDirector.CanEnemyReclaim(BattlefieldSalvageDirector.ReclaimRange - 0.05f, false, 2f) ||
                BattlefieldSalvageDirector.CanEnemyReclaim(BattlefieldSalvageDirector.ReclaimRange + 0.05f, false, 2f) ||
                BattlefieldSalvageDirector.CanEnemyReclaim(0.1f, true, 2f))
            {
                details = "enemy reclaim bounds failed";
                return false;
            }

            int field = BattlefieldSalvageDirector.BondsForRecovery(false, SalvageRecoveryMode.Field);
            int strategic = BattlefieldSalvageDirector.BondsForRecovery(false, SalvageRecoveryMode.Strategic);
            int logistics = BattlefieldSalvageDirector.BondsForRecovery(true, SalvageRecoveryMode.Strategic);
            if (field != 1 || strategic != BattlefieldSalvageDirector.StrategicBondReward || logistics != BattlefieldSalvageDirector.StrategicLogisticsBondReward || !(field < strategic && strategic < logistics))
            {
                details = $"reward ordering failed field={field} strategic={strategic} logistics={logistics}";
                return false;
            }

            details = $"eligible={eligible.Length} activeCap={BattlefieldSalvageDirector.MaxActiveSalvage} dropsPerRound={BattlefieldSalvageDirector.MaxEnemyDropsPerRound} counter={early}/{mid}/{late} rewards={field}/{strategic}/{logistics}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "BATTLEFIELD_SALVAGE_PASS.txt" : "BATTLEFIELD_SALVAGE_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
