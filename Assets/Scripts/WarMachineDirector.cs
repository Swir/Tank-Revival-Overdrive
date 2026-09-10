using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum WarOrder
    {
        Vanguard,
        HunterKiller,
        SiegeNetwork,
        Overdrive,
        IronGuard
    }

    public enum EliteMutation
    {
        Vanguard,
        Berserker,
        Engineer,
        Hunter
    }

    /// <summary>
    /// v0.7 campaign escalation layer. It turns late-game enemies into named battlefield
    /// specialists and deploys enemy support relays without replacing the proven EnemyTank AI.
    /// </summary>
    public sealed class WarMachineDirector : MonoBehaviour
    {
        private TankGame _game;
        private readonly HashSet<int> _processed = new HashSet<int>();
        private int _lastRound = -1;
        private float _nextScan;
        private float _nextRelay;
        private WarOrder _order;
        private GUIStyle _title;
        private GUIStyle _small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (FindAnyObjectByType<WarMachineDirector>() != null) return;
            new GameObject("WarMachineDirector").AddComponent<WarMachineDirector>();
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying) return;

            int round = _game.CurrentRound;
            if (round != _lastRound)
                BeginRound(round);

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.42f;
                UpgradeEnemies(round);
            }

            if (round >= 24 && Time.time >= _nextRelay)
            {
                int cap = round >= 75 ? 3 : round >= 48 ? 2 : 1;
                int alive = FindObjectsByType<EnemySupportRelay>(FindObjectsSortMode.None).Length;
                if (alive < cap) SpawnRelay(round);
                _nextRelay = Time.time + Mathf.Lerp(28f, 13f, Mathf.InverseLerp(24f, 100f, round));
            }
        }

        private void BeginRound(int round)
        {
            _lastRound = round;
            _processed.Clear();
            _order = SelectOrder(round);
            _nextRelay = Time.time + (round >= 60 ? 6f : 10f);
        }

        private static WarOrder SelectOrder(int round)
        {
            if (round >= 90) return WarOrder.Overdrive;
            int sector = (round - 1) / 10;
            int index = (round + sector * 3) % 5;
            return (WarOrder)index;
        }

        private void UpgradeEnemies(int round)
        {
            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            foreach (EnemyTank enemy in enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (_processed.Contains(id)) continue;
                _processed.Add(id);

                if (round < 18 || enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) continue;

                float chance = BaseMutationChance(round);
                if (_order == WarOrder.Overdrive) chance += 0.12f;
                if (_order == WarOrder.IronGuard && (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege)) chance += 0.16f;
                if (_order == WarOrder.HunterKiller && (enemy.Kind == EnemyKind.Fast || enemy.Kind == EnemyKind.Sniper || enemy.Kind == EnemyKind.Elite)) chance += 0.14f;

                if (Random.value > Mathf.Clamp01(chance)) continue;

                EliteMutation mutation = ChooseMutation(enemy, round);
                var module = enemy.GetComponent<EliteWarMachine>();
                if (module == null) module = enemy.gameObject.AddComponent<EliteWarMachine>();
                module.Initialize(_game, enemy, mutation, round, _order);
            }
        }

        private static float BaseMutationChance(int round)
        {
            if (round < 30) return 0.12f;
            if (round < 50) return 0.20f;
            if (round < 70) return 0.29f;
            if (round < 90) return 0.38f;
            return 0.52f;
        }

        private EliteMutation ChooseMutation(EnemyTank enemy, int round)
        {
            if (_order == WarOrder.SiegeNetwork && (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy))
                return EliteMutation.Engineer;
            if (_order == WarOrder.HunterKiller && (enemy.Kind == EnemyKind.Fast || enemy.Kind == EnemyKind.Sniper))
                return EliteMutation.Hunter;
            if (_order == WarOrder.IronGuard && enemy.Kind == EnemyKind.Heavy)
                return EliteMutation.Vanguard;
            if (_order == WarOrder.Overdrive && Random.value < 0.55f)
                return EliteMutation.Berserker;

            int max = round >= 55 ? 4 : round >= 34 ? 3 : 2;
            return (EliteMutation)Random.Range(0, max);
        }

        private void SpawnRelay(int round)
        {
            Vector2[] points =
            {
                new Vector2(-8.4f, 3.8f), new Vector2(8.4f, 3.8f),
                new Vector2(-6.0f, 1.8f), new Vector2(6.0f, 1.8f),
                new Vector2(-3.6f, 4.7f), new Vector2(3.6f, 4.7f)
            };

            Vector2 pos = points[Random.Range(0, points.Length)] + Random.insideUnitCircle * 0.45f;
            var go = new GameObject("ENEMY_SUPPORT_RELAY");
            go.transform.position = pos;
            var relay = go.AddComponent<EnemySupportRelay>();
            relay.Initialize(_game, round, _order);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.48f, 0.16f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                normal = { textColor = new Color(0.86f, 0.78f, 0.70f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _game.CurrentRound < 18) return;
            EnsureStyles();
            GUI.Label(new Rect(Screen.width - 350f, 14f, 330f, 24f), "WAR ORDER // " + OrderName(_order), _title);
            GUI.Label(new Rect(Screen.width - 350f, 36f, 330f, 22f), "Elite war-machines active • support network online", _small);
        }

        private static string OrderName(WarOrder order)
        {
            return order switch
            {
                WarOrder.HunterKiller => "HUNTER-KILLER",
                WarOrder.SiegeNetwork => "SIEGE NETWORK",
                WarOrder.Overdrive => "TOTAL OVERDRIVE",
                WarOrder.IronGuard => "IRON GUARD",
                _ => "VANGUARD"
            };
        }
    }

    /// <summary>
    /// Elite modifier layered onto an existing EnemyTank. Each mutation has its own
    /// visible marker and combat behavior so elite units are readable, not hidden stat buffs.
    /// </summary>
    public sealed class EliteWarMachine : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private EliteMutation _mutation;
        private int _round;
        private float _nextAbility;
        private float _pulse;
        private Transform _marker;
        private bool _initialized;

        public void Initialize(TankGame game, EnemyTank enemy, EliteMutation mutation, int round, WarOrder order)
        {
            if (_initialized) return;
            _initialized = true;
            _game = game;
            _enemy = enemy;
            _mutation = mutation;
            _round = round;

            Color color = MutationColor(mutation);
            _marker = new GameObject("EliteMarker_" + mutation).transform;
            _marker.SetParent(transform, false);
            _marker.localPosition = new Vector3(0f, 0f, 0f);
            VisualFactory.RingPulse(transform.position, color, 0.72f);
            VisualFactory.Disc("EliteCore", _marker, new Vector2(0.18f, 0.18f), color, new Vector3(0f, 0.03f, 0f), 22);

            if (mutation == EliteMutation.Vanguard)
            {
                int bonus = Mathf.Clamp(1 + round / 35, 1, 4);
                enemy.Health.Heal(bonus);
            }

            _nextAbility = Time.time + Random.Range(1.4f, 3.2f);
        }

        private void Update()
        {
            if (!_initialized || _game == null || !_game.IsPlaying || _enemy == null || _enemy.Health == null || _enemy.Health.IsDead) return;

            _pulse += Time.deltaTime;
            if (_marker != null)
            {
                float s = 1f + Mathf.Sin(_pulse * 5.5f) * 0.18f;
                _marker.localScale = Vector3.one * s;
            }

            if (Time.time < _nextAbility) return;
            UseAbility();
        }

        private void UseAbility()
        {
            float pressure = Mathf.InverseLerp(18f, 100f, _round);
            switch (_mutation)
            {
                case EliteMutation.Vanguard:
                    VanguardPulse();
                    _nextAbility = Time.time + Mathf.Lerp(7.5f, 4.5f, pressure);
                    break;
                case EliteMutation.Berserker:
                    BerserkerSalvo();
                    _nextAbility = Time.time + Mathf.Lerp(5.8f, 3.1f, pressure);
                    break;
                case EliteMutation.Engineer:
                    RepairPulse();
                    _nextAbility = Time.time + Mathf.Lerp(8f, 5.2f, pressure);
                    break;
                default:
                    HunterShot();
                    _nextAbility = Time.time + Mathf.Lerp(6f, 3.4f, pressure);
                    break;
            }
        }

        private void VanguardPulse()
        {
            Color c = MutationColor(_mutation);
            VisualFactory.RingPulse(transform.position, c, 1.25f);
            if (_enemy.Health.Current < _enemy.Health.Maximum)
                _enemy.Health.Heal(1);

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 1.7f);
            foreach (Collider2D hit in hits)
            {
                EnemyTank ally = hit.GetComponent<EnemyTank>();
                if (ally == null || ally == _enemy || ally.Health == null || ally.Health.IsDead) continue;
                if (ally.Health.Current < ally.Health.Maximum && Random.value < 0.45f)
                    ally.Health.Heal(1);
            }
        }

        private void BerserkerSalvo()
        {
            Vector2 origin = transform.position;
            Vector2 target = _game.PlayerPosition;
            Vector2 dir = (target - origin).normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            int damage = _round >= 72 ? 2 : 1;
            float speed = 9.0f + _round * 0.025f;
            Color c = MutationColor(_mutation);

            _game.SpawnProjectile(origin + dir * 0.76f, (dir + side * 0.13f).normalized, Team.Enemy, damage, speed, c, AmmoType.Basic);
            _game.SpawnProjectile(origin + dir * 0.76f, dir, Team.Enemy, damage, speed, c, AmmoType.Basic);
            _game.SpawnProjectile(origin + dir * 0.76f, (dir - side * 0.13f).normalized, Team.Enemy, damage, speed, c, AmmoType.Basic);
            VisualFactory.MuzzleFlash(origin + dir * 0.72f, c, 1.0f);
            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.18f, 0.06f);
        }

        private void RepairPulse()
        {
            VisualFactory.RingPulse(transform.position, MutationColor(_mutation), 1.6f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2.8f);
            int repaired = 0;
            foreach (Collider2D hit in hits)
            {
                EnemyTank ally = hit.GetComponent<EnemyTank>();
                if (ally == null || ally.Health == null || ally.Health.IsDead) continue;
                if (ally.Health.Current >= ally.Health.Maximum) continue;
                ally.Health.Heal(1);
                repaired++;
                if (repaired >= (_round >= 70 ? 4 : 2)) break;
            }
        }

        private void HunterShot()
        {
            Vector2 origin = transform.position;
            Vector2 target = _game.PlayerPosition;
            Vector2 dir = (target - origin).normalized;
            Color c = MutationColor(_mutation);
            float speed = 13.5f + _round * 0.025f;
            int damage = _round >= 60 ? 2 : 1;
            VisualFactory.RingPulse(target, new Color(c.r, c.g, c.b, 0.55f), 0.42f);
            _game.SpawnProjectile(origin + dir * 0.78f, dir, Team.Enemy, damage, speed, c, AmmoType.Basic);
            VisualFactory.MuzzleFlash(origin + dir * 0.72f, c, 0.72f);
            BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.14f, 0.04f);
        }

        private static Color MutationColor(EliteMutation mutation)
        {
            return mutation switch
            {
                EliteMutation.Vanguard => new Color(0.18f, 0.82f, 1f),
                EliteMutation.Berserker => new Color(1f, 0.12f, 0.06f),
                EliteMutation.Engineer => new Color(0.22f, 1f, 0.40f),
                _ => new Color(0.92f, 0.25f, 1f)
            };
        }
    }

    /// <summary>
    /// Destructible enemy battlefield installation. It repairs enemy armor and fires
    /// pressure shots, creating a clear priority target for the player in late rounds.
    /// </summary>
    public sealed class EnemySupportRelay : MonoBehaviour
    {
        private TankGame _game;
        private Health _health;
        private int _round;
        private WarOrder _order;
        private float _nextHeal;
        private float _nextShot;
        private Transform _dish;

        public void Initialize(TankGame game, int round, WarOrder order)
        {
            _game = game;
            _round = round;
            _order = order;

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.52f;

            _health = gameObject.AddComponent<Health>();
            _health.Initialize(Team.Enemy, Mathf.Clamp(3 + round / 18, 4, 9));
            _health.Died += _ => OnDestroyed();

            Color body = order == WarOrder.SiegeNetwork ? new Color(0.60f, 0.14f, 0.06f) : new Color(0.20f, 0.24f, 0.32f);
            VisualFactory.Rect("RelayBase", transform, new Vector2(0.92f, 0.92f), body, Vector3.zero, 8);
            VisualFactory.Rect("RelayCore", transform, new Vector2(0.38f, 0.62f), new Color(1f, 0.30f, 0.08f), new Vector3(0f, 0f, 0f), 10);
            _dish = new GameObject("RelayDish").transform;
            _dish.SetParent(transform, false);
            VisualFactory.Rect("DishBar", _dish, new Vector2(0.80f, 0.10f), new Color(1f, 0.58f, 0.12f), new Vector3(0f, 0.42f, 0f), 11);
            VisualFactory.Disc("DishNode", _dish, new Vector2(0.18f, 0.18f), new Color(1f, 0.86f, 0.36f), new Vector3(0f, 0.42f, 0f), 12);
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.30f, 0.06f), 1.05f);

            _nextHeal = Time.time + 2.5f;
            _nextShot = Time.time + 1.6f;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead) return;
            if (_dish != null) _dish.Rotate(0f, 0f, 75f * Time.deltaTime);

            if (Time.time >= _nextHeal)
            {
                RepairNetwork();
                _nextHeal = Time.time + (_order == WarOrder.SiegeNetwork ? 4.3f : 5.8f);
            }

            if (Time.time >= _nextShot)
            {
                FirePressureShot();
                _nextShot = Time.time + Mathf.Lerp(3.2f, 1.75f, Mathf.InverseLerp(24f, 100f, _round));
            }
        }

        private void RepairNetwork()
        {
            int maxRepairs = _round >= 70 ? 4 : 2;
            int repaired = 0;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _order == WarOrder.SiegeNetwork ? 5.2f : 4.0f);
            foreach (Collider2D hit in hits)
            {
                EnemyTank enemy = hit.GetComponent<EnemyTank>();
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Health.Current >= enemy.Health.Maximum) continue;
                enemy.Health.Heal(1);
                repaired++;
                if (repaired >= maxRepairs) break;
            }
            if (repaired > 0) VisualFactory.RingPulse(transform.position, new Color(0.25f, 1f, 0.40f), 1.35f);
        }

        private void FirePressureShot()
        {
            Vector2 origin = transform.position;
            Vector2 target = _order == WarOrder.SiegeNetwork ? _game.BasePosition : _game.PlayerPosition;
            Vector2 dir = (target - origin).normalized;
            int damage = _round >= 68 ? 2 : 1;
            Color color = new Color(1f, 0.42f, 0.08f);
            _game.SpawnProjectile(origin + dir * 0.62f, dir, Team.Enemy, damage, 8.4f + _round * 0.018f, color, AmmoType.Basic);
            VisualFactory.MuzzleFlash(origin + dir * 0.56f, color, 0.68f);
        }

        private void OnDestroyed()
        {
            Vector3 pos = transform.position;
            VisualFactory.Explosion(pos, new Color(1f, 0.30f, 0.06f), 1.45f);
            VisualFactory.RingPulse(pos, new Color(1f, 0.75f, 0.20f), 1.65f);
            if (_game != null) _game.KickCamera(0.16f, 0.10f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.42f, 0.04f);
        }
    }
}
