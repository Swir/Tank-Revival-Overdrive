using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(420)]
    public sealed class TacticalNavigationDirector : MonoBehaviour
    {
        public const float DecisionCadence = 0.30f;
        public const float ProbeDistance = 1.35f;
        public const float SeparationRadius = 1.10f;
        public const float AntiStallSeconds = 1.25f;
        public const int MaxManagedEnemies = 40;

        private static TacticalNavigationDirector _instance;
        private readonly Dictionary<EnemyTank, TacticalNavigationAgent> _agents = new Dictionary<EnemyTank, TacticalNavigationAgent>(40);
        private TankGame _game;
        private float _nextDecision;
        private int _lastRevision = -1;
        private int _lastRound = -1;
        private int _activeAgents;
        private int _roleCoverage;

        public static TacticalNavigationDirector Instance => _instance;
        public static bool ConfigurationValid => DecisionCadence >= 0.20f && DecisionCadence <= 0.50f && ProbeDistance >= 0.8f && ProbeDistance <= 1.8f && SeparationRadius >= 0.7f && SeparationRadius <= 1.5f && AntiStallSeconds >= 0.8f && AntiStallSeconds <= 2.0f && MaxManagedEnemies >= 24 && MaxManagedEnemies <= 48;
        public int ActiveAgents => _activeAgents;
        public int RoleCoverage => _roleCoverage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalNavigationDirector>() != null) return;
            var go = new GameObject("TacticalNavigationDirector_v7_3");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalNavigationDirector>();
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
            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);

            if (_lastRevision != RuntimeBattleRegistry.Revision || _lastRound != round)
            {
                ReconcileAgents(round);
                _lastRevision = RuntimeBattleRegistry.Revision;
                _lastRound = round;
            }

            if (Time.time < _nextDecision) return;
            _nextDecision = Time.time + DecisionCadence;
            IssueFormationOrders(round);
        }

        private void ReconcileAgents(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            var live = new HashSet<EnemyTank>();
            int count = 0;
            int roleMask = 0;

            for (int i = 0; i < enemies.Length && count < MaxManagedEnemies; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                live.Add(enemy);
                if (!_agents.TryGetValue(enemy, out TacticalNavigationAgent agent) || agent == null)
                {
                    agent = enemy.gameObject.GetComponent<TacticalNavigationAgent>();
                    if (agent == null) agent = enemy.gameObject.AddComponent<TacticalNavigationAgent>();
                    agent.Initialize(enemy);
                    _agents[enemy] = agent;
                }
                SquadTacticalRole role = ChooseRole(enemy, round, count);
                agent.SetRole(role);
                roleMask |= 1 << (int)role;
                count++;
            }

            var stale = new List<EnemyTank>();
            foreach (var kv in _agents)
                if (kv.Key == null || !live.Contains(kv.Key)) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++) _agents.Remove(stale[i]);

            _activeAgents = count;
            _roleCoverage = CountBits(roleMask);
        }

        private void IssueFormationOrders(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;
            float progress = (round - 1f) / 99f;

            for (int i = 0; i < enemies.Length && i < MaxManagedEnemies; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || !_agents.TryGetValue(enemy, out TacticalNavigationAgent agent) || agent == null) continue;

                Vector2 pos = enemy.transform.position;
                Vector2 objective = player;
                float standoff = 2.4f;
                float speedScale = 1f;
                SquadTacticalRole role = agent.Role;

                switch (role)
                {
                    case SquadTacticalRole.Flanker:
                    {
                        Vector2 toPlayer = player - pos;
                        Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized;
                        float sign = ((enemy.GetInstanceID() ^ round) & 1) == 0 ? 1f : -1f;
                        objective = player + side * sign * Mathf.Lerp(3.2f, 4.6f, progress);
                        standoff = 1.5f;
                        speedScale = 1.10f;
                        break;
                    }
                    case SquadTacticalRole.Suppressor:
                        objective = player;
                        standoff = enemy.Kind == EnemyKind.Sniper ? Mathf.Lerp(5.8f, 7.2f, progress) : 4.2f;
                        speedScale = 0.86f;
                        break;
                    case SquadTacticalRole.Breaker:
                        objective = eagle;
                        standoff = 1.25f;
                        speedScale = 0.96f;
                        break;
                    case SquadTacticalRole.Escort:
                    {
                        EnemyTank protectedUnit = FindNearestPriorityUnit(pos, enemies);
                        objective = protectedUnit != null ? (Vector2)protectedUnit.transform.position : Vector2.Lerp(player, eagle, 0.45f);
                        standoff = 1.9f;
                        speedScale = 0.90f;
                        break;
                    }
                    case SquadTacticalRole.Hunter:
                        objective = player;
                        standoff = 1.45f;
                        speedScale = 1.12f;
                        break;
                    default:
                        objective = Vector2.Lerp(player, eagle, round >= 55 ? 0.30f : 0.12f);
                        standoff = enemy.Kind == EnemyKind.Heavy ? 2.0f : 1.6f;
                        break;
                }

                if (enemy.Kind == EnemyKind.Sniper) standoff = Mathf.Max(standoff, 5.5f);
                if (enemy.Kind == EnemyKind.Siege && role != SquadTacticalRole.Escort)
                {
                    objective = eagle;
                    standoff = 2.2f;
                }
                if (enemy.Kind == EnemyKind.Heavy) speedScale *= 0.92f;
                if (enemy.Kind == EnemyKind.Boss) speedScale *= 0.84f;

                TerrainIntelligenceDirector terrain = TerrainIntelligenceDirector.Instance;
                if (terrain != null && terrain.TryRefineOrder(enemy, role, objective, standoff, enemies, out Vector2 refinedObjective, out float refinedStandoff))
                {
                    objective = refinedObjective;
                    standoff = refinedStandoff;
                    if (role == SquadTacticalRole.Escort || enemy.Kind == EnemyKind.Heavy) speedScale *= 0.94f;
                }

                agent.SetOrder(objective, standoff, speedScale, enemies);
            }
        }

        private static EnemyTank FindNearestPriorityUnit(Vector2 from, EnemyTank[] enemies)
        {
            EnemyTank best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                if (e.Kind != EnemyKind.Siege && e.Kind != EnemyKind.Boss && e.Kind != EnemyKind.Heavy) continue;
                float sqr = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr && sqr > 0.04f) { bestSqr = sqr; best = e; }
            }
            return best;
        }

        private static SquadTacticalRole ChooseRole(EnemyTank enemy, int round, int index)
        {
            switch (enemy.Kind)
            {
                case EnemyKind.Siege: return SquadTacticalRole.Breaker;
                case EnemyKind.Sniper: return index % 2 == 0 ? SquadTacticalRole.Suppressor : SquadTacticalRole.Hunter;
                case EnemyKind.Fast: return index % 3 == 0 ? SquadTacticalRole.Hunter : SquadTacticalRole.Flanker;
                case EnemyKind.Heavy: return index % 2 == 0 ? SquadTacticalRole.Vanguard : SquadTacticalRole.Escort;
                case EnemyKind.Elite: return round >= 50 ? SquadTacticalRole.Hunter : SquadTacticalRole.Suppressor;
                case EnemyKind.Boss: return SquadTacticalRole.Vanguard;
                default: return (SquadTacticalRole)((index + round / 10) % EnemySquadTacticsDirector.RoleCount);
            }
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }
    }

    [DefaultExecutionOrder(450)]
    public sealed class TacticalNavigationAgent : MonoBehaviour
    {
        private EnemyTank _enemy;
        private Rigidbody2D _body;
        private Vector2 _objective;
        private float _standoff;
        private float _speedScale = 1f;
        private EnemyTank[] _neighbors;
        private Vector2 _lastPosition;
        private float _lastProgressAt;
        private Vector2 _detour;
        private float _detourUntil;

        public SquadTacticalRole Role { get; private set; }

        public void Initialize(EnemyTank enemy)
        {
            _enemy = enemy;
            _body = GetComponent<Rigidbody2D>();
            _lastPosition = transform.position;
            _lastProgressAt = Time.time;
        }

        public void SetRole(SquadTacticalRole role) => Role = role;

        public void SetOrder(Vector2 objective, float standoff, float speedScale, EnemyTank[] neighbors)
        {
            _objective = objective;
            _standoff = Mathf.Clamp(standoff, 0.8f, 7.5f);
            _speedScale = Mathf.Clamp(speedScale, 0.72f, 1.18f);
            _neighbors = neighbors;
        }

        private void FixedUpdate()
        {
            if (_enemy == null || _body == null || _enemy.Health == null || _enemy.Health.IsDead) return;
            Vector2 pos = _body.position;
            Vector2 toObjective = _objective - pos;
            float distance = toObjective.magnitude;
            Vector2 desired;

            if (distance > _standoff + 0.45f) desired = toObjective.normalized;
            else if (distance < _standoff - 0.55f) desired = -toObjective.normalized;
            else desired = Role == SquadTacticalRole.Flanker || Role == SquadTacticalRole.Suppressor ? Perpendicular(toObjective.normalized) : Vector2.zero;

            desired += SeparationVector(pos) * 0.72f;
            desired = AvoidObstacle(pos, desired);

            if (Time.time < _detourUntil && _detour.sqrMagnitude > 0.01f)
                desired = (_detour + desired * 0.35f).normalized;

            if (((Vector2)_lastPosition - pos).sqrMagnitude > 0.0225f)
            {
                _lastPosition = pos;
                _lastProgressAt = Time.time;
            }
            else if (desired.sqrMagnitude > 0.1f && Time.time - _lastProgressAt >= TacticalNavigationDirector.AntiStallSeconds)
            {
                _detour = Perpendicular(desired).normalized;
                if (((GetInstanceID() >> 2) & 1) == 0) _detour = -_detour;
                _detourUntil = Time.time + 0.9f;
                _lastProgressAt = Time.time;
            }

            if (desired.sqrMagnitude < 0.02f) return;
            desired.Normalize();
            desired = Cardinalize(desired);
            float baseSpeed = ClassSpeed(_enemy.Kind);
            _body.MovePosition(pos + desired * (baseSpeed * _speedScale * Time.fixedDeltaTime));
        }

        private Vector2 AvoidObstacle(Vector2 pos, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.05f) return desired;
            Vector2 dir = desired.normalized;
            RaycastHit2D[] hits = Physics2D.RaycastAll(pos, dir, TacticalNavigationDirector.ProbeDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D c = hits[i].collider;
                if (c == null || c.transform == transform || c.isTrigger) continue;
                if (c.GetComponent<Projectile>() != null) continue;
                Vector2 side = Perpendicular(dir);
                if (((GetInstanceID() + i) & 1) == 0) side = -side;
                return (side * 0.92f + dir * 0.22f).normalized;
            }
            return desired;
        }

        private Vector2 SeparationVector(Vector2 pos)
        {
            if (_neighbors == null) return Vector2.zero;
            Vector2 repel = Vector2.zero;
            float radiusSqr = TacticalNavigationDirector.SeparationRadius * TacticalNavigationDirector.SeparationRadius;
            int considered = 0;
            for (int i = 0; i < _neighbors.Length && considered < 10; i++)
            {
                EnemyTank other = _neighbors[i];
                if (other == null || other == _enemy || other.Health == null || other.Health.IsDead) continue;
                Vector2 delta = pos - (Vector2)other.transform.position;
                float sqr = delta.sqrMagnitude;
                if (sqr < 0.001f || sqr > radiusSqr) continue;
                repel += delta.normalized * (1f - Mathf.Sqrt(sqr) / TacticalNavigationDirector.SeparationRadius);
                considered++;
            }
            return repel;
        }

        private static Vector2 Cardinalize(Vector2 v)
        {
            return Mathf.Abs(v.x) >= Mathf.Abs(v.y) ? new Vector2(Mathf.Sign(v.x), 0f) : new Vector2(0f, Mathf.Sign(v.y));
        }

        private static Vector2 Perpendicular(Vector2 v) => new Vector2(-v.y, v.x);

        private static float ClassSpeed(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return 3.45f;
                case EnemyKind.Heavy: return 1.72f;
                case EnemyKind.Sniper: return 1.88f;
                case EnemyKind.Siege: return 1.50f;
                case EnemyKind.Elite: return 2.48f;
                case EnemyKind.Boss: return 1.68f;
                default: return 2.24f;
            }
        }
    }
}
