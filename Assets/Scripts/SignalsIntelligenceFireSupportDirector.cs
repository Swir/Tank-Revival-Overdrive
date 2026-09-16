using UnityEngine;

namespace TankRevival
{
    public enum SignalsFireSupportState
    {
        Inactive,
        Searching,
        Triangulating,
        DeceptionRisk,
        Verified,
        FireWindow,
        Firing,
        Resolved
    }

    /// <summary>
    /// v12.5 SIGINT/fire-control layer. Two existing scout relays provide bounded geometric confidence
    /// against one physical true emitter and at most one physical decoy. The player must verify the
    /// deception picture before finite HE fire missions become available. Support shells are spawned only
    /// through TankGame.SpawnProjectile; enemy counter-SIGINT movement stays under TacticalNavigationAgent.
    /// </summary>
    [DefaultExecutionOrder(650)]
    public sealed class SignalsIntelligenceFireSupportDirector : MonoBehaviour
    {
        public const int EarliestRound = 72;
        public const int MaxEmitters = 2;
        public const int MaxGuardActors = 3;
        public const int EmitterHealthMin = 5;
        public const int EmitterHealthMax = 9;
        public const int MaxFireMissionsPerOperation = 2;
        public const int ShellsPerMission = 3;
        public const int FireSupportProjectileDamage = 2;
        public const float VerificationRadius = 1.80f;
        public const float VerificationHoldSeconds = 1.55f;
        public const float VerificationDecayPerSecond = 0.52f;
        public const float MinTriangulationQuality = 0.55f;
        public const float FireSupportWindowSeconds = 8.0f;
        public const float FireSupportCooldownSeconds = 12.0f;
        public const float ShellInterval = 0.24f;
        public const float FireSupportProjectileSpeed = 7.6f;
        public const float FireSupportSpawnHeight = 5.4f;
        public const float FireSupportScatterRadius = 0.78f;
        public const float GuardOrderCadence = 0.38f;
        public const float GuardSearchRadius = 10.5f;
        public const float EmitterLaneOffset = 0.72f;

        private static SignalsIntelligenceFireSupportDirector _instance;
        private readonly GameObject[] _emitters = new GameObject[MaxEmitters];
        private readonly Health[] _emitterHealth = new Health[MaxEmitters];
        private readonly bool[] _emitterVerified = new bool[MaxEmitters];
        private readonly bool[] _emitterResolved = new bool[MaxEmitters];
        private readonly float[] _verificationProgress = new float[MaxEmitters];
        private readonly EnemyTank[] _guards = new EnemyTank[MaxGuardActors];
        private readonly float[] _guardDistances = new float[MaxGuardActors];

        private TankGame _game;
        private CombinedArmsMobileFrontDirector _front;
        private OperationalSustainmentDirector _sustainment;
        private LogisticsRouteIntelligenceDirector _route;
        private ReconElectronicWarfareDirector _recon;
        private bool _active;
        private bool _trueSignalVerified;
        private bool _decoySignalVerified;
        private bool _supportEverUnlocked;
        private int _round = -1;
        private int _trueLane = -1;
        private int _decoyLane = -1;
        private int _guardCount;
        private int _guardOrders;
        private int _fireMissions;
        private int _supportShellsSpawned;
        private int _emittersSpawned;
        private int _emittersDestroyed;
        private float _triangulationQuality;
        private float _supportWindowUntil;
        private float _supportCooldownUntil;
        private float _nextGuardOrder;
        private float _nextShellAt;
        private int _pendingShells;
        private Vector2 _fireMissionTarget;
        private SignalsFireSupportState _state;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _bodyStyle;

        public static SignalsIntelligenceFireSupportDirector Instance => _instance;
        public bool OperationActive => _active && DependenciesLive;
        public SignalsFireSupportState State => _state;
        public int GuardCount => _guardCount;
        public int GuardOrders => _guardOrders;
        public int FireMissions => _fireMissions;
        public int SupportShellsSpawned => _supportShellsSpawned;
        public int EmittersSpawned => _emittersSpawned;
        public int EmittersDestroyed => _emittersDestroyed;
        public float TriangulationQuality => _triangulationQuality;
        public bool TrueSignalVerified => _trueSignalVerified;
        public bool DecoySignalVerified => _decoySignalVerified;
        public bool FireSupportWindowActive => OperationActive && _supportEverUnlocked && Time.time < _supportWindowUntil && Time.time >= _supportCooldownUntil && _fireMissions < MaxFireMissionsPerOperation;
        public float FireSupportWindowRemaining => FireSupportWindowActive ? Mathf.Max(0f, _supportWindowUntil - Time.time) : 0f;
        public int PendingShells => _pendingShells;

