using UnityEngine;

namespace TankRevival
{
    public enum EnemyFaction
    {
        IronLegion,
        ScorchBrigade,
        StormCorps,
        BlackGuard,
        OverdriveHost
    }

    public enum EnemyBattleRole
    {
        Vanguard,
        Breacher,
        Raider,
        Suppressor,
        Engineer
    }

    /// <summary>
    /// v1.5 ENEMY FACTIONS & WAR COMMANDERS
    /// Converts the 100-round campaign into five enemy military factions.
    /// Every spawned enemy receives a functional battlefield role and selected
    /// assault rounds receive a War Commander that coordinates nearby armor.
    /// </summary>
    public sealed class EnemyFactionDirector : MonoBehaviour
    {
        private static readonly string[] FactionNames =
        {
            "IRON LEGION",
            "SCORCH BRIGADE",
            "STORM CORPS",
            "BLACK GUARD",
            "OVERDRIVE HOST"
        };

        private static readonly string[] DoctrineNames =
        {
            "ARMORED ADVANCE",
            "BURNING BREACH",
            "ELECTRONIC ASSAULT",
            "SHADOW ESCORT",
            "TOTAL EAGLE HUNT"
        };

        private TankGame _game;
        private int _round = -1;
        private float _nextScan;
        private bool _commanderAssigned;
        private WarCommander _commander;

        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static EnemyFactionDirector Instance { get; private set; }
        public EnemyFaction CurrentFaction => FactionForRound(_round < 1 ? 1 : _round);
        public bool CommanderAlive => _commander != null && _commander.Health != null && !_commander.Health.IsDead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemyFactionDirector>() != null) return;
            var go = new GameObject("EnemyFactionDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemyFactionDirector>();
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
                _round = -1;
                _commanderAssigned = false;
                _commander = null;
                return;
            }

            if (_round != _game.CurrentRound)
            {
                _round = _game.CurrentRound;
                _commanderAssigned = false;
                _commander = null;
                _nextScan = 0f;
            }

            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.45f;
            AttachFactionAgents();
            TryAssignCommander();
        }

        private void AttachFactionAgents()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyFaction faction = CurrentFaction;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.GetComponent<EnemyDoctrineAgent>() != null) continue;

                EnemyBattleRole role = RoleFor(enemy);
                var agent = enemy.gameObject.AddComponent<EnemyDoctrineAgent>();
                agent.Initialize(_game, faction, role, _round);
            }
        }

        private void TryAssignCommander()
        {
            if (_commanderAssigned || !CommanderRound(_round)) return;

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank candidate = null;
            int best = -1;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Supply) continue;

                int score = CommanderPriority(enemy.Kind);
                if (score > best)
                {
                    best = score;
                    candidate = enemy;
                }
            }

            if (candidate == null) return;

            _commanderAssigned = true;
            _commander = candidate.gameObject.AddComponent<WarCommander>();
            _commander.Initialize(_game, CurrentFaction, _round);
        }

        private static bool CommanderRound(int round)
        {
            if (round < 15) return false;
            if (round % 10 == 5) return true;
            return round >= 70 && round % 8 == 0;
        }

        private static int CommanderPriority(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Elite: return 7;
                case EnemyKind.Siege: return 6;
                case EnemyKind.Heavy: return 5;
                case EnemyKind.Sniper: return 4;
                case EnemyKind.Fast: return 3;
                default: return 1;
            }
        }

        private static EnemyBattleRole RoleFor(EnemyTank enemy)
        {
            switch (enemy.Kind)
            {
                case EnemyKind.Siege:
                case EnemyKind.Heavy:
                    return EnemyBattleRole.Breacher;
                case EnemyKind.Fast:
                case EnemyKind.Elite:
                    return EnemyBattleRole.Raider;
                case EnemyKind.Sniper:
                    return EnemyBattleRole.Suppressor;
                case EnemyKind.Supply:
                    return EnemyBattleRole.Engineer;
                case EnemyKind.Boss:
                    return EnemyBattleRole.Breacher;
                default:
                    return (enemy.GetInstanceID() & 1) == 0 ? EnemyBattleRole.Vanguard : EnemyBattleRole.Suppressor;
            }
        }

        public static EnemyFaction FactionForRound(int round)
        {
            if (round <= 20) return EnemyFaction.IronLegion;
            if (round <= 40) return EnemyFaction.ScorchBrigade;
            if (round <= 60) return EnemyFaction.StormCorps;
            if (round <= 80) return EnemyFaction.BlackGuard;
            return EnemyFaction.OverdriveHost;
        }

        public static Color FactionColor(EnemyFaction faction)
        {
            switch (faction)
            {
                case EnemyFaction.IronLegion: return new Color(0.72f, 0.78f, 0.84f);
                case EnemyFaction.ScorchBrigade: return new Color(1f, 0.32f, 0.08f);
                case EnemyFaction.StormCorps: return new Color(0.22f, 0.82f, 1f);
                case EnemyFaction.BlackGuard: return new Color(0.72f, 0.30f, 0.92f);
                default: return new Color(1f, 0.10f, 0.24f);
            }
        }

        public static string FactionName(EnemyFaction faction) => FactionNames[(int)faction];
        public static string DoctrineName(EnemyFaction faction) => DoctrineNames[(int)faction];

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.50f, 0.22f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.82f, 0.88f, 0.93f) }
            };
            _warning = new GUIStyle(_title)
            {
                normal = { textColor = new Color(1f, 0.16f, 0.10f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _round < 1) return;
            EnsureStyles();

            EnemyFaction faction = CurrentFaction;
            float width = 330f;
            float x = Mathf.Max(12f, Screen.width - width - 14f);
            float y = 12f;

            GUI.color = new Color(0.025f, 0.030f, 0.045f, 0.91f);
            GUI.Box(new Rect(x, y, width, CommanderAlive ? 78f : 60f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 7f, width - 24f, 20f), "ENEMY FACTION // " + FactionName(faction), _title);
            GUI.Label(new Rect(x + 12f, y + 28f, width - 24f, 18f), "DOCTRINE: " + DoctrineName(faction), _body);
            if (CommanderAlive)
                GUI.Label(new Rect(x + 12f, y + 49f, width - 24f, 20f), "WAR COMMANDER ACTIVE // BREAK THE FORMATION", _warning);
        }
    }
}
