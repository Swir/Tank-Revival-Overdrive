using UnityEngine;

namespace TankRevival
{
    public enum TacticalTerrainDoctrineV134
    {
        OpenLanes = 0,
        CrossfireGrid = 1,
        FortifiedCorridor = 2,
        BreachBelt = 3,
        RiverCuts = 4,
        CounterattackLanes = 5,
        SiegeApproach = 6
    }

    public struct TacticalTerrainPlanV134
    {
        public int Round;
        public TacticalTerrainDoctrineV134 Doctrine;
        public int CoverCount;
        public int SteelBudget;
        public int WaterBudget;
        public float SafeLaneHalfWidth;
        public int SlotOffset;
        public int Signature;

        public int BrickBudget => Mathf.Max(0, CoverCount - SteelBudget - WaterBudget);
        public string CompactLabel => Doctrine + " C" + CoverCount + " S" + SteelBudget + " W" + WaterBudget;
    }

    /// <summary>
    /// Pure deterministic v13.4 planner. It shapes a small tactical overlay around the existing arena,
    /// while TankGame remains round/arena authority and Obstacle remains the only structural-damage authority.
    /// </summary>
    public static class TacticalTerrainPlannerV134
    {
        public const int PlannedRounds = 100;
        public const int DoctrineCount = 7;
        public const int MaxCoverNodes = 12;
        public const int CandidateSlotCount = 24;
        public const float MinSafeLaneHalfWidth = 1.55f;
        public const float MaxSafeLaneHalfWidth = 2.25f;

        private static readonly Vector2[] CandidateSlots =
        {
            new Vector2(-9.2f, 3.2f), new Vector2(-6.6f, 3.0f), new Vector2(-4.4f, 2.1f),
            new Vector2(4.4f, 2.1f), new Vector2(6.6f, 3.0f), new Vector2(9.2f, 3.2f),
            new Vector2(-9.4f, 0.7f), new Vector2(-6.8f, 0.4f), new Vector2(-4.6f, -0.4f),
            new Vector2(4.6f, -0.4f), new Vector2(6.8f, 0.4f), new Vector2(9.4f, 0.7f),
            new Vector2(-9.0f, -2.2f), new Vector2(-6.5f, -2.5f), new Vector2(-4.5f, -2.8f),
            new Vector2(4.5f, -2.8f), new Vector2(6.5f, -2.5f), new Vector2(9.0f, -2.2f),
            new Vector2(-10.0f, 4.1f), new Vector2(-5.4f, 4.0f), new Vector2(-3.6f, 3.1f),
            new Vector2(3.6f, 3.1f), new Vector2(5.4f, 4.0f), new Vector2(10.0f, 4.1f)
        };

        public static bool ConfigurationValid =>
            PlannedRounds == 100 && DoctrineCount == 7 && MaxCoverNodes == 12 && CandidateSlotCount == CandidateSlots.Length &&
            MinSafeLaneHalfWidth >= 1.45f && MaxSafeLaneHalfWidth <= 2.40f && MinSafeLaneHalfWidth < MaxSafeLaneHalfWidth;

        public static TacticalTerrainPlanV134 PlanForRound(int requestedRound, int encounterSignature, int objectiveSignature)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            int band = Mathf.Clamp((round - 1) / 20, 0, 4);
            TacticalTerrainDoctrineV134 doctrine = ResolveDoctrine(round, band);
            int cover = Mathf.Clamp(7 + band + ((round + (int)doctrine) % 3), 7, MaxCoverNodes);
            int water = doctrine == TacticalTerrainDoctrineV134.RiverCuts ? Mathf.Clamp(2 + band / 2, 2, 4) :
                (round >= 22 && doctrine == TacticalTerrainDoctrineV134.OpenLanes ? 1 : 0);
            int steel = round < 18 ? 0 : Mathf.Clamp(1 + band + (doctrine == TacticalTerrainDoctrineV134.FortifiedCorridor || doctrine == TacticalTerrainDoctrineV134.SiegeApproach ? 1 : 0), 1, 5);
            if (steel + water > cover - 3) steel = Mathf.Max(0, cover - water - 3);
            float lane = Mathf.Lerp(MaxSafeLaneHalfWidth, MinSafeLaneHalfWidth, (round - 1f) / 99f);
            if (doctrine == TacticalTerrainDoctrineV134.OpenLanes || doctrine == TacticalTerrainDoctrineV134.CounterattackLanes)
                lane = Mathf.Min(MaxSafeLaneHalfWidth, lane + 0.22f);

