using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum LogisticsRoutePlan
    {
        Direct,
        WesternHook,
        EasternHook
    }

    public enum RouteIntelState
    {
        Unknown,
        Contact,
        Verified
    }

    /// <summary>
    /// v12.2 tactical route/intelligence layer for the physical v12.1 operational sustainment column.
    /// It never moves the real column, fires projectiles, applies damage, or owns rewards. Instead it
    /// writes only the existing OperationalSustainmentDirector lane intent; that director and its
    /// canonical kinematic Rigidbody2D remain the sole movement authority for the real logistics vehicle.
    /// A bounded physical decoy has canonical Health/collision but carries no manifest and grants no reward.
    /// Existing EnemyTank actors are temporarily retasked through TacticalNavigationAgent for screens/ambushes.
    /// </summary>
    [DefaultExecutionOrder(625)]
    public sealed class LogisticsRouteIntelligenceDirector : MonoBehaviour
    {
        public const int RouteCount = 3;
        public const int MaxReroutes = 2;
        public const int MaxDecoys = 1;
        public const int MaxAmbushActors = 3;
        public const int MaxThreatActorsSampled = 12;
        public const float ThreatRefreshSeconds = 0.90f;
        public const float RerouteCooldownSeconds = 4.50f;
        public const float RerouteThreatDelta = 0.72f;
        public const float ForceRerouteThreat = 2.65f;
        public const float ThreatRadius = 7.25f;
        public const float BreachProbeRadius = 3.40f;
        public const float ContactRevealRadius = 6.25f;
        public const float VerifiedRevealRadius = 3.20f;
        public const float DecoyRevealRadius = 2.30f;
        public const float AmbushSearchRadius = 10.0f;
        public const float AmbushCadence = 0.24f;
        public const int DecoyHealthMin = 3;
        public const int DecoyHealthMax = 6;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo SustainmentLaneField = typeof(OperationalSustainmentDirector).GetField("_lane", PrivateInstance);
        private static readonly FieldInfo SustainmentBodyField = typeof(OperationalSustainmentDirector).GetField("_body", PrivateInstance);

        private static LogisticsRouteIntelligenceDirector _instance;
        private readonly EnemyTank[] _routeActors = new EnemyTank[MaxAmbushActors];
        private readonly float[] _routeActorDistances = new float[MaxAmbushActors];
        private TankGame _game;
        private OperationalSustainmentDirector _sustainment;
        private CombinedArmsMobileFrontDirector _front;
        private Rigidbody2D _leadBody;
        private bool _tracking;
        private Team _team = Team.Neutral;
        private int _round = -1;
        private int _baseLane = -1;
        private int _routeLane = -1;
        private LogisticsRoutePlan _routePlan;
        private RouteIntelState _intel;
        private int _reroutes;
        private int _totalReroutes;
        private int _decoysSpawned;
        private int _decoysDestroyed;
        private int _intelTransitions;
        private int _ambushOrders;
        private int _threatSamples;
        private int _routeActorCount;
        private float _lastThreat;
        private float _nextThreatRefresh;
        private float _nextRerouteAt;
        private float _nextAmbushOrder;
        private GameObject _decoy;
        private Health _decoyHealth;
        private int _decoyLane = -1;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _bodyStyle;

        public static LogisticsRouteIntelligenceDirector Instance => _instance;
        public bool RouteActive => _tracking && _sustainment != null && _sustainment.ColumnActive;
        public LogisticsRoutePlan CurrentPlan => _routePlan;
        public RouteIntelState IntelState => _intel;
        public int CurrentRouteLane => _routeLane;
        public int ReroutesThisColumn => _reroutes;
        public int TotalReroutes => _totalReroutes;
        public int DecoysSpawned => _decoysSpawned;
        public int DecoysDestroyed => _decoysDestroyed;
        public int IntelTransitions => _intelTransitions;
        public int AmbushOrders => _ambushOrders;
        public int ThreatSamples => _threatSamples;
        public int RouteActorCount => _routeActorCount;
        public float CurrentThreat => _lastThreat;
        public bool DecoyActive => _decoy != null && _decoyHealth != null && !_decoyHealth.IsDead;

        public static bool BridgeAvailable => SustainmentLaneField != null && SustainmentBodyField != null;

        public static bool ConfigurationValid =>
            RouteCount == 3 && MaxReroutes == 2 && MaxDecoys == 1 &&
            MaxAmbushActors >= 2 && MaxAmbushActors <= 3 && MaxThreatActorsSampled <= 16 &&
            ThreatRefreshSeconds >= 0.6f && ThreatRefreshSeconds <= 1.2f &&
            RerouteCooldownSeconds >= 3.0f && RerouteCooldownSeconds <= 6.0f &&
            RerouteThreatDelta >= 0.5f && RerouteThreatDelta <= 1.0f &&
            ForceRerouteThreat >= 2.0f && ForceRerouteThreat <= 3.5f &&
            ThreatRadius >= 6f && ThreatRadius <= 9f && BreachProbeRadius <= 4f &&
            ContactRevealRadius > VerifiedRevealRadius && VerifiedRevealRadius > DecoyRevealRadius &&
            AmbushSearchRadius >= 8f && AmbushSearchRadius <= 12f &&
            AmbushCadence >= 0.18f && AmbushCadence < TacticalNavigationDirector.DecisionCadence &&
            DecoyHealthMin >= 2 && DecoyHealthMax <= 7 && DecoyHealthMin < DecoyHealthMax &&
            BridgeAvailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<LogisticsRouteIntelligenceDirector>() != null) return;
            GameObject go = new GameObject("LogisticsRouteIntelligenceDirector_v12_2");
            DontDestroyOnLoad(go);
            go.AddComponent<LogisticsRouteIntelligenceDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ClearRouteState();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_sustainment == null) _sustainment = OperationalSustainmentDirector.Instance;
            if (_front == null) _front = CombinedArmsMobileFrontDirector.Instance;

            if (_game == null || !_game.IsPlaying)
            {
                if (_tracking || _round >= 0) ResetRun();
                return;
            }

            int currentRound = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_round != currentRound)
            {
                if (_tracking) ClearRouteState();
                _round = currentRound;
            }

            bool active = _sustainment != null && _front != null && _front.IsOperationActive && _sustainment.ColumnActive;
            if (!active)
            {
                if (_tracking) ClearRouteState();
                return;
            }

            if (!_tracking) BeginRoute(currentRound);
            if (!_tracking) return;

            if (_leadBody == null && SustainmentBodyField != null)
                _leadBody = SustainmentBodyField.GetValue(_sustainment) as Rigidbody2D;

            UpdateIntel();

            if (Time.time >= _nextThreatRefresh)
            {
                _nextThreatRefresh = Time.time + ThreatRefreshSeconds;
                EvaluateRouteThreat();
            }

            if (Time.time >= _nextAmbushOrder)
            {
                _nextAmbushOrder = Time.time + AmbushCadence;
                AssignRouteCell();
            }
        }

        private void BeginRoute(int round)
        {
            if (_sustainment == null || _front == null || !_sustainment.ColumnActive || !_front.IsOperationActive || !BridgeAvailable) return;
            _tracking = true;
            _team = _sustainment.ColumnTeam;
            _baseLane = Mathf.Clamp(_front.CurrentLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            _routePlan = InitialPlanForRound(round, _team, _baseLane);
            _routeLane = RouteLaneForPlan(_baseLane, _routePlan);
            _intel = _team == Team.Enemy ? RouteIntelState.Unknown : RouteIntelState.Verified;
            _reroutes = 0;
            _routeActorCount = 0;
            _lastThreat = 0f;
            _leadBody = SustainmentBodyField.GetValue(_sustainment) as Rigidbody2D;
            ApplyRouteLane(_routeLane);
            _nextThreatRefresh = Time.time + 0.15f;
            _nextRerouteAt = Time.time + RerouteCooldownSeconds;
            _nextAmbushOrder = Time.time;

            if (_team == Team.Enemy && ShouldSpawnDecoy(round, _baseLane)) SpawnDecoy(round);
            ShowStatus(_team == Team.Enemy ? "ROUTE INTELLIGENCE // MULTIPLE LOGISTICS CONTACTS" : "ORZELEK ROUTE CONTROL // ESCORT CORRIDOR LIVE", 4.0f);
        }

        private void EvaluateRouteThreat()
        {
            if (!_tracking || _front == null || !_front.IsOperationActive) return;
            _threatSamples++;

            float currentScore = ScorePlan(_routePlan);
            LogisticsRoutePlan bestPlan = _routePlan;
            float bestScore = currentScore;
            for (int i = 0; i < RouteCount; i++)
            {
                LogisticsRoutePlan plan = (LogisticsRoutePlan)i;
                float score = ScorePlan(plan);
                if (score + 0.001f >= bestScore) continue;
                bestScore = score;
                bestPlan = plan;
            }
            _lastThreat = currentScore;

            if (_reroutes >= MaxReroutes || Time.time < _nextRerouteAt || bestPlan == _routePlan) return;
            if (currentScore - bestScore < RerouteThreatDelta && currentScore < ForceRerouteThreat) return;

            int nextLane = RouteLaneForPlan(_baseLane, bestPlan);
            if (nextLane == _routeLane) return;
            _routePlan = bestPlan;
            _routeLane = nextLane;
            _reroutes++;
            _totalReroutes++;
            _nextRerouteAt = Time.time + RerouteCooldownSeconds;
            ApplyRouteLane(_routeLane);
            if (_leadBody != null) VisualFactory.RingPulse(_leadBody.position, _team == Team.Enemy ? new Color(1f, 0.42f, 0.08f) : new Color(0.16f, 0.82f, 1f), 1.05f);
            ShowStatus((_team == Team.Enemy ? "ENEMY" : "ORZELEK") + " LOGISTICS REROUTE // " + PlanLabel(_routePlan), 3.0f);
        }

        private float ScorePlan(LogisticsRoutePlan plan)
        {
            int lane = RouteLaneForPlan(_baseLane, plan);
            float progress = _front != null ? _front.CurrentProgress : 0.5f;
            Vector2 anchor = OperationalSustainmentDirector.SupportAnchor(lane, progress, _team == Team.Enemy);
            float score = plan == _routePlan ? -0.12f : 0f;

            if (_team == Team.Enemy)
            {
                PlayerTank player = CombatRoster.Player;
                if (player != null && player.Health != null && !player.Health.IsDead)
                    score += ProximityThreat(Vector2.Distance(player.transform.position, anchor));
            }
            else
            {
                EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
                int sampled = 0;
                if (enemies != null)
                {
                    for (int i = 0; i < enemies.Length && sampled < MaxThreatActorsSampled; i++)
                    {
                        EnemyTank enemy = enemies[i];
                        if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                        float distance = Vector2.Distance(enemy.transform.position, anchor);
                        if (distance > ThreatRadius) continue;
                        score += ProximityThreat(distance) * (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite ? 0.42f : 0.26f);
                        sampled++;
                    }
                }
            }

            Team opposing = _team == Team.Enemy ? Team.Player : Team.Enemy;
            if (ReactiveCoverBreachDirector.TryFindRecentBreachNear(anchor, opposing, BreachProbeRadius, out ReactiveCoverBreachDirector.BreachSnapshot hostile) && hostile.Valid)
                score += BreachThreatContribution(hostile.AttackerTeam, _team);
            if (ReactiveCoverBreachDirector.TryFindRecentBreachNear(anchor, _team, BreachProbeRadius, out ReactiveCoverBreachDirector.BreachSnapshot friendly) && friendly.Valid)
                score += BreachThreatContribution(friendly.AttackerTeam, _team);
            return Mathf.Max(-1f, score);
        }

        private void ApplyRouteLane(int lane)
        {
            if (_sustainment == null || SustainmentLaneField == null) return;
            SustainmentLaneField.SetValue(_sustainment, Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1));
        }

        private void UpdateIntel()
        {
            if (!_tracking) return;
            if (_team != Team.Enemy)
            {
                SetIntel(RouteIntelState.Verified);
                return;
            }

            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;
            Vector2 playerPos = player.transform.position;
            float realDistance = _leadBody != null ? Vector2.Distance(playerPos, _leadBody.position) : float.MaxValue;
            float decoyDistance = DecoyActive ? Vector2.Distance(playerPos, _decoy.transform.position) : float.MaxValue;

            if (realDistance <= VerifiedRevealRadius || (_decoy != null && !DecoyActive)) SetIntel(RouteIntelState.Verified);
            else if (realDistance <= ContactRevealRadius || decoyDistance <= ContactRevealRadius) SetIntel(RouteIntelState.Contact);
        }

        private void SetIntel(RouteIntelState state)
        {
            if (state <= _intel) return;
            _intel = state;
            _intelTransitions++;
            if (state == RouteIntelState.Contact) ShowStatus("LOGISTICS CONTACT ACQUIRED // IDENTITY UNCONFIRMED", 2.8f);
            else if (state == RouteIntelState.Verified) ShowStatus("ROUTE VERIFIED // REAL SUSTAINMENT " + PlanLabel(_routePlan), 3.4f);
        }

        private void SpawnDecoy(int round)
        {
            if (_decoy != null || MaxDecoys < 1 || _front == null) return;
            LogisticsRoutePlan decoyPlan = AlternatePlan(_routePlan, _baseLane);
            _decoyLane = RouteLaneForPlan(_baseLane, decoyPlan);
            Vector2 position = OperationalSustainmentDirector.SupportAnchor(_decoyLane, Mathf.Clamp01(_front.CurrentProgress + 0.08f), true);

            _decoy = new GameObject("ENEMY_LOGISTICS_CONTACT_DECOY_v12_2");
            _decoy.transform.position = position;
            Color body = new Color(0.50f, 0.13f, 0.075f);
            Color accent = new Color(1f, 0.46f, 0.08f);
            VisualFactory.Rect("DecoyHull", _decoy.transform, new Vector2(1.16f, 0.70f), body, Vector3.zero, 8);
            VisualFactory.Rect("DecoyPodA", _decoy.transform, new Vector2(0.28f, 0.38f), accent, new Vector3(-0.22f, 0.04f, 0f), 9);
            VisualFactory.Rect("DecoyPodB", _decoy.transform, new Vector2(0.28f, 0.38f), new Color(0.72f, 0.24f, 0.07f), new Vector3(0.22f, 0.04f, 0f), 9);
            VisualFactory.Disc("DecoyBeacon", _decoy.transform, new Vector2(0.16f, 0.16f), accent, new Vector3(0f, 0.45f, 0f), 10);
            BoxCollider2D collider = _decoy.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.06f, 0.64f);
            Rigidbody2D body2d = _decoy.AddComponent<Rigidbody2D>();
            body2d.bodyType = RigidbodyType2D.Kinematic;
            body2d.gravityScale = 0f;
            body2d.freezeRotation = true;
            _decoyHealth = _decoy.AddComponent<Health>();
            _decoyHealth.Initialize(Team.Enemy, DecoyHealthForRound(round));
            _decoyHealth.Damaged += OnDecoyDamaged;
            _decoyHealth.Died += OnDecoyDied;
            _decoysSpawned++;
            VisualFactory.RingPulse(position, accent, 1.2f);
        }

        private void OnDecoyDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("LOGISTICS CONTACT HIT // MANIFEST SIGNATURE UNCLEAR", 2.0f);
        }

        private void OnDecoyDied(Health health)
        {
            _decoysDestroyed++;
            SetIntel(RouteIntelState.Verified);
            if (_decoy != null) VisualFactory.Explosion(_decoy.transform.position, new Color(1f, 0.38f, 0.08f), 0.75f);
            ShowStatus("DECOY LOGISTICS CONTACT DESTROYED // REAL ROUTE IDENTIFIED", 3.4f);
        }

        private void AssignRouteCell()
        {
            _routeActorCount = 0;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;
            Vector2 anchor = _leadBody != null ? _leadBody.position : OperationalSustainmentDirector.SupportAnchor(_routeLane, _front != null ? _front.CurrentProgress : 0.5f, _team == Team.Enemy);
            for (int i = 0; i < MaxAmbushActors; i++) { _routeActors[i] = null; _routeActorDistances[i] = float.MaxValue; }

            int count = 0;
            float limitSq = AmbushSearchRadius * AmbushSearchRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsRouteActorKind(enemy.Kind, _team)) continue;
                float d = ((Vector2)enemy.transform.position - anchor).sqrMagnitude;
                if (d > limitSq) continue;

                int insert;
                if (count < MaxAmbushActors) { insert = count; count++; }
                else
                {
                    if (d >= _routeActorDistances[MaxAmbushActors - 1]) continue;
                    insert = MaxAmbushActors - 1;
                }
                while (insert > 0 && d < _routeActorDistances[insert - 1])
                {
                    _routeActorDistances[insert] = _routeActorDistances[insert - 1];
                    _routeActors[insert] = _routeActors[insert - 1];
                    insert--;
                }
                _routeActorDistances[insert] = d;
                _routeActors[insert] = enemy;
            }

            _routeActorCount = count;
            for (int i = 0; i < count; i++)
            {
                EnemyTank actor = _routeActors[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                TacticalNavigationAgent nav = actor.GetComponent<TacticalNavigationAgent>();
                if (nav == null)
                {
                    nav = actor.gameObject.AddComponent<TacticalNavigationAgent>();
                    nav.Initialize(actor);
                }

                SquadTacticalRole role = RouteRole(actor.Kind, _team);
                float side = (i & 1) == 0 ? -1f : 1f;
                Vector2 objective = anchor + new Vector2(side * (1.15f + i * 0.32f), _team == Team.Enemy ? -0.55f : 0.55f);
                float standoff = actor.Kind == EnemyKind.Sniper ? 5.5f : 1.55f + i * 0.16f;
                float speed = actor.Kind == EnemyKind.Fast ? 1.10f : actor.Kind == EnemyKind.Elite ? 1.04f : 0.88f;
                nav.SetRole(role);
                nav.SetOrder(objective, standoff, speed, enemies);
                _ambushOrders++;
            }
        }

        public static bool IsRouteActorKind(EnemyKind kind, Team logisticsTeam)
        {
            if (logisticsTeam == Team.Enemy) return kind == EnemyKind.Sniper;
            return kind == EnemyKind.Fast || kind == EnemyKind.Elite || kind == EnemyKind.Sniper;
        }

        public static SquadTacticalRole RouteRole(EnemyKind kind, Team logisticsTeam)
        {
            if (kind == EnemyKind.Sniper) return SquadTacticalRole.Suppressor;
            if (logisticsTeam == Team.Player && kind == EnemyKind.Fast) return SquadTacticalRole.Flanker;
            return SquadTacticalRole.Hunter;
        }

        public static LogisticsRoutePlan InitialPlanForRound(int round, Team logisticsTeam, int baseLane)
        {
            int seed = Mathf.Clamp(round, 1, 100) + Mathf.Clamp(baseLane, 0, 2) * 3 + (logisticsTeam == Team.Enemy ? 1 : 0);
            return (LogisticsRoutePlan)(Mathf.Abs(seed) % RouteCount);
        }

        public static int RouteLaneForPlan(int baseLane, LogisticsRoutePlan plan)
        {
            int lane = Mathf.Clamp(baseLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            switch (plan)
            {
                case LogisticsRoutePlan.WesternHook: return lane == 0 ? 1 : lane - 1;
                case LogisticsRoutePlan.EasternHook: return lane == DynamicFrontlineTerritoryDirector.LaneCount - 1 ? lane - 1 : lane + 1;
                default: return lane;
            }
        }

        public static LogisticsRoutePlan AlternatePlan(LogisticsRoutePlan current, int baseLane)
        {
            for (int step = 1; step < RouteCount; step++)
            {
                LogisticsRoutePlan candidate = (LogisticsRoutePlan)(((int)current + step) % RouteCount);
                if (RouteLaneForPlan(baseLane, candidate) != RouteLaneForPlan(baseLane, current)) return candidate;
            }
            return current == LogisticsRoutePlan.Direct ? LogisticsRoutePlan.WesternHook : LogisticsRoutePlan.Direct;
        }

        public static bool ShouldSpawnDecoy(int round, int baseLane)
        {
            if (round < OperationalSustainmentDirector.EarliestRound) return false;
            return ((round + Mathf.Clamp(baseLane, 0, 2) * 2) % 3) != 1;
        }

        public static int DecoyHealthForRound(int round)
        {
            return Mathf.Clamp(DecoyHealthMin + Mathf.Max(0, round - OperationalSustainmentDirector.EarliestRound) / 18, DecoyHealthMin, DecoyHealthMax);
        }

        public static float ProximityThreat(float distance)
        {
            return Mathf.Clamp01((ThreatRadius - Mathf.Max(0f, distance)) / ThreatRadius) * 4f;
        }

        public static float BreachThreatContribution(Team breachAttacker, Team logisticsTeam)
        {
            return breachAttacker == logisticsTeam ? -0.65f : 1.25f;
        }

        private static string PlanLabel(LogisticsRoutePlan plan)
        {
            switch (plan)
            {
                case LogisticsRoutePlan.WesternHook: return "WESTERN HOOK";
                case LogisticsRoutePlan.EasternHook: return "EASTERN HOOK";
                default: return "DIRECT CORRIDOR";
            }
        }

        private void ClearRouteState()
        {
            if (_decoyHealth != null)
            {
                _decoyHealth.Damaged -= OnDecoyDamaged;
                _decoyHealth.Died -= OnDecoyDied;
            }
            if (_decoy != null) Destroy(_decoy);
            _decoy = null;
            _decoyHealth = null;
            _decoyLane = -1;
            _leadBody = null;
            _tracking = false;
            _team = Team.Neutral;
            _baseLane = -1;
            _routeLane = -1;
            _reroutes = 0;
            _routeActorCount = 0;
            _lastThreat = 0f;
            for (int i = 0; i < _routeActors.Length; i++) _routeActors[i] = null;
        }

        private void ResetRun()
        {
            ClearRouteState();
            _round = -1;
            _totalReroutes = 0;
            _decoysSpawned = 0;
            _decoysDestroyed = 0;
            _intelTransitions = 0;
            _ambushOrders = 0;
            _threatSamples = 0;
            _status = string.Empty;
        }

        private void ShowStatus(string text, float duration)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.72f, 0.22f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!RouteActive && Time.unscaledTime >= _statusUntil) return;
            EnsureStyles();
            float width = 540f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 146f;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.90f);
            GUI.Box(new Rect(x, y, width, RouteActive ? 56f : 32f), string.Empty);
            GUI.color = Color.white;
            string title = RouteActive ? "LOGISTICS ROUTE INTELLIGENCE // " + (_intel == RouteIntelState.Verified ? PlanLabel(_routePlan) : _intel.ToString().ToUpperInvariant()) : _status;
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f), title, _header);
            if (RouteActive)
            {
                string decoy = DecoyActive ? "DECOY CONTACT LIVE" : (_decoysSpawned > 0 ? "DECOY RESOLVED" : "NO DECOY");
                GUI.Label(new Rect(x + 8f, y + 25f, width - 16f, 20f), "LANE " + (_routeLane + 1) + " // REROUTES " + _reroutes + "/" + MaxReroutes + " // " + decoy + " // CELL " + _routeActorCount, _bodyStyle);
            }
        }
    }
}
