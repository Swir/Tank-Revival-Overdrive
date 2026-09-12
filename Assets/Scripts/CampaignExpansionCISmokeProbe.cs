using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v5.3 packaged-player runtime verification. Runs only with -campaign-expansion-smoke.
    /// Verifies director installation, complete operation/contract catalogs and a real boss-contract
    /// health mutation on an EnemyTank created inside the standalone Windows player.
    /// </summary>
    [DefaultExecutionOrder(21020)]
    public sealed class CampaignExpansionCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 12f;
        private float _startedAt;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-campaign-expansion-smoke")) return;
            GameObject go = new GameObject("CampaignExpansionCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignExpansionCISmokeProbe>();
        }

        private void Awake()
        {
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (_finished) return;

            TankGame game = FindAnyObjectByType<TankGame>();
            CampaignExpansionDirector director = CampaignExpansionDirector.Instance;
            if (game == null || director == null)
            {
                if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
                    Finish(false, "TankGame/campaign expansion director timeout");
                return;
            }

            _finished = true;
            if (!CampaignExpansionDirector.ValidateCatalog(out string catalog))
            {
                Finish(false, catalog);
                return;
            }

            GameObject dummy = new GameObject("CI_v53_BossContract");
            try
            {
                EnemyTank boss = dummy.AddComponent<EnemyTank>();
                boss.Initialize(game, EnemyKind.Boss, 100);
                int before = boss.Health != null ? boss.Health.Maximum : 0;
                if (before <= 0)
                {
                    Finish(false, "dummy boss Health did not initialize");
                    return;
                }

                ExpansionBossContractAgent agent = dummy.AddComponent<ExpansionBossContractAgent>();
                agent.Initialize(game, boss, ExpansionBossContract.IronOath, 10);
                int after = boss.Health.Maximum;
                if (!agent.IsConfigured || after <= before)
                {
                    Finish(false, $"boss contract failed before={before} after={after} configured={agent.IsConfigured}");
                    return;
                }

                Finish(true, $"{catalog} bossHp={before}->{after} operation={CampaignExpansionDirector.OperationName(ExpansionSectorOperation.CrossfireGrid)} challenge={CampaignExpansionDirector.ChallengeName(ExpansionChallengeContract.ArmorQuarry)}");
            }
            catch (Exception ex)
            {
                Finish(false, ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Destroy(dummy);
            }
        }

        private static bool HasArgument(string expected)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Finish(bool pass, string details)
        {
            string file = pass ? "CAMPAIGN_EXPANSION_PASS.txt" : "CAMPAIGN_EXPANSION_FAIL.txt";
            string path = Path.Combine(Directory.GetCurrentDirectory(), file);
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "v5.3 campaign expansion smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[CampaignExpansionCISmokeProbe] " + text.Replace("\n", " | "));
            Application.Quit(pass ? 0 : 41);
        }
    }
}
