using UnityEngine;

namespace TankRevival
{
    public enum MobileSignalWarfareState
    {
        Inactive,
        Tracking,
        Intercepting,
        CounterReconRaid,
        CounterJamming,
        Resolved
    }

    /// <summary>
    /// v12.4 mobile signal warfare layer. A single physical mobile jammer trails active enemy logistics,
    /// pulses bounded interference into the v12.3 recon network, and creates a proximity intercept objective.
    /// Existing Fast/Elite/Sniper tanks may be temporarily retasked as counter-recon hunters through
    /// TacticalNavigationAgent. They suppress relay signal contribution rather than applying hidden damage.
    /// This director never moves the real logistics column, never moves EnemyTank rigidbodies directly and
    /// never creates projectile/direct-damage authority.
    /// </summary>
    [DefaultExecutionOrder(640)]
    public sealed class MobileSignalWarfareDirector : MonoBehaviour
    {
        public const int EarliestRound = OperationalSustainmentDirector.EarliestRound;
        public const int MaxMobileJammers = 1;
        public const int MaxRaidActors = 3;
        public const int MobileJammerHealthMin = 7;
        public const int MobileJammerHealthMax = 12;
        public const int InterceptBondReward = 5;
        public const float MobileJammerSpeed = 0.82f;
        public const float MobileJammerFollowOffset = 1.18f;
        public const float MobileJamPulseCadence = 0.65f;
        public const float MobileJamPulseSeconds = 1.15f;
        public const float InterceptRadius = 2.20f;
        public const float InterceptHoldSeconds = 4.0f;
        public const float InterceptDecayPerSecond = 0.45f;
        public const float CounterJamWindowSeconds = 9.0f;
        public const float RelaySuppressionSeconds = 5.0f;
        public const float RelaySabotageRadius = 1.20f;
        public const float RelaySabotageCooldown = 7.0f;
        public const float RaidOrderCadence = 0.36f;
        public const float RaidSearchRadius = 10.25f;

        private static MobileSignalWarfareDirector _instance;
        private readonly EnemyTank[] _raidActors = new EnemyTank[MaxRaidActors];
        private readonly float[] _raidDistances = new float[MaxRaidActors];
        private readonly float[] _relaySabotageReadyAt = new float[ReconElectronicWarfareDirector.MaxRelayNodes];

        private TankGame _game;
        private CombinedArmsMobileFrontDirector _front;
        private OperationalSustainmentDirector _sustainment;
        private LogisticsRouteIntelligenceDirector _route;
        private ReconElectronicWarfareDirector _recon;
        private GameObject _mobileJammer;
        private Rigidbody2D _mobileBody;
        private Health _mobileHealth;
        private bool _active;
        private bool _objectiveResolved;
        private int _round = -1;
        private int _routeLane = -1;
        private int _raidCount;
        private int _raidOrders;
        private int _relaySuppressions;
        private int _mobileJammersSpawned;
        private int _mobileJammersDestroyed;
        private int _interceptsCompleted;
        private float _interceptProgress;
        private float _nextJamPulse;
        private float _nextRaidOrder;
        private string _status = string.Empty;
        private float _statusUntil;
        private MobileSignalWarfareState _state;
        private GUIStyle _header;
        private GUIStyle _bodyStyle;

        public static MobileSignalWarfareDirector Instance => _instance;
        public bool OperationActive => _active && _recon != null && _recon.OperationActive && _route != null && _route.RouteActive && _sustainment != null && _sustainment.ColumnTeam == Team.Enemy;
        public bool MobileJammerActive => _mobileJammer != null && _mobileHealth != null && !_mobileHealth.IsDead;
        public bool ObjectiveResolved => _objectiveResolved;
        public int RaidCount => _raidCount;
        public int RaidOrders => _raidOrders;
        public int RelaySuppressions => _relaySuppressions;
        public int MobileJammersSpawned => _mobileJammersSpawned;
        public int MobileJammersDestroyed => _mobileJammersDestroyed;
        public int InterceptsCompleted => _interceptsCompleted;
        public float InterceptProgress01 => Mathf.Clamp01(_interceptProgress / InterceptHoldSeconds);
        public MobileSignalWarfareState State => _state;

