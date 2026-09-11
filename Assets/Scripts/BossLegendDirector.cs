using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum BossLegendKind
    {
        IronJackal = 1,
        TwinViper = 2,
        AshWarden = 3,
        StormRam = 4,
        CrimsonBastion = 5,
        BlackHydra = 6,
        FrostReaper = 7,
        NightTyrant = 8,
        BurningCrown = 9,
        OverdriveZero = 10
    }

    public sealed class BossWeakPoint : MonoBehaviour
    {
        private Health _bossHealth;
        private BossLegendDirector _owner;
        private float _multiplier;
        private float _disabledUntil;

        public bool IsAvailable => _bossHealth != null && !_bossHealth.IsDead && Time.time >= _disabledUntil;
        public Health TargetHealth => _bossHealth;

        public void Initialize(BossLegendDirector owner, Health health, float multiplier)
        {
            _owner = owner;
            _bossHealth = health;
            _multiplier = Mathf.Clamp(multiplier, 1.25f, 3.5f);
        }

        public bool ResolveHit(int rawDamage, Team source, out int resolvedDamage)
        {
            resolvedDamage = 0;
            if (!IsAvailable || source != Team.Player) return false;
            resolvedDamage = Mathf.Max(1, Mathf.CeilToInt(rawDamage * _multiplier));
            if (!_bossHealth.Damage(resolvedDamage, source)) return false;
            _disabledUntil = Time.time + 0.38f;
            _owner?.OnWeakPointHit(this, resolvedDamage);
            return true;
        }
    }

    [DefaultExecutionOrder(260)]
    public sealed class BossLegendDirector : MonoBehaviour
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            {1, "IRON JACKAL"}, {2, "TWIN VIPER"}, {3, "ASH WARDEN"}, {4, "STORM RAM"},
            {5, "CRIMSON BASTION"}, {6, "BLACK HYDRA"}, {7, "FROST REAPER"}, {8, "NIGHT TYRANT"},
            {9, "BURNING CROWN"}, {10, "OVERDRIVE ZERO"}
        };

        private TankGame _game;
        private EnemyTank _boss;
        private Health _health;
        private CombatStatus _status;
        private int _round;
        private int _tier;
        private int _phase;
        private float _nextLegendAttack;
        private float _nextHazard;
        private float _phaseLockUntil;
        private readonly List<BossWeakPoint> _weakPoints = new List<BossWeakPoint>(4);
        private GUIStyle _nameStyle;
        private GUIStyle _phaseStyle;
        private GUIStyle _weakStyle;

        public BossLegendKind Legend => (BossLegendKind)Mathf.Clamp(_tier, 1, 10);
        public int Phase => _phase;
        public string LegendName => Names.TryGetValue(_tier, out string n) ? n : "COMMAND TANK";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BossLegendBootstrap>() != null) return;
            var go = new GameObject("BossLegendBootstrap_v3_3");
            DontDestroyOnLoad(go);
            go.AddComponent<BossLegendBootstrap>();
        }

        public void Initialize(EnemyTank boss, int round)
        {
            _boss = boss;
            _health = boss != null ? boss.Health : null;
            _status = boss != null ? boss.GetComponent<CombatStatus>() : null;
            _game = FindAnyObjectByType<TankGame>();
            _round = Mathf.Clamp(round, 10, 100);
            _tier = Mathf.Clamp(_round / 10, 1, 10);
            _phase = 1;
            _nextLegendAttack = Time.time + 3.2f;
            _nextHazard = Time.time + 5.4f;

            if (_health != null)
            {
                _health.Damaged -= OnBossDamaged;
                _health.Damaged += OnBossDamaged;
                _health.Died -= OnBossDied;
                _health.Died += OnBossDied;
            }

            BuildWeakPoints();
            if (GetComponent<BossLegend3DPresentation>() == null)
                gameObject.AddComponent<BossLegend3DPresentation>().Initialize(this, _tier);

            CampaignEncounterDirector.Instance?.Broadcast("LEGEND CONTACT // " + LegendName);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Damaged -= OnBossDamaged;
                _health.Died -= OnBossDied;
            }
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;
            if (_status == null) _status = GetComponent<CombatStatus>();
            if (_status != null && _status.IsEmpDisabled) return;
            if (Time.time < _phaseLockUntil) return;

            if (Time.time >= _nextLegendAttack)
            {
                _nextLegendAttack = Time.time + AttackCadence();
                ExecuteLegendAttack();
            }

            if (_phase >= 2 && Time.time >= _nextHazard)
            {
                _nextHazard = Time.time + Mathf.Lerp(8.0f, 4.2f, (_tier - 1f) / 9f) - (_phase - 2) * 0.45f;
                StartCoroutine(ExecuteHazard());
            }
        }

        private void OnBossDamaged(Health h, int amount)
        {
            if (h == null || h.Maximum <= 0 || h.IsDead) return;
            float ratio = h.Current / (float)h.Maximum;
            int desired = ratio <= 0.25f ? 4 : ratio <= 0.50f ? 3 : ratio <= 0.75f ? 2 : 1;
            while (_phase < desired) EnterNextPhase();
        }

        private void EnterNextPhase()
        {
            _phase++;
            _phaseLockUntil = Time.time + 0.75f;
            _nextLegendAttack = Time.time + 1.0f;
            _nextHazard = Time.time + 2.2f;

            Color c = LegendColor();
            VisualFactory.RingPulse(transform.position, c, 1.5f + _phase * 0.18f);
            VisualFactory.MicroBurst(transform.position, c, 1.4f + _phase * 0.2f);
            BattleAudio.PlayGlobal(_phase >= 4 ? SoundCue.ExplosionLarge : SoundCue.HeavyShot, 0.55f, 0.04f);
            _game?.KickCamera(0.18f + _phase * 0.025f, 0.12f);
            CampaignEncounterDirector.Instance?.Broadcast(LegendName + " // PHASE " + _phase);

            if (_status != null) _status.ApplyEmp(0.22f);
            ActivateWeakPointsForPhase();
        }

        private void OnBossDied(Health h)
        {
            Color c = LegendColor();
            VisualFactory.Explosion(transform.position, c, 1.8f + _tier * 0.05f);
            VisualFactory.RingPulse(transform.position, Color.white, 2.2f);
            CampaignEncounterDirector.Instance?.Broadcast("LEGEND DESTROYED // " + LegendName);
        }

        private void BuildWeakPoints()
        {
            if (_health == null) return;
            CreateWeakPoint("WeakPoint_Reactor", new Vector2(0f, -0.30f), 0.24f, 2.00f);
            if (_tier >= 3) CreateWeakPoint("WeakPoint_LeftDrive", new Vector2(-0.48f, 0.05f), 0.20f, 1.70f);
            if (_tier >= 5) CreateWeakPoint("WeakPoint_RightDrive", new Vector2(0.48f, 0.05f), 0.20f, 1.70f);
            if (_tier >= 8) CreateWeakPoint("WeakPoint_CommandCore", new Vector2(0f, 0.34f), 0.18f, 2.35f);
            ActivateWeakPointsForPhase();
        }

        private void CreateWeakPoint(string name, Vector2 localPosition, float radius, float multiplier)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = radius;
            collider.isTrigger = true;
            var wp = go.AddComponent<BossWeakPoint>();
            wp.Initialize(this, _health, multiplier);
            _weakPoints.Add(wp);
        }

        private void ActivateWeakPointsForPhase()
        {
            for (int i = 0; i < _weakPoints.Count; i++)
            {
                if (_weakPoints[i] == null) continue;
                bool active = i == 0 || _phase >= 2 + Mathf.Min(i, 2);
                _weakPoints[i].gameObject.SetActive(active);
            }
        }

        public void OnWeakPointHit(BossWeakPoint point, int damage)
        {
            Color c = Color.Lerp(LegendColor(), Color.white, 0.35f);
            VisualFactory.MicroBurst(point.transform.position, c, 0.9f);
            VisualFactory.RingPulse(point.transform.position, c, 0.55f);
            BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.30f, 0.04f);
            _game?.KickCamera(0.055f, 0.035f);
        }

        private float AttackCadence()
        {
            float baseCadence = Mathf.Lerp(4.0f, 2.0f, (_tier - 1f) / 9f);
            return Mathf.Max(1.10f, baseCadence - (_phase - 1) * 0.28f + Random.Range(-0.16f, 0.20f));
        }

        private void ExecuteLegendAttack()
        {
            Vector2 forward = ((Vector2)transform.up).normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 muzzle = (Vector2)transform.position + forward * 1.08f;
            Color c = LegendColor();
            int damage = 1 + Mathf.CeilToInt(_tier / 4f) + (_phase >= 4 ? 1 : 0);
            float speed = 8.8f + _tier * 0.38f + _phase * 0.25f;

            switch (Legend)
            {
                case BossLegendKind.IronJackal:
                    FireFan(muzzle, forward, 3 + _phase, 9f, damage, speed, c);
                    break;
                case BossLegendKind.TwinViper:
                    for (int i = 0; i < 2 + _phase; i++)
                    {
                        Fire(muzzle + side * 0.34f, Rotate(forward, 8f + i * 4f), damage, speed, c, AmmoType.Basic);
                        Fire(muzzle - side * 0.34f, Rotate(forward, -8f - i * 4f), damage, speed, c, AmmoType.Basic);
                    }
                    break;
                case BossLegendKind.AshWarden:
                    FireFan(muzzle, forward, 3 + _phase, 14f, damage, speed * 0.92f, c, AmmoType.Incendiary);
                    break;
                case BossLegendKind.StormRam:
                    FireFan(muzzle, forward, 2 + _phase, 11f, damage, speed * 1.06f, c, AmmoType.EMP);
                    break;
                case BossLegendKind.CrimsonBastion:
                    FireFan(muzzle, forward, 3 + _phase, 8f, damage + 1, speed * 0.84f, c, AmmoType.Explosive);
                    break;
                case BossLegendKind.BlackHydra:
                    FireRadial(6 + _phase * 2, damage, speed, c, AmmoType.Basic, Time.time * 18f);
                    break;
                case BossLegendKind.FrostReaper:
                    FireFan(muzzle, forward, 4 + _phase, 10f, damage, speed * 1.12f, c, AmmoType.ArmorPiercing);
                    break;
                case BossLegendKind.NightTyrant:
                    FireRadial(8 + _phase * 2, damage, speed * 1.05f, c, AmmoType.Plasma, 11f * _phase);
                    break;
                case BossLegendKind.BurningCrown:
                    FireFan(muzzle, forward, 5 + _phase, 9f, damage + 1, speed, c, _phase >= 3 ? AmmoType.Plasma : AmmoType.Explosive);
                    break;
                default:
                    FireRadial(10 + _phase * 3, damage + 1, speed * 1.12f, c, AmmoType.Plasma, Time.time * 24f);
                    FireFan(muzzle, forward, 3 + _phase, 7f, damage + 1, speed * 1.18f, Color.white, AmmoType.ArmorPiercing);
                    break;
            }

            VisualFactory.MuzzleFlash(muzzle, c, 1.25f + _phase * 0.16f);
            _game?.KickCamera(0.08f + _phase * 0.018f, 0.055f);
        }

        private IEnumerator ExecuteHazard()
        {
            if (_game == null) yield break;
            Vector2 center = _tier % 2 == 0 ? _game.PlayerPosition : _game.BasePosition;
            int strikes = Mathf.Clamp(1 + _phase / 2 + (_tier >= 8 ? 1 : 0), 1, 4);
            Color c = LegendColor();
            for (int i = 0; i < strikes; i++)
            {
                Vector2 target = center + Random.insideUnitCircle * (1.2f + _tier * 0.04f);
                target.x = Mathf.Clamp(target.x, -10.6f, 10.6f);
                target.y = Mathf.Clamp(target.y, -5.5f, 5.1f);
                VisualFactory.RingPulse(target, c, 0.85f + _phase * 0.08f);
                yield return new WaitForSeconds(Mathf.Lerp(0.85f, 0.52f, (_tier - 1f) / 9f));
                if (_game == null || !_game.IsPlaying) yield break;
                VisualFactory.Explosion(target, c, 0.92f + _phase * 0.08f);
                DamagePlayerTargets(target, 0.78f + _phase * 0.08f, _phase >= 4 ? 2 : 1);
                yield return new WaitForSeconds(0.10f);
            }
        }

        private static void DamagePlayerTargets(Vector2 center, float radius, int damage)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Health h = hits[i] != null ? hits[i].GetComponent<Health>() : null;
                if (h != null && !h.IsDead && h.Team == Team.Player)
                    h.Damage(damage, Team.Enemy);
            }
        }

        private void FireFan(Vector2 origin, Vector2 forward, int count, float step, int damage, float speed, Color c, AmmoType ammo = AmmoType.Basic)
        {
            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++) Fire(origin, Rotate(forward, (i - middle) * step), damage, speed, c, ammo);
        }

        private void FireRadial(int count, int damage, float speed, Color c, AmmoType ammo, float offset)
        {
            for (int i = 0; i < count; i++)
            {
                float a = offset + i * 360f / count;
                Vector2 d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Fire((Vector2)transform.position + d * 0.68f, d, damage, speed, c, ammo);
            }
        }

        private void Fire(Vector2 origin, Vector2 direction, int damage, float speed, Color c, AmmoType ammo)
        {
            if (_game != null) _game.SpawnProjectile(origin, direction.normalized, Team.Enemy, damage, speed, c, ammo);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(r);
            float sn = Mathf.Sin(r);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs).normalized;
        }

        public Color LegendColor()
        {
            Color[] colors =
            {
                new Color(1f,0.55f,0.08f), new Color(0.92f,0.18f,0.35f), new Color(0.86f,0.32f,0.08f),
                new Color(0.20f,0.78f,1f), new Color(0.92f,0.05f,0.08f), new Color(0.52f,0.10f,0.72f),
                new Color(0.45f,0.86f,1f), new Color(0.34f,0.18f,0.82f), new Color(1f,0.28f,0.02f), new Color(0.82f,0.18f,1f)
            };
            return colors[Mathf.Clamp(_tier - 1, 0, colors.Length - 1)];
        }

        private void EnsureStyles()
        {
            if (_nameStyle != null) return;
            _nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            _phaseStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _weakStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
        }

        private void OnGUI()
        {
            if (_health == null || _health.IsDead || _game == null || !_game.IsPlaying) return;
            EnsureStyles();
            Color c = LegendColor();
            _nameStyle.normal.textColor = c;
            _phaseStyle.normal.textColor = Color.white;
            _weakStyle.normal.textColor = new Color(0.92f,0.92f,0.92f,0.82f);
            float width = Mathf.Min(620f, Screen.width - 40f);
            float x = (Screen.width - width) * 0.5f;
            GUI.Label(new Rect(x, 14f, width, 26f), LegendName, _nameStyle);
            float ratio = _health.Maximum > 0 ? Mathf.Clamp01(_health.Current / (float)_health.Maximum) : 0f;
            GUI.color = new Color(0.03f,0.03f,0.04f,0.92f); GUI.Box(new Rect(x + 40f, 42f, width - 80f, 12f), string.Empty);
            GUI.color = c; GUI.Box(new Rect(x + 42f, 44f, (width - 84f) * ratio, 8f), string.Empty); GUI.color = Color.white;
            GUI.Label(new Rect(x, 56f, width, 20f), "PHASE " + _phase + " / 4", _phaseStyle);
            int activeWeak = 0;
            for (int i = 0; i < _weakPoints.Count; i++) if (_weakPoints[i] != null && _weakPoints[i].gameObject.activeSelf) activeWeak++;
            GUI.Label(new Rect(x, 74f, width, 18f), "WEAK POINTS ONLINE: " + activeWeak, _weakStyle);
        }
    }

    public sealed class BossLegendBootstrap : MonoBehaviour
    {
        private readonly HashSet<int> _configured = new HashSet<int>();
        private TankGame _game;
        private float _nextScan;

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) { _configured.Clear(); return; }
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + 0.32f;

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Kind != EnemyKind.Boss || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (_configured.Contains(id)) continue;
                _configured.Add(id);
                var legend = enemy.GetComponent<BossLegendDirector>();
                if (legend == null) legend = enemy.gameObject.AddComponent<BossLegendDirector>();
                legend.Initialize(enemy, Mathf.Clamp(_game.CurrentRound, 10, 100));
            }
            _configured.RemoveWhere(id => !StillExists(enemies, id));
        }

        private static bool StillExists(EnemyTank[] enemies, int id)
        {
            for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null && enemies[i].GetInstanceID() == id) return true;
            return false;
        }
    }
}
