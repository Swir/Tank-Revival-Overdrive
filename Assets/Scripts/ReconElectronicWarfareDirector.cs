using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum ReconEWState
    {
        Inactive,
        Searching,
        Jammed,
        Contact,
        Verified,
        RelayLost
    }

    /// <summary>
    /// v12.3 reconnaissance/electronic-warfare layer for the existing v12.1/v12.2 logistics operation.
    /// v12.4 extends the public bridge with bounded relay suppression, recovered-intelligence packets and
    /// counter-jamming windows so mobile EW/counter-recon systems can interact without owning route,
    /// projectile, tank-movement or damage authority. Physical assets continue to use canonical Health.
    /// </summary>
    [DefaultExecutionOrder(630)]
    public sealed class ReconElectronicWarfareDirector : MonoBehaviour
    {
        public const int MaxRelayNodes = 2;
        public const int MaxJammers = 1;
        public const int MaxGuardActors = 3;
        public const int RequiredPacketsForVerified = 2;
        public const float RelaySyncRadius = 2.60f;
        public const float JammedRelaySyncRadius = 1.55f;
        public const float GuardSearchRadius = 9.50f;
        public const float GuardOrderCadence = 0.34f;
        public const float SignalPulseSeconds = 0.85f;
        public const float RelayY = -2.85f;
        public const float JammerY = 2.15f;
        public const int RelayHealthMin = 4;
        public const int RelayHealthMax = 6;
        public const int JammerHealthMin = 6;
        public const int JammerHealthMax = 10;
        public const int JammerBondReward = 5;
        public const float ExternalSuppressionMinSeconds = 1.0f;
        public const float ExternalSuppressionMaxSeconds = 8.0f;
        public const float CounterJamMinSeconds = 2.0f;
        public const float CounterJamMaxSeconds = 12.0f;
        public const float MobileJammingPulseMaxSeconds = 2.0f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo RouteSetIntelMethod = typeof(LogisticsRouteIntelligenceDirector).GetMethod("SetIntel", PrivateInstance);

        private static ReconElectronicWarfareDirector _instance;
        private readonly GameObject[] _relayObjects = new GameObject[MaxRelayNodes];
        private readonly Health[] _relayHealth = new Health[MaxRelayNodes];
        private readonly bool[] _relaySynced = new bool[MaxRelayNodes];
        private readonly float[] _relaySuppressedUntil = new float[MaxRelayNodes];
        private readonly EnemyTank[] _guards = new EnemyTank[MaxGuardActors];
        private readonly float[] _guardDistances = new float[MaxGuardActors];

        private TankGame _game;
        private OperationalSustainmentDirector _sustainment;
        private LogisticsRouteIntelligenceDirector _route;
        private GameObject _jammerObject;
        private Health _jammerHealth;
        private bool _active;
        private int _round = -1;
        private int _routeLane = -1;
        private int _jammerLane = -1;
        private int _intelPackets;
        private int _relaySyncs;
        private int _jammersDestroyed;
        private int _intelForces;
        private int _guardOrders;
        private int _guardCount;
        private int _externalSuppressions;
        private int _recoveredPackets;
        private float _nextGuardOrder;
        private float _nextSignalPulse;
        private float _counterJamUntil;
        private float _mobileJammingUntil;
        private ReconEWState _state;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _bodyStyle;

        public static ReconElectronicWarfareDirector Instance => _instance;
        public bool OperationActive => _active && _route != null && _route.RouteActive && _sustainment != null && _sustainment.ColumnTeam == Team.Enemy;
        public bool JammerActive => _jammerObject != null && _jammerHealth != null && !_jammerHealth.IsDead;
        public bool CounterJammingActive => OperationActive && Time.time < _counterJamUntil;
        public bool ExternalMobileJammingActive => OperationActive && Time.time < _mobileJammingUntil;
        public bool EffectiveJammerActive => OperationActive && (JammerActive || ExternalMobileJammingActive) && !CounterJammingActive;
        public int IntelPackets => _intelPackets;
        public int RelaySyncs => _relaySyncs;
        public int JammersDestroyed => _jammersDestroyed;
        public int IntelForces => _intelForces;
        public int GuardOrders => _guardOrders;
        public int GuardCount => _guardCount;
        public int RouteLane => _routeLane;
        public int JammerLane => _jammerLane;
        public int SuppressedRelays => SuppressedRelayCount();
        public int ExternalSuppressions => _externalSuppressions;
        public int RecoveredPackets => _recoveredPackets;
        public ReconEWState State => _state;
        public float CounterJamRemaining => CounterJammingActive ? Mathf.Max(0f, _counterJamUntil - Time.time) : 0f;
        public float SignalQuality => SignalQualityForState(_intelPackets, EffectiveJammerActive, Mathf.Max(0, LiveRelayCount() - SuppressedRelayCount()));
        public bool SpoofRisk => OperationActive && EffectiveJammerActive && _intelPackets < RequiredPacketsForVerified;

        public static bool BridgeAvailable => RouteSetIntelMethod != null;
        public static bool ExternalWarfareBridgeValid =>
            ExternalSuppressionMinSeconds > 0f && ExternalSuppressionMaxSeconds <= 8f && ExternalSuppressionMinSeconds < ExternalSuppressionMaxSeconds &&
            CounterJamMinSeconds >= 1f && CounterJamMaxSeconds <= 12f && CounterJamMinSeconds < CounterJamMaxSeconds &&
            MobileJammingPulseMaxSeconds > 0.5f && MobileJammingPulseMaxSeconds <= 2f;

        public static bool ConfigurationValid =>
            MaxRelayNodes == 2 && MaxJammers == 1 && MaxGuardActors >= 2 && MaxGuardActors <= 3 &&
            RequiredPacketsForVerified == MaxRelayNodes &&
            RelaySyncRadius >= 2.0f && RelaySyncRadius <= 3.2f &&
            JammedRelaySyncRadius >= 1.1f && JammedRelaySyncRadius < RelaySyncRadius &&
            GuardSearchRadius >= 8f && GuardSearchRadius <= 11f &&
            GuardOrderCadence >= TacticalNavigationDirector.DecisionCadence && GuardOrderCadence <= 0.50f &&
            SignalPulseSeconds >= 0.6f && SignalPulseSeconds <= 1.2f &&
            RelayHealthMin >= 3 && RelayHealthMax <= 8 && RelayHealthMin <= RelayHealthMax &&
            JammerHealthMin >= 5 && JammerHealthMax <= 12 && JammerHealthMin < JammerHealthMax &&
            JammerBondReward >= 2 && JammerBondReward <= 8 && BridgeAvailable && ExternalWarfareBridgeValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ReconElectronicWarfareDirector>() != null) return;
            GameObject go = new GameObject("ReconElectronicWarfareDirector_v12_3");
            DontDestroyOnLoad(go);
            go.AddComponent<ReconElectronicWarfareDirector>();
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
            if (_sustainment == null) _sustainment = OperationalSustainmentDirector.Instance;
            if (_route == null) _route = LogisticsRouteIntelligenceDirector.Instance;

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

            bool eligible = _route != null && _route.RouteActive && _sustainment != null &&
                            _sustainment.ColumnActive && _sustainment.ColumnTeam == Team.Enemy;
            if (!eligible)
            {
                if (_active) CleanupOperation();
                return;
            }

            if (!_active) BeginOperation(currentRound);
            if (!_active) return;

            UpdateRelaySync();
            UpdateState();

            if (Time.time >= _nextGuardOrder)
            {
                _nextGuardOrder = Time.time + GuardOrderCadence;
                AssignEWGuards();
            }

            if (Time.time >= _nextSignalPulse)
            {
                _nextSignalPulse = Time.time + SignalPulseSeconds;
                PulseSignals();
            }
        }

        private void BeginOperation(int round)
        {
            if (_route == null || !_route.RouteActive || _sustainment == null || _sustainment.ColumnTeam != Team.Enemy || !BridgeAvailable) return;
            _active = true;
            _routeLane = Mathf.Clamp(_route.CurrentRouteLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            _jammerLane = JammerLaneForRoute(_routeLane, round);
            _intelPackets = 0;
            _guardCount = 0;
            _counterJamUntil = 0f;
            _mobileJammingUntil = 0f;
            _state = ReconEWState.Searching;
            for (int i = 0; i < MaxRelayNodes; i++)
            {
                _relaySynced[i] = false;
                _relaySuppressedUntil[i] = 0f;
            }
            SpawnRelays(round);
            SpawnJammer(round);
            _nextGuardOrder = Time.time;
            _nextSignalPulse = Time.time + 0.25f;
            ShowStatus("RECON NETWORK ONLINE // SYNC RELAYS OR DESTROY ENEMY JAMMER", 4.2f);
        }

        private void SpawnRelays(int round)
        {
            for (int i = 0; i < MaxRelayNodes; i++)
            {
                int lane = RelayLaneForIndex(_routeLane, i);
                Vector2 position = RelayAnchor(lane, i);
                GameObject relay = new GameObject("ORZELEK_SCOUT_RELAY_" + (i + 1) + "_v12_3");
                relay.transform.position = position;
                Color body = new Color(0.06f, 0.28f, 0.48f);
                Color accent = new Color(0.18f, 0.92f, 1f);
                VisualFactory.Rect("RelayBase", relay.transform, new Vector2(0.82f, 0.54f), body, Vector3.zero, 8);
                VisualFactory.Rect("RelayMast", relay.transform, new Vector2(0.12f, 0.78f), accent, new Vector3(0f, 0.48f, 0f), 9);
                VisualFactory.Disc("RelayDish", relay.transform, new Vector2(0.34f, 0.18f), accent, new Vector3(0f, 0.86f, 0f), 10);
                BoxCollider2D collider = relay.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.80f, 0.54f);
                Rigidbody2D body2d = relay.AddComponent<Rigidbody2D>();
                body2d.bodyType = RigidbodyType2D.Kinematic;
                body2d.gravityScale = 0f;
                body2d.freezeRotation = true;
                Health health = relay.AddComponent<Health>();
                health.Initialize(Team.Player, RelayHealthForRound(round));
                int captured = i;
                health.Damaged += (h, amount) => OnRelayDamaged(captured, h, amount);
                health.Died += h => OnRelayDied(captured, h);
                _relayObjects[i] = relay;
                _relayHealth[i] = health;
                VisualFactory.RingPulse(position, accent, 0.72f);
            }
        }

        private void SpawnJammer(int round)
        {
            Vector2 position = JammerAnchor(_jammerLane, round);
            _jammerObject = new GameObject("ENEMY_EW_JAMMER_v12_3");
            _jammerObject.transform.position = position;
            Color body = new Color(0.38f, 0.08f, 0.13f);
            Color accent = new Color(1f, 0.18f, 0.42f);
            VisualFactory.Rect("JammerBase", _jammerObject.transform, new Vector2(1.05f, 0.68f), body, Vector3.zero, 8);
            VisualFactory.Rect("JammerArrayL", _jammerObject.transform, new Vector2(0.16f, 0.82f), accent, new Vector3(-0.26f, 0.48f, 0f), 9);
            VisualFactory.Rect("JammerArrayR", _jammerObject.transform, new Vector2(0.16f, 0.82f), accent, new Vector3(0.26f, 0.48f, 0f), 9);
            VisualFactory.Disc("JammerEmitter", _jammerObject.transform, new Vector2(0.38f, 0.38f), new Color(1f, 0.36f, 0.08f), new Vector3(0f, 0.88f, 0f), 10);
            BoxCollider2D collider = _jammerObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.02f, 0.66f);
            Rigidbody2D body2d = _jammerObject.AddComponent<Rigidbody2D>();
            body2d.bodyType = RigidbodyType2D.Kinematic;
            body2d.gravityScale = 0f;
            body2d.freezeRotation = true;
            _jammerHealth = _jammerObject.AddComponent<Health>();
            _jammerHealth.Initialize(Team.Enemy, JammerHealthForRound(round));
            _jammerHealth.Damaged += OnJammerDamaged;
            _jammerHealth.Died += OnJammerDied;
            VisualFactory.RingPulse(position, accent, 1.05f);
        }

        private void UpdateRelaySync()
        {
            PlayerTank player = CombatRoster.Player;
            if (player == null || player.Health == null || player.Health.IsDead) return;
            float radius = EffectiveJammerActive ? JammedRelaySyncRadius : RelaySyncRadius;
            float radiusSq = radius * radius;
            for (int i = 0; i < MaxRelayNodes; i++)
            {
                if (_relaySynced[i] || IsRelaySuppressed(i)) continue;
                GameObject relay = _relayObjects[i];
                Health health = _relayHealth[i];
                if (relay == null || health == null || health.IsDead) continue;
                float d = ((Vector2)player.transform.position - (Vector2)relay.transform.position).sqrMagnitude;
                if (d > radiusSq) continue;
                _relaySynced[i] = true;
                _intelPackets = Mathf.Clamp(_intelPackets + 1, 0, RequiredPacketsForVerified);
                _relaySyncs++;
                RouteIntelState intel = IntelForPackets(_intelPackets, EffectiveJammerActive);
                RaiseRouteIntel(intel);
                VisualFactory.RingPulse(relay.transform.position, new Color(0.18f, 1f, 0.64f), 1.0f);
                BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.20f, 0.02f);
                ShowStatus("RECON PACKET " + _intelPackets + "/" + RequiredPacketsForVerified + " // " + intel.ToString().ToUpperInvariant(), 2.8f);
            }
        }

        private void UpdateState()
        {
            if (!_active) { _state = ReconEWState.Inactive; return; }
            if (LiveRelayCount() == 0) { _state = ReconEWState.RelayLost; return; }
            RouteIntelState intel = IntelForPackets(_intelPackets, EffectiveJammerActive);
            if (intel == RouteIntelState.Verified) _state = ReconEWState.Verified;
            else if (intel == RouteIntelState.Contact) _state = ReconEWState.Contact;
            else _state = EffectiveJammerActive ? ReconEWState.Jammed : ReconEWState.Searching;
        }

        private void PulseSignals()
        {
            if (!_active) return;
            Color relayColor = EffectiveJammerActive ? new Color(0.16f, 0.66f, 0.92f) : new Color(0.18f, 1f, 0.64f);
            for (int i = 0; i < MaxRelayNodes; i++)
            {
                if (_relayObjects[i] == null || _relayHealth[i] == null || _relayHealth[i].IsDead) continue;
                bool suppressed = IsRelaySuppressed(i);
                Color color = suppressed ? new Color(1f, 0.45f, 0.08f) : relayColor;
                VisualFactory.RingPulse(_relayObjects[i].transform.position, color, suppressed ? 0.54f : (_relaySynced[i] ? 0.46f : 0.30f));
            }
            if (JammerActive)
                VisualFactory.RingPulse(_jammerObject.transform.position, CounterJammingActive ? new Color(0.18f, 1f, 0.64f) : new Color(1f, 0.18f, 0.42f), 0.42f);
        }

        private void AssignEWGuards()
        {
            _guardCount = 0;
            if (!JammerActive) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;
            Vector2 anchor = _jammerObject.transform.position;
            for (int i = 0; i < MaxGuardActors; i++) { _guards[i] = null; _guardDistances[i] = float.MaxValue; }

            int count = 0;
            float limitSq = GuardSearchRadius * GuardSearchRadius;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || !IsEWGuardKind(enemy.Kind)) continue;
                float d = ((Vector2)enemy.transform.position - anchor).sqrMagnitude;
                if (d > limitSq) continue;
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
                EnemyTank guard = _guards[i];
                if (guard == null || guard.Health == null || guard.Health.IsDead) continue;
                TacticalNavigationAgent nav = guard.GetComponent<TacticalNavigationAgent>();
                if (nav == null)
                {
                    nav = guard.gameObject.AddComponent<TacticalNavigationAgent>();
                    nav.Initialize(guard);
                }
                float side = (i & 1) == 0 ? -1f : 1f;
                Vector2 objective = anchor + new Vector2(side * (0.95f + i * 0.30f), -0.30f + i * 0.22f);
                SquadTacticalRole role = EWGuardRole(guard.Kind);
                float standoff = guard.Kind == EnemyKind.Sniper ? 5.2f : 1.45f + 0.18f * i;
                float speed = guard.Kind == EnemyKind.Fast ? 1.10f : guard.Kind == EnemyKind.Elite ? 1.03f : 0.90f;
                nav.SetRole(role);
                nav.SetOrder(objective, standoff, speed, enemies);
                _guardOrders++;
            }
        }

        private void OnRelayDamaged(int index, Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("SCOUT RELAY " + (index + 1) + " UNDER FIRE // SIGNAL DEGRADED", 2.0f);
        }

        private void OnRelayDied(int index, Health health)
        {
            if (index >= 0 && index < MaxRelayNodes)
            {
                if (_relaySynced[index]) _intelPackets = Mathf.Max(0, _intelPackets - 1);
                _relaySynced[index] = false;
                _relaySuppressedUntil[index] = 0f;
            }
            ShowStatus("SCOUT RELAY " + (index + 1) + " LOST // RECON COVERAGE REDUCED", 3.0f);
        }

        private void OnJammerDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            ShowStatus("ENEMY JAMMER HIT // EW SCREEN WEAKENING", 2.0f);
        }

        private void OnJammerDied(Health health)
        {
            _jammersDestroyed++;
            RouteIntelState intel = IntelForPackets(_intelPackets, ExternalMobileJammingActive && !CounterJammingActive);
            RaiseRouteIntel(intel);
            WarEconomyDirector.AwardMissionBonds(JammerBondReward, "ENEMY EW JAMMER DESTROYED");
            if (_jammerObject != null)
            {
                VisualFactory.Explosion(_jammerObject.transform.position, new Color(1f, 0.18f, 0.42f), 1.05f);
                VisualFactory.RingPulse(_jammerObject.transform.position, new Color(0.18f, 1f, 0.64f), 1.35f);
            }
            ShowStatus("EW JAMMER DESTROYED // SIGNAL RESTORED // " + intel.ToString().ToUpperInvariant(), 4.0f);
        }

        private void RaiseRouteIntel(RouteIntelState target)
        {
            if (_route == null || RouteSetIntelMethod == null || target == RouteIntelState.Unknown) return;
            if (_route.IntelState >= target) return;
            RouteSetIntelMethod.Invoke(_route, new object[] { target });
            _intelForces++;
        }

        public bool TryGetRelayPosition(int index, out Vector2 position)
        {
            position = Vector2.zero;
            if (index < 0 || index >= MaxRelayNodes) return false;
            GameObject relay = _relayObjects[index];
            Health health = _relayHealth[index];
            if (!_active || relay == null || health == null || health.IsDead) return false;
            position = relay.transform.position;
            return true;
        }

        public bool IsRelaySuppressed(int index)
        {
            return index >= 0 && index < MaxRelayNodes && _active && Time.time < _relaySuppressedUntil[index];
        }

        public bool SuppressRelay(int index, float seconds)
        {
            if (index < 0 || index >= MaxRelayNodes || !_active) return false;
            GameObject relay = _relayObjects[index];
            Health health = _relayHealth[index];
            if (relay == null || health == null || health.IsDead) return false;
            float until = Time.time + ClampSuppressionSeconds(seconds);
            if (until <= _relaySuppressedUntil[index] + 0.01f) return false;
            _relaySuppressedUntil[index] = until;
            if (_relaySynced[index])
            {
                _relaySynced[index] = false;
                _intelPackets = Mathf.Max(0, _intelPackets - 1);
            }
            _externalSuppressions++;
            VisualFactory.RingPulse(relay.transform.position, new Color(1f, 0.45f, 0.08f), 0.85f);
            ShowStatus("SCOUT RELAY " + (index + 1) + " SUPPRESSED // CLEAR RAID AND RE-SYNC", 2.8f);
            return true;
        }

        public void ApplyCounterJamming(float seconds)
        {
            if (!_active) return;
            _counterJamUntil = Mathf.Max(_counterJamUntil, Time.time + ClampCounterJamSeconds(seconds));
            ShowStatus("COUNTER-JAM WINDOW ACTIVE // FULL RELAY SYNC RANGE", 2.8f);
        }

        public void ApplyMobileJammingPulse(float seconds)
        {
            if (!_active || CounterJammingActive) return;
            _mobileJammingUntil = Mathf.Max(_mobileJammingUntil, Time.time + Mathf.Clamp(seconds, 0.25f, MobileJammingPulseMaxSeconds));
        }

        public bool ApplyRecoveredIntelPacket()
        {
            if (!_active || _intelPackets >= RequiredPacketsForVerified) return false;
            _intelPackets++;
            _recoveredPackets++;
            RouteIntelState intel = IntelForPackets(_intelPackets, EffectiveJammerActive);
            RaiseRouteIntel(intel);
            ShowStatus("RECOVERED EW INTELLIGENCE // PACKETS " + _intelPackets + "/" + RequiredPacketsForVerified, 2.8f);
            return true;
        }

        public static float ClampSuppressionSeconds(float seconds)
        {
            return Mathf.Clamp(seconds, ExternalSuppressionMinSeconds, ExternalSuppressionMaxSeconds);
        }

        public static float ClampCounterJamSeconds(float seconds)
        {
            return Mathf.Clamp(seconds, CounterJamMinSeconds, CounterJamMaxSeconds);
        }

        public static RouteIntelState IntelForPackets(int packets, bool jammerAlive)
        {
            int safe = Mathf.Clamp(packets, 0, RequiredPacketsForVerified);
            if (safe >= RequiredPacketsForVerified) return RouteIntelState.Verified;
            if (safe >= 1 || !jammerAlive) return RouteIntelState.Contact;
            return RouteIntelState.Unknown;
        }

        public static float SignalQualityForState(int packets, bool jammerAlive, int liveRelays)
        {
            float quality = jammerAlive ? 0.16f : 0.42f;
            quality += Mathf.Clamp(packets, 0, RequiredPacketsForVerified) * 0.29f;
            quality += Mathf.Clamp(liveRelays, 0, MaxRelayNodes) * 0.08f;
            return Mathf.Clamp01(quality);
        }

        public static int RelayLaneForIndex(int routeLane, int index)
        {
            int lane = Mathf.Clamp(routeLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            if (index <= 0) return lane == 0 ? 1 : lane - 1;
            return lane == DynamicFrontlineTerritoryDirector.LaneCount - 1 ? lane - 1 : lane + 1;
        }

        public static int JammerLaneForRoute(int routeLane, int round)
        {
            int lane = Mathf.Clamp(routeLane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1);
            int candidate = (lane + ((Mathf.Clamp(round, 1, 100) & 1) == 0 ? 1 : 2)) % DynamicFrontlineTerritoryDirector.LaneCount;
            if (candidate == lane) candidate = (candidate + 1) % DynamicFrontlineTerritoryDirector.LaneCount;
            return candidate;
        }

        public static Vector2 RelayAnchor(int lane, int index)
        {
            Vector2 basePos = DynamicFrontlineTerritoryDirector.LanePosition(Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1));
            float xOffset = (index & 1) == 0 ? -0.42f : 0.42f;
            return new Vector2(Mathf.Clamp(basePos.x + xOffset, -CombinedArmsMobileFrontDirector.ArenaXLimit, CombinedArmsMobileFrontDirector.ArenaXLimit), RelayY + (index & 1) * 0.55f);
        }

        public static Vector2 JammerAnchor(int lane, int round)
        {
            Vector2 basePos = DynamicFrontlineTerritoryDirector.LanePosition(Mathf.Clamp(lane, 0, DynamicFrontlineTerritoryDirector.LaneCount - 1));
            float xOffset = ((Mathf.Clamp(round, 1, 100) / 2) & 1) == 0 ? -0.36f : 0.36f;
            return new Vector2(Mathf.Clamp(basePos.x + xOffset, -CombinedArmsMobileFrontDirector.ArenaXLimit, CombinedArmsMobileFrontDirector.ArenaXLimit), JammerY);
        }

        public static int RelayHealthForRound(int round)
        {
            return Mathf.Clamp(RelayHealthMin + Mathf.Max(0, round - OperationalSustainmentDirector.EarliestRound) / 25, RelayHealthMin, RelayHealthMax);
        }

        public static int JammerHealthForRound(int round)
        {
            return Mathf.Clamp(JammerHealthMin + Mathf.Max(0, round - OperationalSustainmentDirector.EarliestRound) / 10, JammerHealthMin, JammerHealthMax);
        }

        public static bool IsEWGuardKind(EnemyKind kind)
        {
            return kind == EnemyKind.Fast || kind == EnemyKind.Elite || kind == EnemyKind.Sniper;
        }

        public static SquadTacticalRole EWGuardRole(EnemyKind kind)
        {
            if (kind == EnemyKind.Sniper) return SquadTacticalRole.Suppressor;
            if (kind == EnemyKind.Fast) return SquadTacticalRole.Flanker;
            return SquadTacticalRole.Escort;
        }

        private int LiveRelayCount()
        {
            int count = 0;
            for (int i = 0; i < MaxRelayNodes; i++)
                if (_relayObjects[i] != null && _relayHealth[i] != null && !_relayHealth[i].IsDead) count++;
            return count;
        }

        private int SuppressedRelayCount()
        {
            int count = 0;
            for (int i = 0; i < MaxRelayNodes; i++) if (IsRelaySuppressed(i)) count++;
            return count;
        }

        private void CleanupOperation()
        {
            for (int i = 0; i < MaxRelayNodes; i++)
            {
                if (_relayObjects[i] != null) Destroy(_relayObjects[i]);
                _relayObjects[i] = null;
                _relayHealth[i] = null;
                _relaySynced[i] = false;
                _relaySuppressedUntil[i] = 0f;
            }
            if (_jammerHealth != null)
            {
                _jammerHealth.Damaged -= OnJammerDamaged;
                _jammerHealth.Died -= OnJammerDied;
            }
            if (_jammerObject != null) Destroy(_jammerObject);
            _jammerObject = null;
            _jammerHealth = null;
            _active = false;
            _routeLane = -1;
            _jammerLane = -1;
            _intelPackets = 0;
            _guardCount = 0;
            _counterJamUntil = 0f;
            _mobileJammingUntil = 0f;
            _state = ReconEWState.Inactive;
            for (int i = 0; i < MaxGuardActors; i++) _guards[i] = null;
        }

        private void ResetRun()
        {
            CleanupOperation();
            _round = -1;
            _relaySyncs = 0;
            _jammersDestroyed = 0;
            _intelForces = 0;
            _guardOrders = 0;
            _externalSuppressions = 0;
            _recoveredPackets = 0;
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
            _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.26f, 0.96f, 0.82f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (!OperationActive && Time.unscaledTime >= _statusUntil) return;
            EnsureStyles();
            float width = 570f;
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = 205f;
            GUI.color = new Color(0.02f, 0.04f, 0.055f, 0.90f);
            GUI.Box(new Rect(x, y, width, OperationActive ? 58f : 34f), string.Empty);
            GUI.color = Color.white;
            string title = OperationActive ? "RECON / EW // " + _state.ToString().ToUpperInvariant() : _status;
            GUI.Label(new Rect(x + 8f, y + 3f, width - 16f, 20f), title, _header);
            if (OperationActive)
            {
                string jammer = EffectiveJammerActive ? "JAMMER EFFECTIVE" : (JammerActive ? "JAMMER BYPASSED" : "JAMMER DOWN");
                string spoof = SpoofRisk ? "SPOOF RISK" : "ROUTE CLEAN";
                string counter = CounterJammingActive ? " // C-JAM " + CounterJamRemaining.ToString("0.0") + "s" : string.Empty;
                GUI.Label(new Rect(x + 8f, y + 25f, width - 16f, 20f),
                    "PACKETS " + _intelPackets + "/" + RequiredPacketsForVerified + " // SIGNAL " + Mathf.RoundToInt(SignalQuality * 100f) + "% // " + jammer + " // " + spoof + " // SUPP " + SuppressedRelayCount() + " // GUARD " + _guardCount + counter,
                    _bodyStyle);
            }
        }
    }
}
