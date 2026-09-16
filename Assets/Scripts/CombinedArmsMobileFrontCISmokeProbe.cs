using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE qualification for v12.0 combined-arms mobile-front contracts.</summary>
    public sealed class CombinedArmsMobileFrontCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V12_0_MOBILE_FRONT_SMOKE_OK.txt";
        public const string FailMarker = "V12_0_MOBILE_FRONT_SMOKE_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v120-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<CombinedArmsMobileFrontCISmokeProbe>() != null) return;
                GameObject go = new GameObject("CombinedArmsMobileFrontCISmokeProbe_v12_0");
                DontDestroyOnLoad(go);
                go.AddComponent<CombinedArmsMobileFrontCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!CombinedArmsMobileFrontDirector.ConfigurationValid) { Fail("Mobile-front configuration invalid"); return; }
                if (!ReactiveCoverBreachDirector.ConfigurationValid) { Fail("Breach intelligence configuration invalid"); return; }
                if (CombinedArmsMobileFrontDirector.MaxRetaskedSpecialists > 8) { Fail("Escort cap exceeds bounded budget"); return; }
                if (CombinedArmsMobileFrontDirector.OperationDuration > 55f) { Fail("Operation lifetime exceeds bounded contract"); return; }
                if (CombinedArmsMobileFrontDirector.RewardForRound(99) > CombinedArmsMobileFrontDirector.RewardMax) { Fail("Reward exceeds bounded economy contract"); return; }
                if (CombinedArmsMobileFrontDirector.NodeHitPointsForRound(99) > CombinedArmsMobileFrontDirector.NodeHealthMax) { Fail("Node health exceeds bounded contract"); return; }

                // Candidate schedule must stay in the late campaign and never collide with boss/frontline rounds.
                for (int round = 1; round <= 100; round++)
                {
                    if (!CombinedArmsMobileFrontDirector.IsCandidateRound(round)) continue;
                    if (round < CombinedArmsMobileFrontDirector.EarliestRound || round > CombinedArmsMobileFrontDirector.LatestRound || round % 10 == 0)
                    { Fail("Candidate schedule escaped late non-boss bounds at round " + round); return; }
                    if (DynamicFrontlineTerritoryDirector.HasOperationForRound(round) || CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(round) || MultiStageOperationDirector.HasOperationForRound(round) || CombinedArmsDirector.HasOperationForRound(round))
                    { Fail("Candidate schedule overlaps existing operation at round " + round); return; }
                }

                if (CombinedArmsMobileFrontDirector.ComputePresenceDelta(false, 1, 0) != 1 || CombinedArmsMobileFrontDirector.ComputePresenceDelta(false, 0, 1) != -1)
                { Fail("Friendly mobile objective presence authority invalid"); return; }
                if (CombinedArmsMobileFrontDirector.ComputePresenceDelta(true, 2, 0) != 1 || CombinedArmsMobileFrontDirector.ComputePresenceDelta(true, 0, 1) != -1)
                { Fail("Enemy breakthrough presence authority invalid"); return; }
                if (CombinedArmsMobileFrontDirector.ComputeProgress(new Vector2(0f, -4f), new Vector2(0f, 4f), Vector2.zero) < 0.49f)
                { Fail("Mobile objective progress projection invalid"); return; }
                Vector2 clamped = CombinedArmsMobileFrontDirector.ClampRoutePoint(new Vector2(100f, -100f));
                if (Mathf.Abs(clamped.x) > CombinedArmsMobileFrontDirector.ArenaXLimit + 0.01f || Mathf.Abs(clamped.y) > CombinedArmsMobileFrontDirector.ArenaYLimit + 0.01f)
                { Fail("Breach-route clamp invalid"); return; }
                if (!CombinedArmsMobileFrontDirector.IsSpecialist(EnemyKind.Heavy) || !CombinedArmsMobileFrontDirector.IsSpecialist(EnemyKind.Sniper) || !CombinedArmsMobileFrontDirector.IsSpecialist(EnemyKind.Siege) || !CombinedArmsMobileFrontDirector.IsSpecialist(EnemyKind.Elite) || CombinedArmsMobileFrontDirector.IsSpecialist(EnemyKind.Boss))
                { Fail("Specialist retask filter invalid"); return; }

                // Resolved counter-breaches must disappear from route queries while a fresh re-breach is usable.
                Vector2 probe = new Vector2(5.35f, 3.45f);
                ReactiveCoverBreachDirector.ReportBreach(probe, Team.Player, AmmoType.Explosive, ObstacleKind.Steel);
                int closed = ReactiveCoverBreachDirector.Sequence;
                if (!ReactiveCoverBreachDirector.IsBreachActive(closed) || !ReactiveCoverBreachDirector.ResolveBreach(closed) || ReactiveCoverBreachDirector.IsBreachActive(closed))
                { Fail("Resolved counter-breach lifecycle invalid"); return; }
                if (ReactiveCoverBreachDirector.TryFindBestBreach(probe - Vector2.right, probe + Vector2.up, Team.Player, out ReactiveCoverBreachDirector.BreachSnapshot stale) && stale.Sequence == closed)
                { Fail("Resolved breach remains routable"); return; }
                ReactiveCoverBreachDirector.ReportBreach(probe, Team.Player, AmmoType.Plasma, ObstacleKind.Steel);
                int reopened = ReactiveCoverBreachDirector.Sequence;
                if (reopened <= closed || !ReactiveCoverBreachDirector.IsBreachActive(reopened))
                { Fail("Re-breached route did not publish fresh sequence"); return; }

                // Objective uses canonical Health authority and real collision semantics.
                GameObject authority = new GameObject("V120_MOBILE_NODE_AUTHORITY_PROBE");
                authority.AddComponent<BoxCollider2D>();
                Rigidbody2D body = authority.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                Health health = authority.AddComponent<Health>();
                int hp = CombinedArmsMobileFrontDirector.NodeHitPointsForRound(88);
                health.Initialize(Team.Player, hp);
                if (!health.Damage(2, Team.Enemy) || health.Current != hp - 2 || body.bodyType != RigidbodyType2D.Kinematic)
                { Destroy(authority); Fail("Physical command-post Health/collision authority invalid"); return; }
                Destroy(authority);

                string report =
                    "v12.0 combined-arms mobile front smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "escortCap=" + CombinedArmsMobileFrontDirector.MaxRetaskedSpecialists + " duration=" + CombinedArmsMobileFrontDirector.OperationDuration + "\n" +
                    "nodeHP99=" + CombinedArmsMobileFrontDirector.NodeHitPointsForRound(99) + " reward99=" + CombinedArmsMobileFrontDirector.RewardForRound(99) + "\n" +
                    "closedSeq=" + closed + " reopenedSeq=" + reopened + "\n";
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), PassMarker), report);
                Debug.Log("[TankRevival] " + report.Replace("\n", " | "));
                Application.Quit(0);
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Fail(string reason)
        {
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), FailMarker), reason); } catch (Exception) { }
            Debug.LogError("[TankRevival] v12.0 mobile-front smoke FAIL: " + reason);
            Application.Quit(70);
        }
    }
}
