using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(22101)]
    public sealed class CampaignPacingCISmokeProbe : MonoBehaviour
    {
        private const float TimeoutSeconds = 14f;
        private float _startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasArgument("-campaign-pacing-smoke")) return;
            GameObject go = new GameObject("CampaignPacingCISmokeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignPacingCISmokeProbe>();
        }

        private void Awake() => _startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            bool hasGame = FindAnyObjectByType<TankGame>() != null;
            bool hasEncounter = CampaignEncounterDirector.Instance != null;
            bool hasPacing = CampaignPacingDirector.Instance != null;
            bool bridge = CampaignPacingDirector.BridgeAvailable;
            bool config = CampaignPacingDirector.ConfigurationValid;
            bool schedule = ValidateSchedule(out string scheduleDetails);
            bool version = Application.version == "8.1.0-dev";

            if (hasGame && hasEncounter && hasPacing && bridge && config && schedule && version)
            {
                WriteMarker(true,
                    $"game={hasGame} encounter={hasEncounter} pacing={hasPacing} bridge={bridge} config={config} schedule={schedule} " +
                    $"bounds=wave[{CampaignPacingDirector.MinWaveMultiplier:0.00},{CampaignPacingDirector.MaxWaveMultiplier:0.00}] " +
                    $"spawn[{CampaignPacingDirector.MinSpawnDelayMultiplier:0.00},{CampaignPacingDirector.MaxSpawnDelayMultiplier:0.00}] {scheduleDetails} version={Application.version}");
                Application.Quit(0);
                return;
            }

            if (Time.realtimeSinceStartup - _startedAt >= TimeoutSeconds)
            {
                WriteMarker(false,
                    $"game={hasGame} encounter={hasEncounter} pacing={hasPacing} bridge={bridge} config={config} schedule={schedule} {scheduleDetails} version={Application.version}");
                Application.Quit(41);
            }
        }

        public static bool ValidateSchedule(out string details)
        {
            int recovery = 0;
            int skirmish = 0;
            int offensive = 0;
            int special = 0;
            int escalation = 0;
            int boss = 0;
            int longestHardRun = 0;
            int hardRun = 0;

            for (int round = 1; round <= 100; round++)
            {
                CampaignPacingProfile profile = CampaignPacingDirector.Resolve(round);
                if (profile.Round != round || profile.ApplyWaveBudget(20) < 4 || profile.ApplyWaveBudget(20) > 62 ||
                    profile.ApplyAliveCap(8) < 3 || profile.ApplyAliveCap(8) > 14)
                {
                    details = $"invalid bounds at round {round}";
                    return false;
                }

                switch (profile.Beat)
                {
                    case CampaignPacingBeat.Recovery: recovery++; hardRun = 0; break;
                    case CampaignPacingBeat.Skirmish: skirmish++; hardRun = 0; break;
                    case CampaignPacingBeat.Offensive: offensive++; hardRun++; break;
                    case CampaignPacingBeat.SpecialOperation: special++; hardRun = 0; break;
                    case CampaignPacingBeat.Escalation: escalation++; hardRun++; break;
                    case CampaignPacingBeat.BossClimax: boss++; hardRun++; break;
                }
                longestHardRun = Mathf.Max(longestHardRun, hardRun);

                if (round % 10 == 0 && profile.Beat != CampaignPacingBeat.BossClimax)
                {
                    details = $"boss cadence broken at round {round}";
                    return false;
                }
                if (round > 10 && ((round - 1) % 10) == 0 && profile.Beat != CampaignPacingBeat.Recovery)
                {
                    details = $"post-boss recovery missing at round {round}";
                    return false;
                }
            }

            CampaignPacingProfile r10 = CampaignPacingDirector.Resolve(10);
            CampaignPacingProfile r11 = CampaignPacingDirector.Resolve(11);
            CampaignPacingProfile r36 = CampaignPacingDirector.Resolve(36);
            CampaignPacingProfile r50 = CampaignPacingDirector.Resolve(50);
            CampaignPacingProfile r80 = CampaignPacingDirector.Resolve(80);
            CampaignPacingProfile r90 = CampaignPacingDirector.Resolve(90);
            CampaignPacingProfile r100 = CampaignPacingDirector.Resolve(100);

            bool checkpoints =
                r10.Beat == CampaignPacingBeat.BossClimax &&
                r11.Beat == CampaignPacingBeat.Recovery &&
                r36.Beat == CampaignPacingBeat.SpecialOperation &&
                r50.Beat == CampaignPacingBeat.BossClimax &&
                r80.Beat == CampaignPacingBeat.BossClimax &&
                r90.Beat == CampaignPacingBeat.BossClimax &&
                r100.Beat == CampaignPacingBeat.BossClimax;

            bool counts = recovery == 9 && boss == 10 && skirmish >= 15 && offensive >= 10 && special >= 10 && escalation >= 15;
            bool breathingRoom = longestHardRun <= 5;
            details = $"recovery={recovery} skirmish={skirmish} offensive={offensive} special={special} escalation={escalation} boss={boss} longestHardRun={longestHardRun} checkpoints={checkpoints}";
            return checkpoints && counts && breathingRoom;
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
            string path = Path.Combine(Directory.GetCurrentDirectory(), pass ? "CAMPAIGN_PACING_PASS.txt" : "CAMPAIGN_PACING_FAIL.txt");
            string text =
                "Tank Revival: Orzel Overdrive\n" +
                "Campaign pacing runtime smoke: " + (pass ? "PASS" : "FAIL") + "\n" +
                "Version: " + Application.version + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Details: " + details + "\n";
            File.WriteAllText(path, text);
            Debug.Log("[CampaignPacingCISmokeProbe] " + text.Replace("\n", " | "));
        }
    }
}