        private bool DependenciesLive =>
            _recon != null && _recon.OperationActive &&
            _route != null && _route.RouteActive &&
            _sustainment != null && _sustainment.ColumnActive && _sustainment.ColumnTeam == Team.Enemy &&
            _front != null && _front.IsOperationActive;

        public static bool ConfigurationValid =>
            EarliestRound >= MobileSignalWarfareDirector.EarliestRound && EarliestRound <= 80 &&
            MaxEmitters == 2 && MaxGuardActors >= 2 && MaxGuardActors <= 3 &&
            EmitterHealthMin >= 4 && EmitterHealthMax <= 10 && EmitterHealthMin < EmitterHealthMax &&
            VerificationRadius >= 1.4f && VerificationRadius <= 2.4f &&
            VerificationHoldSeconds >= 1.0f && VerificationHoldSeconds <= 2.5f &&
            VerificationDecayPerSecond >= 0.25f && VerificationDecayPerSecond <= 0.8f &&
            MinTriangulationQuality >= 0.45f && MinTriangulationQuality <= 0.70f &&
            FireSupportWindowSeconds >= 5f && FireSupportWindowSeconds <= 10f &&
            FireSupportCooldownSeconds >= FireSupportWindowSeconds && FireSupportCooldownSeconds <= 16f &&
            MaxFireMissionsPerOperation >= 1 && MaxFireMissionsPerOperation <= 2 &&
            ShellsPerMission >= 2 && ShellsPerMission <= 3 && MaxFireMissionsPerOperation * ShellsPerMission <= 6 &&
            FireSupportProjectileDamage >= 1 && FireSupportProjectileDamage <= 3 &&
            FireSupportProjectileSpeed >= 6f && FireSupportProjectileSpeed <= 9f &&
            FireSupportSpawnHeight >= 4.5f && FireSupportSpawnHeight <= 6.5f &&
            FireSupportScatterRadius >= 0.45f && FireSupportScatterRadius <= 1.0f &&
            GuardOrderCadence >= TacticalNavigationDirector.DecisionCadence && GuardOrderCadence <= 0.55f &&
            GuardSearchRadius >= 8f && GuardSearchRadius <= 12f &&
            ReconElectronicWarfareDirector.ConfigurationValid && ReconElectronicWarfareDirector.ExternalWarfareBridgeValid &&
            MobileSignalWarfareDirector.ConfigurationValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<SignalsIntelligenceFireSupportDirector>() != null) return;
            GameObject go = new GameObject("SignalsIntelligenceFireSupportDirector_v12_5");
            DontDestroyOnLoad(go);
            go.AddComponent<SignalsIntelligenceFireSupportDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            CleanupOperation();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            ResolveDependencies();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0 || _active) ResetRun();
                return;
            }

            int currentRound = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_round != currentRound)
            {
                if (_active) CleanupOperation();
                _round = currentRound;
            }

            bool eligible = currentRound >= EarliestRound && DependenciesLive;
            if (!eligible)
            {
                if (_active) CleanupOperation();
                return;
            }

            if (!_active) BeginOperation(currentRound);
            if (!_active) return;

            UpdateTriangulation();
            UpdateVerification();
            if (Time.time >= _nextGuardOrder)
            {
                _nextGuardOrder = Time.time + GuardOrderCadence;
                AssignCounterSigintGuards();
            }
            ExecutePendingShells();
            if (FireSupportWindowActive && Input.GetKeyDown(KeyCode.F))
                RequestFireMission();
            UpdateState();
        }

        private void ResolveDependencies()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_front == null) _front = CombinedArmsMobileFrontDirector.Instance;
            if (_sustainment == null) _sustainment = OperationalSustainmentDirector.Instance;
            if (_route == null) _route = LogisticsRouteIntelligenceDirector.Instance;
            if (_recon == null) _recon = ReconElectronicWarfareDirector.Instance;
        }

        private void BeginOperation(int round)
        {
            if (!DependenciesLive) return;
            _active = true;
            _state = SignalsFireSupportState.Searching;
            _trueSignalVerified = false;
            _decoySignalVerified = false;
            _supportEverUnlocked = false;
            _triangulationQuality = 0f;
            _fireMissions = 0;
            _supportShellsSpawned = 0;
            _pendingShells = 0;
            _supportWindowUntil = 0f;
            _supportCooldownUntil = 0f;
            for (int i = 0; i < MaxEmitters; i++)
            {
                _emitterVerified[i] = false;
                _emitterResolved[i] = false;
                _verificationProgress[i] = 0f;
            }

            int routeLane = Mathf.Clamp(_route.CurrentRouteLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            _trueLane = routeLane;
            _decoyLane = AlternateLane(routeLane, round);
            SpawnEmitter(0, false, EmitterAnchor(_trueLane, _front.CurrentProgress, round, false), round);
            SpawnEmitter(1, true, EmitterAnchor(_decoyLane, _front.CurrentProgress, round, true), round);
            _nextGuardOrder = Time.time;
            ShowStatus("SIGINT CONTACT // TRIANGULATE AND VERIFY ENEMY FIRE-CONTROL EMITTER", 4.2f);
        }

        private void SpawnEmitter(int index, bool decoy, Vector2 position, int round)
        {
            if (index < 0 || index >= MaxEmitters) return;
            GameObject go = new GameObject(decoy ? "ENEMY_DECOY_EMITTER_v12_5" : "ENEMY_TRUE_FIRE_CONTROL_EMITTER_v12_5");
            go.transform.position = position;
            Color hull = decoy ? new Color(0.24f, 0.11f, 0.28f) : new Color(0.28f, 0.075f, 0.08f);
            Color signal = decoy ? new Color(0.78f, 0.25f, 1f) : new Color(1f, 0.20f, 0.08f);
            VisualFactory.Rect("EmitterHull", go.transform, new Vector2(0.92f, 0.66f), hull, Vector3.zero, 9);
            VisualFactory.Rect("EmitterRack", go.transform, new Vector2(0.48f, 0.22f), signal, new Vector3(0f, 0.42f, 0f), 10);
            VisualFactory.Disc("EmitterDish", go.transform, new Vector2(0.34f, 0.22f), signal, new Vector3(0f, 0.66f, 0f), 11);
            VisualFactory.Rect("EmitterMast", go.transform, new Vector2(0.07f, 0.58f), signal, new Vector3(0f, 0.58f, 0f), 10);
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.88f, 0.62f);
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            Health health = go.AddComponent<Health>();
            health.Initialize(Team.Enemy, EmitterHealthForRound(round));
            if (index == 0)
            {
                health.Damaged += OnTrueEmitterDamaged;
                health.Died += OnTrueEmitterDied;
            }
            else
            {
                health.Damaged += OnDecoyEmitterDamaged;
                health.Died += OnDecoyEmitterDied;
            }
            _emitters[index] = go;
            _emitterHealth[index] = health;
            _emittersSpawned++;
            VisualFactory.RingPulse(position, signal, 0.92f);
        }

        private void UpdateTriangulation()
        {
            _triangulationQuality = 0f;
            if (_recon == null || _emitters[0] == null || _emitterResolved[0]) return;
            Vector2 relayA;
            Vector2 relayB;
            bool hasA = _recon.TryGetRelayPosition(0, out relayA) && !_recon.IsRelaySuppressed(0);
            bool hasB = _recon.TryGetRelayPosition(1, out relayB) && !_recon.IsRelaySuppressed(1);
            if (!hasA || !hasB) return;
            _triangulationQuality = TriangulationQualityForGeometry(relayA, relayB, _emitters[0].transform.position);
            if (_triangulationQuality >= MinTriangulationQuality && !_trueSignalVerified && !_decoySignalVerified)
                _state = SignalsFireSupportState.Triangulating;
            TryUnlockFireSupport();
        }

        private void UpdateVerification()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;
            Vector2 playerPosition = player.transform.position;
            for (int i = 0; i < MaxEmitters; i++)
            {
                if (_emitters[i] == null || _emitterResolved[i] || _emitterVerified[i]) continue;
                float distance = Vector2.Distance(playerPosition, _emitters[i].transform.position);
                if (distance <= VerificationRadius)
                {
                    float quality = Mathf.Clamp01(1f - distance / VerificationRadius);
                    _verificationProgress[i] = Mathf.Min(VerificationHoldSeconds, _verificationProgress[i] + Time.deltaTime * Mathf.Lerp(0.65f, 1.30f, quality));
                    if (_verificationProgress[i] >= VerificationHoldSeconds)
                        VerifyEmitter(i);
                }
                else
                {
                    _verificationProgress[i] = Mathf.Max(0f, _verificationProgress[i] - VerificationDecayPerSecond * Time.deltaTime);
                }
            }
        }

        private void VerifyEmitter(int index)
        {
            if (index < 0 || index >= MaxEmitters || _emitterVerified[index]) return;
            _emitterVerified[index] = true;
            _verificationProgress[index] = VerificationHoldSeconds;
            if (index == 0)
            {
                _trueSignalVerified = true;
                ShowStatus("TRUE FIRE-CONTROL EMITTER VERIFIED // BUILDING FIRING SOLUTION", 3.2f);
            }
            else
            {
                _decoySignalVerified = true;
                ShowStatus("DECOY TRANSMITTER CONFIRMED // TRUE EMITTER ISOLATED", 3.2f);
            }
            TryUnlockFireSupport();
        }

        private void TryUnlockFireSupport()
        {
            if (!_active || _supportEverUnlocked || _emitters[0] == null || _emitterResolved[0]) return;
            bool deceptionSolved = _trueSignalVerified || (_decoySignalVerified && _emitters[0] != null);
            bool relayEvidence = _recon != null && !_recon.IsRelaySuppressed(0) && !_recon.IsRelaySuppressed(1) && _triangulationQuality >= MinTriangulationQuality;
            if (!deceptionSolved || !relayEvidence) return;
            _supportEverUnlocked = true;
            _supportWindowUntil = Time.time + FireSupportWindowSeconds;
            _supportCooldownUntil = 0f;
            _state = SignalsFireSupportState.FireWindow;
            VisualFactory.RingPulse(_emitters[0].transform.position, new Color(0.16f, 1f, 0.58f), 1.55f);
            ShowStatus("SIGINT FIRING SOLUTION VERIFIED // PRESS F FOR HE FIRE MISSION", 4.0f);
        }

        private void RequestFireMission()
        {
            if (!FireSupportWindowActive || _emitters[0] == null || _emitterResolved[0] || _pendingShells > 0) return;
            _fireMissions++;
            _pendingShells = ShellsPerMission;
            _fireMissionTarget = _emitters[0].transform.position;
            _nextShellAt = Time.time;
            _supportCooldownUntil = Time.time + FireSupportCooldownSeconds;
            _supportWindowUntil = Mathf.Max(_supportWindowUntil, Time.time + FireSupportWindowSeconds);
            _state = SignalsFireSupportState.Firing;
            ShowStatus("SIGINT FIRE MISSION COMMITTED // HE SALVO INBOUND", 3.2f);
        }

        private void ExecutePendingShells()
        {
            if (_pendingShells <= 0 || _game == null || Time.time < _nextShellAt) return;
            int shellIndex = ShellsPerMission - _pendingShells;
            Vector2 scatter = FireMissionScatter(_round, _fireMissions, shellIndex);
            Vector2 target = _fireMissionTarget + scatter;
            Vector2 spawn = target + new Vector2(scatter.x * 0.18f, FireSupportSpawnHeight);
            Vector2 direction = (target - spawn).normalized;
            _game.SpawnProjectile(spawn, direction, Team.Player, FireSupportProjectileDamage, FireSupportProjectileSpeed, new Color(1f, 0.52f, 0.10f), AmmoType.Explosive);
            VisualFactory.RingPulse(target, new Color(1f, 0.42f, 0.08f), 0.56f);
            _supportShellsSpawned++;
            _pendingShells--;
            _nextShellAt = Time.time + ShellInterval;
            if (_pendingShells <= 0)
            {
                if (_fireMissions >= MaxFireMissionsPerOperation)
                {
                    _state = SignalsFireSupportState.Resolved;
                    ShowStatus("SIGINT FIRE-SUPPORT ALLOTMENT EXPENDED", 2.8f);
                }
                else
                {
                    _supportWindowUntil = _supportCooldownUntil + FireSupportWindowSeconds;
                    ShowStatus("FIRE MISSION COMPLETE // REACQUIRE AFTER COOLDOWN", 2.6f);
                }
            }
        }

        private void AssignCounterSigintGuards()
        {
            _guardCount = 0;
            Vector2 anchor = CounterSigintAnchor();
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;
            for (int i = 0; i < MaxGuardActors; i++) { _guards[i] = null; _guardDistances[i] = float.MaxValue; }
            int count = 0;
            float maxSq = GuardSearchRadius * GuardSearchRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsGuardKind(enemy.Kind)) continue;
                float d = ((Vector2)enemy.transform.position - anchor).sqrMagnitude;
                if (d > maxSq) continue;
                int insert;
                if (count < MaxGuardActors) { insert = count; count++; }
                else
                {
                    if (d >= _guardDistances[MaxGuardActors - 1]) continue;
                    insert = MaxGuardActors - 1;
                }
                while (insert > 0 && d < _guardDistances[insert - 1])
                {
                    _guardDistances[insert] = _guardDistances[insert - 1];
                    _guards[insert] = _guards[insert - 1];
                    insert--;
                }
                _guardDistances[insert] = d;
                _guards[insert] = enemy;
            }
            _guardCount = count;
            for (int i = 0; i < count; i++)
            {
                EnemyTank actor = _guards[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                TacticalNavigationAgent nav = actor.GetComponent<TacticalNavigationAgent>();
                if (nav == null)
                {
                    nav = actor.gameObject.AddComponent<TacticalNavigationAgent>();
                    nav.Initialize(actor);
                }
                float side = (i & 1) == 0 ? -1f : 1f;
                float standoff = actor.Kind == EnemyKind.Sniper ? 5.0f : actor.Kind == EnemyKind.Fast ? 1.0f : 1.35f;
                float speed = actor.Kind == EnemyKind.Fast ? 1.12f : actor.Kind == EnemyKind.Elite ? 1.04f : 0.90f;
                Vector2 objective = anchor + new Vector2(side * (0.72f + i * 0.28f), actor.Kind == EnemyKind.Sniper ? 0.80f : -0.15f);
                nav.SetRole(GuardRole(actor.Kind));
                nav.SetOrder(objective, standoff, speed, enemies);
                _guardOrders++;
            }
        }

        private Vector2 CounterSigintAnchor()
        {
            if (_emitters[0] != null && !_emitterResolved[0]) return _emitters[0].transform.position;
            if (_emitters[1] != null && !_emitterResolved[1]) return _emitters[1].transform.position;
            return OperationalSustainmentDirector.SupportAnchor(Mathf.Max(0, _trueLane), _front != null ? _front.CurrentProgress : 0f, true);
        }

        private void OnTrueEmitterDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("TRUE/UNKNOWN EMITTER HIT // VERIFY BEFORE FIRE-SUPPORT COMMIT", 1.8f);
        }

        private void OnDecoyEmitterDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("UNKNOWN EMITTER HIT // SIGNAL ANALYSIS CONTINUES", 1.8f);
        }

        private void OnTrueEmitterDied(Health health)
        {
            _emittersDestroyed++;
            _emitterResolved[0] = true;
            _trueSignalVerified = true;
            if (_emitters[0] != null)
            {
                Vector2 p = _emitters[0].transform.position;
                VisualFactory.Explosion(p, new Color(1f, 0.20f, 0.06f), 1.1f);
                VisualFactory.RingPulse(p, new Color(0.18f, 1f, 0.58f), 1.25f);
            }
            _pendingShells = 0;
            _supportWindowUntil = 0f;
            _state = SignalsFireSupportState.Resolved;
            ShowStatus("TRUE FIRE-CONTROL EMITTER DESTROYED // ENEMY TARGETING COLLAPSED", 3.8f);
        }

        private void OnDecoyEmitterDied(Health health)
        {
            _emittersDestroyed++;
            _emitterResolved[1] = true;
            _decoySignalVerified = true;
            if (_emitters[1] != null)
            {
                Vector2 p = _emitters[1].transform.position;
                VisualFactory.Explosion(p, new Color(0.72f, 0.18f, 1f), 0.86f);
                VisualFactory.RingPulse(p, new Color(0.18f, 1f, 0.58f), 1.05f);
            }
            ShowStatus("DECOY TRANSMITTER DESTROYED // TRUE EMITTER ISOLATED", 3.2f);
            TryUnlockFireSupport();
        }

        private void UpdateState()
        {
            if (!_active) { _state = SignalsFireSupportState.Inactive; return; }
            if (_emitterResolved[0]) { _state = SignalsFireSupportState.Resolved; return; }
            if (_pendingShells > 0) { _state = SignalsFireSupportState.Firing; return; }
            if (FireSupportWindowActive) { _state = SignalsFireSupportState.FireWindow; return; }
            if (_supportEverUnlocked) { _state = SignalsFireSupportState.Verified; return; }
            if (_decoySignalVerified || _trueSignalVerified) { _state = SignalsFireSupportState.Verified; return; }
            if (_triangulationQuality >= MinTriangulationQuality) { _state = SignalsFireSupportState.Triangulating; return; }
            _state = SignalsFireSupportState.DeceptionRisk;
        }

        public static int AlternateLane(int routeLane, int round)
        {
            int lane = Mathf.Clamp(routeLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            if (DynamicFrontlineTerritoryDirector.LaneCount <= 1) return lane;
            int direction = ((Mathf.Clamp(round, 1, 100) + lane) & 1) == 0 ? 1 : -1;
            int candidate = lane + direction;
            if (candidate < 0 || candidate >= DynamicFrontlineTerritoryDirector.LaneCount) candidate = lane - direction;
            return Mathf.Clamp(candidate, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
        }

        public static Vector2 EmitterAnchor(int lane, float frontProgress, int round, bool decoy)
        {
            int boundedLane = Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            Vector2 anchor = OperationalSustainmentDirector.SupportAnchor(boundedLane, Mathf.Clamp01(frontProgress), true);
            float side = ((Mathf.Clamp(round, 1, 100) + boundedLane + (decoy ? 1 : 0)) & 1) == 0 ? -1f : 1f;
            anchor.x = Mathf.Clamp(anchor.x + side * (decoy ? EmitterLaneOffset + 0.35f : EmitterLaneOffset), -CombinedArmsMobileFrontDirector.ArenaXLimit, CombinedArmsMobileFrontDirector.ArenaXLimit);
            anchor.y = Mathf.Clamp(anchor.y + (decoy ? 1.05f : 0.30f), -4.20f, 4.25f);
            return anchor;
        }

        public static int EmitterHealthForRound(int round)
        {
            return Mathf.Clamp(EmitterHealthMin + Mathf.Max(0, Mathf.Clamp(round, 1, 100) - EarliestRound) / 8, EmitterHealthMin, EmitterHealthMax);
        }

        public static float VerificationQualityForDistance(float distance)
        {
            return Mathf.Clamp01(1f - Mathf.Max(0f, distance) / VerificationRadius);
        }

        public static float TriangulationQualityForGeometry(Vector2 relayA, Vector2 relayB, Vector2 target)
        {
            float baseline = Vector2.Distance(relayA, relayB);
            if (baseline < 0.25f) return 0f;
            Vector2 a = target - relayA;
            Vector2 b = target - relayB;
            float da = a.magnitude;
            float db = b.magnitude;
            if (da < 0.10f || db < 0.10f) return 1f;
            Vector2 an = a / da;
            Vector2 bn = b / db;
            float crossing = Mathf.Abs(an.x * bn.y - an.y * bn.x);
            float baselineScore = Mathf.Clamp01((baseline - 1.0f) / 5.0f);
            float distanceScore = Mathf.Clamp01(1f - Mathf.Max(da, db) / 18f);
            return Mathf.Clamp01(crossing * 0.58f + baselineScore * 0.27f + distanceScore * 0.15f);
        }

        public static Vector2 FireMissionScatter(int round, int mission, int shellIndex)
        {
            int seed = Mathf.Clamp(round, 1, 100) * 92821 + Mathf.Max(0, mission) * 2971 + Mathf.Max(0, shellIndex) * 619;
            float angle = (seed % 360) * Mathf.Deg2Rad;
            float radius = (((seed / 7) % 100) / 99f) * FireSupportScatterRadius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.55f) * radius;
        }

        public static bool IsGuardKind(EnemyKind kind)
        {
            return kind == EnemyKind.Fast || kind == EnemyKind.Elite || kind == EnemyKind.Sniper;
        }

        public static SquadTacticalRole GuardRole(EnemyKind kind)
        {
            if (kind == EnemyKind.Sniper) return SquadTacticalRole.Suppressor;
            if (kind == EnemyKind.Fast) return SquadTacticalRole.Flanker;
            return SquadTacticalRole.Hunter;
        }

        private void CleanupOperation()
        {
            if (_emitterHealth[0] != null)
            {
                _emitterHealth[0].Damaged -= OnTrueEmitterDamaged;
                _emitterHealth[0].Died -= OnTrueEmitterDied;
            }
            if (_emitterHealth[1] != null)
            {
                _emitterHealth[1].Damaged -= OnDecoyEmitterDamaged;
                _emitterHealth[1].Died -= OnDecoyEmitterDied;
            }
            for (int i = 0; i < MaxEmitters; i++)
            {
                if (_emitters[i] != null) Destroy(_emitters[i]);
                _emitters[i] = null;
                _emitterHealth[i] = null;
                _emitterVerified[i] = false;
                _emitterResolved[i] = false;
                _verificationProgress[i] = 0f;
            }
            for (int i = 0; i < MaxGuardActors; i++) _guards[i] = null;
            _active = false;
            _trueSignalVerified = false;
            _decoySignalVerified = false;
            _supportEverUnlocked = false;
            _trueLane = -1;
            _decoyLane = -1;
            _guardCount = 0;
            _pendingShells = 0;
            _supportWindowUntil = 0f;
            _supportCooldownUntil = 0f;
            _triangulationQuality = 0f;
            _state = SignalsFireSupportState.Inactive;
        }

        private void ResetRun()
        {
            CleanupOperation();
            _round = -1;
            _guardOrders = 0;
            _fireMissions = 0;
            _supportShellsSpawned = 0;
            _emittersSpawned = 0;
            _emittersDestroyed = 0;
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
            _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.70f, 0.18f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!OperationActive && Time.unscaledTime >= _statusUntil) return;
            EnsureStyles();
            float width = 610f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 330f;
            GUI.color = new Color(0.07f, 0.035f, 0.012f, 0.92f);
            GUI.Box(new Rect(x, y, width, OperationActive ? 62f : 34f), string.Empty);
            GUI.color = Color.white;
            string title = OperationActive ? "SIGINT FIRE CONTROL // " + _state.ToString().ToUpperInvariant() : _status;
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f), title, _header);
            if (OperationActive)
            {
                string identity = _trueSignalVerified ? "TRUE VERIFIED" : _decoySignalVerified ? "DECOY EXPOSED" : "IDENTITY UNKNOWN";
                string support = FireSupportWindowActive ? "[F] FIRE " + FireSupportWindowRemaining.ToString("0.0") + "s" : _pendingShells > 0 ? "SALVO " + _pendingShells : "SUPPORT HOLD";
                GUI.Label(new Rect(x + 8f, y + 25f, width - 16f, 22f),
                    "TRIANG " + Mathf.RoundToInt(_triangulationQuality * 100f) + "% // " + identity + " // " + support + " // MISSIONS " + _fireMissions + "/" + MaxFireMissionsPerOperation + " // GUARD " + _guardCount,
                    _bodyStyle);
            }
        }
    }
}
