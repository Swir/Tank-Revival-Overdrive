using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22105)]
    public sealed class StrategicReserveAttritionCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-strategic-reserves-smoke")) return;
            GameObject go = new GameObject("StrategicReserveAttritionCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<StrategicReserveAttritionCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool encounter = CampaignEncounterDirector.Instance != null;
            bool memory = WarStateCampaignMemoryDirector.Instance != null;
            bool reserves = StrategicReserveAttritionDirector.Instance != null;
            bool bridge = StrategicReserveAttritionDirector.BridgeAvailable;
            bool config = StrategicReserveAttritionDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.5.0-dev";

            if (game && encounter && memory && reserves && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} encounter={encounter} memory={memory} reserves={reserves} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} encounter={encounter} memory={memory} reserves={reserves} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(45);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            StrategicReserveSnapshot previous = default;
            int victoryWeaker = 0;
            int defeatStronger = 0;
            int criticalChecks = 0;

            for (int sector = 0; sector < StrategicReserveAttritionDirector.SectorCount; sector++)
            {
                StrategicReserveSnapshot neutral = StrategicReserveAttritionDirector.ResolveSectorEntry(sector, previous, SectorWarState.Unresolved);
                if (neutral.Armor < 0 || neutral.Armor > neutral.ArmorCapacity ||
                    neutral.FireSupport < 0 || neutral.FireSupport > neutral.FireSupportCapacity ||
                    neutral.ElectronicWarfare < 0 || neutral.ElectronicWarfare > neutral.ElectronicWarfareCapacity)
                {
                    details = $"reserve bounds failed sector={sector}";
                    return false;
                }

                if (sector > 0)
                {
                    StrategicReserveSnapshot won = StrategicReserveAttritionDirector.ResolveSectorEntry(sector, previous, SectorWarState.Victory);
                    StrategicReserveSnapshot lost = StrategicReserveAttritionDirector.ResolveSectorEntry(sector, previous, SectorWarState.Defeat);
                    if (won.Armor > lost.Armor || won.FireSupport > lost.FireSupport || won.ElectronicWarfare > lost.ElectronicWarfare)
                    {
                        details = $"war-state reserve ordering failed sector={sector}";
                        return false;
                    }
                    if (won.Armor < neutral.Armor || won.FireSupport < neutral.FireSupport || won.ElectronicWarfare < neutral.ElectronicWarfare) victoryWeaker++;
                    if (lost.Armor > neutral.Armor || lost.FireSupport > neutral.FireSupport || lost.ElectronicWarfare > neutral.ElectronicWarfare) defeatStronger++;
                }

                RoundEncounterProfile baseline = CampaignEncounterDirector.Resolve(Mathf.Min(100, sector * 10 + 8));
                StrategicReserveSnapshot full = new StrategicReserveSnapshot(10, 10, 10, 10, 10, 10);
                StrategicReserveSnapshot empty = new StrategicReserveSnapshot(0, 10, 0, 10, 0, 10);
                RoundEncounterProfile fullProfile = StrategicReserveAttritionDirector.RefineEncounter(baseline, full);
                RoundEncounterProfile emptyProfile = StrategicReserveAttritionDirector.RefineEncounter(baseline, empty);
                if (emptyProfile.HealthMultiplier > fullProfile.HealthMultiplier + 0.001f ||
                    emptyProfile.FireSupportMultiplier > fullProfile.FireSupportMultiplier + 0.001f ||
                    emptyProfile.StrikeCadence < fullProfile.StrikeCadence - 0.001f)
                {
                    details = $"attrition pressure ordering failed sector={sector}";
                    return false;
                }
                if (!baseline.BossRound && (emptyProfile.ChampionEnabled || emptyProfile.StrategicStrikes)) criticalChecks++;
                previous = neutral;
            }

            if (!StrategicReserveAttritionDirector.TryReserveKind(EnemyKind.Heavy, out StrategicReserveKind heavyKind, out int heavyCost) || heavyKind != StrategicReserveKind.Armor || heavyCost != 2)
            {
                details = "heavy reserve mapping failed";
                return false;
            }
            if (!StrategicReserveAttritionDirector.TryReserveKind(EnemyKind.Siege, out StrategicReserveKind siegeKind, out int siegeCost) || siegeKind != StrategicReserveKind.FireSupport || siegeCost != 2)
            {
                details = "siege reserve mapping failed";
                return false;
            }
            if (!StrategicReserveAttritionDirector.TryReserveKind(EnemyKind.Elite, out StrategicReserveKind eliteKind, out int eliteCost) || eliteKind != StrategicReserveKind.ElectronicWarfare || eliteCost != 2)
            {
                details = "elite reserve mapping failed";
                return false;
            }
            if (victoryWeaker == 0 || defeatStronger == 0)
            {
                details = $"carryover consequence missing victory={victoryWeaker} defeat={defeatStronger}";
                return false;
            }

            details = $"sectors={StrategicReserveAttritionDirector.SectorCount} victoryWeaker={victoryWeaker} defeatStronger={defeatStronger} criticalChecks={criticalChecks} carry={StrategicReserveAttritionDirector.CarryoverRatio:0.00}";
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "STRATEGIC_RESERVES_PASS.txt" : "STRATEGIC_RESERVES_FAIL.txt");
            File.WriteAllText(path, (pass ? "PASS " : "FAIL ") + message + Environment.NewLine);
        }
    }
}
