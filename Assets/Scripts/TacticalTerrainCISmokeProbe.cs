using System;
using System.IO;
using UnityEngine;

namespace TankRevival
{
    /// <summary>Packaged-EXE smoke for v13.4 deterministic tactical terrain, live overlay and safe-route contracts.</summary>
    public sealed class TacticalTerrainCISmokeProbe : MonoBehaviour
    {
        public const string PassMarker = "V13_4_TACTICAL_TERRAIN_OK.txt";
        public const string FailMarker = "V13_4_TACTICAL_TERRAIN_FAIL.txt";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-tr-v134-smoke", StringComparison.OrdinalIgnoreCase)) continue;
                if (FindAnyObjectByType<TacticalTerrainCISmokeProbe>() != null) return;
                GameObject go = new GameObject("TacticalTerrainCISmokeProbe_v13_4");
                DontDestroyOnLoad(go);
                go.AddComponent<TacticalTerrainCISmokeProbe>();
                return;
            }
        }

        private void Start()
        {
            try
            {
                if (!TacticalTerrainPlannerV134.ConfigurationValid || !TacticalTerrainDirector.ConfigurationValid)
                { Fail("v13.4 tactical terrain configuration invalid"); return; }
                TacticalTerrainDirector runtimeDirector = TacticalTerrainDirector.EnsureInstalled();
                if (runtimeDirector == null || TacticalTerrainDirector.Instance == null)
                { Fail("tactical terrain runtime director was not installed"); return; }
                if (TacticalTerrainPlannerV134.PlannedRounds != 100 || TacticalTerrainPlannerV134.MaxCoverNodes != 12 || TacticalTerrainPlannerV134.CandidateSlotCount != 24)
                { Fail("hard terrain budgets regressed"); return; }

                int[] doctrineCoverage = new int[TacticalTerrainPlannerV134.DoctrineCount];
                int signatureFold = 61;
                TacticalTerrainDoctrineV134 previous = (TacticalTerrainDoctrineV134)(-1);
                for (int round = 1; round <= 100; round++)
                {
                    int encounterSignature = unchecked(round * 7919 + 130);
                    int objectiveSignature = unchecked(round * 3571 + 131);
                    TacticalTerrainPlanV134 a = TacticalTerrainPlannerV134.PlanForRound(round, encounterSignature, objectiveSignature);
                    TacticalTerrainPlanV134 b = TacticalTerrainPlannerV134.PlanForRound(round, encounterSignature, objectiveSignature);
                    if (a.Signature != b.Signature || a.Doctrine != b.Doctrine || a.CoverCount != b.CoverCount ||
                        a.SteelBudget != b.SteelBudget || a.WaterBudget != b.WaterBudget ||
                        Mathf.Abs(a.SafeLaneHalfWidth - b.SafeLaneHalfWidth) > 0.0001f || a.SlotOffset != b.SlotOffset)
                    { Fail("terrain plan determinism mismatch round=" + round); return; }
                    if (a.Round != round || a.CoverCount < 7 || a.CoverCount > TacticalTerrainPlannerV134.MaxCoverNodes)
                    { Fail("cover budget escaped bounds round=" + round); return; }
                    if (a.SteelBudget < 0 || a.WaterBudget < 0 || a.BrickBudget < 3 || a.SteelBudget + a.WaterBudget > a.CoverCount - 3)
                    { Fail("cover composition safety floor regressed round=" + round); return; }
                    if (a.SafeLaneHalfWidth < TacticalTerrainPlannerV134.MinSafeLaneHalfWidth || a.SafeLaneHalfWidth > TacticalTerrainPlannerV134.MaxSafeLaneHalfWidth)
                    { Fail("safe lane width escaped bounds round=" + round); return; }
                    if ((int)a.Doctrine < 0 || (int)a.Doctrine >= doctrineCoverage.Length)
                    { Fail("invalid doctrine round=" + round); return; }
                    doctrineCoverage[(int)a.Doctrine]++;
                    if (round > 1 && a.Doctrine == previous)
                    { Fail("adjacent terrain doctrine repeated round=" + round); return; }
                    previous = a.Doctrine;

                    bool[] candidateSlots = new bool[TacticalTerrainPlannerV134.CandidateSlotCount];
                    int water = 0, steel = 0, brick = 0;
                    for (int ordinal = 0; ordinal < a.CoverCount; ordinal++)
                    {
                        int slot = TacticalTerrainPlannerV134.CandidateSlotIndex(a, ordinal);
                        if (slot < 0 || slot >= candidateSlots.Length || candidateSlots[slot])
                        { Fail("duplicate/invalid tactical slot round=" + round + " ordinal=" + ordinal + " slot=" + slot); return; }
                        candidateSlots[slot] = true;

                        Vector2 p = TacticalTerrainPlannerV134.CandidatePosition(a, ordinal);
                        if (TacticalTerrainPlannerV134.IsReservedSafeLane(a, p))
                        { Fail("planned cover entered reserved safe lane round=" + round + " ordinal=" + ordinal); return; }
                        if (Mathf.Abs(p.x) > 10.5f || p.y < -3.4f || p.y > 4.45f)
                        { Fail("planned cover escaped bounded arena overlay round=" + round); return; }

                        ObstacleKind kind = TacticalTerrainPlannerV134.KindForOrdinal(a, ordinal);
                        int hp = TacticalTerrainPlannerV134.HitPointsFor(a, kind, ordinal);
                        if (hp < 1 || (kind == ObstacleKind.Steel && hp < 6))
                        { Fail("terrain HP contract regressed round=" + round); return; }
                        if (kind == ObstacleKind.Water) water++;
                        else if (kind == ObstacleKind.Steel) steel++;
                        else brick++;
                    }
                    if (water != a.WaterBudget || steel != a.SteelBudget || brick != a.BrickBudget)
                    { Fail("cover kind budget mismatch round=" + round + " B/S/W=" + brick + "/" + steel + "/" + water); return; }

                    if (!TacticalTerrainPlannerV134.IsReservedSafeLane(a, new Vector2(0f, -5.0f)) ||
                        !TacticalTerrainPlannerV134.IsReservedSafeLane(a, new Vector2(0f, -3.2f)) ||
                        !TacticalTerrainPlannerV134.IsReservedSafeLane(a, new Vector2(-9.5f, 5.65f)) ||
                        !TacticalTerrainPlannerV134.IsReservedSafeLane(a, new Vector2(9.5f, 5.65f)))
                    { Fail("Orzeł/spawn safe-route contract regressed round=" + round); return; }

                    signatureFold = unchecked(signatureFold * 31 + a.Signature);
                }

                for (int i = 0; i < doctrineCoverage.Length; i++)
                    if (doctrineCoverage[i] < 8) { Fail("terrain doctrine coverage too low index=" + i + " count=" + doctrineCoverage[i]); return; }

                if (Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Basic, 4, false) != 0 ||
                    Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.EMP, 4, false) != 0 ||
                    Obstacle.StructuralDamageFor(ObstacleKind.Steel, AmmoType.Explosive, 2, false) <= 0 ||
                    Obstacle.StructuralDamageFor(ObstacleKind.Brick, AmmoType.ArmorPiercing, 1, false) <= 0)
                { Fail("canonical Obstacle ammo/counterplay authority regressed"); return; }

                Vector2 fallback = TacticalTerrainPlannerV134.BreachAwareDirection(Vector2.zero, Vector2.up * 4f, Vector2.right, Team.Enemy);
                if (fallback != Vector2.right)
                { Fail("breach-aware fallback must preserve cardinal movement when no breach is active"); return; }

                // Exercise the actual runtime overlay, not only the pure planner. This catches component,
                // safe-lane and same-frame rebuild regressions in the packaged Windows player.
                runtimeDirector.ApplyDeterministicPlan(64, 640130, 640131);
                int firstCount = runtimeDirector.ActiveCoverCount;
                string overlayReason = string.Empty;
                bool firstOverlayValid = runtimeDirector.ValidateActiveOverlay(out overlayReason);
                if (firstCount < 3 || firstCount > runtimeDirector.CurrentPlan.CoverCount || !firstOverlayValid)
                { Fail("live tactical overlay invalid first build: count=" + firstCount + " reason=" + overlayReason); return; }
                runtimeDirector.ApplyDeterministicPlan(65, 650130, 650131);
                int rebuiltCount = runtimeDirector.ActiveCoverCount;
                overlayReason = string.Empty;
                bool rebuiltOverlayValid = runtimeDirector.ValidateActiveOverlay(out overlayReason);
                if (rebuiltCount < 3 || rebuiltCount > runtimeDirector.CurrentPlan.CoverCount || !rebuiltOverlayValid)
                { Fail("same-frame tactical overlay rebuild invalid: count=" + rebuiltCount + " reason=" + overlayReason); return; }

                string report =
                    "v13.4 tactical terrain + cover warfare smoke: PASS\n" +
                    "Version: " + Application.version + "\n" +
                    "rounds=100 doctrines=" + TacticalTerrainPlannerV134.DoctrineCount +
                    " maxCover=" + TacticalTerrainPlannerV134.MaxCoverNodes + " candidates=" + TacticalTerrainPlannerV134.CandidateSlotCount +
                    " signatureFold=" + signatureFold + "\n" +
                    "liveOverlay=" + firstCount + " rebuiltOverlay=" + rebuiltCount +
                    " safeLane=" + TacticalTerrainPlannerV134.MinSafeLaneHalfWidth.ToString("0.00") + ".." + TacticalTerrainPlannerV134.MaxSafeLaneHalfWidth.ToString("0.00") +
                    " breachMemory=" + ReactiveCoverBreachDirector.MaxRecentBreaches + "\n";
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
            Debug.LogError("[TankRevival] v13.4 tactical terrain smoke FAIL: " + reason);
            Application.Quit(82);
        }
    }
}
