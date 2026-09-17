using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for v13.3 bounded squad roster, cohesion shock and formation intent.</summary>
    public sealed class BattlefieldCohesionCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_3_BATTLEFIELD_COHESION_OK.txt";
        public const string FailMarker = "V13_3_BATTLEFIELD_COHESION_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v133-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<BattlefieldCohesionCISmokeProbe>() != null) return;
                GameObject go = new GameObject("BattlefieldCohesionCISmokeProbe_v13_3");
                DontDestroyOnLoad(go);
                go.AddComponent<BattlefieldCohesionCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!BattlefieldCohesionModelV133.ConfigurationValid || !BattlefieldCohesionDirector.ConfigurationValid)
                { Fail("v13.3 cohesion configuration invalid"); return; }
                if (BattlefieldCohesionDirector.Instance == null)
                { Fail("battlefield cohesion runtime director was not installed"); return; }
                if (BattlefieldCohesionModelV133.MaxTrackedActors != 24 || BattlefieldCohesionModelV133.MaxSquads != 6 || BattlefieldCohesionModelV133.SquadSize != 4)
                { Fail("fixed roster capacity contract regressed"); return; }
                if (BattlefieldCohesionModelV133.IsEligible(EnemyKind.Boss) || BattlefieldCohesionModelV133.IsEligible(EnemyKind.Supply))
                { Fail("Boss/Supply leaked into v13.3 squad ownership"); return; }

                SquadRoleV133[] expectedRoles = { SquadRoleV133.Leader, SquadRoleV133.Wingman, SquadRoleV133.Breacher, SquadRoleV133.Support };
                for (int slot = 0; slot < BattlefieldCohesionModelV133.SquadSize; slot++)
                {
                    if (BattlefieldCohesionModelV133.RoleForSlot(slot) != expectedRoles[slot])
                    { Fail("role mapping regressed at slot " + slot); return; }
                }
                if (BattlefieldCohesionModelV133.PromotedLeaderSlot(0) != -1 ||
                    BattlefieldCohesionModelV133.PromotedLeaderSlot(0b1010) != 1 ||
                    BattlefieldCohesionModelV133.PromotedLeaderSlot(0b1100) != 2)
                { Fail("deterministic leader promotion order regressed"); return; }

                int assignmentFold = 43;
                int[] roleCoverage = new int[4];
                for (int round = 1; round <= 100; round++)
                {
                    for (int ordinal = 0; ordinal < BattlefieldCohesionModelV133.MaxTrackedActors; ordinal++)
                    {
                        EnemyKind kind = (EnemyKind)(ordinal % 6);
                        SquadAssignmentV133 a = BattlefieldCohesionModelV133.AssignmentForOrdinal(round, ordinal, kind);
                        SquadAssignmentV133 b = BattlefieldCohesionModelV133.AssignmentForOrdinal(round, ordinal, kind);
                        if (!a.Tracked || a.Signature != b.Signature || a.SquadId != b.SquadId || a.Slot != b.Slot || a.Role != b.Role)
                        { Fail("assignment determinism mismatch round=" + round + " ordinal=" + ordinal); return; }
                        if (a.SquadId < 0 || a.SquadId >= BattlefieldCohesionModelV133.MaxSquads || a.Slot < 0 || a.Slot >= BattlefieldCohesionModelV133.SquadSize)
                        { Fail("assignment escaped fixed roster bounds"); return; }
                        roleCoverage[(int)a.Role]++;
                        assignmentFold = unchecked(assignmentFold * 31 + a.Signature);
                    }
                }
                for (int i = 0; i < roleCoverage.Length; i++)
                    if (roleCoverage[i] <= 0) { Fail("role coverage incomplete: " + i); return; }

                int postureFold = 47;
                for (int stateValue = 0; stateValue <= (int)SquadCohesionStateV133.Regrouping; stateValue++)
                {
                    for (int roleValue = 0; roleValue <= (int)SquadRoleV133.Support; roleValue++)
                    {
                        SquadCohesionIntentV133 intent = BattlefieldCohesionModelV133.IntentFor(
                            (SquadCohesionStateV133)stateValue, (SquadRoleV133)roleValue, roleValue == 0);
                        if (intent.MovementScale < BattlefieldCohesionModelV133.MinMovementScale || intent.MovementScale > BattlefieldCohesionModelV133.MaxMovementScale ||
                            intent.ReloadScale < BattlefieldCohesionModelV133.MinReloadScale || intent.ReloadScale > BattlefieldCohesionModelV133.MaxReloadScale ||
                            intent.SpreadScale < BattlefieldCohesionModelV133.MinSpreadScale || intent.SpreadScale > BattlefieldCohesionModelV133.MaxSpreadScale ||
                            intent.Cohesion01 < 0f || intent.Cohesion01 > 1f)
                        { Fail("cohesion intent escaped hard bounds"); return; }
                        postureFold = unchecked(postureFold * 31 + Mathf.RoundToInt(intent.MovementScale * 1000f));
                        postureFold = unchecked(postureFold * 31 + Mathf.RoundToInt(intent.ReloadScale * 1000f));
                        postureFold = unchecked(postureFold * 31 + Mathf.RoundToInt(intent.SpreadScale * 1000f));
                    }
                }

                SquadCohesionIntentV133 cohesive = BattlefieldCohesionModelV133.IntentFor(SquadCohesionStateV133.Cohesive, SquadRoleV133.Leader, true);
                SquadCohesionIntentV133 shocked = BattlefieldCohesionModelV133.IntentFor(SquadCohesionStateV133.Shocked, SquadRoleV133.Leader, false);
                SquadCohesionIntentV133 regroup = BattlefieldCohesionModelV133.IntentFor(SquadCohesionStateV133.Regrouping, SquadRoleV133.Wingman, false);
                if (shocked.Cohesion01 >= cohesive.Cohesion01 || shocked.MovementScale >= cohesive.MovementScale || shocked.ReloadScale <= cohesive.ReloadScale)
                { Fail("leader-loss shock does not soften squad pressure"); return; }
                if (regroup.Cohesion01 <= shocked.Cohesion01 || regroup.Cohesion01 >= cohesive.Cohesion01)
                { Fail("regroup cohesion is not monotonic between shock and cohesive states"); return; }

                Vector2 current = Vector2.down;
                Vector2 towardLeader = BattlefieldCohesionModelV133.FormationDirection(SquadCohesionStateV133.Regrouping,
                    SquadRoleV133.Wingman, Vector2.zero, new Vector2(4f, 1f), current, 0);
                Vector2 repeatDirection = BattlefieldCohesionModelV133.FormationDirection(SquadCohesionStateV133.Regrouping,
                    SquadRoleV133.Wingman, Vector2.zero, new Vector2(4f, 1f), current, 0);
                if (towardLeader != Vector2.right || repeatDirection != towardLeader)
                { Fail("regroup direction is not deterministic/cardinal toward leader"); return; }
                Vector2 separate = BattlefieldCohesionModelV133.FormationDirection(SquadCohesionStateV133.Cohesive,
                    SquadRoleV133.Support, Vector2.zero, new Vector2(0.4f, 0f), Vector2.up, 2);
                if (separate != Vector2.left)
                { Fail("close-formation separation intent regressed"); return; }

                string report =
                    "v13.3 battlefield cohesion + squad command smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "capacity=" + BattlefieldCohesionModelV133.MaxTrackedActors + " squads=" + BattlefieldCohesionModelV133.MaxSquads +
                    " squadSize=" + BattlefieldCohesionModelV133.SquadSize + " assignmentFold=" + assignmentFold + " postureFold=" + postureFold + "\n" +
                    "shock=" + shocked.Cohesion01.ToString("0.00") + " regroup=" + regroup.Cohesion01.ToString("0.00") +
                    " cohesive=" + cohesive.Cohesion01.ToString("0.00") + " leaderShockSeconds=" + BattlefieldCohesionModelV133.LeaderShockSeconds.ToString("0.00") + "\n";
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
            Debug.LogError("[TankRevival] v13.3 battlefield cohesion smoke FAIL: " + reason);
            Application.Quit(81);
        }
    }
}
