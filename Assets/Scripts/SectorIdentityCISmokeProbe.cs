using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22102)]
    public sealed class SectorIdentityCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-sector-identity-smoke")) return;
            GameObject go = new GameObject("SectorIdentityCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<SectorIdentityCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool game = FindAnyObjectByType<TankGame>() != null;
            bool encounter = CampaignEncounterDirector.Instance != null;
            bool pacing = CampaignPacingDirector.Instance != null;
            bool sectors = SectorIdentityDirector.Instance != null;
            bool bridge = SectorIdentityDirector.BridgeAvailable;
            bool config = SectorIdentityDirector.ConfigurationValid;
            bool matrix = ValidateMatrix(out string details);
            bool version = Application.version == "8.2.0-dev";

            if (game && encounter && pacing && sectors && bridge && config && matrix && version)
            {
                WriteMarker(true, $"game={game} encounter={encounter} pacing={pacing} sectors={sectors} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false, $"game={game} encounter={encounter} pacing={pacing} sectors={sectors} bridge={bridge} config={config} matrix={matrix} {details} version={Application.version}");
                Application.Quit(42);
            }
        }

        public static bool ValidateMatrix(out string details)
        {
            int distinctDoctrineCount = 0;
            string lastName = string.Empty;
            for (int sector = 0; sector < SectorIdentityDirector.SectorCount; sector++)
            {
                for (int deck = 0; deck < SectorIdentityDirector.DeckCount; deck++)
                {
                    SectorIdentityProfile identity = SectorIdentityDirector.ResolveSector(sector, (EncounterDeck)deck);
                    if (identity.Sector != sector || string.IsNullOrEmpty(identity.Name) || string.IsNullOrEmpty(identity.Directive))
                    {
                        details = $"invalid identity sector={sector} deck={deck}";
                        return false;
                    }
                    if (identity.HealthMultiplier < SectorIdentityDirector.MinHealthMultiplier || identity.HealthMultiplier > SectorIdentityDirector.MaxHealthMultiplier ||
                        identity.FireSupportMultiplier < SectorIdentityDirector.MinFireSupportMultiplier || identity.FireSupportMultiplier > SectorIdentityDirector.MaxFireSupportMultiplier)
                    {
                        details = $"identity bounds failed sector={sector} deck={deck}";
                        return false;
                    }

                    int round = sector * 10 + 7;
                    RoundEncounterProfile baseline = CampaignPacingDirector.RefineEncounter(CampaignEncounterDirector.Resolve(round), CampaignPacingDirector.Resolve(round));
                    RoundEncounterProfile refined = SectorIdentityDirector.RefineEncounter(baseline, identity);
                    if (refined.Round != round || refined.HealthMultiplier < 0.84f || refined.HealthMultiplier > 1.42f || refined.FireSupportMultiplier < 0.82f || refined.FireSupportMultiplier > 1.48f)
                    {
                        details = $"refinement bounds failed round={round} deck={deck}";
                        return false;
                    }
                }

                SectorIdentityProfile named = SectorIdentityDirector.ResolveSector(sector, EncounterDeck.Spearhead);
                if (named.Name != lastName) distinctDoctrineCount++;
                lastName = named.Name;
            }

            for (int sector = 0; sector < 10; sector++)
            {
                EncounterDeck a = SectorIdentityDirector.ResolveDeckForRun(sector, 11);
                EncounterDeck b = SectorIdentityDirector.ResolveDeckForRun(sector, 12);
                if (a == b)
                {
                    details = $"replay rotation failed sector={sector}";
                    return false;
                }
            }

            int[] bossRounds = { 10, 20, 50, 80, 100 };
            for (int i = 0; i < bossRounds.Length; i++)
            {
                int round = bossRounds[i];
                RoundEncounterProfile baseline = CampaignPacingDirector.RefineEncounter(CampaignEncounterDirector.Resolve(round), CampaignPacingDirector.Resolve(round));
                SectorIdentityProfile identity = SectorIdentityDirector.ResolveSector((round - 1) / 10, EncounterDeck.Disruption);
                RoundEncounterProfile refined = SectorIdentityDirector.RefineEncounter(baseline, identity);
                if (!refined.BossRound || refined.Archetype != baseline.Archetype || refined.Codename != baseline.Codename)
                {
                    details = $"boss preservation failed round={round}";
                    return false;
                }
            }

            details = $"doctrines={distinctDoctrineCount}/10 matrix=30 replayRotation=10/10 bossPreservation=5/5";
            return distinctDoctrineCount == 10;
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
            string path = Path.Combine(Environment.CurrentDirectory, pass ? "SECTOR_IDENTITY_PASS.txt" : "SECTOR_IDENTITY_FAIL.txt");
            File.WriteAllText(path, $"{(pass ? "PASS" : "FAIL")} v8.2 sector identity\n{details}\n");
        }
    }
}
