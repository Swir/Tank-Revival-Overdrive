using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum MobileFrontOperationKind
    {
        None,
        FriendlyAdvance,
        EnemyBreakthrough
    }

    /// <summary>
    /// v12.0 late-campaign combined-arms operation layer. It owns only the mobile objective and
    /// temporary navigation orders. TankGame remains round/spawn/projectile authority, Health is
    /// the only objective-damage authority, and the existing TacticalNavigationAgent/Rigidbody2D
    /// stack remains enemy movement authority.
    ///
    /// The director deliberately reads/writes the existing DynamicFrontline lane arrays through a
    /// cached reflection bridge because v9.0 did not expose per-lane scores. No parallel territory
    /// model is created: operation outcomes are folded back into the original frontline authority.
    /// </summary>
    [DefaultExecutionOrder(610)]
    public sealed class CombinedArmsMobileFrontDirector : MonoBehaviour
    {
        public const int EarliestRound = 60;
        public const int LatestRound = 99;
        public const int FirstCandidateRound = 63;
        public const int CandidateInterval = 5;
        public const int MaxRetaskedSpecialists = 8;
        public const int MinPlayableOperations = 4;
        public const float OperationDuration = 46f;
        public const float PresenceRadius = 3.10f;
        public const float AdvanceSpeed = 0.94f;
        public const float FallbackSpeed = 0.42f;
        public const float RetaskRadius = 9.50f;
        // TacticalNavigationDirector refreshes its baseline at 0.30s. v12 orders deliberately
        // reassert slightly faster and execute later (order 610 vs 420), without adding movement authority.
        public const float RetaskCadence = 0.22f;
        public const float RouteRefreshCadence = 0.70f;
        public const float GoalThreshold = 0.96f;
        public const float MinimumTimeoutProgress = 0.56f;
        public const float BreachRouteMinProgressGain = 0.04f;
        public const float ArenaXLimit = 6.0f;
        public const float ArenaYLimit = 4.65f;
        public const float FrontlineOutcomePressure = 8.0f;
        public const float FrontlinePressureMaxPerOperation = 10.0f;
        public const int NodeHealthMin = 8;
        public const int NodeHealthMax = 14;
        public const int RewardMin = 16;
        public const int RewardMax = 24;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FrontlineControlField = typeof(DynamicFrontlineTerritoryDirector).GetField("_control", PrivateInstance);
        private static readonly FieldInfo FrontlineStatesField = typeof(DynamicFrontlineTerritoryDirector).GetField("_states", PrivateInstance);
        private static CombinedArmsMobileFrontDirector _instance;

        private readonly EnemyTank[] _retaskActors = new EnemyTank[MaxRetaskedSpecialists];
        private readonly float[] _retaskDistances = new float[MaxRetaskedSpecialists];
        private TankGame _game;
        private int _round = -1;
        private int _operationLane = -1;
        private MobileFrontOperationKind _kind;
        private bool _resolved;
        private float _operationEndsAt;
        private GameObject _node;
        private Rigidbody2D _nodeBody;
        private Health _nodeHealth;
        private Vector2 _start;
        private Vector2 _goal;
        private Vector2 _routePoint;
        private bool _hasBreachRoute;
        private int _breachSequence;
        private float _nextRouteRefresh;
        private float _nextRetask;
        private int _retaskedThisBeat;
        private int _operationsStarted;
        private int _operationsWon;
        private int _operationsLost;
        private int _totalRetasks;
        private int _breachRoutesAccepted;
        private float _lastOutcomePressure;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static CombinedArmsMobileFrontDirector Instance => _instance;
        public bool IsOperationActive => _kind != MobileFrontOperationKind.None && !_resolved && _nodeHealth != null && !_nodeHealth.IsDead;
        public MobileFrontOperationKind CurrentKind => _kind;
        public int CurrentLane => _operationLane;
        public float CurrentProgress => ComputeProgress(_start, _goal, _nodeBody != null ? _nodeBody.position : _start);
        public int CurrentNodeHealth => _nodeHealth != null ? _nodeHealth.Current : 0;
        public int CurrentNodeMaxHealth => _nodeHealth != null ? _nodeHealth.Maximum : 0;
        public int CurrentBreachSequence => _hasBreachRoute ? _breachSequence : 0;
        public int RetaskedThisBeat => _retaskedThisBeat;
        public int OperationsStarted => _operationsStarted;
        public int OperationsWon => _operationsWon;
        public int OperationsLost => _operationsLost;
        public int TotalRetasks => _totalRetasks;
        public int BreachRoutesAccepted => _breachRoutesAccepted;
        public float LastOutcomePressure => _lastOutcomePressure;

        public static bool ConfigurationValid =>
            EarliestRound >= 55 && EarliestRound <= 70 && LatestRound == 99 &&
            FirstCandidateRound >= EarliestRound && CandidateInterval >= 4 && CandidateInterval <= 7 &&
            EligibleOperationCount() >= MinPlayableOperations &&
            MaxRetaskedSpecialists >= 4 && MaxRetaskedSpecialists <= 8 &&
            OperationDuration >= 38f && OperationDuration <= 55f &&
            PresenceRadius >= 2.4f && PresenceRadius <= 3.6f &&
            AdvanceSpeed >= 0.70f && AdvanceSpeed <= 1.20f &&
            FallbackSpeed >= 0.25f && FallbackSpeed < AdvanceSpeed &&
            RetaskRadius >= 7f && RetaskRadius <= 12f &&
            RetaskCadence >= 0.15f && RetaskCadence < TacticalNavigationDirector.DecisionCadence &&
            RouteRefreshCadence >= 0.45f && RouteRefreshCadence <= 1.25f &&
            GoalThreshold >= 0.90f && GoalThreshold <= 0.99f &&
            MinimumTimeoutProgress >= 0.50f && MinimumTimeoutProgress <= 0.70f &&
            BreachRouteMinProgressGain >= 0.02f && BreachRouteMinProgressGain <= 0.10f &&
            FrontlineOutcomePressure >= 5f && FrontlineOutcomePressure <= FrontlinePressureMaxPerOperation &&
            NodeHealthMin >= 6 && NodeHealthMax <= 16 && NodeHealthMin < NodeHealthMax &&
            RewardMin >= 12 && RewardMax <= 28 && RewardMin < RewardMax &&
            ReactiveCoverBreachDirector.MaxRecentBreaches <= 24 &&
            FrontlineControlField != null && FrontlineStatesField != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombinedArmsMobileFrontDirector>() != null) return;
            GameObject go = new GameObject("CombinedArmsMobileFrontDirector_v12_0");
            DontDestroyOnLoad(go);
            go.AddComponent<CombinedArmsMobileFrontDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ClearOperation();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round)
            {
                ClearOperation();
                _round = current;
                if (IsCandidateRound(current) && !MajorOperationBusy(current)) BeginOperation(current);
            }

            if (!IsOperationActive) return;
            if (Time.time >= _nextRouteRefresh)
            {
                _nextRouteRefresh = Time.time + RouteRefreshCadence;
                RefreshBreachRoute();
            }
            if (Time.time >= _nextRetask)
            {
                _nextRetask = Time.time + RetaskCadence;
                RetaskSpecialists();
            }
            if (Time.time >= _operationEndsAt) ResolveTimeout();
        }

        private void FixedUpdate()
        {
            if (!IsOperationActive || _nodeBody == null) return;

            PlayerTank player = CombatRoster.Player;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int enemiesNear = CountLivingEnemiesNear(_nodeBody.position, PresenceRadius, enemies);
            bool playerNear = player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, _nodeBody.position) <= PresenceRadius;
            bool enemyOperation = _kind == MobileFrontOperationKind.EnemyBreakthrough;
            int support = enemyOperation ? Mathf.Min(enemiesNear, 3) : (playerNear ? 1 : 0);
            int opposition = enemyOperation ? (playerNear ? 1 : 0) : Mathf.Min(enemiesNear, 2);
            int sign = ComputePresenceDelta(enemyOperation, support, opposition);
            if (sign == 0) return;

            Vector2 destination = sign > 0 ? CurrentRouteDestination() : _start;
            float speed = sign > 0 ? AdvanceSpeed * (1f + Mathf.Max(0, support - 1) * 0.08f) : FallbackSpeed;
            Vector2 next = Vector2.MoveTowards(_nodeBody.position, destination, speed * Time.fixedDeltaTime);
            _nodeBody.MovePosition(next);

            if (sign > 0 && CurrentProgress >= GoalThreshold)
                ResolveOperation(!enemyOperation, enemyOperation ? "ENEMY BREAKTHROUGH REACHED ORZELEK LINE" : "MOBILE FRONT SECURED");
        }

        public static bool IsCandidateRound(int round)
        {
            if (round < EarliestRound || round > LatestRound || round % 10 == 0) return false;
            if ((round - FirstCandidateRound) % CandidateInterval != 0) return false;
            if (DynamicFrontlineTerritoryDirector.HasOperationForRound(round)) return false;
            if (CombinedArmsCampaignCommandDirector.HasCommandOperationForRound(round)) return false;
            if (MultiStageOperationDirector.HasOperationForRound(round)) return false;
            if (CombinedArmsDirector.HasOperationForRound(round)) return false;
            return true;
        }

        public static int EligibleOperationCount()
        {
            int count = 0;
            for (int round = 1; round <= 100; round++) if (IsCandidateRound(round)) count++;
            return count;
        }

        public static bool IsSpecialist(EnemyKind kind) =>
            kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite;

        public static SquadTacticalRole RoleForSpecialist(EnemyKind kind, bool enemyOperation)
        {
            switch (kind)
            {
                case EnemyKind.Heavy: return enemyOperation ? SquadTacticalRole.Escort : SquadTacticalRole.Breaker;
                case EnemyKind.Sniper: return SquadTacticalRole.Suppressor;
                case EnemyKind.Siege: return SquadTacticalRole.Breaker;
                case EnemyKind.Elite: return enemyOperation ? SquadTacticalRole.Escort : SquadTacticalRole.Flanker;
                default: return SquadTacticalRole.Breaker;
            }
        }

        public static float StandoffForSpecialist(EnemyKind kind, bool enemyOperation)
        {
            switch (kind)
            {
                case EnemyKind.Sniper: return 5.2f;
                case EnemyKind.Siege: return 2.45f;
                case EnemyKind.Heavy: return enemyOperation ? 1.55f : 1.05f;
                case EnemyKind.Elite: return enemyOperation ? 1.45f : 1.20f;
                default: return 1.15f;
            }
        }

        public static Vector2 SpecialistOffset(EnemyKind kind, bool enemyOperation, int slot)
        {
            float side = (slot & 1) == 0 ? -1f : 1f;
            float lateral;
            float depth;
            switch (kind)
            {
                case EnemyKind.Heavy:
                    lateral = 0.64f + (slot / 2) * 0.22f;
                    depth = enemyOperation ? 0.92f : 1.02f;
                    break;
                case EnemyKind.Sniper:
                    lateral = 1.35f + (slot / 2) * 0.28f;
                    depth = enemyOperation ? -1.35f : 0.55f;
                    break;
                case EnemyKind.Siege:
                    lateral = 1.05f + (slot / 2) * 0.24f;
                    depth = enemyOperation ? -0.82f : 0.42f;
                    break;
                case EnemyKind.Elite:
                    lateral = 0.92f + (slot / 2) * 0.31f;
                    depth = enemyOperation ? 0.72f : 1.18f;
                    break;
                default:
                    lateral = 0.80f;
                    depth = 0.80f;
                    break;
            }
            float forward = enemyOperation ? -1f : 1f;
            return new Vector2(side * lateral, forward * depth);
        }

        public static int NodeHitPointsForRound(int round) => Mathf.Clamp(NodeHealthMin + Mathf.Max(0, round - EarliestRound) / 12, NodeHealthMin, NodeHealthMax);
        public static int RewardForRound(int round) => Mathf.Clamp(RewardMin + Mathf.Max(0, round - EarliestRound) / 8, RewardMin, RewardMax);

        public static int ComputePresenceDelta(bool enemyOperation, int support, int opposition)
        {
            int boundedSupport = Mathf.Clamp(support, 0, enemyOperation ? 3 : 1);
            int boundedOpposition = Mathf.Clamp(opposition, 0, enemyOperation ? 1 : 2);
            if (boundedSupport > boundedOpposition) return 1;
            if (boundedSupport < boundedOpposition) return -1;
            return 0;
        }

        public static float ComputeProgress(Vector2 start, Vector2 goal, Vector2 current)
        {
            Vector2 axis = goal - start;
            float denom = axis.sqrMagnitude;
            return denom <= 0.001f ? 0f : Mathf.Clamp01(Vector2.Dot(current - start, axis) / denom);
        }

        public static bool IsForwardRoutePoint(Vector2 start, Vector2 goal, Vector2 current, Vector2 candidate)
        {
            float currentProgress = ComputeProgress(start, goal, current);
            float candidateProgress = ComputeProgress(start, goal, candidate);
            return candidateProgress >= currentProgress + BreachRouteMinProgressGain && candidateProgress < GoalThreshold;
        }

        public static Vector2 ClampRoutePoint(Vector2 point) => new Vector2(Mathf.Clamp(point.x, -ArenaXLimit, ArenaXLimit), Mathf.Clamp(point.y, -ArenaYLimit, ArenaYLimit));

        public static float ProjectedLaneScore(float current, Team pressureSide, float amount)
        {
            float bounded = Mathf.Clamp(amount, 0f, FrontlinePressureMaxPerOperation);
            if (pressureSide == Team.Player) return Mathf.Clamp(current + bounded, 0f, 100f);
            if (pressureSide == Team.Enemy) return Mathf.Clamp(current - bounded, 0f, 100f);
            return Mathf.Clamp(current, 0f, 100f);
        }

        public static float FrontlineScoreForLane(int lane)
        {
            lane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            if (frontline == null || FrontlineControlField == null) return 50f;
            float[] scores = FrontlineControlField.GetValue(frontline) as float[];
            return scores != null && lane < scores.Length ? scores[lane] : 50f;
        }

        public static int SelectOperationLane(int round, bool enemyOperation)
        {
            int lanes = DynamicFrontlineTerritoryDirector.LaneCount;
            int start = Mathf.Abs(round / Mathf.Max(1, CandidateInterval)) % lanes;
            int best = start;
            float bestScore = FrontlineScoreForLane(best);
            for (int step = 1; step < lanes; step++)
            {
                int lane = (start + step) % lanes;
                float score = FrontlineScoreForLane(lane);
                bool better = enemyOperation ? score > bestScore + 0.01f : score < bestScore - 0.01f;
                if (!better) continue;
                best = lane;
                bestScore = score;
            }
            return best;
        }

        public static bool ApplyFrontlinePressure(int lane, Team pressureSide, float amount)
        {
            if (pressureSide == Team.Neutral) return false;
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            if (frontline == null || FrontlineControlField == null || FrontlineStatesField == null) return false;
            float[] scores = FrontlineControlField.GetValue(frontline) as float[];
            FrontlineControlState[] states = FrontlineStatesField.GetValue(frontline) as FrontlineControlState[];
            if (scores == null || states == null || scores.Length < DynamicFrontlineTerritoryDirector.LaneCount || states.Length < DynamicFrontlineTerritoryDirector.LaneCount) return false;
            lane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            scores[lane] = ProjectedLaneScore(scores[lane], pressureSide, amount);
            states[lane] = DynamicFrontlineTerritoryDirector.StateForScore(scores[lane]);
            return true;
        }

        private static bool MajorOperationBusy(int round)
        {
            if (DynamicFrontlineTerritoryDirector.Instance != null && DynamicFrontlineTerritoryDirector.Instance.OperationActive) return true;
            CombinedArmsCampaignCommandDirector command = CombinedArmsCampaignCommandDirector.Instance;
            if (command != null && command.CurrentPhase != CampaignCommandPhase.None && command.CurrentPhase != CampaignCommandPhase.Resolved) return true;
            return MultiStageOperationDirector.HasOperationForRound(round) || CombinedArmsDirector.HasOperationForRound(round);
        }

        private void BeginOperation(int round)
        {
            int pressure = 0;
            DynamicFrontlineTerritoryDirector frontline = DynamicFrontlineTerritoryDirector.Instance;
            if (frontline != null) pressure += frontline.EnemyLanes - frontline.FriendlyLanes;
            CombinedArmsCampaignCommandDirector command = CombinedArmsCampaignCommandDirector.Instance;
            if (command != null) pressure += command.CommandMomentum < 0 ? 1 : command.CommandMomentum > 0 ? -1 : 0;
            bool enemyOperation = pressure > 0 || (pressure == 0 && ((round / CandidateInterval) & 1) == 1);
            _kind = enemyOperation ? MobileFrontOperationKind.EnemyBreakthrough : MobileFrontOperationKind.FriendlyAdvance;
            _resolved = false;
            _operationEndsAt = Time.time + OperationDuration;
            _nextRouteRefresh = Time.time;
            _nextRetask = Time.time + 0.15f;
            _retaskedThisBeat = 0;
            _hasBreachRoute = false;
            _breachSequence = 0;
            _lastOutcomePressure = 0f;
            _operationsStarted++;

            _operationLane = SelectOperationLane(round, enemyOperation);
            float laneX = DynamicFrontlineTerritoryDirector.LanePosition(_operationLane).x;
            _start = new Vector2(laneX, enemyOperation ? ArenaYLimit : -ArenaYLimit);
            _goal = new Vector2(laneX, enemyOperation ? -ArenaYLimit : ArenaYLimit);
            CreateCommandPost(enemyOperation ? Team.Enemy : Team.Player, NodeHitPointsForRound(round));
            RefreshBreachRoute();

            _status = enemyOperation ? "ENEMY MOBILE FRONT // INTERDICT THE COMMAND POST" : "FRIENDLY MOBILE FRONT // ESCORT THE COMMAND POST";
            _statusUntil = Time.unscaledTime + 4.5f;
            VisualFactory.RingPulse(_start, enemyOperation ? new Color(1f, 0.24f, 0.10f) : new Color(0.18f, 0.92f, 1f), 2.1f);
            BattleAudio.PlayGlobal(enemyOperation ? SoundCue.EagleAlarm : SoundCue.RoundClear, 0.40f, 0.01f);
        }

        private void CreateCommandPost(Team team, int hp)
        {
            _node = new GameObject(team == Team.Enemy ? "ENEMY_MOBILE_COMMAND_POST_v12_0" : "FRIENDLY_MOBILE_COMMAND_POST_v12_0");
            _node.transform.position = _start;
            Color baseColor = team == Team.Enemy ? new Color(0.82f, 0.10f, 0.08f) : new Color(0.08f, 0.62f, 0.92f);
            VisualFactory.Rect("CommandHull", _node.transform, new Vector2(1.18f, 0.76f), baseColor, Vector3.zero, 7);
            VisualFactory.Rect("CommandCore", _node.transform, new Vector2(0.52f, 0.52f), Color.white, Vector3.zero, 8);
            BoxCollider2D collider = _node.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.05f, 0.70f);
            _nodeBody = _node.AddComponent<Rigidbody2D>();
            _nodeBody.bodyType = RigidbodyType2D.Kinematic;
            _nodeBody.gravityScale = 0f;
            _nodeBody.freezeRotation = true;
            _nodeBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            _nodeBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _nodeHealth = _node.AddComponent<Health>();
            _nodeHealth.Initialize(team, hp);
            _nodeHealth.Died += OnCommandPostDestroyed;
        }

        private void OnCommandPostDestroyed(Health health)
        {
            if (_resolved || _kind == MobileFrontOperationKind.None) return;
            bool playerSuccess = _kind == MobileFrontOperationKind.EnemyBreakthrough;
            ResolveOperation(playerSuccess, playerSuccess ? "ENEMY MOBILE COMMAND POST DESTROYED" : "FRIENDLY MOBILE COMMAND POST LOST");
        }

        private void RefreshBreachRoute()
        {
            if (_nodeBody == null) return;
            Team team = _kind == MobileFrontOperationKind.EnemyBreakthrough ? Team.Enemy : Team.Player;
            if (_hasBreachRoute && ReactiveCoverBreachDirector.IsBreachActive(_breachSequence) && Vector2.Distance(_nodeBody.position, _routePoint) > 0.85f) return;
            _hasBreachRoute = false;
            _breachSequence = 0;
            if (!ReactiveCoverBreachDirector.TryFindBestBreach(_nodeBody.position, _goal, team, out ReactiveCoverBreachDirector.BreachSnapshot breach)) return;
            Vector2 candidate = ClampRoutePoint(breach.Position);
            if (!IsForwardRoutePoint(_start, _goal, _nodeBody.position, candidate)) return;
            _routePoint = candidate;
            _breachSequence = breach.Sequence;
            _hasBreachRoute = ReactiveCoverBreachDirector.IsBreachActive(_breachSequence);
            if (_hasBreachRoute) _breachRoutesAccepted++;
        }

        private Vector2 CurrentRouteDestination()
        {
            if (_hasBreachRoute && ReactiveCoverBreachDirector.IsBreachActive(_breachSequence) && Vector2.Distance(_nodeBody.position, _routePoint) > 0.75f) return _routePoint;
            _hasBreachRoute = false;
            _breachSequence = 0;
            return _goal;
        }

        private void RetaskSpecialists()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) { _retaskedThisBeat = 0; return; }
            bool enemyOperation = _kind == MobileFrontOperationKind.EnemyBreakthrough;
            Vector2 anchor = _nodeBody != null ? _nodeBody.position : _start;
            int selected = SelectNearestSpecialists(enemies, anchor);
            int ordered = 0;

            for (int i = 0; i < selected; i++)
            {
                EnemyTank enemy = _retaskActors[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                TacticalNavigationAgent navigation = enemy.GetComponent<TacticalNavigationAgent>();
                if (navigation == null) continue;
                Vector2 offset = SpecialistOffset(enemy.Kind, enemyOperation, ordered);
                Vector2 target = ClampRoutePoint(anchor + offset);
                navigation.SetRole(RoleForSpecialist(enemy.Kind, enemyOperation));
                navigation.SetOrder(target, StandoffForSpecialist(enemy.Kind, enemyOperation), enemy.Kind == EnemyKind.Sniper ? 0.84f : enemy.Kind == EnemyKind.Heavy ? 0.92f : 1.08f, enemies);
                ordered++;
            }

            _retaskedThisBeat = ordered;
            _totalRetasks += ordered;
        }

        private int SelectNearestSpecialists(EnemyTank[] enemies, Vector2 anchor)
        {
            for (int i = 0; i < MaxRetaskedSpecialists; i++)
            {
                _retaskActors[i] = null;
                _retaskDistances[i] = float.MaxValue;
            }

            int count = 0;
            float maxDistanceSq = RetaskRadius * RetaskRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsSpecialist(enemy.Kind)) continue;
                if (enemy.GetComponent<TacticalNavigationAgent>() == null) continue;
                float distanceSq = ((Vector2)enemy.transform.position - anchor).sqrMagnitude;
                if (distanceSq > maxDistanceSq) continue;

                int insert;
                if (count < MaxRetaskedSpecialists)
                {
                    insert = count;
                    count++;
                }
                else
                {
                    if (distanceSq >= _retaskDistances[MaxRetaskedSpecialists - 1]) continue;
                    insert = MaxRetaskedSpecialists - 1;
                }

                while (insert > 0 && distanceSq < _retaskDistances[insert - 1])
                {
                    if (insert < MaxRetaskedSpecialists)
                    {
                        _retaskDistances[insert] = _retaskDistances[insert - 1];
                        _retaskActors[insert] = _retaskActors[insert - 1];
                    }
                    insert--;
                }
                _retaskDistances[insert] = distanceSq;
                _retaskActors[insert] = enemy;
            }
            return count;
        }

        private static int CountLivingEnemiesNear(Vector2 position, float radius, EnemyTank[] enemies)
        {
            if (enemies == null) return 0;
            int count = 0;
            float radiusSq = radius * radius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                if (((Vector2)enemy.transform.position - position).sqrMagnitude <= radiusSq) count++;
            }
            return count;
        }

        private void ResolveTimeout()
        {
            float progress = CurrentProgress;
            bool playerSuccess = _kind == MobileFrontOperationKind.EnemyBreakthrough ? progress < MinimumTimeoutProgress : progress >= MinimumTimeoutProgress;
            ResolveOperation(playerSuccess, playerSuccess ? "MOBILE FRONT HELD" : "MOBILE FRONT STALLED");
        }

        private void ResolveOperation(bool playerSuccess, string reason)
        {
            if (_resolved) return;
            _resolved = true;
            Vector2 effectPosition = _nodeBody != null ? _nodeBody.position : _goal;
            Team pressureSide = playerSuccess ? Team.Player : Team.Enemy;
            if (_operationLane >= 0 && ApplyFrontlinePressure(_operationLane, pressureSide, FrontlineOutcomePressure))
                _lastOutcomePressure = pressureSide == Team.Player ? FrontlineOutcomePressure : -FrontlineOutcomePressure;

            if (playerSuccess)
            {
                _operationsWon++;
                WarEconomyDirector.AwardMissionBonds(RewardForRound(_round), reason);
                if (_game != null) _game.RepairEagle(1);
                PlayerTank player = CombatRoster.Player;
                if (player != null)
                {
                    if (player.Health != null && !player.Health.IsDead) player.Health.Heal(1);
                    player.AddAmmo(AmmoType.ArmorPiercing, 1);
                    player.AddAmmo(AmmoType.Explosive, 1);
                }
                VisualFactory.RingPulse(effectPosition, new Color(0.14f, 1f, 0.55f), 2.8f);
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.48f, 0.02f);
            }
            else
            {
                _operationsLost++;
                VisualFactory.RingPulse(effectPosition, new Color(1f, 0.18f, 0.08f), 2.8f);
                BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.42f, 0.00f);
            }
            _status = reason + (_lastOutcomePressure == 0f ? string.Empty : " // FRONTLINE SHIFT " + (_lastOutcomePressure > 0f ? "+" : string.Empty) + _lastOutcomePressure.ToString("0"));
            _statusUntil = Time.unscaledTime + 4.0f;
            RemoveCommandPost();
        }

        private void RemoveCommandPost()
        {
            if (_nodeHealth != null) _nodeHealth.Died -= OnCommandPostDestroyed;
            if (_node != null) Destroy(_node);
            _node = null;
            _nodeBody = null;
            _nodeHealth = null;
        }

        private void ClearOperation()
        {
            RemoveCommandPost();
            _kind = MobileFrontOperationKind.None;
            _resolved = false;
            _hasBreachRoute = false;
            _breachSequence = 0;
            _retaskedThisBeat = 0;
            _operationLane = -1;
        }

        private void ResetRun()
        {
            ClearOperation();
            _round = -1;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.50f, 0.90f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!IsOperationActive && Time.unscaledTime >= _statusUntil) return;
            EnsureStyles();
            float width = 560f;
            float x = Screen.width * 0.5f - width * 0.5f;
            GUI.color = new Color(0.018f, 0.035f, 0.050f, 0.88f);
            GUI.Box(new Rect(x, 18f, width, IsOperationActive ? 58f : 34f), string.Empty);
            GUI.color = Color.white;
            string title = IsOperationActive ? (_kind == MobileFrontOperationKind.EnemyBreakthrough ? "ENEMY MOBILE FRONT" : "FRIENDLY MOBILE FRONT") : _status;
            GUI.Label(new Rect(x + 10f, 22f, width - 20f, 20f), title, _header);
            if (IsOperationActive)
            {
                string route = _hasBreachRoute ? " // BREACH " + _breachSequence : string.Empty;
                GUI.Label(new Rect(x + 10f, 43f, width - 20f, 20f), "LANE " + (_operationLane + 1) + " // ADVANCE " + Mathf.RoundToInt(CurrentProgress * 100f) + "% // CP " + CurrentNodeHealth + "/" + CurrentNodeMaxHealth + " // ESCORT " + _retaskedThisBeat + route, _body);
            }
        }
    }
}
