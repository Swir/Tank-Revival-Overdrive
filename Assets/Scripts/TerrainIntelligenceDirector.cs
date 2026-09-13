using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(405)]
    public sealed class TerrainIntelligenceDirector : MonoBehaviour
    {
        public const float ScanCadence = 0.85f;
        public const float CoverSearchRadius = 7.5f;
        public const float CoverOffset = 0.82f;
        public const float BreachCorridorWidth = 0.72f;
        public const float BreachRange = 7.0f;
        public const float MinimumBreachCadence = 2.25f;
        public const int MaxCachedObstacles = 96;
        public const int MaxBreachActorsPerBeat = 4;

        private static TerrainIntelligenceDirector _instance;
        private readonly List<Obstacle> _obstacles = new List<Obstacle>(MaxCachedObstacles);
        private readonly Dictionary<EnemyTank, float> _nextBreachAt = new Dictionary<EnemyTank, float>(16);
        private TankGame _game;
        private float _nextScan;
        private int _coverCandidates;
        private int _breachCandidates;
        private int _lastObstacleCount;

        public static TerrainIntelligenceDirector Instance => _instance;
        public static bool ConfigurationValid =>
            ScanCadence >= 0.5f && ScanCadence <= 1.5f &&
            CoverSearchRadius >= 5f && CoverSearchRadius <= 10f &&
            CoverOffset >= 0.55f && CoverOffset <= 1.25f &&
            BreachCorridorWidth >= 0.45f && BreachCorridorWidth <= 1.1f &&
            BreachRange >= 5f && BreachRange <= 9f &&
            MinimumBreachCadence >= 1.8f && MinimumBreachCadence <= 3.5f &&
            MaxCachedObstacles >= 64 && MaxCachedObstacles <= 128 &&
            MaxBreachActorsPerBeat >= 2 && MaxBreachActorsPerBeat <= 6;

        public int CachedObstacleCount => _lastObstacleCount;
        public int CoverCandidateCount => _coverCandidates;
        public int BreachCandidateCount => _breachCandidates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TerrainIntelligenceDirector>() != null) return;
            var go = new GameObject("TerrainIntelligenceDirector_v7_4");
            DontDestroyOnLoad(go);
            go.AddComponent<TerrainIntelligenceDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanCadence;
                RefreshObstacleCache();
            }

            ExecuteBreachDoctrine(Mathf.Clamp(_game.CurrentRound, 1, 100));
        }

        private void RefreshObstacleCache()
        {
            _obstacles.Clear();
            Obstacle[] found = FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length && _obstacles.Count < MaxCachedObstacles; i++)
            {
                Obstacle obstacle = found[i];
                if (obstacle == null || !obstacle.isActiveAndEnabled) continue;
                _obstacles.Add(obstacle);
            }

            _lastObstacleCount = _obstacles.Count;
            _coverCandidates = 0;
            _breachCandidates = 0;
            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle obstacle = _obstacles[i];
                if (obstacle == null) continue;
                if (obstacle.Kind == ObstacleKind.Brick || obstacle.Kind == ObstacleKind.Steel) _coverCandidates++;
                if (obstacle.Kind == ObstacleKind.Brick) _breachCandidates++;
            }
        }

        public bool TryRefineOrder(
            EnemyTank actor,
            SquadTacticalRole role,
            Vector2 objective,
            float standoff,
            EnemyTank[] squad,
            out Vector2 refinedObjective,
            out float refinedStandoff)
        {
            refinedObjective = objective;
            refinedStandoff = standoff;
            if (actor == null || _game == null || _obstacles.Count == 0) return false;

            Vector2 actorPos = actor.transform.position;
            Vector2 player = _game.PlayerPosition;

            if (role == SquadTacticalRole.Suppressor || actor.Kind == EnemyKind.Sniper)
            {
                if (TryFindCoverAnchor(actorPos, player, true, out Vector2 cover))
                {
                    refinedObjective = cover;
                    refinedStandoff = 0.35f;
                    return true;
                }
            }

            if (role == SquadTacticalRole.Escort || actor.Kind == EnemyKind.Heavy)
            {
                if (TryFindScreenAnchor(actor, squad, player, out Vector2 screen))
                {
                    refinedObjective = screen;
                    refinedStandoff = 0.45f;
                    return true;
                }

                if (TryFindCoverAnchor(actorPos, player, false, out Vector2 cover))
                {
                    refinedObjective = cover;
                    refinedStandoff = 0.45f;
                    return true;
                }
            }

            if (role == SquadTacticalRole.Breaker || actor.Kind == EnemyKind.Siege)
            {
                if (TryFindBreachObstacle(actorPos, _game.BasePosition, out Obstacle obstacle))
                {
                    refinedObjective = obstacle.transform.position;
                    refinedStandoff = 1.45f;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindCoverAnchor(Vector2 actorPos, Vector2 threat, bool preferLongRange, out Vector2 anchor)
        {
            anchor = actorPos;
            float bestScore = float.MinValue;
            float radiusSqr = CoverSearchRadius * CoverSearchRadius;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle obstacle = _obstacles[i];
                if (obstacle == null || (obstacle.Kind != ObstacleKind.Brick && obstacle.Kind != ObstacleKind.Steel)) continue;

                Vector2 obstaclePos = obstacle.transform.position;
                float actorSqr = (obstaclePos - actorPos).sqrMagnitude;
                if (actorSqr > radiusSqr) continue;

                Vector2 awayFromThreat = obstaclePos - threat;
                if (awayFromThreat.sqrMagnitude < 0.04f) continue;
                awayFromThreat.Normalize();

                Vector2 candidate = obstaclePos + awayFromThreat * CoverOffset;
                float moveCost = Vector2.Distance(actorPos, candidate);
                float threatDistance = Vector2.Distance(candidate, threat);
                float coverStrength = obstacle.Kind == ObstacleKind.Steel ? 3.0f : 1.8f;
                float rangeScore = preferLongRange ? Mathf.Clamp(threatDistance, 3.5f, 8.0f) * 0.55f : Mathf.Clamp(threatDistance, 2.0f, 6.0f) * 0.24f;
                float score = coverStrength + rangeScore - moveCost * 0.42f;

                if (score > bestScore)
                {
                    bestScore = score;
                    anchor = candidate;
                }
            }

            return bestScore > float.MinValue;
        }

        private static bool TryFindScreenAnchor(EnemyTank actor, EnemyTank[] squad, Vector2 threat, out Vector2 anchor)
        {
            anchor = actor.transform.position;
            if (squad == null) return false;

            EnemyTank protectedUnit = null;
            float best = float.MaxValue;
            Vector2 actorPos = actor.transform.position;

            for (int i = 0; i < squad.Length; i++)
            {
                EnemyTank ally = squad[i];
                if (ally == null || ally == actor || ally.Health == null || ally.Health.IsDead) continue;
                TacticalNavigationAgent nav = ally.GetComponent<TacticalNavigationAgent>();
                bool vulnerable = ally.Kind == EnemyKind.Sniper || (nav != null && nav.Role == SquadTacticalRole.Suppressor);
                if (!vulnerable) continue;

                float sqr = ((Vector2)ally.transform.position - actorPos).sqrMagnitude;
                if (sqr < best && sqr <= 30.25f)
                {
                    best = sqr;
                    protectedUnit = ally;
                }
            }

            if (protectedUnit == null) return false;
            Vector2 allyPos = protectedUnit.transform.position;
            Vector2 towardThreat = threat - allyPos;
            if (towardThreat.sqrMagnitude < 0.04f) return false;
            towardThreat.Normalize();
            anchor = allyPos + towardThreat * 1.15f;
            return true;
        }

        private bool TryFindBreachObstacle(Vector2 from, Vector2 target, out Obstacle bestObstacle)
        {
            bestObstacle = null;
            Vector2 lane = target - from;
            float laneLength = lane.magnitude;
            if (laneLength < 1f) return false;
            Vector2 laneDir = lane / laneLength;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                Obstacle obstacle = _obstacles[i];
                if (obstacle == null || obstacle.Kind != ObstacleKind.Brick) continue;

                Vector2 point = obstacle.transform.position;
                Vector2 relative = point - from;
                float along = Vector2.Dot(relative, laneDir);
                if (along < 0.65f || along > Mathf.Min(laneLength - 0.35f, BreachRange)) continue;

                Vector2 closest = from + laneDir * along;
                float lateral = Vector2.Distance(point, closest);
                if (lateral > BreachCorridorWidth) continue;

                float score = lateral * 3.0f + along * 0.08f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestObstacle = obstacle;
                }
            }

            return bestObstacle != null;
        }

        private void ExecuteBreachDoctrine(int round)
        {
            if (round < 12 || _obstacles.Count == 0) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            int fired = 0;
            Vector2 eagle = _game.BasePosition;
            float progress = (round - 1f) / 99f;

            for (int i = 0; i < enemies.Length && fired < MaxBreachActorsPerBeat; i++)
            {
                EnemyTank actor = enemies[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead || actor.Kind == EnemyKind.Supply) continue;

                TacticalNavigationAgent nav = actor.GetComponent<TacticalNavigationAgent>();
                bool breaker = actor.Kind == EnemyKind.Siege || (nav != null && nav.Role == SquadTacticalRole.Breaker);
                if (!breaker) continue;

                if (_nextBreachAt.TryGetValue(actor, out float next) && Time.time < next) continue;
                Vector2 origin = actor.transform.position;
                if (!TryFindBreachObstacle(origin, eagle, out Obstacle obstacle)) continue;

                Vector2 target = obstacle.transform.position;
                Vector2 direction = target - origin;
                if (direction.sqrMagnitude < 0.16f || direction.magnitude > BreachRange + 0.8f) continue;
                direction.Normalize();

                int damage = round >= 60 ? 2 : 1;
                float speed = Mathf.Lerp(7.8f, 10.0f, progress);
                Vector2 muzzle = origin + direction * (actor.Kind == EnemyKind.Siege ? 0.94f : 0.78f);
                _game.SpawnProjectile(muzzle, direction, Team.Enemy, damage, speed, AmmoDatabase.Color(AmmoType.Explosive), AmmoType.Explosive);
                VisualFactory.MuzzleFlash(muzzle, new Color(1f, 0.33f, 0.07f), actor.Kind == EnemyKind.Siege ? 0.95f : 0.72f);
                VisualFactory.RingPulse(target, new Color(1f, 0.22f, 0.05f), 0.55f);
                BattleAudio.PlayGlobal(actor.Kind == EnemyKind.Siege ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.08f, 0.08f);

                _nextBreachAt[actor] = Time.time + MinimumBreachCadence + (actor.GetInstanceID() & 3) * 0.18f;
                fired++;
            }

            if (_nextBreachAt.Count > 24) PruneBreachTimers(enemies);
        }

        private void PruneBreachTimers(EnemyTank[] enemies)
        {
            var live = new HashSet<EnemyTank>();
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null) live.Add(enemies[i]);
            var stale = new List<EnemyTank>();
            foreach (KeyValuePair<EnemyTank, float> kv in _nextBreachAt)
                if (kv.Key == null || !live.Contains(kv.Key)) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++) _nextBreachAt.Remove(stale[i]);
        }
    }
}
