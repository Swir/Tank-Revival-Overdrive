using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.1 adaptive endgame director for rounds 60-100.
    /// Raises pressure when the defense is healthy, shifts toward player hunting when the tank is weak,
    /// and deliberately spaces siege specials when Orzelek is critical to keep late-game difficulty readable.
    /// </summary>
    public sealed class EndgamePressureDirector : MonoBehaviour
    {
        public enum PressureMode
        {
            Breakthrough,
            TankHunter,
            LastStand
        }

        private TankGame _game;
        private int _round;
        private float _scanAt;
        private float _rethinkAt;
        private PressureMode _mode;
        private readonly HashSet<int> _attached = new HashSet<int>();
        private GUIStyle _title;
        private GUIStyle _body;

        public PressureMode Mode => _mode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EndgamePressureDirector>() != null) return;
            var go = new GameObject("EndgamePressureDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EndgamePressureDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying || _game.CurrentRound < 60) return;

            if (_game.CurrentRound != _round)
            {
                _round = _game.CurrentRound;
                _attached.Clear();
            }

            if (Time.unscaledTime >= _rethinkAt)
            {
                _rethinkAt = Time.unscaledTime + 1.0f;
                RecalculateMode();
            }

            if (Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.35f;
                AttachPressureAgents();
            }
        }

        private void RecalculateMode()
        {
            Health eagle = FindEagleHealth();
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            int eagleHp = eagle != null ? eagle.Current : 0;
            int eagleMax = eagle != null ? eagle.Maximum : 6;
            int playerHp = player != null && player.Health != null ? player.Health.Current : 0;

            if (eagle != null && eagleHp <= Mathf.Max(2, eagleMax / 3))
                _mode = PressureMode.LastStand;
            else if (player != null && playerHp <= 2)
                _mode = PressureMode.TankHunter;
            else
                _mode = PressureMode.Breakthrough;
        }

        private static Health FindEagleHealth()
        {
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (Health health in all)
            {
                if (health != null && health.Team == Team.Player && health.gameObject.name.Contains("ORZELEK"))
                    return health;
            }
            return null;
        }

        private void AttachPressureAgents()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (!Eligible(enemy.Kind)) continue;
                int id = enemy.GetInstanceID();
                if (!_attached.Add(id)) continue;

                EndgamePressureAgent agent = enemy.GetComponent<EndgamePressureAgent>();
                if (agent == null) agent = enemy.gameObject.AddComponent<EndgamePressureAgent>();
                agent.Initialize(_game, this, enemy, _round);
            }
        }

        private static bool Eligible(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite || kind == EnemyKind.Boss;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.46f, 0.18f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.82f, 0.88f, 0.94f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _game.CurrentRound < 60) return;
            EnsureStyles();
            string mode = _mode == PressureMode.Breakthrough ? "EAGLE BREAKTHROUGH" : _mode == PressureMode.TankHunter ? "TANK HUNTER" : "LAST STAND";
            string detail = _mode == PressureMode.LastStand ? "Critical Orzelek: siege cadence spaced for readable defense" : _mode == PressureMode.TankHunter ? "Enemy elites prioritize weakened player" : "Enemy elites prioritize the Eagle defense line";
            float x = Mathf.Max(14f, Screen.width - 390f);
            float y = 104f;
            GUI.color = new Color(0.03f, 0.018f, 0.016f, 0.91f);
            GUI.Box(new Rect(x, y, 376f, 54f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 7f, 350f, 18f), "ENDGAME DIRECTOR // " + mode, _title);
            GUI.Label(new Rect(x + 12f, y + 27f, 350f, 18f), detail, _body);
        }
    }

    public sealed class EndgamePressureAgent : MonoBehaviour
    {
        private TankGame _game;
        private EndgamePressureDirector _director;
        private EnemyTank _enemy;
        private int _round;
        private float _nextAttack;
        private float _telegraphAt = -1f;
        private Vector2 _telegraphTarget;
        private bool _telegraphEagle;

        public void Initialize(TankGame game, EndgamePressureDirector director, EnemyTank enemy, int round)
        {
            _game = game;
            _director = director;
            _enemy = enemy;
            _round = Mathf.Clamp(round, 60, 100);
            _nextAttack = Time.time + Random.Range(2.8f, 5.0f);
        }

        private void Update()
        {
            if (_game == null || _director == null || _enemy == null || !_game.IsPlaying || _enemy.Health == null || _enemy.Health.IsDead) return;

            if (_telegraphAt > 0f && Time.time >= _telegraphAt)
            {
                ExecutePressureShot();
                _telegraphAt = -1f;
                ScheduleNext();
                return;
            }

            if (_telegraphAt < 0f && Time.time >= _nextAttack)
                BeginTelegraph();
        }

        private void BeginTelegraph()
        {
            EndgamePressureDirector.PressureMode mode = _director.Mode;
            _telegraphEagle = mode == EndgamePressureDirector.PressureMode.Breakthrough || (_enemy.Kind == EnemyKind.Siege || _enemy.Kind == EnemyKind.Boss) && mode != EndgamePressureDirector.PressureMode.TankHunter;
            _telegraphTarget = _telegraphEagle ? _game.BasePosition : _game.PlayerPosition;
            float warning = mode == EndgamePressureDirector.PressureMode.LastStand ? 1.15f : 0.78f;
            _telegraphAt = Time.time + warning;
            VisualFactory.RingPulse(_telegraphTarget, _telegraphEagle ? new Color(1f, 0.12f, 0.04f) : new Color(1f, 0.55f, 0.10f), 1.05f);
        }

        private void ExecutePressureShot()
        {
            Vector2 origin = transform.position;
            Vector2 direction = (_telegraphTarget - origin).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;

            EndgamePressureDirector.PressureMode mode = _director.Mode;
            AmmoType ammo;
            if (mode == EndgamePressureDirector.PressureMode.TankHunter)
                ammo = _round >= 82 ? AmmoType.EMP : AmmoType.ArmorPiercing;
            else if (_enemy.Kind == EnemyKind.Boss && _round >= 90)
                ammo = AmmoType.Plasma;
            else if (_enemy.Kind == EnemyKind.Siege)
                ammo = AmmoType.Explosive;
            else
                ammo = AmmoType.ArmorPiercing;

            int damage = _enemy.Kind == EnemyKind.Boss || _enemy.Kind == EnemyKind.Siege ? 2 : 1;
            float speed = 9.1f + (_round - 60) * 0.025f;
            Color color = AmmoDatabase.Color(ammo);
            _game.SpawnProjectile(origin + direction * 0.80f, direction, Team.Enemy, damage, speed, color, ammo);

            if (_round >= 85 && mode != EndgamePressureDirector.PressureMode.LastStand && (_enemy.Kind == EnemyKind.Elite || _enemy.Kind == EnemyKind.Boss))
            {
                Vector2 side = new Vector2(-direction.y, direction.x);
                _game.SpawnProjectile(origin + direction * 0.75f + side * 0.18f, (direction + side * 0.09f).normalized, Team.Enemy, 1, speed * 0.95f, color, ammo);
            }
        }

        private void ScheduleNext()
        {
            EndgamePressureDirector.PressureMode mode = _director.Mode;
            float baseDelay = Mathf.Lerp(6.0f, 3.6f, (_round - 60f) / 40f);
            if (_enemy.Kind == EnemyKind.Boss) baseDelay *= 0.78f;
            else if (_enemy.Kind == EnemyKind.Siege) baseDelay *= 0.90f;

            // Fairness valve: when Orzelek is critical, attacks stay dangerous but no longer overlap as aggressively.
            if (mode == EndgamePressureDirector.PressureMode.LastStand) baseDelay *= 1.55f;
            _nextAttack = Time.time + Random.Range(baseDelay * 0.82f, baseDelay * 1.20f);
        }
    }
}
