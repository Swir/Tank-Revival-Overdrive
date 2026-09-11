using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Destructible defense layer around Orzelek. v2.9 keeps the existing fortress loop but
    /// reads EagleFortressCommandDirector doctrine modifiers for armor, sentry performance and repair.
    /// </summary>
    public sealed class EagleFortressDirector : MonoBehaviour
    {
        public static EagleFortressDirector Instance { get; private set; }

        private TankGame _game;
        private Health _eagle;
        private Transform _fortressRoot;
        private readonly List<Health> _modules = new List<Health>();
        private int _round = -1;
        private string _alert = string.Empty;
        private float _alertUntil;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EagleFortressDirector>() != null) return;
            var go = new GameObject("EagleFortressDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<EagleFortressDirector>();
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
            if (_round != _game.CurrentRound || _eagle == null) TryBuildFortress();
        }

        private void TryBuildFortress()
        {
            GameObject eagleObject = GameObject.Find("ORZELEK_DEFENSE_CORE");
            if (eagleObject == null) return;
            Health eagle = eagleObject.GetComponent<Health>();
            if (eagle == null) return;

            _round = _game.CurrentRound;
            _eagle = eagle;
            _eagle.Damaged -= OnEagleDamaged;
            _eagle.Damaged += OnEagleDamaged;

            if (_fortressRoot != null) Destroy(_fortressRoot.gameObject);
            var root = new GameObject("EAGLE_FORTRESS_R" + _round.ToString("000"));
            _fortressRoot = root.transform;
            _modules.Clear();

            Vector2 core = _game.BasePosition;
            int commandArmor = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.FortressModuleBonus : 0;
            int armorHp = Mathf.Clamp(2 + _round / 28 + commandArmor, 2, 9);

            CreateArmorNode("FORTRESS_LEFT", core + new Vector2(-1.95f, 0.85f), new Vector2(0.72f, 0.72f), armorHp);
            CreateArmorNode("FORTRESS_RIGHT", core + new Vector2(1.95f, 0.85f), new Vector2(0.72f, 0.72f), armorHp);
            if (_round >= 35) CreateArmorNode("FORTRESS_CENTER", core + new Vector2(0f, 1.15f), new Vector2(0.82f, 0.52f), armorHp + 1);
            if (EagleFortressCommandDirector.Instance != null && EagleFortressCommandDirector.Instance.Doctrine == FortressDoctrine.Bastion && EagleFortressCommandDirector.Instance.DoctrineLevel >= 3)
            {
                CreateArmorNode("FORTRESS_INNER_LEFT", core + new Vector2(-0.95f, 0.48f), new Vector2(0.50f, 0.50f), armorHp);
                CreateArmorNode("FORTRESS_INNER_RIGHT", core + new Vector2(0.95f, 0.48f), new Vector2(0.50f, 0.50f), armorHp);
            }

            CreateSentry("EAGLE_SENTRY_ALPHA", core + new Vector2(-2.55f, 0.25f), _round);
            if (_round >= 18) CreateSentry("EAGLE_SENTRY_BRAVO", core + new Vector2(2.55f, 0.25f), _round);
            if (EagleFortressCommandDirector.Instance != null && EagleFortressCommandDirector.Instance.Doctrine == FortressDoctrine.HunterGrid && EagleFortressCommandDirector.Instance.DoctrineLevel >= 3)
                CreateSentry("EAGLE_SENTRY_CHARLIE", core + new Vector2(0f, 1.85f), _round);

            if (_round >= 25) CreateRepairRelay(core + new Vector2(0f, -0.52f), _round);
            _alert = _round >= 75 ? "FORTRESS // MAXIMUM ALERT" : _round >= 35 ? "FORTRESS // REINFORCED" : "FORTRESS // ONLINE";
            _alertUntil = Time.unscaledTime + 2.2f;
        }

        private void CreateArmorNode(string name, Vector2 position, Vector2 size, int hp)
        {
            var go = CreateModuleShell(name, position, size, hp, new Color(0.18f, 0.48f, 0.64f), new Color(0.34f, 0.92f, 1f));
            VisualFactory.Rect("ArmorStripe", go.transform, new Vector2(size.x * 0.78f, 0.08f), new Color(0.72f, 0.96f, 1f), new Vector3(0f, size.y * 0.22f, 0f), 4);
        }

        private void CreateSentry(string name, Vector2 position, int round)
        {
            int bonus = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.FortressModuleBonus : 0;
            int hp = Mathf.Clamp(2 + round / 32 + bonus, 2, 8);
            var go = CreateModuleShell(name, position, new Vector2(0.66f, 0.66f), hp, new Color(0.10f, 0.32f, 0.40f), new Color(0.28f, 1f, 0.70f));
            VisualFactory.Disc("SentryHead", go.transform, new Vector2(0.42f, 0.42f), new Color(0.22f, 0.72f, 0.78f), Vector3.zero, 5);
            go.AddComponent<EagleSentryModule>().Initialize(_game, round);
        }

        private void CreateRepairRelay(Vector2 position, int round)
        {
            int bonus = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.FortressModuleBonus : 0;
            int hp = Mathf.Clamp(2 + round / 35 + bonus, 2, 7);
            var go = CreateModuleShell("EAGLE_REPAIR_RELAY", position, new Vector2(0.74f, 0.48f), hp, new Color(0.12f, 0.38f, 0.22f), new Color(0.36f, 1f, 0.52f));
            VisualFactory.Disc("RelayCore", go.transform, new Vector2(0.24f, 0.24f), new Color(0.54f, 1f, 0.64f), Vector3.zero, 6);
            go.AddComponent<EagleRepairRelay>().Initialize(_eagle, round);
        }

        private GameObject CreateModuleShell(string name, Vector2 position, Vector2 size, int hp, Color body, Color accent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_fortressRoot, false);
            go.transform.position = position;
            go.AddComponent<BoxCollider2D>().size = size;
            VisualFactory.Rect("ModuleShadow", go.transform, size * 1.08f, new Color(0f, 0f, 0f, 0.42f), new Vector3(0.04f, -0.04f, 0f), 1);
            VisualFactory.Rect("ModuleBody", go.transform, size, body, Vector3.zero, 2);
            VisualFactory.Rect("ModuleTop", go.transform, new Vector2(size.x * 0.78f, size.y * 0.16f), accent, new Vector3(0f, size.y * 0.28f, 0f), 3);
            var health = go.AddComponent<Health>();
            health.Initialize(Team.Player, hp);
            health.Died += h => OnModuleDestroyed(name, h.transform.position);
            _modules.Add(health);
            return go;
        }

        private void OnModuleDestroyed(string moduleName, Vector3 position)
        {
            VisualFactory.Explosion(position, new Color(0.14f, 0.78f, 1f), 0.90f);
            VisualFactory.RingPulse(position, new Color(1f, 0.34f, 0.10f), 0.90f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.52f, 0.04f);
            _alert = moduleName.Replace('_', ' ') + " DESTROYED";
            _alertUntil = Time.unscaledTime + 2.0f;
        }

        private void OnEagleDamaged(Health eagle, int amount)
        {
            if (eagle == null || eagle.IsDead) return;
            VisualFactory.RingPulse(eagle.transform.position, new Color(1f, 0.12f, 0.04f), eagle.Current <= 2 ? 1.8f : 1.15f);
            if (eagle.Current <= 2)
            {
                _alert = "ORZELEK CORE CRITICAL // HOLD THE LINE";
                _alertUntil = Time.unscaledTime + 2.6f;
                BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.78f, 0f);
            }
        }

        public void ActivateEmergencyShield(float duration)
        {
            if (_eagle == null || _eagle.IsDead) return;
            _eagle.InvulnerableUntil = Mathf.Max(_eagle.InvulnerableUntil, Time.time + Mathf.Max(0.5f, duration));
            VisualFactory.RingPulse(_eagle.transform.position, new Color(0.18f, 0.86f, 1f), 2.1f);
            VisualFactory.RingPulse(_eagle.transform.position, Color.white, 1.55f);
            _alert = "EAGLE AEGIS // TEMPORARY CORE SHIELD";
            _alertUntil = Time.unscaledTime + 2.1f;
        }

        public void RepairFortress(int amount)
        {
            if (amount <= 0) return;
            for (int i = 0; i < _modules.Count; i++)
            {
                Health module = _modules[i];
                if (module != null && !module.IsDead) module.Heal(amount);
            }
        }

        private int ActiveModules()
        {
            int count = 0;
            for (int i = 0; i < _modules.Count; i++) if (_modules[i] != null && !_modules[i].IsDead) count++;
            return count;
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.36f, 0.94f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            _warning = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.30f, 0.14f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _eagle == null) return;
            EnsureStyles();
            float width = 360f;
            float x = Mathf.Max(12f, Screen.width - width - 14f);
            float y = Screen.height - 116f;
            GUI.color = new Color(0.018f, 0.034f, 0.050f, 0.93f);
            GUI.Box(new Rect(x, y, width, 88f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 12f, y + 8f, width - 24f, 20f), "EAGLE FORTRESS // ORZELEK DEFENSE CORE", _header);
            GUI.Label(new Rect(x + 12f, y + 31f, width - 24f, 18f), $"CORE {_eagle.Current}/{_eagle.Maximum}   MODULES {ActiveModules()}/{_modules.Count}", _eagle.Current <= 2 ? _warning : _body);
            string doctrine = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.Doctrine.ToString().ToUpperInvariant() + " L" + EagleFortressCommandDirector.Instance.DoctrineLevel : "LEGACY GRID";
            GUI.Label(new Rect(x + 12f, y + 51f, width - 24f, 18f), "NETWORK " + doctrine, _body);
            if (Time.unscaledTime < _alertUntil) GUI.Label(new Rect(x + 12f, y + 68f, width - 24f, 18f), _alert, _warning);
        }
    }

    public sealed class EagleSentryModule : MonoBehaviour
    {
        private TankGame _game;
        private int _round;
        private float _nextShot;

        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = Mathf.Clamp(round, 1, 100);
            _nextShot = Time.time + Random.Range(0.3f, 0.8f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || Time.time < _nextShot) return;
            EnemyTank target = FindTarget();
            float rate = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.SentryFireRateMultiplier : 1f;
            _nextShot = Time.time + Mathf.Max(0.34f, (1.08f - _round * 0.0048f) * rate);
            if (target == null) return;
            Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            Vector2 muzzle = (Vector2)transform.position + direction * 0.48f;
            int bonus = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.SentryDamageBonus : 0;
            int damage = 1 + (_round >= 70 ? 1 : 0) + bonus;
            _game.SpawnProjectile(muzzle, direction, Team.Player, damage, 11.8f + _round * 0.018f, new Color(0.30f, 1f, 0.72f), AmmoType.Basic);
            VisualFactory.MuzzleFlash(muzzle, new Color(0.30f, 1f, 0.72f), 0.55f);
            BattleAudio.PlayGlobal(SoundCue.PlayerShot, 0.09f, 0.08f);
        }

        private EnemyTank FindTarget()
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            EnemyTank best = null;
            float bonus = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.SentryRangeBonus : 0f;
            float bestDistance = Mathf.Lerp(6.6f, 8.6f, (_round - 1f) / 99f) + bonus;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < bestDistance) { bestDistance = distance; best = enemy; }
            }
            return best;
        }
    }

    public sealed class EagleRepairRelay : MonoBehaviour
    {
        private Health _eagle;
        private int _charges;
        private float _nextRepair;

        public void Initialize(Health eagle, int round)
        {
            _eagle = eagle;
            int bonus = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.RepairBonus : 0;
            _charges = Mathf.Clamp(1 + round / 35 + bonus, 1, 6);
            _nextRepair = Time.time + 10f;
        }

        private void Update()
        {
            if (_eagle == null || _eagle.IsDead || _charges <= 0 || Time.time < _nextRepair) return;
            float rate = EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.RepairIntervalMultiplier : 1f;
            _nextRepair = Time.time + 15f * rate;
            if (_eagle.Current >= _eagle.Maximum) return;
            int amount = 1 + (EagleFortressCommandDirector.Instance != null ? EagleFortressCommandDirector.Instance.RepairBonus : 0);
            _charges--;
            _eagle.Heal(amount);
            VisualFactory.RingPulse(_eagle.transform.position, new Color(0.32f, 1f, 0.50f), 1.10f);
            VisualFactory.RingPulse(transform.position, new Color(0.40f, 1f, 0.60f), 0.72f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.34f, 0.02f);
        }
    }
}