        public static bool ConfigurationValid =>
            EarliestRound == OperationalSustainmentDirector.EarliestRound &&
            MaxMobileJammers == 1 && MaxRaidActors >= 2 && MaxRaidActors <= 3 &&
            MobileJammerHealthMin >= 6 && MobileJammerHealthMax <= 14 && MobileJammerHealthMin < MobileJammerHealthMax &&
            MobileJammerSpeed >= 0.65f && MobileJammerSpeed <= 1.05f && MobileJammerFollowOffset >= 0.8f && MobileJammerFollowOffset <= 1.6f &&
            MobileJamPulseCadence >= 0.45f && MobileJamPulseCadence <= 0.9f && MobileJamPulseSeconds > MobileJamPulseCadence && MobileJamPulseSeconds <= ReconElectronicWarfareDirector.MobileJammingPulseMaxSeconds &&
            InterceptRadius >= 1.8f && InterceptRadius <= 2.8f && InterceptHoldSeconds >= 3f && InterceptHoldSeconds <= 6f &&
            InterceptDecayPerSecond >= 0.25f && InterceptDecayPerSecond <= 0.8f &&
            CounterJamWindowSeconds >= ReconElectronicWarfareDirector.CounterJamMinSeconds && CounterJamWindowSeconds <= ReconElectronicWarfareDirector.CounterJamMaxSeconds &&
            RelaySuppressionSeconds >= ReconElectronicWarfareDirector.ExternalSuppressionMinSeconds && RelaySuppressionSeconds <= ReconElectronicWarfareDirector.ExternalSuppressionMaxSeconds &&
            RelaySabotageRadius >= 0.8f && RelaySabotageRadius <= 1.6f && RelaySabotageCooldown > RelaySuppressionSeconds && RelaySabotageCooldown <= 9f &&
            RaidOrderCadence >= TacticalNavigationDirector.DecisionCadence && RaidOrderCadence <= 0.55f && RaidSearchRadius >= 8f && RaidSearchRadius <= 12f &&
            InterceptBondReward >= 2 && InterceptBondReward <= 8 &&
            ReconElectronicWarfareDirector.ConfigurationValid && ReconElectronicWarfareDirector.ExternalWarfareBridgeValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MobileSignalWarfareDirector>() != null) return;
            GameObject go = new GameObject("MobileSignalWarfareDirector_v12_4");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileSignalWarfareDirector>();
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
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_front == null) _front = CombinedArmsMobileFrontDirector.Instance;
            if (_sustainment == null) _sustainment = OperationalSustainmentDirector.Instance;
            if (_route == null) _route = LogisticsRouteIntelligenceDirector.Instance;
            if (_recon == null) _recon = ReconElectronicWarfareDirector.Instance;

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

            bool eligible = currentRound >= EarliestRound && _recon != null && _recon.OperationActive &&
                            _route != null && _route.RouteActive && _sustainment != null && _sustainment.ColumnActive &&
                            _sustainment.ColumnTeam == Team.Enemy && _front != null && _front.IsOperationActive;
            if (!eligible)
            {
                if (_active) CleanupOperation();
                return;
            }

            if (!_active) BeginOperation(currentRound);
            if (!_active) return;

            if (MobileJammerActive && !_objectiveResolved)
            {
                UpdateIntercept();
                if (Time.time >= _nextJamPulse)
                {
                    _nextJamPulse = Time.time + MobileJamPulseCadence;
                    _recon.ApplyMobileJammingPulse(MobileJamPulseSeconds);
                    VisualFactory.RingPulse(_mobileJammer.transform.position, new Color(0.96f, 0.12f, 0.66f), 0.46f);
                }
                if (Time.time >= _nextRaidOrder)
                {
                    _nextRaidOrder = Time.time + RaidOrderCadence;
                    AssignCounterReconRaid();
                }
            }
            else
            {
                _raidCount = 0;
            }

            UpdateState();
        }

        private void FixedUpdate()
        {
            if (!OperationActive || !MobileJammerActive || _front == null || _route == null) return;
            _routeLane = Mathf.Clamp(_route.CurrentRouteLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            Vector2 target = MobileJammerAnchor(_routeLane, _front.CurrentProgress, _round);
            if (Vector2.Distance(_mobileBody.position, target) <= 0.18f) return;
            _mobileBody.MovePosition(Vector2.MoveTowards(_mobileBody.position, target, MobileJammerSpeed * Time.fixedDeltaTime));
        }

        private void BeginOperation(int round)
        {
            if (_recon == null || !_recon.OperationActive || _route == null || !_route.RouteActive || _front == null || !_front.IsOperationActive) return;
            _active = true;
            _objectiveResolved = false;
            _routeLane = Mathf.Clamp(_route.CurrentRouteLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            _interceptProgress = 0f;
            _raidCount = 0;
            _state = MobileSignalWarfareState.Tracking;
            for (int i = 0; i < _relaySabotageReadyAt.Length; i++) _relaySabotageReadyAt[i] = 0f;
            SpawnMobileJammer(round);
            _nextJamPulse = Time.time;
            _nextRaidOrder = Time.time;
            ShowStatus("MOBILE EW CONTACT // INTERCEPT TRANSMISSION OR DESTROY JAMMER", 4.0f);
        }

        private void SpawnMobileJammer(int round)
        {
            Vector2 position = MobileJammerAnchor(_routeLane, _front != null ? _front.CurrentProgress : 0f, round);
            _mobileJammer = new GameObject("ENEMY_MOBILE_SIGNAL_JAMMER_v12_4");
            _mobileJammer.transform.position = position;
            Color body = new Color(0.30f, 0.045f, 0.24f);
            Color accent = new Color(1f, 0.12f, 0.68f);
            VisualFactory.Rect("EWCarrierHull", _mobileJammer.transform, new Vector2(1.12f, 0.68f), body, Vector3.zero, 9);
            VisualFactory.Rect("EWCarrierRack", _mobileJammer.transform, new Vector2(0.56f, 0.25f), accent, new Vector3(0f, 0.44f, 0f), 10);
            VisualFactory.Disc("EWCarrierDish", _mobileJammer.transform, new Vector2(0.36f, 0.22f), new Color(1f, 0.42f, 0.12f), new Vector3(0f, 0.68f, 0f), 11);
            VisualFactory.Rect("EWCarrierAntennaL", _mobileJammer.transform, new Vector2(0.08f, 0.62f), accent, new Vector3(-0.27f, 0.58f, 0f), 10);
            VisualFactory.Rect("EWCarrierAntennaR", _mobileJammer.transform, new Vector2(0.08f, 0.62f), accent, new Vector3(0.27f, 0.58f, 0f), 10);
            BoxCollider2D collider = _mobileJammer.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.06f, 0.64f);
            _mobileBody = _mobileJammer.AddComponent<Rigidbody2D>();
            _mobileBody.bodyType = RigidbodyType2D.Kinematic;
            _mobileBody.gravityScale = 0f;
            _mobileBody.freezeRotation = true;
            _mobileHealth = _mobileJammer.AddComponent<Health>();
            _mobileHealth.Initialize(Team.Enemy, MobileJammerHealthForRound(round));
            _mobileHealth.Damaged += OnMobileJammerDamaged;
            _mobileHealth.Died += OnMobileJammerDied;
            _mobileJammersSpawned++;
            VisualFactory.RingPulse(position, accent, 1.12f);
        }

        private void UpdateIntercept()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead || _mobileJammer == null) return;
            float distance = Vector2.Distance(player.transform.position, _mobileJammer.transform.position);
            float quality = InterceptQualityForDistance(distance);
            if (quality > 0f)
            {
                _interceptProgress = Mathf.Min(InterceptHoldSeconds, _interceptProgress + Time.deltaTime * Mathf.Lerp(0.70f, 1.25f, quality));
                _state = MobileSignalWarfareState.Intercepting;
                if (_interceptProgress >= InterceptHoldSeconds)
                    ResolveSignalObjective("MOBILE EW TRANSMISSION INTERCEPTED");
            }
            else
            {
                _interceptProgress = Mathf.Max(0f, _interceptProgress - InterceptDecayPerSecond * Time.deltaTime);
            }
        }

        private void AssignCounterReconRaid()
        {
            _raidCount = 0;
            if (_recon == null || _objectiveResolved) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            Vector2 relayA;
            Vector2 relayB;
            bool hasA = _recon.TryGetRelayPosition(0, out relayA);
            bool hasB = _recon.TryGetRelayPosition(1, out relayB);
            if (!hasA && !hasB) return;
            Vector2 anchor = hasA && hasB ? (relayA + relayB) * 0.5f : (hasA ? relayA : relayB);
            for (int i = 0; i < MaxRaidActors; i++) { _raidActors[i] = null; _raidDistances[i] = float.MaxValue; }

            int count = 0;
            float limitSq = RaidSearchRadius * RaidSearchRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsRaidKind(enemy.Kind)) continue;
                float d = ((Vector2)enemy.transform.position - anchor).sqrMagnitude;
                if (d > limitSq) continue;
                int insert;
                if (count < MaxRaidActors) { insert = count; count++; }
                else
                {
                    if (d >= _raidDistances[MaxRaidActors - 1]) continue;
                    insert = MaxRaidActors - 1;
                }
                while (insert > 0 && d < _raidDistances[insert - 1])
                {
                    _raidDistances[insert] = _raidDistances[insert - 1];
                    _raidActors[insert] = _raidActors[insert - 1];
                    insert--;
                }
                _raidDistances[insert] = d;
                _raidActors[insert] = enemy;
            }

            _raidCount = count;
            for (int i = 0; i < count; i++)
            {
                EnemyTank actor = _raidActors[i];
                if (actor == null || actor.Health == null || actor.Health.IsDead) continue;
                int relayIndex = (i & 1);
                Vector2 relayPosition;
                if (!_recon.TryGetRelayPosition(relayIndex, out relayPosition))
                {
                    relayIndex = 1 - relayIndex;
                    if (!_recon.TryGetRelayPosition(relayIndex, out relayPosition)) continue;
                }

                TacticalNavigationAgent nav = actor.GetComponent<TacticalNavigationAgent>();
                if (nav == null)
                {
                    nav = actor.gameObject.AddComponent<TacticalNavigationAgent>();
                    nav.Initialize(actor);
                }
                SquadTacticalRole role = RaidRole(actor.Kind);
                float side = (i & 1) == 0 ? -1f : 1f;
                Vector2 objective = relayPosition + new Vector2(side * (actor.Kind == EnemyKind.Sniper ? 1.0f : 0.18f), actor.Kind == EnemyKind.Sniper ? 0.70f : 0.10f);
                float standoff = actor.Kind == EnemyKind.Sniper ? 4.8f : actor.Kind == EnemyKind.Fast ? 0.62f : 0.78f;
                float speed = actor.Kind == EnemyKind.Fast ? 1.12f : actor.Kind == EnemyKind.Elite ? 1.05f : 0.90f;
                nav.SetRole(role);
                nav.SetOrder(objective, standoff, speed, enemies);
                _raidOrders++;

                if (actor.Kind != EnemyKind.Sniper && Time.time >= _relaySabotageReadyAt[relayIndex] &&
                    Vector2.Distance(actor.transform.position, relayPosition) <= RelaySabotageRadius)
                {
                    if (_recon.SuppressRelay(relayIndex, RelaySuppressionSeconds))
                    {
                        _relaySuppressions++;
                        _relaySabotageReadyAt[relayIndex] = Time.time + RelaySabotageCooldown;
                        VisualFactory.RingPulse(relayPosition, new Color(1f, 0.44f, 0.08f), 0.92f);
                        ShowStatus("COUNTER-RECON RAID // RELAY " + (relayIndex + 1) + " SUPPRESSED", 2.7f);
                    }
                }
            }
        }

        private void OnMobileJammerDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("MOBILE EW CARRIER HIT // INTERFERENCE PLATFORM EXPOSED", 2.0f);
        }

        private void OnMobileJammerDied(Health health)
        {
            _mobileJammersDestroyed++;
            if (_mobileJammer != null)
            {
                Vector2 position = _mobileJammer.transform.position;
                VisualFactory.Explosion(position, new Color(1f, 0.12f, 0.68f), 1.05f);
                VisualFactory.RingPulse(position, new Color(0.18f, 1f, 0.64f), 1.35f);
            }
            if (!_objectiveResolved) ResolveSignalObjective("MOBILE EW JAMMER DESTROYED");
        }

        private void ResolveSignalObjective(string reason)
        {
            if (_objectiveResolved) return;
            _objectiveResolved = true;
            _interceptProgress = InterceptHoldSeconds;
            _interceptsCompleted++;
            if (_recon != null)
            {
                _recon.ApplyCounterJamming(CounterJamWindowSeconds);
                _recon.ApplyRecoveredIntelPacket();
            }
            WarEconomyDirector.AwardMissionBonds(InterceptBondReward, reason);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.24f, 0.02f);
            if (_mobileJammer != null) VisualFactory.RingPulse(_mobileJammer.transform.position, new Color(0.18f, 1f, 0.64f), 1.15f);
            _state = MobileSignalWarfareState.CounterJamming;
            ShowStatus(reason + " // COUNTER-JAM WINDOW + RECOVERED INTEL", 4.0f);
        }

        private void UpdateState()
        {
            if (!_active) { _state = MobileSignalWarfareState.Inactive; return; }
            if (_objectiveResolved)
            {
                _state = _recon != null && _recon.CounterJammingActive ? MobileSignalWarfareState.CounterJamming : MobileSignalWarfareState.Resolved;
                return;
            }
            if (_interceptProgress > 0.01f) _state = MobileSignalWarfareState.Intercepting;
            else if (_raidCount > 0) _state = MobileSignalWarfareState.CounterReconRaid;
            else _state = MobileSignalWarfareState.Tracking;
        }

        public static int MobileJammerHealthForRound(int round)
        {
            return Mathf.Clamp(MobileJammerHealthMin + Mathf.Max(0, round - EarliestRound) / 9, MobileJammerHealthMin, MobileJammerHealthMax);
        }

        public static Vector2 MobileJammerAnchor(int routeLane, float frontProgress, int round)
        {
            int lane = Mathf.Clamp(routeLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            Vector2 baseAnchor = OperationalSustainmentDirector.SupportAnchor(lane, Mathf.Clamp01(frontProgress), true);
            float side = ((Mathf.Clamp(round, 1, 100) + lane) & 1) == 0 ? -1f : 1f;
            baseAnchor.x = Mathf.Clamp(baseAnchor.x + side * MobileJammerFollowOffset, -CombinedArmsMobileFrontDirector.ArenaXLimit, CombinedArmsMobileFrontDirector.ArenaXLimit);
            baseAnchor.y = Mathf.Clamp(baseAnchor.y + 0.62f, -4.25f, 4.35f);
            return baseAnchor;
        }

        public static float InterceptQualityForDistance(float distance)
        {
            return Mathf.Clamp01(1f - Mathf.Max(0f, distance) / InterceptRadius);
        }

        public static bool IsRaidKind(EnemyKind kind)
        {
            return kind == EnemyKind.Fast || kind == EnemyKind.Elite || kind == EnemyKind.Sniper;
        }

        public static SquadTacticalRole RaidRole(EnemyKind kind)
        {
            if (kind == EnemyKind.Sniper) return SquadTacticalRole.Suppressor;
            if (kind == EnemyKind.Fast) return SquadTacticalRole.Flanker;
            return SquadTacticalRole.Hunter;
        }

        private void CleanupOperation()
        {
            if (_mobileHealth != null)
            {
                _mobileHealth.Damaged -= OnMobileJammerDamaged;
                _mobileHealth.Died -= OnMobileJammerDied;
            }
            if (_mobileJammer != null) Destroy(_mobileJammer);
            _mobileJammer = null;
            _mobileBody = null;
            _mobileHealth = null;
            _active = false;
            _objectiveResolved = false;
            _routeLane = -1;
            _raidCount = 0;
            _interceptProgress = 0f;
            _state = MobileSignalWarfareState.Inactive;
            for (int i = 0; i < MaxRaidActors; i++) _raidActors[i] = null;
            for (int i = 0; i < _relaySabotageReadyAt.Length; i++) _relaySabotageReadyAt[i] = 0f;
        }

        private void ResetRun()
        {
            CleanupOperation();
            _round = -1;
            _raidOrders = 0;
            _relaySuppressions = 0;
            _mobileJammersSpawned = 0;
            _mobileJammersDestroyed = 0;
            _interceptsCompleted = 0;
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
            _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.34f, 0.78f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!OperationActive && Time.unscaledTime >= _statusUntil) return;
            EnsureStyles();
            float width = 570f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 267f;
            GUI.color = new Color(0.055f, 0.018f, 0.065f, 0.91f);
            GUI.Box(new Rect(x, y, width, OperationActive ? 58f : 34f), string.Empty);
            GUI.color = Color.white;
            string title = OperationActive ? "MOBILE SIGNAL WARFARE // " + _state.ToString().ToUpperInvariant() : _status;
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f), title, _header);
            if (OperationActive)
            {
                string jammer = MobileJammerActive ? "MOBILE JAMMER LIVE" : "MOBILE JAMMER DOWN";
                string counter = _recon != null && _recon.CounterJammingActive ? "C-JAM " + _recon.CounterJamRemaining.ToString("0.0") + "s" : "C-JAM OFF";
                int suppressed = _recon != null ? _recon.SuppressedRelays : 0;
                GUI.Label(new Rect(x + 8f, y + 25f, width - 16f, 20f),
                    jammer + " // INTERCEPT " + Mathf.RoundToInt(InterceptProgress01 * 100f) + "% // " + counter + " // RELAYS SUPP " + suppressed + " // RAID " + _raidCount,
                    _bodyStyle);
            }
        }
    }
}