            int signature = 59;
            signature = unchecked(signature * 31 + round);
            signature = unchecked(signature * 31 + encounterSignature);
            signature = unchecked(signature * 31 + objectiveSignature);
            signature = unchecked(signature * 31 + (int)doctrine);
            signature = unchecked(signature * 31 + cover);
            signature = unchecked(signature * 31 + steel);
            signature = unchecked(signature * 31 + water);
            signature = unchecked(signature * 31 + Mathf.RoundToInt(lane * 1000f));
            int offset = PositiveMod(signature ^ (round * 97), CandidateSlotCount);

            return new TacticalTerrainPlanV134
            {
                Round = round,
                Doctrine = doctrine,
                CoverCount = cover,
                SteelBudget = steel,
                WaterBudget = water,
                SafeLaneHalfWidth = lane,
                SlotOffset = offset,
                Signature = signature
            };
        }

        public static TacticalTerrainDoctrineV134 ResolveDoctrine(int round, int band)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            int b = Mathf.Clamp(band, 0, 4);
            TacticalTerrainDoctrineV134 current = (TacticalTerrainDoctrineV134)PositiveMod(r * 5 + b * 3 + r / 10, DoctrineCount);
            if (r <= 1) return current;
            TacticalTerrainDoctrineV134 previousRaw = (TacticalTerrainDoctrineV134)PositiveMod((r - 1) * 5 + Mathf.Clamp((r - 2) / 20, 0, 4) * 3 + (r - 1) / 10, DoctrineCount);
            if (current == previousRaw)
                current = (TacticalTerrainDoctrineV134)PositiveMod((int)current + 1, DoctrineCount);
            return current;
        }

        public static int CandidateSlotIndex(TacticalTerrainPlanV134 plan, int ordinal)
        {
            return PositiveMod(plan.SlotOffset + Mathf.Max(0, ordinal) * 5, CandidateSlotCount);
        }

        public static Vector2 CandidatePosition(TacticalTerrainPlanV134 plan, int ordinal)
        {
            int slot = CandidateSlotIndex(plan, ordinal);
            Vector2 basePosition = CandidateSlots[slot];
            int h = unchecked(plan.Signature * 31 + ordinal * 131 + slot * 17);
            float jitterX = (PositiveMod(h, 7) - 3) * 0.055f;
            float jitterY = (PositiveMod(h / 7, 7) - 3) * 0.045f;
            Vector2 p = basePosition + new Vector2(jitterX, jitterY);
            if (IsReservedSafeLane(plan, p))
                p.x = Mathf.Sign(p.x == 0f ? 1f : p.x) * (plan.SafeLaneHalfWidth + 1.25f);
            return p;
        }

        public static ObstacleKind KindForOrdinal(TacticalTerrainPlanV134 plan, int ordinal)
        {
            // A cyclic permutation gives every ordinal exactly one rank for every possible
            // CoverCount (7..12). The previous *7 stride collapsed quotas whenever the
            // cover count shared a divisor with 7, e.g. all seven round-5 nodes got one rank.
            int rank = PositiveMod(Mathf.Max(0, ordinal) + plan.SlotOffset, Mathf.Max(1, plan.CoverCount));
            if (rank < plan.WaterBudget) return ObstacleKind.Water;
            if (rank < plan.WaterBudget + plan.SteelBudget) return ObstacleKind.Steel;
            return ObstacleKind.Brick;
        }

        public static int HitPointsFor(TacticalTerrainPlanV134 plan, ObstacleKind kind, int ordinal)
        {
            if (kind == ObstacleKind.Water) return 1;
            int band = Mathf.Clamp((plan.Round - 1) / 20, 0, 4);
            if (kind == ObstacleKind.Steel) return Mathf.Clamp(6 + band + (ordinal & 1), 6, 11);
            return Mathf.Clamp(1 + band / 2 + ((ordinal + plan.Round) % 3 == 0 ? 1 : 0), 1, 4);
        }

        public static bool IsReservedSafeLane(TacticalTerrainPlanV134 plan, Vector2 position)
        {
            // Permanent Orzełek/player corridor and a wider lower staging area.
            if (position.y <= -2.8f && Mathf.Abs(position.x) < plan.SafeLaneHalfWidth) return true;
            if (position.y <= -4.5f && Mathf.Abs(position.x) < 3.0f) return true;
            // Protect all four canonical enemy spawn exits at the north edge.
            if (position.y >= 4.75f && (Mathf.Abs(position.x) < 2.2f || Mathf.Abs(position.x) > 7.2f)) return true;
            return false;
        }

        public static Vector2 BreachAwareDirection(Vector2 actorPosition, Vector2 targetPosition, Vector2 current, Team actorTeam)
        {
            ReactiveCoverBreachDirector.BreachSnapshot breach;
            if (!ReactiveCoverBreachDirector.TryFindBestBreach(actorPosition, targetPosition, actorTeam, out breach))
                return Cardinal(current, Vector2.down);
            Vector2 toBreach = breach.Position - actorPosition;
            if (toBreach.sqrMagnitude < 0.64f)
                return Cardinal(current, Vector2.down);
            return Cardinal(toBreach, current);
        }

        public static Vector2 Cardinal(Vector2 value, Vector2 fallback)
        {
            Vector2 v = value.sqrMagnitude > 0.0001f ? value : fallback;
            if (v.sqrMagnitude <= 0.0001f) return Vector2.down;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y)
                ? new Vector2(Mathf.Sign(v.x), 0f)
                : new Vector2(0f, Mathf.Sign(v.y));
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    /// <summary>
    /// Small fixed-capacity terrain overlay. It creates only canonical Obstacle components and never
    /// owns structural damage, projectile collision, Health, tank movement, spawning or economy.
    /// </summary>
    public sealed class TacticalTerrainDirector : MonoBehaviour
    {
        public static TacticalTerrainDirector Instance { get; private set; }
        public static bool ConfigurationValid => TacticalTerrainPlannerV134.ConfigurationValid;

        private static readonly Collider2D[] PlacementBuffer = new Collider2D[4];
        private readonly Obstacle[] _activeCover = new Obstacle[TacticalTerrainPlannerV134.MaxCoverNodes];
        private GameObject _roundRoot;
        private TankGame _game;
        private TacticalTerrainPlanV134 _plan;
        private int _activeCount;
        private int _destroyedCount;
        private int _round = -1;
        private string _hudText = "TRN STANDBY";

        public TacticalTerrainPlanV134 CurrentPlan => _plan;
        public int ActiveCoverCount => _activeCount;
        public int DestroyedCoverCount => _destroyedCount;
        public string HudText => _hudText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static TacticalTerrainDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            TacticalTerrainDirector existing = FindAnyObjectByType<TacticalTerrainDirector>();
            if (existing != null) return existing;
            GameObject go = new GameObject("TacticalTerrainDirector_v13_4");
            DontDestroyOnLoad(go);
            return go.AddComponent<TacticalTerrainDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginRound(TankGame game, int round, EncounterPlan encounter, ObjectivePlanV131 objective)
        {
            _game = game;
            ApplyDeterministicPlan(round, encounter.Signature, objective.Signature);
        }

        public void ApplyDeterministicPlan(int round, int encounterSignature, int objectiveSignature)
        {
            int boundedRound = Mathf.Clamp(round, 1, TacticalTerrainPlannerV134.PlannedRounds);
            _plan = TacticalTerrainPlannerV134.PlanForRound(boundedRound, encounterSignature, objectiveSignature);
            _round = boundedRound;
            RebuildOverlay();
        }

        public bool ValidateActiveOverlay(out string reason)
        {
            if (_round < 1 || _roundRoot == null)
            {
                reason = "no active tactical terrain round";
                return false;
            }
            if (_activeCount < 1 || _activeCount > _plan.CoverCount || _activeCount > TacticalTerrainPlannerV134.MaxCoverNodes)
            {
                reason = "active cover count outside plan bounds: " + _activeCount + "/" + _plan.CoverCount;
                return false;
            }
            for (int i = 0; i < _activeCount; i++)
            {
                Obstacle obstacle = _activeCover[i];
                if (obstacle == null)
                {
                    reason = "null cover entry inside compact active range index=" + i;
                    return false;
                }
                Vector2 position = obstacle.transform.position;
                if (TacticalTerrainPlannerV134.IsReservedSafeLane(_plan, position))
                {
                    reason = "live cover entered reserved safe lane index=" + i;
                    return false;
                }
                BoxCollider2D collider = obstacle.GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    reason = "canonical cover missing BoxCollider2D index=" + i;
                    return false;
                }
                if (obstacle.Kind == ObstacleKind.Water && !collider.isTrigger)
                {
                    reason = "water cover must be trigger index=" + i;
                    return false;
                }
                if (obstacle.Kind != ObstacleKind.Water && collider.isTrigger)
                {
                    reason = "solid cover unexpectedly trigger index=" + i;
                    return false;
                }
            }
            reason = "OK";
            return true;
        }

        public static Vector2 AdjustDirection(EnemyTank actor, Vector2 self, Vector2 target, Vector2 current)
        {
            TacticalTerrainDirector d = Instance;
            if (d == null || actor == null || d._round < 1)
                return TacticalTerrainPlannerV134.Cardinal(current, Vector2.down);

            // Existing ReactiveCoverBreachDirector is the canonical fixed breach memory. v13.4 only
            // consumes it as a bounded directional hint; EnemyTank remains movement/target/fire authority.
            if (d._plan.Doctrine == TacticalTerrainDoctrineV134.BreachBelt ||
                d._plan.Doctrine == TacticalTerrainDoctrineV134.FortifiedCorridor ||
                d._plan.Doctrine == TacticalTerrainDoctrineV134.SiegeApproach ||
                ReactiveCoverBreachDirector.RecentCount > 0)
                return TacticalTerrainPlannerV134.BreachAwareDirection(self, target, current, Team.Enemy);

            return TacticalTerrainPlannerV134.Cardinal(current, Vector2.down);
        }

        private void RebuildOverlay()
        {
            if (_roundRoot != null)
            {
                // Destroy is end-of-frame; deactivate first so old colliders cannot poison same-frame placement.
                _roundRoot.SetActive(false);
                Destroy(_roundRoot);
            }
            for (int i = 0; i < _activeCover.Length; i++) _activeCover[i] = null;
            _activeCount = 0;
            _destroyedCount = 0;

            _roundRoot = new GameObject("TacticalTerrain_v13_4_R" + _plan.Round.ToString("000"));
            _roundRoot.transform.SetParent(transform, false);

            int attempts = 0;
            int candidate = 0;
            while (_activeCount < _plan.CoverCount && attempts < TacticalTerrainPlannerV134.CandidateSlotCount)
            {
                Vector2 position = TacticalTerrainPlannerV134.CandidatePosition(_plan, candidate);
                ObstacleKind kind = TacticalTerrainPlannerV134.KindForOrdinal(_plan, candidate);
                int hp = TacticalTerrainPlannerV134.HitPointsFor(_plan, kind, candidate);
                candidate++;
                attempts++;
                if (TacticalTerrainPlannerV134.IsReservedSafeLane(_plan, position)) continue;
                if (PlacementBlocked(position)) continue;
                Obstacle obstacle = CreateCanonicalCover(position, kind, hp, _activeCount);
                if (obstacle == null) continue;
                _activeCover[_activeCount++] = obstacle;
            }
            RefreshTelemetry();
        }

        private static bool PlacementBlocked(Vector2 position)
        {
            int hits = Physics2D.OverlapCircleNonAlloc(position, 0.46f, PlacementBuffer);
            bool blocked = false;
            for (int i = 0; i < hits; i++)
            {
                if (PlacementBuffer[i] != null) blocked = true;
                PlacementBuffer[i] = null;
            }
            return blocked;
        }

        private Obstacle CreateCanonicalCover(Vector2 position, ObstacleKind kind, int hp, int ordinal)
        {
            if (_roundRoot == null) return null;
            GameObject root = new GameObject("TACTICAL_V134_" + kind + "_" + ordinal.ToString("00"));
            root.transform.SetParent(_roundRoot.transform, false);
            root.transform.position = position;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.78f, 0.78f);
            if (kind == ObstacleKind.Water) collider.isTrigger = true;
            Obstacle obstacle = root.AddComponent<Obstacle>();
            obstacle.Initialize(kind, hp);
            BuildCoverVisual(root.transform, kind);
            return obstacle;
        }

        private static void BuildCoverVisual(Transform root, ObstacleKind kind)
        {
            if (kind == ObstacleKind.Brick)
            {
                VisualFactory.Rect("TacticalBrickShadow", root, new Vector2(0.78f, 0.78f), new Color(0.12f, 0.03f, 0.015f), new Vector3(0.04f, -0.04f, 0f), 0);
                VisualFactory.Rect("TacticalBrick", root, new Vector2(0.70f, 0.70f), new Color(0.62f, 0.15f, 0.05f), Vector3.zero, 1);
                VisualFactory.Rect("TacticalBrickTop", root, new Vector2(0.55f, 0.10f), new Color(0.95f, 0.38f, 0.10f), new Vector3(0f, 0.20f, 0f), 2);
            }
            else if (kind == ObstacleKind.Steel)
            {
                VisualFactory.Rect("TacticalSteelShadow", root, new Vector2(0.78f, 0.78f), new Color(0.04f, 0.055f, 0.075f), new Vector3(0.04f, -0.04f, 0f), 0);
                VisualFactory.Rect("TacticalSteel", root, new Vector2(0.70f, 0.70f), new Color(0.31f, 0.40f, 0.50f), Vector3.zero, 1);
                VisualFactory.Rect("TacticalSteelTop", root, new Vector2(0.53f, 0.11f), new Color(0.68f, 0.86f, 0.96f), new Vector3(0f, 0.20f, 0f), 2);
            }
            else
            {
                VisualFactory.Rect("TacticalWater", root, new Vector2(0.74f, 0.74f), new Color(0.02f, 0.24f, 0.48f, 0.78f), Vector3.zero, -2);
                VisualFactory.Rect("TacticalWaterShine", root, new Vector2(0.55f, 0.07f), new Color(0.20f, 0.82f, 1f, 0.76f), new Vector3(0f, 0.14f, 0f), -1);
            }
        }

        private void Update()
        {
            if (_round < 1) return;
            bool dirty = false;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_activeCover[i] == null) { dirty = true; break; }
            }
            if (!dirty) return;
            _destroyedCount += CompactActiveCover();
            RefreshTelemetry();
        }

        private int CompactActiveCover()
        {
            int oldCount = _activeCount;
            int write = 0;
            for (int read = 0; read < oldCount; read++)
            {
                Obstacle cover = _activeCover[read];
                if (cover == null) continue;
                _activeCover[write++] = cover;
            }
            for (int i = write; i < oldCount; i++) _activeCover[i] = null;
            _activeCount = write;
            return oldCount - write;
        }

        private void RefreshTelemetry()
        {
            _activeCount = Mathf.Clamp(_activeCount, 0, TacticalTerrainPlannerV134.MaxCoverNodes);
            int breaches = ReactiveCoverBreachDirector.RecentCount;
            _hudText = "TRN " + _plan.Doctrine + " C" + _activeCount + "/" + _plan.CoverCount +
                " D" + _destroyedCount + " BR" + breaches + " L" + _plan.SafeLaneHalfWidth.ToString("0.0") +
                " SIG " + _plan.Signature.ToString("X8");
        }
    }
}
