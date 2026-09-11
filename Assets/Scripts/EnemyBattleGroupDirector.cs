using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum EnemyBattleDoctrine
    {
        Assault,
        Pincer,
        SiegeColumn,
        FireSupport,
        Breakthrough,
        EagleRaid,
        FightingWithdrawal
    }

    public enum EnemyBattleRole
    {
        Vanguard,
        LeftFlank,
        RightFlank,
        SiegeEscort,
        FireSupport,
        Raider,
        Reserve,
        Commander
    }

    /// <summary>
    /// v2.7 ENEMY TACTICS & BATTLE GROUPS.
    /// Builds persistent enemy squads from the real combat roster and gives them coordinated
    /// battlefield objectives. The existing EnemyTank component remains responsible for shooting,
    /// armor, status and collision authority; this layer overwrites only the final movement intent
    /// and adds bounded synchronized volleys so the feature is fully reversible.
    /// </summary>
    public sealed class EnemyBattleGroupDirector : MonoBehaviour
    {
        private sealed class BattleGroup
        {
            public int Id;
            public readonly List<EnemyTank> Members = new List<EnemyTank>(6);
            public EnemyBattleDoctrine Doctrine;
            public EnemyTank Leader;
            public int StartingStrength;
            public float CreatedAt;
            public float NextVolley;
            public float NextDoctrineReview;
            public float RallyUntil;
            public string Callsign;
            public int Sector;
        }

        public static EnemyBattleGroupDirector Instance { get; private set; }
        public int ActiveGroups { get; private set; }
        public string ActiveDoctrineSummary { get; private set; } = "NO CONTACT";
        public int CoordinatedUnits { get; private set; }

        private readonly List<BattleGroup> _groups = new List<BattleGroup>(12);
        private readonly HashSet<int> _assigned = new HashSet<int>();
        private TankGame _game;
        private float _nextRosterPass;
        private float _nextOrderPass;
        private int _nextGroupId = 1;
        private int _lastRound;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _alertStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemyBattleGroupDirector>() != null) return;
            var go = new GameObject("EnemyBattleGroupDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyBattleGroupDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
                if (_groups.Count > 0) ResetGroups();
                return;
            }

            if (_lastRound != _game.CurrentRound)
            {
                _lastRound = _game.CurrentRound;
                ReviewAllDoctrines(true);
            }

            if (Time.time >= _nextRosterPass)
            {
                _nextRosterPass = Time.time + 0.42f;
                RebuildGroups();
            }

            if (Time.time >= _nextOrderPass)
            {
                _nextOrderPass = Time.time + 0.24f;
                IssueOrders();
            }
        }

        private void ResetGroups()
        {
            _groups.Clear();
            _assigned.Clear();
            ActiveGroups = 0;
            CoordinatedUnits = 0;
            ActiveDoctrineSummary = "NO CONTACT";
        }

        private void RebuildGroups()
        {
            CleanupDeadMembers();

            EnemyTank[] enemies = CombatRoster.Enemies;
            if (enemies == null || enemies.Length == 0)
                enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsCombatReady(enemy)) continue;
                int id = enemy.GetInstanceID();
                if (_assigned.Contains(id)) continue;

                BattleGroup group = FindGroupFor(enemy);
                if (group == null)
                    group = CreateGroup(enemy);

                AddToGroup(group, enemy);
            }

            ActiveGroups = 0;
            CoordinatedUnits = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                BattleGroup group = _groups[i];
                int alive = CountAlive(group);
                if (alive <= 0) continue;
                ActiveGroups++;
                CoordinatedUnits += alive;
                if (Time.time >= group.NextDoctrineReview)
                    ReviewDoctrine(group, false);
            }

            BuildDoctrineSummary();
        }

        private void CleanupDeadMembers()
        {
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                BattleGroup group = _groups[g];
                for (int i = group.Members.Count - 1; i >= 0; i--)
                {
                    EnemyTank member = group.Members[i];
                    if (IsCombatReady(member)) continue;
                    if (member != null) _assigned.Remove(member.GetInstanceID());
                    group.Members.RemoveAt(i);
                }

                if (group.Members.Count == 0)
                {
                    _groups.RemoveAt(g);
                    continue;
                }

                if (!IsCombatReady(group.Leader))
                    group.Leader = SelectLeader(group);
            }
        }

        private BattleGroup FindGroupFor(EnemyTank enemy)
        {
            BattleGroup best = null;
            float bestScore = float.MinValue;
            int maxSize = _game.CurrentRound >= 70 ? 6 : _game.CurrentRound >= 35 ? 5 : 4;

            for (int i = 0; i < _groups.Count; i++)
            {
                BattleGroup group = _groups[i];
                int alive = CountAlive(group);
                if (alive >= maxSize) continue;

                float score = 0f;
                if (Time.time - group.CreatedAt < 6f) score += 3f;
                if (group.Sector == CurrentSector()) score += 1f;
                if (enemy.Kind == EnemyKind.Siege && group.Doctrine == EnemyBattleDoctrine.SiegeColumn) score += 8f;
                if (enemy.Kind == EnemyKind.Sniper && group.Doctrine == EnemyBattleDoctrine.FireSupport) score += 6f;
                if (enemy.Kind == EnemyKind.Fast && group.Doctrine == EnemyBattleDoctrine.Pincer) score += 5f;
                if ((enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite) && group.Doctrine == EnemyBattleDoctrine.Breakthrough) score += 5f;
                score -= alive * 0.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = group;
                }
            }

            return best;
        }

        private BattleGroup CreateGroup(EnemyTank seed)
        {
            var group = new BattleGroup
            {
                Id = _nextGroupId++,
                CreatedAt = Time.time,
                StartingStrength = 0,
                Sector = CurrentSector(),
                Callsign = BuildCallsign(_nextGroupId - 1),
                NextVolley = Time.time + Random.Range(3.8f, 7.2f),
                NextDoctrineReview = Time.time + Random.Range(2.5f, 4.5f)
            };
            _groups.Add(group);
            group.Doctrine = InitialDoctrine(seed.Kind, group.Sector);
            return group;
        }

        private void AddToGroup(BattleGroup group, EnemyTank enemy)
        {
            group.Members.Add(enemy);
            group.StartingStrength = Mathf.Max(group.StartingStrength, group.Members.Count);
            _assigned.Add(enemy.GetInstanceID());

            EnemyTacticalAgent agent = enemy.GetComponent<EnemyTacticalAgent>();
            if (agent == null) agent = enemy.gameObject.AddComponent<EnemyTacticalAgent>();
            agent.Bind(_game, group.Id);

            group.Leader = SelectLeader(group);
            ReviewDoctrine(group, true);
        }

        private void ReviewAllDoctrines(bool force)
        {
            for (int i = 0; i < _groups.Count; i++)
                ReviewDoctrine(_groups[i], force);
        }

        private void ReviewDoctrine(BattleGroup group, bool force)
        {
            if (group == null || group.Members.Count == 0) return;
            if (!force && Time.time < group.NextDoctrineReview) return;
            group.NextDoctrineReview = Time.time + Random.Range(3.5f, 6.5f);

            int alive = CountAlive(group);
            int siege = CountKind(group, EnemyKind.Siege);
            int sniper = CountKind(group, EnemyKind.Sniper);
            int fast = CountKind(group, EnemyKind.Fast);
            int heavy = CountKind(group, EnemyKind.Heavy) + CountKind(group, EnemyKind.Elite) + CountKind(group, EnemyKind.Boss);
            float morale = group.StartingStrength > 0 ? alive / (float)group.StartingStrength : 1f;
            bool commander = HasCommander(group);

            if (morale <= 0.45f && !commander && _game.CurrentRound >= 20)
            {
                group.Doctrine = EnemyBattleDoctrine.FightingWithdrawal;
                group.RallyUntil = Time.time + Random.Range(2.2f, 4.1f);
            }
            else if (siege > 0)
            {
                group.Doctrine = EnemyBattleDoctrine.SiegeColumn;
            }
            else if (sniper >= 2 || (sniper >= 1 && alive >= 4))
            {
                group.Doctrine = EnemyBattleDoctrine.FireSupport;
            }
            else if (fast >= 2)
            {
                group.Doctrine = EnemyBattleDoctrine.Pincer;
            }
            else if (heavy >= 2 || commander)
            {
                group.Doctrine = EnemyBattleDoctrine.Breakthrough;
            }
            else
            {
                int sector = CurrentSector();
                if (sector == 2 || sector == 5) group.Doctrine = EnemyBattleDoctrine.Pincer;
                else if (sector == 6 || sector == 9) group.Doctrine = EnemyBattleDoctrine.EagleRaid;
                else group.Doctrine = EnemyBattleDoctrine.Assault;
            }

            group.Leader = SelectLeader(group);
        }

        private void IssueOrders()
        {
            if (_groups.Count == 0) return;

            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;
            int round = _game.CurrentRound;

            for (int g = 0; g < _groups.Count; g++)
            {
                BattleGroup group = _groups[g];
                int alive = CountAlive(group);
                if (alive <= 0) continue;

                Vector2 center = GroupCenter(group);
                Vector2 objective = ObjectiveFor(group, player, eagle);
                bool commander = HasCommander(group);
                float intensity = Mathf.Lerp(0.92f, 1.20f, (round - 1f) / 99f) * (commander ? 1.08f : 1f);

                int aliveIndex = 0;
                for (int i = 0; i < group.Members.Count; i++)
                {
                    EnemyTank enemy = group.Members[i];
                    if (!IsCombatReady(enemy)) continue;
                    EnemyTacticalAgent agent = enemy.GetComponent<EnemyTacticalAgent>();
                    if (agent == null) continue;

                    EnemyBattleRole role = ResolveRole(group, enemy, aliveIndex, alive);
                    Vector2 memberTarget = BuildMemberTarget(group, role, objective, center, aliveIndex, alive);
                    agent.SetOrder(group.Doctrine, role, memberTarget, center, intensity, Time.time + 0.65f);
                    aliveIndex++;
                }

                if (round >= 12 && Time.time >= group.NextVolley)
                    ExecuteCoordinatedVolley(group, objective);
            }
        }

        private Vector2 ObjectiveFor(BattleGroup group, Vector2 player, Vector2 eagle)
        {
            switch (group.Doctrine)
            {
                case EnemyBattleDoctrine.SiegeColumn:
                case EnemyBattleDoctrine.EagleRaid:
                case EnemyBattleDoctrine.Breakthrough:
                    return eagle;
                case EnemyBattleDoctrine.FireSupport:
                case EnemyBattleDoctrine.Pincer:
                    return player;
                case EnemyBattleDoctrine.FightingWithdrawal:
                    return player;
                default:
                    return Vector2.Lerp(player, eagle, 0.32f);
            }
        }

        private Vector2 BuildMemberTarget(BattleGroup group, EnemyBattleRole role, Vector2 objective, Vector2 center, int index, int count)
        {
            Vector2 toObjective = objective - center;
            Vector2 forward = toObjective.sqrMagnitude > 0.01f ? toObjective.normalized : Vector2.down;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float lane = count <= 1 ? 0f : Mathf.Lerp(-1f, 1f, index / (float)(count - 1));

            switch (role)
            {
                case EnemyBattleRole.LeftFlank:
                    return objective - forward * 1.8f - side * 3.4f;
                case EnemyBattleRole.RightFlank:
                    return objective - forward * 1.8f + side * 3.4f;
                case EnemyBattleRole.SiegeEscort:
                    return (group.Leader != null ? (Vector2)group.Leader.transform.position : center) + side * (lane * 1.5f) - forward * 0.8f;
                case EnemyBattleRole.FireSupport:
                    return objective - forward * 6.2f + side * (lane * 3.0f);
                case EnemyBattleRole.Raider:
                    return objective + side * (lane * 2.2f);
                case EnemyBattleRole.Reserve:
                    return center - forward * 1.8f + side * (lane * 1.3f);
                case EnemyBattleRole.Commander:
                    return center - forward * 0.9f;
                default:
                    return objective - forward * (0.55f + Mathf.Abs(lane) * 0.35f) + side * (lane * 1.15f);
            }
        }

        private EnemyBattleRole ResolveRole(BattleGroup group, EnemyTank enemy, int index, int alive)
        {
            if (enemy == group.Leader && HasCommander(group)) return EnemyBattleRole.Commander;
            if (enemy.Kind == EnemyKind.Siege) return EnemyBattleRole.Vanguard;
            if (enemy.Kind == EnemyKind.Sniper) return EnemyBattleRole.FireSupport;
            if (group.Doctrine == EnemyBattleDoctrine.SiegeColumn && enemy.Kind != EnemyKind.Siege) return EnemyBattleRole.SiegeEscort;
            if (group.Doctrine == EnemyBattleDoctrine.EagleRaid && (enemy.Kind == EnemyKind.Fast || enemy.Kind == EnemyKind.Basic)) return EnemyBattleRole.Raider;
            if (group.Doctrine == EnemyBattleDoctrine.Pincer)
                return index % 2 == 0 ? EnemyBattleRole.LeftFlank : EnemyBattleRole.RightFlank;
            if (enemy.Kind == EnemyKind.Fast)
                return index % 2 == 0 ? EnemyBattleRole.LeftFlank : EnemyBattleRole.RightFlank;
            if (index == alive - 1 && alive >= 4) return EnemyBattleRole.Reserve;
            return EnemyBattleRole.Vanguard;
        }

        private void ExecuteCoordinatedVolley(BattleGroup group, Vector2 objective)
        {
            int round = _game.CurrentRound;
            int shooters = 0;
            int maxShooters = round >= 70 ? 4 : round >= 35 ? 3 : 2;
            float cadence = Mathf.Lerp(8.4f, 5.2f, (round - 1f) / 99f);

            if (group.Doctrine == EnemyBattleDoctrine.FightingWithdrawal)
                cadence *= 1.35f;
            else if (group.Doctrine == EnemyBattleDoctrine.FireSupport || group.Doctrine == EnemyBattleDoctrine.SiegeColumn)
                cadence *= 0.82f;

            group.NextVolley = Time.time + Random.Range(cadence * 0.84f, cadence * 1.18f);

            for (int i = 0; i < group.Members.Count && shooters < maxShooters; i++)
            {
                EnemyTank enemy = group.Members[i];
                if (!IsCombatReady(enemy)) continue;
                if (enemy.Kind == EnemyKind.Supply) continue;

                Vector2 from = enemy.transform.position;
                Vector2 delta = objective - from;
                if (delta.sqrMagnitude < 1.2f || delta.sqrMagnitude > 115f) continue;

                Vector2 dir = delta.normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float spread = (shooters - (maxShooters - 1) * 0.5f) * 0.055f;
                dir = (dir + side * spread).normalized;

                float muzzleDistance = enemy.Kind == EnemyKind.Boss ? 1.0f : enemy.Kind == EnemyKind.Siege ? 0.84f : 0.73f;
                Vector2 muzzle = from + dir * muzzleDistance;
                Color color = group.Doctrine == EnemyBattleDoctrine.FireSupport
                    ? new Color(1f, 0.48f, 0.12f)
                    : new Color(1f, 0.20f, 0.06f);
                float speed = 8.8f + round * 0.018f;
                int damage = round >= 80 && (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss) ? 2 : 1;

                _game.SpawnProjectile(muzzle, dir, Team.Enemy, damage, speed, color, AmmoType.Basic);
                VisualFactory.MuzzleFlash(muzzle, color, enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss ? 0.92f : 0.58f);
                shooters++;
            }

            if (shooters > 0)
            {
                if (group.Doctrine == EnemyBattleDoctrine.SiegeColumn || group.Doctrine == EnemyBattleDoctrine.Breakthrough)
                    BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.18f, 0.08f);
                else
                    BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.13f, 0.08f);
            }
        }

        private EnemyBattleDoctrine InitialDoctrine(EnemyKind kind, int sector)
        {
            if (kind == EnemyKind.Siege) return EnemyBattleDoctrine.SiegeColumn;
            if (kind == EnemyKind.Sniper) return EnemyBattleDoctrine.FireSupport;
            if (kind == EnemyKind.Fast) return EnemyBattleDoctrine.Pincer;
            if (kind == EnemyKind.Heavy || kind == EnemyKind.Elite || kind == EnemyKind.Boss) return EnemyBattleDoctrine.Breakthrough;
            if (sector == 6 || sector == 9) return EnemyBattleDoctrine.EagleRaid;
            return EnemyBattleDoctrine.Assault;
        }

        private int CurrentSector()
        {
            return _game == null ? 0 : Mathf.Clamp((_game.CurrentRound - 1) / 10, 0, 9);
        }

        private static bool IsCombatReady(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.gameObject.activeInHierarchy;
        }

        private static int CountAlive(BattleGroup group)
        {
            int count = 0;
            for (int i = 0; i < group.Members.Count; i++)
                if (IsCombatReady(group.Members[i])) count++;
            return count;
        }

        private static int CountKind(BattleGroup group, EnemyKind kind)
        {
            int count = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                EnemyTank enemy = group.Members[i];
                if (IsCombatReady(enemy) && enemy.Kind == kind) count++;
            }
            return count;
        }

        private static bool HasCommander(BattleGroup group)
        {
            for (int i = 0; i < group.Members.Count; i++)
            {
                EnemyTank enemy = group.Members[i];
                if (IsCombatReady(enemy) && enemy.GetComponent<WarCommander>() != null) return true;
            }
            return false;
        }

        private static EnemyTank SelectLeader(BattleGroup group)
        {
            EnemyTank best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < group.Members.Count; i++)
            {
                EnemyTank enemy = group.Members[i];
                if (!IsCombatReady(enemy)) continue;
                float score = TacticalThreatDirector.KindWeight(enemy.Kind);
                if (enemy.GetComponent<WarCommander>() != null) score += 20f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        private static Vector2 GroupCenter(BattleGroup group)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                EnemyTank enemy = group.Members[i];
                if (!IsCombatReady(enemy)) continue;
                sum += (Vector2)enemy.transform.position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private static string BuildCallsign(int id)
        {
            string[] names = { "IRON", "COBRA", "WOLF", "HAMMER", "VIPER", "RAVEN", "ANVIL", "REAPER" };
            return names[(id - 1) % names.Length] + "-" + (((id - 1) / names.Length) + 1).ToString("00");
        }

        private void BuildDoctrineSummary()
        {
            if (ActiveGroups <= 0)
            {
                ActiveDoctrineSummary = "NO CONTACT";
                return;
            }

            int siege = 0;
            int pincer = 0;
            int breakthrough = 0;
            int raid = 0;
            int support = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (CountAlive(_groups[i]) <= 0) continue;
                switch (_groups[i].Doctrine)
                {
                    case EnemyBattleDoctrine.SiegeColumn: siege++; break;
                    case EnemyBattleDoctrine.Pincer: pincer++; break;
                    case EnemyBattleDoctrine.Breakthrough: breakthrough++; break;
                    case EnemyBattleDoctrine.EagleRaid: raid++; break;
                    case EnemyBattleDoctrine.FireSupport: support++; break;
                }
            }

            if (siege > 0) ActiveDoctrineSummary = "SIEGE COLUMNS";
            else if (raid > 0) ActiveDoctrineSummary = "EAGLE RAID";
            else if (breakthrough > 0) ActiveDoctrineSummary = "ARMORED BREAKTHROUGH";
            else if (pincer > 0) ActiveDoctrineSummary = "PINCER ATTACK";
            else if (support > 0) ActiveDoctrineSummary = "FIRE SUPPORT";
            else ActiveDoctrineSummary = "COORDINATED ASSAULT";
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.40f, 0.12f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.86f, 0.88f, 0.92f) }
            };
            _alertStyle = new GUIStyle(_titleStyle)
            {
                normal = { textColor = new Color(1f, 0.12f, 0.06f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || ActiveGroups <= 0) return;
            EnsureStyles();

            float width = 286f;
            float x = 14f;
            float y = Screen.height - 92f;
            GUI.color = new Color(0.035f, 0.025f, 0.022f, 0.90f);
            GUI.Box(new Rect(x, y, width, 76f), string.Empty);
            GUI.color = Color.white;

            bool dangerous = ActiveDoctrineSummary.Contains("SIEGE") || ActiveDoctrineSummary.Contains("EAGLE");
            GUI.Label(new Rect(x + 10f, y + 7f, width - 20f, 18f), "ENEMY BATTLE NET // " + ActiveDoctrineSummary, dangerous ? _alertStyle : _titleStyle);
            GUI.Label(new Rect(x + 10f, y + 28f, width - 20f, 17f), $"GROUPS {ActiveGroups:00}   COORDINATED UNITS {CoordinatedUnits:00}", _bodyStyle);
            GUI.Label(new Rect(x + 10f, y + 47f, width - 20f, 17f), "Flanks, escorts and synchronized volleys active", _bodyStyle);
        }
    }

    /// <summary>
    /// Per-enemy movement executor. Runs after EnemyTank.FixedUpdate and therefore replaces only
    /// the movement destination selected by the legacy individual AI while preserving Rigidbody2D
    /// collision handling and every combat subsystem on EnemyTank.
    /// </summary>
    [DefaultExecutionOrder(800)]
    public sealed class EnemyTacticalAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Rigidbody2D _body;
        private ArmorSystem _armor;
        private Health _health;
        private int _groupId;
        private EnemyBattleDoctrine _doctrine;
        private EnemyBattleRole _role;
        private Vector2 _target;
        private Vector2 _groupCenter;
        private float _intensity = 1f;
        private float _orderExpires;
        private float _retreatUntil;
        private Vector2 _lastPosition;
        private float _stuckSince;
        private float _unstickUntil;
        private Vector2 _unstickDirection;

        public int GroupId => _groupId;
        public EnemyBattleRole Role => _role;
        public EnemyBattleDoctrine Doctrine => _doctrine;

        public void Bind(TankGame game, int groupId)
        {
            _game = game;
            _groupId = groupId;
            _enemy = GetComponent<EnemyTank>();
            _body = GetComponent<Rigidbody2D>();
            _armor = GetComponent<ArmorSystem>();
            _health = GetComponent<Health>();
            _lastPosition = transform.position;
            _stuckSince = Time.time;
        }

        public void SetOrder(EnemyBattleDoctrine doctrine, EnemyBattleRole role, Vector2 target, Vector2 groupCenter, float intensity, float expires)
        {
            _doctrine = doctrine;
            _role = role;
            _target = target;
            _groupCenter = groupCenter;
            _intensity = Mathf.Clamp(intensity, 0.78f, 1.34f);
            _orderExpires = expires;
        }

        private void Update()
        {
            if (_enemy == null) _enemy = GetComponent<EnemyTank>();
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            if (_health == null) _health = GetComponent<Health>();
            if (_armor == null) _armor = GetComponent<ArmorSystem>();

            if (_health == null || _health.IsDead) return;

            float moved = Vector2.Distance(_lastPosition, transform.position);
            if (moved > 0.08f)
            {
                _lastPosition = transform.position;
                _stuckSince = Time.time;
            }
            else if (Time.time - _stuckSince > 1.15f && Time.time >= _unstickUntil)
            {
                Vector2 toward = _target - (Vector2)transform.position;
                if (toward.sqrMagnitude < 0.01f) toward = Random.insideUnitCircle.normalized;
                _unstickDirection = Random.value < 0.5f
                    ? new Vector2(-toward.y, toward.x).normalized
                    : new Vector2(toward.y, -toward.x).normalized;
                _unstickUntil = Time.time + Random.Range(0.36f, 0.62f);
                _stuckSince = Time.time;
            }

            if (_health.Maximum > 1 && _health.Current <= Mathf.Max(1, Mathf.FloorToInt(_health.Maximum * 0.24f)) && _enemy != null && _enemy.Kind != EnemyKind.Boss && _enemy.Kind != EnemyKind.Siege)
                _retreatUntil = Mathf.Max(_retreatUntil, Time.time + 0.45f);
        }

        private void FixedUpdate()
        {
            if (_game == null || !_game.IsPlaying || _body == null || _enemy == null || _health == null || _health.IsDead) return;
            if (Time.time > _orderExpires) return;

            Vector2 position = _body.position;
            Vector2 desired;

            if (Time.time < _unstickUntil)
            {
                desired = _unstickDirection;
            }
            else if (Time.time < _retreatUntil || _doctrine == EnemyBattleDoctrine.FightingWithdrawal)
            {
                Vector2 threat = _game.PlayerPosition;
                desired = position - threat;
                if (desired.sqrMagnitude < 0.01f) desired = Vector2.up;
                desired.Normalize();
                desired = (desired + Separation(position) * 0.85f).normalized;
            }
            else
            {
                desired = BuildRoleDirection(position);
            }

            if (desired.sqrMagnitude < 0.01f) return;
            desired.Normalize();

            float mobility = _armor != null ? _armor.MobilityMultiplier : 1f;
            float speed = TacticalSpeed(_enemy.Kind, _game.CurrentRound) * mobility * _intensity;
            if (_role == EnemyBattleRole.FireSupport && Vector2.Distance(position, _target) < 1.15f) speed *= 0.38f;
            if (_role == EnemyBattleRole.Commander) speed *= 0.88f;
            if (Time.time < _retreatUntil) speed *= 1.10f;

            Vector2 next = position + desired * (speed * Time.fixedDeltaTime);
            next.x = Mathf.Clamp(next.x, -11.25f, 11.25f);
            next.y = Mathf.Clamp(next.y, -6.35f, 6.35f);
            _body.MovePosition(next);
        }

        private Vector2 BuildRoleDirection(Vector2 position)
        {
            Vector2 toTarget = _target - position;
            float distance = toTarget.magnitude;
            Vector2 direct = distance > 0.01f ? toTarget / distance : Vector2.zero;
            Vector2 separation = Separation(position);

            switch (_role)
            {
                case EnemyBattleRole.FireSupport:
                    if (distance < 0.75f)
                    {
                        Vector2 away = position - _game.PlayerPosition;
                        return (away.normalized + separation * 0.55f).normalized;
                    }
                    if (distance < 1.45f)
                    {
                        Vector2 side = new Vector2(-direct.y, direct.x);
                        if ((_groupId & 1) == 0) side = -side;
                        return (side + separation * 0.45f).normalized;
                    }
                    break;

                case EnemyBattleRole.SiegeEscort:
                    if (distance < 0.45f)
                    {
                        Vector2 around = position - _groupCenter;
                        if (around.sqrMagnitude < 0.01f) around = new Vector2((_groupId & 1) == 0 ? 1f : -1f, 0f);
                        return (around.normalized + separation * 0.60f).normalized;
                    }
                    break;

                case EnemyBattleRole.LeftFlank:
                case EnemyBattleRole.RightFlank:
                    if (distance < 0.65f)
                    {
                        Vector2 collapse = _game.PlayerPosition - position;
                        return (collapse.normalized + separation * 0.35f).normalized;
                    }
                    break;

                case EnemyBattleRole.Reserve:
                    if (distance < 0.55f)
                    {
                        Vector2 towardGroup = _groupCenter - position;
                        Vector2 side = new Vector2(-towardGroup.y, towardGroup.x);
                        return (side.normalized + separation * 0.7f).normalized;
                    }
                    break;
            }

            return (direct + separation * 0.58f).normalized;
        }

        private Vector2 Separation(Vector2 position)
        {
            EnemyTank[] enemies = CombatRoster.Enemies;
            if (enemies == null) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank other = enemies[i];
                if (other == null || other == _enemy || other.Health == null || other.Health.IsDead) continue;
                Vector2 delta = position - (Vector2)other.transform.position;
                float sqr = delta.sqrMagnitude;
                if (sqr <= 0.001f || sqr > 1.55f) continue;
                sum += delta.normalized * (1.55f - sqr) / 1.55f;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private static float TacticalSpeed(EnemyKind kind, int round)
        {
            float baseSpeed;
            switch (kind)
            {
                case EnemyKind.Fast: baseSpeed = 3.45f; break;
                case EnemyKind.Heavy: baseSpeed = 1.70f; break;
                case EnemyKind.Sniper: baseSpeed = 1.86f; break;
                case EnemyKind.Siege: baseSpeed = 1.48f; break;
                case EnemyKind.Elite: baseSpeed = 2.50f; break;
                case EnemyKind.Supply: baseSpeed = 2.68f; break;
                case EnemyKind.Boss: baseSpeed = 1.72f; break;
                default: baseSpeed = 2.24f; break;
            }
            return baseSpeed * Mathf.Lerp(1f, 1.20f, Mathf.Clamp01((round - 1f) / 99f));
        }
    }
}
