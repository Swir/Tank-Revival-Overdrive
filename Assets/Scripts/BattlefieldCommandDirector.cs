using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class BattlefieldCommandDirector : MonoBehaviour
    {
        private enum SupportKind
        {
            Airstrike,
            Artillery,
            SupplyDrop,
            AlliedTank
        }

        private TankGame _game;
        private int _commandPoints;
        private int _round;
        private float _nextScan;
        private float _nextPassivePoint;
        private string _status = "COMMAND NET ONLINE";
        private float _statusUntil;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _hotkey;

        public static BattlefieldCommandDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (FindAnyObjectByType<BattlefieldCommandDirector>() != null) return;
            var go = new GameObject("BattlefieldCommandDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldCommandDirector>();
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
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            if (_round != _game.CurrentRound)
            {
                _round = _game.CurrentRound;
                _commandPoints = Mathf.Min(100, _commandPoints + 6 + _round / 10);
                Flash($"ROUND {_round:000} // COMMAND +{6 + _round / 10} CP");
            }

            if (Time.time >= _nextPassivePoint)
            {
                _nextPassivePoint = Time.time + Mathf.Max(8f, 15f - _round * 0.04f);
                AddCommandPoints(1);
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.65f;
                AttachBountyHooks();
            }

            if (Input.GetKeyDown(KeyCode.Z)) TryActivate(SupportKind.Airstrike, 24);
            if (Input.GetKeyDown(KeyCode.X)) TryActivate(SupportKind.Artillery, 16);
            if (Input.GetKeyDown(KeyCode.C)) TryActivate(SupportKind.SupplyDrop, 12);
            if (Input.GetKeyDown(KeyCode.V)) TryActivate(SupportKind.AlliedTank, 30);
        }

        private void AttachBountyHooks()
        {
            foreach (var enemy in FindObjectsByType<EnemyTank>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.Health == null) continue;
                if (enemy.GetComponent<CommandBountyHook>() != null) continue;
                var hook = enemy.gameObject.AddComponent<CommandBountyHook>();
                hook.Bind(enemy.Kind, enemy.Health);
            }
        }

        public void AddCommandPoints(int amount)
        {
            if (amount <= 0) return;
            _commandPoints = Mathf.Clamp(_commandPoints + amount, 0, 100);
        }

        public void AwardKill(EnemyKind kind)
        {
            int reward = kind switch
            {
                EnemyKind.Fast => 2,
                EnemyKind.Heavy => 3,
                EnemyKind.Sniper => 3,
                EnemyKind.Siege => 4,
                EnemyKind.Elite => 5,
                EnemyKind.Supply => 4,
                EnemyKind.Boss => 14,
                _ => 1
            };
            AddCommandPoints(reward);
            if (reward >= 5) Flash($"TACTICAL KILL // +{reward} CP");
        }

        private void TryActivate(SupportKind kind, int cost)
        {
            if (_commandPoints < cost)
            {
                Flash($"COMMAND DENIED // NEED {cost} CP");
                return;
            }

            _commandPoints -= cost;
            switch (kind)
            {
                case SupportKind.Airstrike:
                    StartCoroutine(AirstrikeRoutine());
                    break;
                case SupportKind.Artillery:
                    StartCoroutine(ArtilleryRoutine());
                    break;
                case SupportKind.SupplyDrop:
                    SupplyDrop();
                    break;
                case SupportKind.AlliedTank:
                    DeployAlliedTank();
                    break;
            }
        }

        private IEnumerator AirstrikeRoutine()
        {
            Flash("AIRSTRIKE INBOUND // ZONE PAINTED");
            Vector2 center = PickEnemyCluster();
            VisualFactory.RingPulse(center, new Color(1f, 0.18f, 0.08f), 2.4f);
            yield return new WaitForSeconds(0.85f);

            for (int pass = 0; pass < 5; pass++)
            {
                Vector2 impact = center + new Vector2((pass - 2) * 0.9f, Random.Range(-0.45f, 0.45f));
                StrikeArea(impact, 2.0f, 4 + _round / 30, new Color(1f, 0.22f, 0.06f));
                yield return new WaitForSeconds(0.12f);
            }
            Flash("AIRSTRIKE COMPLETE");
        }

        private IEnumerator ArtilleryRoutine()
        {
            Flash("ARTILLERY FIRE MISSION");
            for (int i = 0; i < 4 + _round / 35; i++)
            {
                Vector2 center = PickEnemyCluster();
                VisualFactory.RingPulse(center, new Color(1f, 0.74f, 0.12f), 1.35f);
                yield return new WaitForSeconds(0.35f);
                StrikeArea(center, 1.45f, 3 + _round / 40, new Color(1f, 0.55f, 0.08f));
                yield return new WaitForSeconds(0.18f);
            }
        }

        private void StrikeArea(Vector2 center, float radius, int damage, Color color)
        {
            VisualFactory.Explosion(center, color, radius * 0.9f);
            _game?.KickCamera(0.18f, 0.11f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.68f, 0.03f);

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            var damaged = new HashSet<Health>();
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var health = hit.GetComponent<Health>();
                if (health == null || health.Team != Team.Enemy || damaged.Contains(health)) continue;
                damaged.Add(health);
                health.Damage(damage, Team.Player);
            }
        }

        private Vector2 PickEnemyCluster()
        {
            var enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            if (enemies.Length == 0) return new Vector2(Random.Range(-7f, 7f), Random.Range(-2f, 4.5f));

            EnemyTank best = enemies[Random.Range(0, enemies.Length)];
            int bestNearby = -1;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                int nearby = 0;
                foreach (var other in enemies)
                {
                    if (other != null && Vector2.Distance(enemy.transform.position, other.transform.position) <= 3.2f) nearby++;
                }
                if (nearby > bestNearby)
                {
                    bestNearby = nearby;
                    best = enemy;
                }
            }
            return best != null ? (Vector2)best.transform.position : Vector2.zero;
        }

        private void SupplyDrop()
        {
            var player = FindAnyObjectByType<PlayerTank>();
            if (player == null)
            {
                Flash("SUPPLY DROP ABORTED // NO TANK LINK");
                _commandPoints = Mathf.Min(100, _commandPoints + 12);
                return;
            }

            AmmoType ammo = _round >= 50 ? AmmoType.Plasma : _round >= 35 ? AmmoType.Twin : _round >= 25 ? AmmoType.EMP : _round >= 15 ? AmmoType.Incendiary : AmmoType.ArmorPiercing;
            int amount = 5 + _round / 12;
            player.AddAmmo(ammo, amount);
            player.ApplyPowerUp(PowerUpKind.Repair);
            if (_round >= 45) player.ApplyPowerUp(PowerUpKind.Shield);
            VisualFactory.RingPulse(player.transform.position, new Color(0.20f, 0.90f, 1f), 1.55f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.78f, 0f);
            Flash($"SUPPLY DELIVERED // {AmmoDatabase.DisplayName(ammo)} +{amount}");
        }

        private void DeployAlliedTank()
        {
            var existing = FindObjectsByType<AlliedSupportTank>(FindObjectsSortMode.None);
            if (existing.Length >= 2)
            {
                Flash("ALLIED SQUAD AT CAPACITY");
                _commandPoints = Mathf.Min(100, _commandPoints + 18);
                return;
            }

            var go = new GameObject("AlliedSupportTank");
            go.transform.position = _game.BasePosition + new Vector2(existing.Length == 0 ? -2.2f : 2.2f, 1.3f);
            go.AddComponent<AlliedSupportTank>().Initialize(_game, _round);
            Flash("ALLIED ARMOR DEPLOYED // VANGUARD ONLINE");
        }

        private void Flash(string message)
        {
            _status = message;
            _statusUntil = Time.unscaledTime + 2.2f;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.25f, 0.92f, 1f) }
            };
            _text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            _hotkey = new GUIStyle(_text)
            {
                normal = { textColor = new Color(1f, 0.82f, 0.26f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();
            float width = 360f;
            Rect panel = new Rect(Screen.width - width - 14f, 12f, width, 126f);
            GUI.color = new Color(0.025f, 0.045f, 0.065f, 0.94f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, 24f), $"BATTLEFIELD COMMAND // CP {_commandPoints:000}/100", _title);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 34f, width - 24f, 22f), "Z AIRSTRIKE 24   X ARTILLERY 16", _hotkey);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 56f, width - 24f, 22f), "C SUPPLY 12      V ALLIED TANK 30", _hotkey);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 80f, width - 24f, 20f), "Kills and combat uptime generate Command Points", _text);
            if (Time.unscaledTime < _statusUntil)
                GUI.Label(new Rect(panel.x + 12f, panel.y + 101f, width - 24f, 20f), _status, _text);
        }
    }

    public sealed class CommandBountyHook : MonoBehaviour
    {
        private EnemyKind _kind;
        private Health _health;
        private bool _awarded;

        public void Bind(EnemyKind kind, Health health)
        {
            _kind = kind;
            _health = health;
            if (_health != null) _health.Died += OnDied;
        }

        private void OnDied(Health health)
        {
            if (_awarded) return;
            _awarded = true;
            BattlefieldCommandDirector.Instance?.AwardKill(_kind);
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Died -= OnDied;
        }
    }

    public sealed class AlliedSupportTank : MonoBehaviour
    {
        private TankGame _game;
        private Health _health;
        private EnemyTank _target;
        private float _nextAcquire;
        private float _nextShot;
        private int _round;

        public void Initialize(TankGame game, int round)
        {
            _game = game;
            _round = round;
            VisualFactory.BuildTankSkin(transform, new Color(0.08f, 0.55f, 0.32f), new Color(0.62f, 1f, 0.78f));
            transform.localScale = Vector3.one * 0.92f;
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.76f, 0.76f);
            _health = gameObject.AddComponent<Health>();
            _health.Initialize(Team.Player, 4 + round / 25);
            _health.Died += _ => VisualFactory.Explosion(transform.position, new Color(0.18f, 0.85f, 0.42f), 1.25f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (Time.time >= _nextAcquire)
            {
                _nextAcquire = Time.time + 0.35f;
                AcquireTarget();
            }
            if (_target == null || _target.Health == null || _target.Health.IsDead) return;

            Vector2 dir = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, 0f, angle), 220f * Time.deltaTime);

            if (Time.time >= _nextShot && Vector2.Distance(transform.position, _target.transform.position) <= 8.8f)
            {
                _nextShot = Time.time + Mathf.Max(0.45f, 0.95f - _round * 0.0035f);
                Vector2 muzzle = (Vector2)transform.position + dir * 0.78f;
                _game.SpawnProjectile(muzzle, dir, Team.Player, 1 + _round / 45, 10.2f, new Color(0.30f, 1f, 0.58f), AmmoType.ArmorPiercing);
                VisualFactory.MuzzleFlash(muzzle, new Color(0.30f, 1f, 0.58f), 0.85f);
                BattleAudio.PlayGlobal(SoundCue.PlayerShot, 0.38f, 0.05f);
            }
        }

        private void AcquireTarget()
        {
            EnemyTank best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in FindObjectsByType<EnemyTank>(FindObjectsSortMode.None))
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float dist = Vector2.Distance(transform.position, enemy.transform.position);
                float priority = enemy.Kind switch
                {
                    EnemyKind.Boss => -5f,
                    EnemyKind.Siege => -3f,
                    EnemyKind.Elite => -2f,
                    _ => 0f
                };
                float score = dist + priority;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            _target = best;
        }
    }
}
