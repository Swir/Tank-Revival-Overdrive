using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.6 SIEGE ENGINEERING — active player-side battlefield construction.
    /// The player earns Defense Parts as rounds advance and spends them during combat on
    /// minefields, barricades and anti-siege turrets around Orzelek.
    /// </summary>
    public sealed class EagleDefenseEngineeringDirector : MonoBehaviour
    {
        public static EagleDefenseEngineeringDirector Instance { get; private set; }

        private TankGame _game;
        private int _round = -1;
        private int _parts;
        private float _nextBuildAt;
        private string _toast = string.Empty;
        private float _toastUntil;
        private readonly List<GameObject> _constructs = new List<GameObject>();
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EagleDefenseEngineeringDirector>() != null) return;
            var go = new GameObject("EagleDefenseEngineeringDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EagleDefenseEngineeringDirector>();
        }

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            if (_round != _game.CurrentRound)
            {
                _round = _game.CurrentRound;
                _parts = Mathf.Min(12, _parts + 2 + (_round >= 40 ? 1 : 0) + (_round >= 75 ? 1 : 0));
                CleanupDeadConstructs();
                Announce($"DEFENSE PARTS + // STOCK {_parts}");
            }

            if (Time.time < _nextBuildAt) return;
            if (Input.GetKeyDown(KeyCode.Z)) BuildMinefield();
            if (Input.GetKeyDown(KeyCode.X)) BuildBarricade();
            if (Input.GetKeyDown(KeyCode.C)) BuildAntiSiegeTurret();
            if (Input.GetKeyDown(KeyCode.V)) EmergencyRepair();
        }

        private void Spend(int amount, float cooldown)
        {
            _parts = Mathf.Max(0, _parts - amount);
            _nextBuildAt = Time.time + cooldown;
        }

        private bool CanSpend(int amount, string label)
        {
            if (_parts >= amount) return true;
            Announce($"{label} // NEED {amount} PARTS");
            return false;
        }

        private void BuildMinefield()
        {
            const int cost = 2;
            if (!CanSpend(cost, "MINEFIELD")) return;
            Spend(cost, 0.45f);

            Vector2 core = _game.BasePosition;
            var root = new GameObject("PLAYER_MINEFIELD_R" + _round.ToString("000"));
            _constructs.Add(root);
            int count = _round >= 55 ? 5 : 3;
            float span = count <= 3 ? 1.5f : 1.25f;
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * span;
                var mine = new GameObject("DefenseMine");
                mine.transform.SetParent(root.transform, false);
                mine.transform.position = core + new Vector2(offset, 2.35f);
                VisualFactory.Disc("MinePlate", mine.transform, new Vector2(0.42f, 0.42f), new Color(0.12f, 0.22f, 0.18f), Vector3.zero, 7);
                VisualFactory.Disc("MineCore", mine.transform, new Vector2(0.17f, 0.17f), new Color(1f, 0.72f, 0.08f), Vector3.zero, 8);
                var trigger = mine.AddComponent<CircleCollider2D>();
                trigger.radius = 0.40f;
                trigger.isTrigger = true;
                var script = mine.AddComponent<DefenseMine>();
                script.Initialize(_round);
            }
            Announce("MINEFIELD DEPLOYED // ORZELEK APPROACH COVERED");
        }

        private void BuildBarricade()
        {
            const int cost = 3;
            if (!CanSpend(cost, "BARRICADE")) return;
            Spend(cost, 0.55f);

            Vector2 core = _game.BasePosition;
            float side = CountAliveNamed("PLAYER_BARRICADE") % 2 == 0 ? -1f : 1f;
            Vector2 position = core + new Vector2(side * 1.35f, 1.55f);
            var go = new GameObject("PLAYER_BARRICADE");
            go.transform.position = position;
            _constructs.Add(go);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.75f, 0.52f);
            VisualFactory.Rect("BarricadeShadow", go.transform, new Vector2(1.90f, 0.62f), new Color(0f, 0f, 0f, 0.45f), new Vector3(0.05f, -0.05f, 0f), 4);
            VisualFactory.Rect("BarricadeBody", go.transform, new Vector2(1.75f, 0.52f), new Color(0.17f, 0.34f, 0.42f), Vector3.zero, 5);
            VisualFactory.Rect("BarricadeStripe", go.transform, new Vector2(1.48f, 0.09f), new Color(0.36f, 0.92f, 1f), new Vector3(0f, 0.13f, 0f), 6);
            var hp = go.AddComponent<Health>();
            int durability = Mathf.Clamp(4 + _round / 22, 4, 8);
            hp.Initialize(Team.Player, durability);
            hp.Died += h =>
            {
                VisualFactory.Explosion(h.transform.position, new Color(0.20f, 0.70f, 1f), 0.85f);
                BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.38f, 0.04f);
            };
            Announce($"FORTIFIED BARRICADE // ARMOR {durability}");
        }

        private void BuildAntiSiegeTurret()
        {
            const int cost = 5;
            if (!CanSpend(cost, "ANTI-SIEGE TURRET")) return;
            if (CountAliveNamed("PLAYER_ANTI_SIEGE") >= 2)
            {
                Announce("ANTI-SIEGE NETWORK // MAX 2 ACTIVE");
                return;
            }
            Spend(cost, 0.70f);

            Vector2 core = _game.BasePosition;
            float side = CountAliveNamed("PLAYER_ANTI_SIEGE") == 0 ? -1f : 1f;
            var go = new GameObject("PLAYER_ANTI_SIEGE");
            go.transform.position = core + new Vector2(side * 3.15f, 1.2f);
            _constructs.Add(go);
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.42f;
            VisualFactory.Disc("TurretBase", go.transform, new Vector2(0.82f, 0.82f), new Color(0.12f, 0.26f, 0.34f), Vector3.zero, 8);
            VisualFactory.Disc("TurretCore", go.transform, new Vector2(0.46f, 0.46f), new Color(0.26f, 0.92f, 1f), Vector3.zero, 9);
            VisualFactory.Rect("TurretBarrel", go.transform, new Vector2(0.13f, 0.70f), new Color(0.68f, 0.92f, 1f), new Vector3(0f, 0.35f, 0f), 10);
            var hp = go.AddComponent<Health>();
            hp.Initialize(Team.Player, Mathf.Clamp(3 + _round / 28, 3, 6));
            var turret = go.AddComponent<AntiSiegeTurret>();
            turret.Initialize(_game, _round);
            Announce("ANTI-SIEGE TURRET ONLINE // PRIORITY HEAVY TARGETS");
        }

        private void EmergencyRepair()
        {
            const int cost = 4;
            if (!CanSpend(cost, "FORTRESS REPAIR")) return;
            Spend(cost, 0.80f);
            _game.RepairEagle(1);
            EagleFortressDirector.Instance?.RepairFortress(2);
            for (int i = 0; i < _constructs.Count; i++)
            {
                GameObject go = _constructs[i];
                if (go == null) continue;
                Health h = go.GetComponent<Health>();
                if (h != null && !h.IsDead) h.Heal(2);
            }
            VisualFactory.RingPulse(_game.BasePosition, new Color(0.32f, 1f, 0.56f), 2.0f);
            Announce("ENGINEER SURGE // ORZELEK + FORTRESS REPAIRED");
        }

        private int CountAliveNamed(string token)
        {
            int count = 0;
            for (int i = 0; i < _constructs.Count; i++)
            {
                GameObject go = _constructs[i];
                if (go != null && go.name.Contains(token)) count++;
            }
            return count;
        }

        private void CleanupDeadConstructs()
        {
            for (int i = _constructs.Count - 1; i >= 0; i--)
                if (_constructs[i] == null) _constructs.RemoveAt(i);
        }

        private void Announce(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.1f;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.35f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.90f, 0.95f, 1f) } };
            _warn = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.18f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float y = Screen.height - 190f;
            GUI.color = new Color(0.018f, 0.032f, 0.045f, 0.92f);
            GUI.Box(new Rect(14f, y, 470f, 64f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(26f, y + 7f, 440f, 19f), $"FIELD ENGINEERS // PARTS {_parts}", _header);
            GUI.Label(new Rect(26f, y + 27f, 440f, 18f), "Z Mines [2]   X Barricade [3]   C Anti-Siege [5]   V Repair [4]", _body);
            if (Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(26f, y + 45f, 440f, 18f), _toast, _warn);
        }
    }

    public sealed class DefenseMine : MonoBehaviour
    {
        private int _round;
        private bool _used;
        public void Initialize(int round) => _round = Mathf.Clamp(round, 1, 100);
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_used) return;
            Health h = other.GetComponent<Health>();
            if (h == null || h.IsDead || h.Team != Team.Enemy) return;
            _used = true;
            int damage = _round >= 70 ? 4 : _round >= 35 ? 3 : 2;
            h.Damage(damage, Team.Player);
            CombatStatus status = h.GetComponent<CombatStatus>();
            if (status == null) status = h.gameObject.AddComponent<CombatStatus>();
            status.ApplyEmp(1.25f);
            VisualFactory.Explosion(transform.position, new Color(1f, 0.52f, 0.08f), 1.15f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.58f, 0.04f);
            Destroy(gameObject);
        }
    }

    public sealed class AntiSiegeTurret : MonoBehaviour
    {
        private TankGame _game;
        private int _round;
        private float _nextShot;
        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = Mathf.Clamp(round, 1, 100);
            _nextShot = Time.time + 0.7f;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || Time.time < _nextShot) return;
            EnemyTank target = FindPriority();
            _nextShot = Time.time + Mathf.Max(0.85f, 1.55f - _round * 0.0045f);
            if (target == null) return;
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            Vector2 muzzle = (Vector2)transform.position + dir * 0.55f;
            int damage = target.Kind == EnemyKind.Siege || target.Kind == EnemyKind.Heavy || target.Kind == EnemyKind.Boss ? 3 : 2;
            _game.SpawnProjectile(muzzle, dir, Team.Player, damage, 13.5f, new Color(0.36f, 0.96f, 1f), AmmoType.ArmorPiercing);
            VisualFactory.MuzzleFlash(muzzle, new Color(0.36f, 0.96f, 1f), 0.88f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.16f, 0.08f);
        }

        private EnemyTank FindPriority()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                float distance = Vector2.Distance(transform.position, e.transform.position);
                if (distance > 10.5f) continue;
                float priority = -distance;
                if (e.Kind == EnemyKind.Siege) priority += 8f;
                if (e.Kind == EnemyKind.Heavy) priority += 5f;
                if (e.Kind == EnemyKind.Elite) priority += 4f;
                if (e.Kind == EnemyKind.Boss) priority += 12f;
                if (priority > bestScore) { bestScore = priority; best = e; }
            }
            return best;
        }
    }
}
