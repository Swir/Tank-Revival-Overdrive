using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.9 combat-engineering layer. It reacts to v11.8 breach intelligence and deploys bounded,
    /// temporary battlefield assets. Obstacle remains structural authority; Health remains vehicle
    /// authority; EnemyTank/Rigidbody2D remain movement authority. Closing a gap only resolves its
    /// tactical snapshot after a real replacement Obstacle has been placed.
    /// </summary>
    [DefaultExecutionOrder(545)]
    public sealed class CombatEngineeringCounterBreachDirector : MonoBehaviour
    {
        public const int FriendlyResponseRound = 18;
        public const int EnemyCounterBreachRound = 36;
        public const int MaxActiveAssets = 8;
        public const int MaxActiveBarriers = 4;
        public const int MaxActiveMines = 4;
        public const int MaxTrackedSectors = 12;
        public const int MaxSectorAttempts = 2;
        public const float DecisionInterval = 0.65f;
        public const float FriendlyResponseRadius = 8.5f;
        public const float EnemyResponseRadius = 7.5f;
        public const float SectorMergeRadius = 1.15f;
        public const float SectorCooldown = 9.0f;
        public const float BarrierLifetime = 20f;
        public const float MineLifetime = 16f;
        public const float UnitPlacementClearance = 0.82f;

        private struct SectorMemory
        {
            public bool Valid;
            public Vector2 Position;
            public Team DefenderTeam;
            public int Attempts;
            public int LastBreachSequence;
            public float CooldownUntil;
        }

        private sealed class FieldAsset
        {
            public GameObject Root;
            public Team DefenderTeam;
            public bool Barrier;
            public int SourceSequence;
        }

        private static CombatEngineeringCounterBreachDirector _instance;
        private readonly List<FieldAsset> _assets = new List<FieldAsset>(MaxActiveAssets);
        private readonly SectorMemory[] _sectors = new SectorMemory[MaxTrackedSectors];
        private TankGame _game;
        private int _round = -1;
        private int _sectorCursor;
        private int _friendlyDeployments;
        private int _enemyDeployments;
        private int _resolvedOpenings;
        private int _peakAssets;
        private float _nextDecision;
        private string _lastOrder = "ENGINEERING NET STANDBY";
        private float _lastOrderUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static CombatEngineeringCounterBreachDirector Instance => _instance;
        public int ActiveAssets { get { PruneAssets(); return _assets.Count; } }
        public int ActiveBarriers => CountAssets(true);
        public int ActiveMines => CountAssets(false);
        public int FriendlyDeployments => _friendlyDeployments;
        public int EnemyDeployments => _enemyDeployments;
        public int ResolvedOpenings => _resolvedOpenings;
        public int PeakAssets => _peakAssets;
        public int TrackedSectors => CountTrackedSectors();

        public static bool ConfigurationValid =>
            FriendlyResponseRound >= 10 && EnemyCounterBreachRound > FriendlyResponseRound && EnemyCounterBreachRound <= 50 &&
            MaxActiveAssets >= 4 && MaxActiveAssets <= 10 && MaxActiveBarriers <= MaxActiveAssets && MaxActiveMines <= MaxActiveAssets &&
            MaxTrackedSectors >= MaxActiveAssets && MaxTrackedSectors <= ReactiveCoverBreachDirector.MaxRecentBreaches &&
            MaxSectorAttempts >= 1 && MaxSectorAttempts <= 3 && DecisionInterval >= 0.4f && DecisionInterval <= 1.2f &&
            FriendlyResponseRadius <= 10f && EnemyResponseRadius <= 9f && SectorCooldown >= 6f && SectorCooldown <= ReactiveCoverBreachDirector.RecentBreachSeconds &&
            BarrierLifetime >= ReactiveCoverBreachDirector.RecentBreachSeconds && MineLifetime >= 10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatEngineeringCounterBreachDirector>() != null) return;
            GameObject go = new GameObject("CombatEngineeringCounterBreachDirector_v11_9");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatEngineeringCounterBreachDirector>();
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
            CleanupAssets(true);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round) BeginRound(current);
            if (Time.time < _nextDecision) return;
            _nextDecision = Time.time + DecisionInterval;

            PruneAssets();
            if (_round >= FriendlyResponseRound) TryRespondForPlayer();
            if (_round >= EnemyCounterBreachRound) TryRespondForEnemy();
        }

        public static ObstacleKind BarrierKindForRound(int round, Team defenderTeam)
        {
            int r = Mathf.Clamp(round, 1, 100);
            int steelThreshold = defenderTeam == Team.Player ? 58 : 68;
            return r >= steelThreshold ? ObstacleKind.Steel : ObstacleKind.Brick;
        }

        public static int BarrierHitPointsForRound(int round, Team defenderTeam)
        {
            int r = Mathf.Clamp(round, 1, 100);
            ObstacleKind kind = BarrierKindForRound(r, defenderTeam);
            if (kind == ObstacleKind.Steel) return Mathf.Clamp(6 + (r - 55) / 20, 6, 8);
            return Mathf.Clamp(3 + r / 35, 3, 5);
        }

        public static int MineDamageForRound(int round)
        {
            int r = Mathf.Clamp(round, 1, 100);
            return r >= 78 ? 4 : r >= 52 ? 3 : 2;
        }

        public static bool CanEnemyCounterBreachRound(int round)
        {
            return round >= EnemyCounterBreachRound && round <= 100;
        }

        private void BeginRound(int round)
        {
            CleanupAssets(true);
            ClearSectorMemory();
            _round = round;
            _friendlyDeployments = 0;
            _enemyDeployments = 0;
            _resolvedOpenings = 0;
            _peakAssets = 0;
            _nextDecision = Time.time + 0.9f;
            Announce(round >= EnemyCounterBreachRound ? "COUNTER-BREACH WARFARE ONLINE" : "ORZELEK ENGINEER RESERVE ONLINE");
        }

        private void ResetRun()
        {
            CleanupAssets(true);
            ClearSectorMemory();
            _round = -1;
            _friendlyDeployments = 0;
            _enemyDeployments = 0;
            _resolvedOpenings = 0;
            _peakAssets = 0;
        }

        private void TryRespondForPlayer()
        {
            if (_game == null) return;
            if (!ReactiveCoverBreachDirector.TryFindRecentBreachNear(_game.BasePosition, Team.Enemy, FriendlyResponseRadius, out ReactiveCoverBreachDirector.BreachSnapshot breach)) return;
            TryDeployCounterBreach(breach, Team.Player, _game.BasePosition);
        }

        private void TryRespondForEnemy()
        {
            if (!TryResolveEnemyEngineerAnchor(out Vector2 anchor)) return;
            if (!ReactiveCoverBreachDirector.TryFindRecentBreachNear(anchor, Team.Player, EnemyResponseRadius, out ReactiveCoverBreachDirector.BreachSnapshot breach)) return;
            TryDeployCounterBreach(breach, Team.Enemy, anchor);
        }

        private bool TryDeployCounterBreach(ReactiveCoverBreachDirector.BreachSnapshot breach, Team defenderTeam, Vector2 protectedAnchor)
        {
            if (!breach.Valid || breach.Sequence <= 0) return false;
            if (!TryAcquireSector(breach, defenderTeam, out int sectorIndex)) return false;

            bool barrierPlaced = TryDeployBarrier(breach, defenderTeam, protectedAnchor);
            bool minePlaced = TryDeployMine(breach, defenderTeam, protectedAnchor);
            if (!barrierPlaced && !minePlaced) return false;

            SectorMemory sector = _sectors[sectorIndex];
            sector.Attempts++;
            sector.LastBreachSequence = breach.Sequence;
            sector.CooldownUntil = Time.time + SectorCooldown;
            sector.Position = breach.Position;
            _sectors[sectorIndex] = sector;

            if (barrierPlaced && ReactiveCoverBreachDirector.ResolveBreach(breach.Sequence))
                _resolvedOpenings++;

            if (defenderTeam == Team.Player) _friendlyDeployments++;
            else _enemyDeployments++;

            string side = defenderTeam == Team.Player ? "ORZELEK" : "ENEMY";
            string package = barrierPlaced && minePlaced ? "BARRIER + DENIAL CHARGE" : barrierPlaced ? "FIELD BARRIER" : "DENIAL CHARGE";
            Announce(side + " COUNTER-BREACH // " + package);
            return true;
        }

        private bool TryDeployBarrier(ReactiveCoverBreachDirector.BreachSnapshot breach, Team defenderTeam, Vector2 protectedAnchor)
        {
            if (_assets.Count >= MaxActiveAssets || CountAssets(true) >= MaxActiveBarriers) return false;
            if (!IsPlacementClear(breach.Position)) return false;

            Vector2 threatDirection = ResolveThreatDirection(breach.Position, defenderTeam, protectedAnchor);
            ObstacleKind kind = BarrierKindForRound(_round, defenderTeam);
            int hp = BarrierHitPointsForRound(_round, defenderTeam);
            GameObject root = new GameObject(defenderTeam == Team.Player ? "ORZELEK_COUNTER_BREACH_BARRIER" : "ENEMY_COUNTER_BREACH_BARRIER");
            root.transform.SetParent(transform, true);
            root.transform.position = breach.Position;
            if (threatDirection.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(threatDirection.y, threatDirection.x) * Mathf.Rad2Deg + 90f;
                root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = kind == ObstacleKind.Steel ? new Vector2(1.50f, 0.52f) : new Vector2(1.38f, 0.48f);
            Obstacle obstacle = root.AddComponent<Obstacle>();
            obstacle.Initialize(kind, hp);

            Color body = defenderTeam == Team.Player
                ? (kind == ObstacleKind.Steel ? new Color(0.20f, 0.48f, 0.58f) : new Color(0.22f, 0.36f, 0.30f))
                : (kind == ObstacleKind.Steel ? new Color(0.50f, 0.18f, 0.12f) : new Color(0.42f, 0.16f, 0.09f));
            Color stripe = defenderTeam == Team.Player ? new Color(0.25f, 0.95f, 1f) : new Color(1f, 0.30f, 0.10f);
            VisualFactory.Rect("EngineeringBarrierShadow", root.transform, collider.size + new Vector2(0.10f, 0.09f), new Color(0f, 0f, 0f, 0.44f), new Vector3(0.04f, -0.04f, 0f), 4);
            VisualFactory.Rect("EngineeringBarrierBody", root.transform, collider.size, body, Vector3.zero, 5);
            VisualFactory.Rect("EngineeringBarrierStripe", root.transform, new Vector2(collider.size.x * 0.78f, 0.075f), stripe, new Vector3(0f, 0.11f, 0f), 6);
            VisualFactory.RingPulse(root.transform.position, stripe, 0.85f);

            CounterBreachFieldAsset lifetime = root.AddComponent<CounterBreachFieldAsset>();
            lifetime.Initialize(defenderTeam, true, BarrierLifetime, breach.Sequence);
            RegisterAsset(root, defenderTeam, true, breach.Sequence);
            return true;
        }

        private bool TryDeployMine(ReactiveCoverBreachDirector.BreachSnapshot breach, Team defenderTeam, Vector2 protectedAnchor)
        {
            if (_assets.Count >= MaxActiveAssets || CountAssets(false) >= MaxActiveMines) return false;
            Vector2 threatDirection = ResolveThreatDirection(breach.Position, defenderTeam, protectedAnchor);
            if (threatDirection.sqrMagnitude < 0.01f) threatDirection = defenderTeam == Team.Player ? Vector2.up : Vector2.down;
            Vector2 position = breach.Position + threatDirection.normalized * 0.72f;

            GameObject root = new GameObject(defenderTeam == Team.Player ? "ORZELEK_BREACH_DENIAL_CHARGE" : "ENEMY_BREACH_DENIAL_CHARGE");
            root.transform.SetParent(transform, true);
            root.transform.position = position;
            CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.42f;
            trigger.isTrigger = true;
            Color color = defenderTeam == Team.Player ? new Color(0.22f, 0.92f, 1f) : new Color(1f, 0.24f, 0.08f);
            VisualFactory.Disc("ChargePlate", root.transform, new Vector2(0.42f, 0.42f), new Color(color.r * 0.30f, color.g * 0.30f, color.b * 0.30f), Vector3.zero, 7);
            VisualFactory.Disc("ChargeCore", root.transform, new Vector2(0.16f, 0.16f), color, Vector3.zero, 8);
            VisualFactory.RingObject("ChargeTelegraph", root.transform, Vector2.one * 0.62f, new Color(color.r, color.g, color.b, 0.46f), Vector3.zero, 8);

            CounterBreachMine mine = root.AddComponent<CounterBreachMine>();
            mine.Initialize(defenderTeam, MineDamageForRound(_round), MineLifetime, breach.Sequence);
            RegisterAsset(root, defenderTeam, false, breach.Sequence);
            return true;
        }

        private void RegisterAsset(GameObject root, Team defenderTeam, bool barrier, int sourceSequence)
        {
            _assets.Add(new FieldAsset { Root = root, DefenderTeam = defenderTeam, Barrier = barrier, SourceSequence = sourceSequence });
            if (_assets.Count > _peakAssets) _peakAssets = _assets.Count;
        }

        private bool TryAcquireSector(ReactiveCoverBreachDirector.BreachSnapshot breach, Team defenderTeam, out int index)
        {
            index = -1;
            float mergeSq = SectorMergeRadius * SectorMergeRadius;
            for (int i = 0; i < _sectors.Length; i++)
            {
                SectorMemory sector = _sectors[i];
                if (!sector.Valid || sector.DefenderTeam != defenderTeam) continue;
                if ((sector.Position - breach.Position).sqrMagnitude > mergeSq) continue;
                if (sector.LastBreachSequence == breach.Sequence || sector.Attempts >= MaxSectorAttempts || Time.time < sector.CooldownUntil) return false;
                index = i;
                return true;
            }

            index = _sectorCursor;
            _sectorCursor = (_sectorCursor + 1) % _sectors.Length;
            _sectors[index] = new SectorMemory
            {
                Valid = true,
                Position = breach.Position,
                DefenderTeam = defenderTeam,
                Attempts = 0,
                LastBreachSequence = 0,
                CooldownUntil = 0f
            };
            return true;
        }

        private bool IsPlacementClear(Vector2 position)
        {
            PlayerTank player = CombatRoster.Player;
            if (player != null && player.Health != null && !player.Health.IsDead && Vector2.Distance(player.transform.position, position) < UnitPlacementClearance) return false;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return true;
            float clearanceSq = UnitPlacementClearance * UnitPlacementClearance;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (((Vector2)enemy.transform.position - position).sqrMagnitude < clearanceSq) return false;
            }
            return true;
        }

        private Vector2 ResolveThreatDirection(Vector2 breachPosition, Team defenderTeam, Vector2 protectedAnchor)
        {
            if (defenderTeam == Team.Enemy)
            {
                PlayerTank player = CombatRoster.Player;
                if (player != null && player.Health != null && !player.Health.IsDead)
                    return ((Vector2)player.transform.position - breachPosition).normalized;
                return (protectedAnchor - breachPosition).normalized;
            }

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank closest = null;
            float bestSq = float.MaxValue;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                    float sq = ((Vector2)enemy.transform.position - breachPosition).sqrMagnitude;
                    if (sq >= bestSq) continue;
                    bestSq = sq;
                    closest = enemy;
                }
            }
            if (closest != null) return ((Vector2)closest.transform.position - breachPosition).normalized;
            return (breachPosition - protectedAnchor).normalized;
        }

        private bool TryResolveEnemyEngineerAnchor(out Vector2 anchor)
        {
            SiegeSapper sapper = FindAnyObjectByType<SiegeSapper>();
            if (sapper != null)
            {
                Health hp = sapper.GetComponent<Health>();
                if (hp != null && !hp.IsDead) { anchor = sapper.transform.position; return true; }
            }
            EnemySiegeRelay relay = FindAnyObjectByType<EnemySiegeRelay>();
            if (relay != null)
            {
                Health hp = relay.GetComponent<Health>();
                if (hp != null && !hp.IsDead) { anchor = relay.transform.position; return true; }
            }
            MobileSiegeBattery battery = FindAnyObjectByType<MobileSiegeBattery>();
            if (battery != null)
            {
                Health hp = battery.GetComponent<Health>();
                if (hp != null && !hp.IsDead) { anchor = battery.transform.position; return true; }
            }
            anchor = default;
            return false;
        }

        private int CountAssets(bool barriers)
        {
            PruneAssets();
            int count = 0;
            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i].Root != null && _assets[i].Barrier == barriers) count++;
            return count;
        }

        private int CountTrackedSectors()
        {
            int count = 0;
            for (int i = 0; i < _sectors.Length; i++) if (_sectors[i].Valid) count++;
            return count;
        }

        private void PruneAssets()
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
                if (_assets[i].Root == null) _assets.RemoveAt(i);
        }

        private void CleanupAssets(bool destroy)
        {
            for (int i = _assets.Count - 1; i >= 0; i--)
            {
                GameObject root = _assets[i].Root;
                if (destroy && root != null) Destroy(root);
            }
            _assets.Clear();
        }

        private void ClearSectorMemory()
        {
            for (int i = 0; i < _sectors.Length; i++) _sectors[i] = default;
            _sectorCursor = 0;
        }

        private void Announce(string text)
        {
            _lastOrder = text;
            _lastOrderUntil = Time.unscaledTime + 2.4f;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.78f, 0.30f) } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.90f, 0.94f, 0.98f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _round < FriendlyResponseRound) return;
            EnsureStyles();
            float width = 350f;
            float x = Mathf.Max(12f, Screen.width - width - 14f);
            float y = Mathf.Max(190f, Screen.height - 255f);
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.88f);
            GUI.Box(new Rect(x, y, width, 58f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, y + 6f, width - 20f, 18f), "COMBAT ENGINEERING // COUNTER-BREACH", _titleStyle);
            GUI.Label(new Rect(x + 10f, y + 25f, width - 20f, 17f), "BAR " + ActiveBarriers + "/" + MaxActiveBarriers + "  DENIAL " + ActiveMines + "/" + MaxActiveMines + "  SECTORS " + TrackedSectors + "/" + MaxTrackedSectors, _bodyStyle);
            if (Time.unscaledTime < _lastOrderUntil)
                GUI.Label(new Rect(x + 10f, y + 40f, width - 20f, 16f), _lastOrder, _bodyStyle);
        }
    }

    /// <summary>Lifetime tag for temporary engineering cover. Expiry is not a breach.</summary>
    public sealed class CounterBreachFieldAsset : MonoBehaviour
    {
        public Team DefenderTeam { get; private set; }
        public bool Barrier { get; private set; }
        public int SourceSequence { get; private set; }
        private float _expiresAt;

        public void Initialize(Team defenderTeam, bool barrier, float lifetime, int sourceSequence)
        {
            DefenderTeam = defenderTeam;
            Barrier = barrier;
            SourceSequence = sourceSequence;
            _expiresAt = Time.time + Mathf.Max(2f, lifetime);
        }

        private void Update()
        {
            if (Time.time >= _expiresAt) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Side-aware, telegraphed denial charge. It damages only the opposing vehicle team and expires,
    /// so breach denial is counter-play rather than permanent map spam.
    /// </summary>
    public sealed class CounterBreachMine : MonoBehaviour
    {
        public Team DefenderTeam { get; private set; }
        public int SourceSequence { get; private set; }
        private int _damage;
        private float _expiresAt;
        private bool _spent;

        public void Initialize(Team defenderTeam, int damage, float lifetime, int sourceSequence)
        {
            DefenderTeam = defenderTeam;
            SourceSequence = sourceSequence;
            _damage = Mathf.Clamp(damage, 1, 5);
            _expiresAt = Time.time + Mathf.Max(2f, lifetime);
        }

        private void Update()
        {
            if (Time.time >= _expiresAt) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_spent || other == null) return;
            Health health = other.GetComponent<Health>();
            if (health == null || health.IsDead || health.Team == DefenderTeam || health.Team == Team.Neutral) return;
            _spent = true;
            health.Damage(_damage, DefenderTeam);
            CombatStatus status = health.GetComponent<CombatStatus>();
            if (status == null) status = health.gameObject.AddComponent<CombatStatus>();
            status.ApplyEmp(0.55f);
            Color color = DefenderTeam == Team.Player ? new Color(0.22f, 0.92f, 1f) : new Color(1f, 0.24f, 0.08f);
            VisualFactory.Explosion(transform.position, color, 1.05f);
            VisualFactory.RingPulse(transform.position, color, 1.20f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.48f, 0.05f);
            Destroy(gameObject);
        }
    }
}
