using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum EnemyCounterDoctrine
    {
        None = 0,
        ArmorTrap = 1,
        DispersedLogistics = 2,
        SiegeBreach = 3
    }

    [DefaultExecutionOrder(22300)]
    public sealed class AdaptiveEnemyHighCommandDirector : MonoBehaviour
    {
        public const int MaxRetaskedCombatants = 4;
        public const int MaxCounterFireShells = 2;
        public const int PlanStartOffset = 4;
        public const int PlanEndOffset = 7;
        public const float OrderRefreshCadence = 0.80f;
        public const float CounterFireCadence = 5.50f;
        public const float DirectiveLifetime = 1.35f;

        private static AdaptiveEnemyHighCommandDirector _instance;
        private readonly List<EnemyTank> _selected = new List<EnemyTank>(MaxRetaskedCombatants);
        private TankGame _game;
        private int _round = -1;
        private int _sector = -1;
        private int _repairedRound = -1;
        private float _nextOrderRefresh;
        private float _nextCounterFire;
        private EnemyCounterDoctrine _activeDoctrine;
        private TheaterSectorDoctrine _observedDoctrine;
        private string _historyLabel = "NO DATA";
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static AdaptiveEnemyHighCommandDirector Instance => _instance;
        public EnemyCounterDoctrine ActiveCounterDoctrine => _activeDoctrine;
        public TheaterSectorDoctrine ObservedPlayerDoctrine => _observedDoctrine;
        public int ActiveRetaskedCombatants => _selected.Count;

        public static bool ConfigurationValid =>
            MaxRetaskedCombatants >= 2 && MaxRetaskedCombatants <= 4 &&
            MaxCounterFireShells >= 1 && MaxCounterFireShells <= 2 &&
            PlanStartOffset == 4 && PlanEndOffset == 7 &&
            OrderRefreshCadence >= 0.6f && OrderRefreshCadence <= 1.2f &&
            CounterFireCadence >= 4.5f && CounterFireCadence <= 7.0f &&
            DirectiveLifetime > OrderRefreshCadence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<AdaptiveEnemyHighCommandDirector>() != null) return;
            var go = new GameObject("AdaptiveEnemyHighCommandDirector_v10_3");
            DontDestroyOnLoad(go);
            go.AddComponent<AdaptiveEnemyHighCommandDirector>();
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
            if (round != _round)
            {
                _round = round;
                _sector = Mathf.Clamp((round - 1) / 10, 0, TheaterConsequenceEngineDirector.SectorCount - 1);
                RefreshDoctrineForSector(_sector);
                _selected.Clear();
                _nextOrderRefresh = Time.time + 0.35f;
                _nextCounterFire = Time.time + 2.0f;
            }

            if (!IsPlanActiveForRound(round) || _activeDoctrine == EnemyCounterDoctrine.None)
            {
                _selected.Clear();
                return;
            }

            if (Time.time >= _nextOrderRefresh)
            {
                _nextOrderRefresh = Time.time + OrderRefreshCadence;
                ApplySectorWarPlan(round);
            }

            if (Time.time >= _nextCounterFire)
            {
                _nextCounterFire = Time.time + CounterFireCadence;
                ExecuteBoundedResponseBeat(round);
            }
        }

        private void RefreshDoctrineForSector(int sector)
        {
            TheaterConsequenceEngineDirector engine = TheaterConsequenceEngineDirector.Instance;
            if (engine == null)
            {
                _observedDoctrine = TheaterSectorDoctrine.None;
                _activeDoctrine = EnemyCounterDoctrine.None;
                _historyLabel = "CONSEQUENCE ENGINE OFFLINE";
                return;
            }

            TheaterSectorDoctrine current = engine.GetDoctrineForSector(sector);
            TheaterSectorDoctrine previous = sector > 0 ? engine.GetDoctrineForSector(sector - 1) : TheaterSectorDoctrine.None;
            TheaterSectorDoctrine older = sector > 1 ? engine.GetDoctrineForSector(sector - 2) : TheaterSectorDoctrine.None;
            _observedDoctrine = current;
            _activeDoctrine = ResolveCounterDoctrine(current, previous, older);
            _historyLabel = $"{Short(current)} > {Short(previous)} > {Short(older)}";
        }

        private void ApplySectorWarPlan(int round)
        {
            _selected.Clear();
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            Transform logisticsTarget = FindLogisticsTarget();

            for (int pass = 0; pass < 2 && _selected.Count < MaxRetaskedCombatants; pass++)
            {
                for (int i = 0; i < enemies.Length && _selected.Count < MaxRetaskedCombatants; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsEligible(enemy) || _selected.Contains(enemy)) continue;
                    bool preferred = IsPreferredForDoctrine(enemy.Kind, _activeDoctrine);
                    if ((pass == 0 && !preferred) || (pass == 1 && preferred)) continue;
                    if (pass == 1 && enemy.Kind == EnemyKind.Basic && _selected.Count >= 2) continue;

                    _selected.Add(enemy);
                    IssueDirective(enemy, logisticsTarget, round, _selected.Count - 1);
                }
            }

            if (_activeDoctrine == EnemyCounterDoctrine.DispersedLogistics && _repairedRound != round)
            {
                if (TryRepairDamagedLogistics()) _repairedRound = round;
            }
        }

        private void IssueDirective(EnemyTank enemy, Transform logisticsTarget, int round, int index)
        {
            HighCommandNavigationDirective directive = enemy.GetComponent<HighCommandNavigationDirective>();
            if (directive == null) directive = enemy.gameObject.AddComponent<HighCommandNavigationDirective>();

            SquadTacticalRole role;
            HighCommandObjective objective;
            Transform fixedTarget = null;
            float standoff;
            float speed;

            switch (_activeDoctrine)
            {
                case EnemyCounterDoctrine.ArmorTrap:
                    role = enemy.Kind == EnemyKind.Sniper ? SquadTacticalRole.Suppressor : (index % 2 == 0 ? SquadTacticalRole.Flanker : SquadTacticalRole.Hunter);
                    objective = HighCommandObjective.Player;
                    standoff = enemy.Kind == EnemyKind.Sniper ? 6.4f : enemy.Kind == EnemyKind.Heavy ? 3.0f : 2.7f;
                    speed = enemy.Kind == EnemyKind.Heavy ? 0.82f : 0.94f;
                    break;

                case EnemyCounterDoctrine.DispersedLogistics:
                    role = SquadTacticalRole.Escort;
                    objective = logisticsTarget != null ? HighCommandObjective.FixedTarget : HighCommandObjective.Player;
                    fixedTarget = logisticsTarget;
                    standoff = logisticsTarget != null ? 2.0f + (index % 2) * 0.55f : 3.6f;
                    speed = enemy.Kind == EnemyKind.Heavy ? 0.84f : 0.93f;
                    break;

                default:
                    role = SquadTacticalRole.Breaker;
                    objective = HighCommandObjective.Eagle;
                    standoff = enemy.Kind == EnemyKind.Siege ? 2.15f : 1.35f;
                    speed = enemy.Kind == EnemyKind.Heavy ? 0.90f : 1.02f;
                    break;
            }

            directive.Configure(_game, role, objective, fixedTarget, standoff, speed, Time.time + DirectiveLifetime);
        }

        private void ExecuteBoundedResponseBeat(int round)
        {
            if (_selected.Count == 0) ApplySectorWarPlan(round);
            if (_selected.Count == 0) return;

            Vector2 target;
            AmmoType ammo;
            int shellCap;
            float speed;
            Color color;

            switch (_activeDoctrine)
            {
                case EnemyCounterDoctrine.ArmorTrap:
                    target = _game.PlayerPosition;
                    ammo = AmmoType.ArmorPiercing;
                    shellCap = MaxCounterFireShells;
                    speed = 10.8f;
                    color = AmmoDatabase.Color(ammo);
                    break;
                case EnemyCounterDoctrine.DispersedLogistics:
                    target = _game.PlayerPosition;
                    ammo = AmmoType.ArmorPiercing;
                    shellCap = 1;
                    speed = 9.8f;
                    color = AmmoDatabase.Color(ammo);
                    break;
                default:
                    target = _game.BasePosition;
                    ammo = AmmoType.Explosive;
                    shellCap = MaxCounterFireShells;
                    speed = 8.8f;
                    color = AmmoDatabase.Color(ammo);
                    break;
            }

            int fired = 0;
            for (int i = 0; i < _selected.Count && fired < shellCap; i++)
            {
                EnemyTank enemy = _selected[i];
                if (!IsEligible(enemy)) continue;
                Vector2 origin = enemy.transform.position;
                Vector2 delta = target - origin;
                if (delta.sqrMagnitude < 0.25f) continue;
                int damage = _activeDoctrine == EnemyCounterDoctrine.SiegeBreach && round >= 75 ? 2 : 1;
                _game.SpawnProjectile(origin + delta.normalized * 0.58f, delta.normalized, Team.Enemy, damage, speed, color, ammo);
                fired++;
            }
        }

        private static bool IsEligible(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;
        }

        private static bool IsPreferredForDoctrine(EnemyKind kind, EnemyCounterDoctrine doctrine)
        {
            switch (doctrine)
            {
                case EnemyCounterDoctrine.ArmorTrap:
                    return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Elite;
                case EnemyCounterDoctrine.DispersedLogistics:
                    return kind == EnemyKind.Heavy || kind == EnemyKind.Elite || kind == EnemyKind.Fast;
                case EnemyCounterDoctrine.SiegeBreach:
                    return kind == EnemyKind.Siege || kind == EnemyKind.Heavy || kind == EnemyKind.Elite;
                default:
                    return false;
            }
        }

        private Transform FindLogisticsTarget()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null && enemy.Kind == EnemyKind.Supply && enemy.Health != null && !enemy.Health.IsDead)
                    return enemy.transform;
            }

            Health[] health = RuntimeBattleRegistry.HealthSnapshot;
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (IsLogisticsHealth(h)) return h.transform;
            }
            return null;
        }

        private static bool TryRepairDamagedLogistics()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Kind != EnemyKind.Supply || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Health.Current < enemy.Health.Maximum)
                {
                    enemy.Health.Heal(1);
                    return true;
                }
            }

            Health[] health = RuntimeBattleRegistry.HealthSnapshot;
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (!IsLogisticsHealth(h) || h.Current >= h.Maximum) continue;
                h.Heal(1);
                return true;
            }
            return false;
        }

        private static bool IsLogisticsHealth(Health health)
        {
            if (health == null || health.IsDead || health.Team != Team.Enemy) return false;
            string n = health.name ?? string.Empty;
            return n.StartsWith("ENEMY_LOGISTICS_", StringComparison.Ordinal) ||
                   n.IndexOf("AMMO_CONVOY", StringComparison.Ordinal) >= 0 ||
                   n.IndexOf("SUPPLY", StringComparison.Ordinal) >= 0;
        }

        public static EnemyCounterDoctrine ResolveCounterDoctrine(TheaterSectorDoctrine current, TheaterSectorDoctrine previous, TheaterSectorDoctrine older)
        {
            int breakthrough = Weight(current, TheaterSectorDoctrine.Breakthrough, 3) + Weight(previous, TheaterSectorDoctrine.Breakthrough, 2) + Weight(older, TheaterSectorDoctrine.Breakthrough, 1);
            int supply = Weight(current, TheaterSectorDoctrine.SupplyStarved, 3) + Weight(previous, TheaterSectorDoctrine.SupplyStarved, 2) + Weight(older, TheaterSectorDoctrine.SupplyStarved, 1);
            int defense = Weight(current, TheaterSectorDoctrine.PreparedDefense, 3) + Weight(previous, TheaterSectorDoctrine.PreparedDefense, 2) + Weight(older, TheaterSectorDoctrine.PreparedDefense, 1);
            int best = Mathf.Max(breakthrough, Mathf.Max(supply, defense));
            if (best <= 0) return EnemyCounterDoctrine.None;
            if (breakthrough == best) return EnemyCounterDoctrine.ArmorTrap;
            if (supply == best) return EnemyCounterDoctrine.DispersedLogistics;
            return EnemyCounterDoctrine.SiegeBreach;
        }

        private static int Weight(TheaterSectorDoctrine value, TheaterSectorDoctrine wanted, int weight) => value == wanted ? weight : 0;

        public static bool IsPlanActiveForRound(int round)
        {
            if (round < 1 || round > 100 || round % 10 == 0) return false;
            int offset = (round - 1) % 10;
            return offset >= PlanStartOffset && offset <= PlanEndOffset;
        }

        public static int SectorForRound(int round) => Mathf.Clamp((round - 1) / 10, 0, TheaterConsequenceEngineDirector.SectorCount - 1);

        public static int CounterShellCap(EnemyCounterDoctrine doctrine)
        {
            return doctrine == EnemyCounterDoctrine.None ? 0 : doctrine == EnemyCounterDoctrine.DispersedLogistics ? 1 : MaxCounterFireShells;
        }

        private static string Short(TheaterSectorDoctrine doctrine)
        {
            switch (doctrine)
            {
                case TheaterSectorDoctrine.Breakthrough: return "BREAK";
                case TheaterSectorDoctrine.SupplyStarved: return "STARVE";
                case TheaterSectorDoctrine.PreparedDefense: return "FORT";
                default: return "NONE";
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(1f, 0.44f, 0.18f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = new Color(0.86f, 0.88f, 0.92f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _activeDoctrine == EnemyCounterDoctrine.None) return;
            EnsureStyles();
            bool active = IsPlanActiveForRound(_round);
            string status = active ? "ACTIVE" : "PLANNING";
            GUI.Label(new Rect(Screen.width - 390f, 72f, 370f, 22f), $"ENEMY HIGH COMMAND // {_activeDoctrine.ToString().ToUpperInvariant()} // {status}", _titleStyle);
            GUI.Label(new Rect(Screen.width - 390f, 93f, 370f, 20f), $"SECTOR {_sector + 1:00}  HISTORY {_historyLabel}  RETASK {_selected.Count}/{MaxRetaskedCombatants}", _bodyStyle);
        }
    }

    public enum HighCommandObjective
    {
        Player,
        Eagle,
        FixedTarget
    }

    [DefaultExecutionOrder(22320)]
    public sealed class HighCommandNavigationDirective : MonoBehaviour
    {
        private EnemyTank _enemy;
        private TacticalNavigationAgent _agent;
        private TankGame _game;
        private SquadTacticalRole _role;
        private HighCommandObjective _objective;
        private Transform _fixedTarget;
        private float _standoff;
        private float _speedScale;
        private float _expiresAt;

        public void Configure(TankGame game, SquadTacticalRole role, HighCommandObjective objective, Transform fixedTarget, float standoff, float speedScale, float expiresAt)
        {
            _game = game;
            _role = role;
            _objective = objective;
            _fixedTarget = fixedTarget;
            _standoff = standoff;
            _speedScale = speedScale;
            _expiresAt = expiresAt;
            if (_enemy == null) _enemy = GetComponent<EnemyTank>();
            if (_agent == null)
            {
                _agent = GetComponent<TacticalNavigationAgent>();
                if (_agent == null && _enemy != null)
                {
                    _agent = gameObject.AddComponent<TacticalNavigationAgent>();
                    _agent.Initialize(_enemy);
                }
            }
        }

        private void LateUpdate()
        {
            if (_enemy == null || _enemy.Health == null || _enemy.Health.IsDead || _agent == null || _game == null || Time.time >= _expiresAt)
            {
                Destroy(this);
                return;
            }

            Vector2 target;
            switch (_objective)
            {
                case HighCommandObjective.Eagle:
                    target = _game.BasePosition;
                    break;
                case HighCommandObjective.FixedTarget:
                    target = _fixedTarget != null ? (Vector2)_fixedTarget.position : _game.PlayerPosition;
                    break;
                default:
                    target = _game.PlayerPosition;
                    break;
            }

            _agent.SetRole(_role);
            _agent.SetOrder(target, _standoff, _speedScale, RuntimeBattleRegistry.EnemySnapshot);
        }
    }
}
