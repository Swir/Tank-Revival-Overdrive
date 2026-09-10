using System.Collections;
using UnityEngine;

namespace TankRevival
{
    public sealed class TankGame : MonoBehaviour
    {
        private enum GameState
        {
            Menu,
            Playing,
            Paused,
            GameOver,
            Victory
        }

        private const float ArenaHalfWidth = 12f;
        private const float ArenaHalfHeight = 7f;
        private const int FinalRound = 100;
        private const int EagleMaxHealth = 6;

        private GameState _state = GameState.Menu;
        private Camera _camera;
        private Transform _worldRoot;
        private PlayerTank _player;
        private GameObject _baseObject;
        private Health _baseHealth;

        private int _round = 1;
        private int _score;
        private int _highScore;
        private int _lives = 3;
        private int _eagleHp = EagleMaxHealth;
        private int _aliveEnemies;
        private int _enemiesToSpawn;
        private int _maxAlive;
        private bool _bossPending;
        private float _nextSpawn;
        private float _roundClearAt = -1f;
        private float _nextEagleAlarm;

        private int _savedShotDamage = 1;
        private float _savedFireDelay = 0.34f;
        private float _savedMoveSpeed = 4.8f;
        private int[] _savedAmmo = new int[AmmoDatabase.AmmoTypeCount];
        private AmmoType _savedActiveAmmo = AmmoType.Basic;

        private float _shakeUntil;
        private float _shakeAmount;
        private string _toast = string.Empty;
        private float _toastUntil;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _hudStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _warningStyle;

        public bool IsPlaying => _state == GameState.Playing;
        public Vector2 PlayerPosition => _player != null ? (Vector2)_player.transform.position : BasePosition;
        public Vector2 BasePosition => _baseObject != null ? (Vector2)_baseObject.transform.position : new Vector2(0f, -5.95f);
        public int CurrentRound => _round;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameExists()
        {
            if (FindAnyObjectByType<TankGame>() != null) return;
            var go = new GameObject("TankGame");
            go.AddComponent<TankGame>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            _highScore = PlayerPrefs.GetInt("TankRevival.HighScore", 0);
            SetupCamera();
            if (GetComponent<BattleAudio>() == null)
                gameObject.AddComponent<BattleAudio>();
        }

        private void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                cameraGo.tag = "MainCamera";
                _camera = cameraGo.AddComponent<Camera>();
                cameraGo.AddComponent<AudioListener>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = 8.15f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.backgroundColor = new Color(0.020f, 0.027f, 0.040f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void Update()
        {
            if (_state == GameState.Menu)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartCampaign();
                return;
            }

            if (_state == GameState.GameOver || _state == GameState.Victory)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartCampaign();
                return;
            }

            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape))
                TogglePause();

            if (_state != GameState.Playing) return;

            if (_roundClearAt > 0f)
            {
                if (Time.time >= _roundClearAt)
                {
                    if (_round >= FinalRound)
                    {
                        WinCampaign();
                    }
                    else
                    {
                        CapturePlayerLoadout();
                        _eagleHp = Mathf.Min(EagleMaxHealth, _eagleHp + 1);
                        _round++;
                        BeginRound(_round);
                    }
                }
                return;
            }

            HandleSpawning();

            if (_enemiesToSpawn <= 0 && _aliveEnemies <= 0 && !_bossPending)
            {
                _roundClearAt = Time.time + 1.9f;
                ShowToast(_round == FinalRound ? "FINAL ARENA CLEARED" : $"ROUND {_round} CLEARED // ORZEŁEK SAFE", 1.8f);
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.62f, 0f);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            Vector3 basePosition = new Vector3(0f, 0f, -10f);
            if (Time.unscaledTime < _shakeUntil)
            {
                Vector2 jitter = Random.insideUnitCircle * _shakeAmount;
                _camera.transform.position = basePosition + new Vector3(jitter.x, jitter.y, 0f);
            }
            else
            {
                _camera.transform.position = basePosition;
            }
        }

        private void TogglePause()
        {
            if (_state == GameState.Playing)
            {
                _state = GameState.Paused;
                Time.timeScale = 0f;
                BattleAudio.Instance?.SetEngineMoving(false, 0f);
            }
            else if (_state == GameState.Paused)
            {
                _state = GameState.Playing;
                Time.timeScale = 1f;
            }
        }

        private void StartCampaign()
        {
            Time.timeScale = 1f;
            _score = 0;
            _round = 1;
            _lives = 3;
            _eagleHp = EagleMaxHealth;
            _savedShotDamage = 1;
            _savedFireDelay = 0.34f;
            _savedMoveSpeed = 4.8f;
            _savedActiveAmmo = AmmoType.Basic;
            _savedAmmo = new int[AmmoDatabase.AmmoTypeCount];
            _state = GameState.Playing;
            BeginRound(_round);
        }

        private void BeginRound(int round)
        {
            _roundClearAt = -1f;
            _aliveEnemies = 0;
            _enemiesToSpawn = Mathf.Min(62, 6 + Mathf.CeilToInt(round * 0.50f));
            _maxAlive = Mathf.Min(14, 4 + round / 10);
            _bossPending = round % 10 == 0;
            _nextSpawn = Time.time + 0.70f;
            BuildArena(round);

            if (_bossPending)
            {
                ShowToast($"ROUND {round:000} // BOSS ASSAULT", 2.0f);
                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.72f, 0f);
            }
            else
            {
                ShowToast($"ROUND {round:000} // DEFEND THE EAGLE", 1.45f);
            }
        }

        private void CapturePlayerLoadout()
        {
            if (_player == null) return;
            _savedShotDamage = _player.ShotDamage;
            _savedFireDelay = _player.FireDelay;
            _savedMoveSpeed = _player.MoveSpeed;
            _savedAmmo = _player.CopyAmmoInventory();
            _savedActiveAmmo = _player.ActiveAmmo;
        }

        private void BuildArena(int round)
        {
            CapturePlayerLoadout();
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);

            var root = new GameObject("World_Round_" + round.ToString("000"));
            _worldRoot = root.transform;
            _player = null;
            _baseObject = null;
            _baseHealth = null;

            BuildGround(round);
            BuildPerimeter();
            BuildEagleBase(round);
            BuildObstacles(round);
            SpawnPlayer();
        }

        private void BuildGround(int round)
        {
            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            Color[] groundPalette =
            {
                new Color(0.050f, 0.070f, 0.082f),
                new Color(0.060f, 0.073f, 0.068f),
                new Color(0.075f, 0.065f, 0.055f),
                new Color(0.052f, 0.068f, 0.075f),
                new Color(0.072f, 0.055f, 0.060f),
                new Color(0.046f, 0.060f, 0.082f),
                new Color(0.064f, 0.052f, 0.078f),
                new Color(0.068f, 0.070f, 0.050f),
                new Color(0.048f, 0.052f, 0.060f),
                new Color(0.060f, 0.040f, 0.045f)
            };

            Color ground = groundPalette[sector];
            VisualFactory.Rect("Ground", _worldRoot, new Vector2(24.8f, 14.5f), ground, Vector3.zero, -60);

            for (int x = -12; x <= 12; x++)
            {
                var c = x % 2 == 0 ? new Color(0.14f, 0.18f, 0.19f, 0.25f) : new Color(0.08f, 0.11f, 0.13f, 0.18f);
                VisualFactory.Rect("GridV", _worldRoot, new Vector2(0.018f, 14f), c, new Vector3(x, 0f, 0f), -58);
            }

            for (int y = -7; y <= 7; y++)
            {
                var c = y % 2 == 0 ? new Color(0.14f, 0.18f, 0.19f, 0.25f) : new Color(0.08f, 0.11f, 0.13f, 0.18f);
                VisualFactory.Rect("GridH", _worldRoot, new Vector2(24f, 0.018f), c, new Vector3(0f, y, 0f), -58);
            }

            var rng = new System.Random(round * 173 + 91);
            int craterCount = 8 + sector * 2;
            for (int i = 0; i < craterCount; i++)
            {
                float x = (float)(rng.NextDouble() * 21.0 - 10.5);
                float y = (float)(rng.NextDouble() * 10.5 - 5.25);
                float size = (float)(0.18 + rng.NextDouble() * 0.38);
                VisualFactory.Disc("Crater", _worldRoot, Vector2.one * size, new Color(0f, 0f, 0f, 0.14f), new Vector3(x, y, 0f), -57);
            }

            for (int i = 0; i < 30; i++)
            {
                float x = (float)(rng.NextDouble() * 22.0 - 11.0);
                float y = (float)(rng.NextDouble() * 12.0 - 6.0);
                float s = (float)(0.04 + rng.NextDouble() * 0.09);
                VisualFactory.Rect("GroundDebris", _worldRoot, new Vector2(s, s), new Color(0.22f, 0.27f, 0.28f, 0.24f), new Vector3(x, y, 0f), -56);
            }
        }

        private void BuildPerimeter()
        {
            for (int x = -12; x <= 12; x++)
            {
                CreateObstacle(new Vector2(x, ArenaHalfHeight), ObstacleKind.Steel, new Vector2(0.95f, 0.50f));
                CreateObstacle(new Vector2(x, -ArenaHalfHeight), ObstacleKind.Steel, new Vector2(0.95f, 0.50f));
            }

            for (int y = -6; y <= 6; y++)
            {
                CreateObstacle(new Vector2(-ArenaHalfWidth, y), ObstacleKind.Steel, new Vector2(0.50f, 0.95f));
                CreateObstacle(new Vector2(ArenaHalfWidth, y), ObstacleKind.Steel, new Vector2(0.50f, 0.95f));
            }
        }

        private void BuildEagleBase(int round)
        {
            var go = new GameObject("ORZELEK_DEFENSE_CORE");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = new Vector3(0f, -5.95f, 0f);
            _baseObject = go;

            VisualFactory.BuildEagleStronghold(go.transform);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.06f, 0.78f);

            _baseHealth = go.AddComponent<Health>();
            _baseHealth.Initialize(Team.Player, EagleMaxHealth, _eagleHp);
            _baseHealth.Damaged += OnEagleDamaged;
            _baseHealth.Died += _ => OnBaseDestroyed(go.transform.position);

            ObstacleKind sideKind = round >= 70 ? ObstacleKind.Steel : ObstacleKind.Brick;
            int wallHp = round >= 45 ? 3 : 2;
            CreateObstacle(new Vector2(-1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);
            CreateObstacle(new Vector2(1.15f, -5.85f), sideKind, new Vector2(0.75f, 0.75f), wallHp);
            CreateObstacle(new Vector2(-0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), wallHp);
            CreateObstacle(new Vector2(0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), wallHp);
        }

        private void OnEagleDamaged(Health eagle, int amount)
        {
            _eagleHp = eagle.Current;
            KickCamera(0.20f, 0.12f);
            VisualFactory.RingPulse(eagle.transform.position, new Color(1f, 0.12f, 0.08f), 1.15f);

            if (_eagleHp <= 2 && Time.time >= _nextEagleAlarm)
            {
                _nextEagleAlarm = Time.time + 2.5f;
                ShowToast("WARNING // ORZEŁEK CRITICAL", 1.9f);
                BattleAudio.PlayGlobal(SoundCue.EagleAlarm, 0.72f, 0f);
            }
        }

        public void RepairEagle(int amount)
        {
            if (amount <= 0) return;
            _eagleHp = Mathf.Min(EagleMaxHealth, _eagleHp + amount);
            if (_baseHealth != null && !_baseHealth.IsDead)
            {
                _baseHealth.Heal(amount);
                _eagleHp = _baseHealth.Current;
                VisualFactory.RingPulse(_baseHealth.transform.position, new Color(0.20f, 1f, 0.48f), 0.85f);
            }
        }

        private void BuildObstacles(int round)
        {
            var rng = new System.Random(round * 7919 + 17);
            int count = 25 + Mathf.Min(21, round / 4);

            for (int i = 0; i < count; i++)
            {
                int gx = rng.Next(-10, 11);
                int gy = rng.Next(-4, 5);
                Vector2 pos = new Vector2(gx, gy);

                if (Mathf.Abs(pos.x) < 2.0f && pos.y < -3.0f) continue;
                if (pos.y > 3.9f && (Mathf.Abs(pos.x) > 7f || Mathf.Abs(pos.x) < 2f)) continue;

                double roll = rng.NextDouble();
                ObstacleKind kind;
                if (round >= 6 && roll < Mathf.Min(0.19f, 0.055f + round * 0.0013f))
                    kind = ObstacleKind.Water;
                else if (round >= 18 && roll > Mathf.Max(0.78f, 0.90f - round * 0.0011f))
                    kind = ObstacleKind.Steel;
                else
                    kind = ObstacleKind.Brick;

                int hp = kind == ObstacleKind.Brick ? 1 + (round >= 35 && rng.NextDouble() < 0.38 ? 1 : 0) : 1;
                CreateObstacle(pos, kind, new Vector2(0.88f, 0.88f), hp);
            }

            int grassCount = 10 + round / 8;
            for (int i = 0; i < grassCount; i++)
            {
                float x = (float)(rng.NextDouble() * 20.0 - 10.0);
                float y = (float)(rng.NextDouble() * 9.0 - 4.0);
                VisualFactory.Rect("Grass", _worldRoot, new Vector2(0.85f, 0.85f), new Color(0.10f, 0.30f, 0.17f, 0.52f), new Vector3(x, y, 0f), 14);
                if (i % 3 == 0)
                    VisualFactory.Rect("GrassBlade", _worldRoot, new Vector2(0.08f, 0.72f), new Color(0.20f, 0.48f, 0.24f, 0.55f), new Vector3(x + 0.12f, y, 0f), 15);
            }
        }

        private GameObject CreateObstacle(Vector2 position, ObstacleKind kind, Vector2 size, int hp = 1)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = position;

            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;

            var obstacle = root.AddComponent<Obstacle>();
            obstacle.Initialize(kind, hp);

            if (kind == ObstacleKind.Brick)
            {
                VisualFactory.Rect("BrickShadow", root.transform, size * 0.98f, new Color(0.15f, 0.040f, 0.018f), new Vector3(0.05f, -0.05f, 0f), 0);
                VisualFactory.Rect("Brick", root.transform, size * 0.90f, new Color(0.58f, 0.13f, 0.045f), Vector3.zero, 1);
                VisualFactory.Rect("BrickTop", root.transform, new Vector2(size.x * 0.72f, size.y * 0.16f), new Color(0.92f, 0.34f, 0.09f), new Vector3(-0.04f, size.y * 0.20f, 0f), 2);
                VisualFactory.Rect("Mortar", root.transform, new Vector2(size.x * 0.10f, size.y * 0.72f), new Color(0.25f, 0.052f, 0.025f), new Vector3(0.10f, 0f, 0f), 2);
                VisualFactory.Rect("BrickEdge", root.transform, new Vector2(size.x * 0.80f, 0.035f), new Color(1f, 0.48f, 0.13f, 0.55f), new Vector3(0f, size.y * 0.34f, 0f), 3);
            }
            else if (kind == ObstacleKind.Steel)
            {
                VisualFactory.Rect("SteelShadow", root.transform, size * 0.98f, new Color(0.055f, 0.07f, 0.09f), new Vector3(0.04f, -0.04f, 0f), 0);
                VisualFactory.Rect("Steel", root.transform, size * 0.90f, new Color(0.32f, 0.39f, 0.47f), Vector3.zero, 1);
                VisualFactory.Rect("SteelTop", root.transform, new Vector2(size.x * 0.70f, size.y * 0.17f), new Color(0.70f, 0.80f, 0.88f), new Vector3(-0.04f, size.y * 0.20f, 0f), 2);
                VisualFactory.Disc("SteelBoltA", root.transform, new Vector2(0.09f, 0.09f), new Color(0.82f, 0.88f, 0.92f), new Vector3(-size.x * 0.28f, -size.y * 0.26f, 0f), 3);
                VisualFactory.Disc("SteelBoltB", root.transform, new Vector2(0.09f, 0.09f), new Color(0.82f, 0.88f, 0.92f), new Vector3(size.x * 0.28f, -size.y * 0.26f, 0f), 3);
            }
            else
            {
                VisualFactory.Rect("Water", root.transform, size * 0.94f, new Color(0.025f, 0.24f, 0.45f), Vector3.zero, -2);
                VisualFactory.Rect("WaterShine", root.transform, new Vector2(size.x * 0.74f, size.y * 0.10f), new Color(0.16f, 0.72f, 0.95f, 0.76f), new Vector3(0f, size.y * 0.18f, 0f), -1);
                VisualFactory.Rect("WaterShine2", root.transform, new Vector2(size.x * 0.42f, size.y * 0.055f), new Color(0.55f, 0.92f, 1f, 0.42f), new Vector3(-0.12f, -size.y * 0.18f, 0f), -1);
            }

            return root;
        }

        private void SpawnPlayer()
        {
            if (_state != GameState.Playing) return;
            var go = new GameObject("PlayerTank");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = new Vector3(0f, -4.05f, 0f);
            _player = go.AddComponent<PlayerTank>();
            _player.Initialize(this);
            _player.RestoreLoadout(_savedShotDamage, _savedFireDelay, _savedMoveSpeed, _savedAmmo, _savedActiveAmmo);
            _player.Health.InvulnerableUntil = Time.time + 1.75f;
            VisualFactory.RingPulse(go.transform.position, new Color(0.18f, 0.86f, 1f), 1.0f);
        }

        private void HandleSpawning()
        {
            if (_aliveEnemies >= _maxAlive || Time.time < _nextSpawn) return;

            if (_enemiesToSpawn > 0)
            {
                _enemiesToSpawn--;
                SpawnEnemy(ChooseEnemyKind(_round));
                _nextSpawn = Time.time + Mathf.Max(0.28f, 1.10f - _round * 0.0068f);
                return;
            }

            if (_bossPending)
            {
                _bossPending = false;
                SpawnEnemy(EnemyKind.Boss);
                ShowToast($"BOSS // ROUND {_round:000}", 2.1f);
                KickCamera(0.38f, 0.16f);
                BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.78f, 0f);
            }
        }

        private EnemyKind ChooseEnemyKind(int round)
        {
            float r = Random.value;
            float supplyChance = round >= 3 ? Mathf.Min(0.14f, 0.055f + round * 0.0008f) : 0f;
            if (r < supplyChance) return EnemyKind.Supply;

            float normalized = supplyChance < 0.99f ? (r - supplyChance) / (1f - supplyChance) : 1f;
            if (round >= 65 && normalized < 0.14f) return EnemyKind.Elite;
            if (round >= 45 && normalized < 0.29f) return EnemyKind.Siege;
            if (round >= 31 && normalized < 0.47f) return EnemyKind.Sniper;
            if (round >= 21 && normalized < 0.67f) return EnemyKind.Heavy;
            if (round >= 11 && normalized < 0.85f) return EnemyKind.Fast;
            return EnemyKind.Basic;
        }

        private AmmoType ChooseSupplyAmmo(int round)
        {
            int max = 1;
            if (round >= AmmoDatabase.UnlockRound(AmmoType.Explosive)) max = 2;
            if (round >= AmmoDatabase.UnlockRound(AmmoType.Incendiary)) max = 3;
            if (round >= AmmoDatabase.UnlockRound(AmmoType.EMP)) max = 4;
            if (round >= AmmoDatabase.UnlockRound(AmmoType.Twin)) max = 5;
            if (round >= AmmoDatabase.UnlockRound(AmmoType.Plasma)) max = 6;
            return (AmmoType)Random.Range(1, max + 1);
        }

        private void SpawnEnemy(EnemyKind kind)
        {
            Vector2[] spawnPoints =
            {
                new Vector2(-9.5f, 5.65f),
                new Vector2(-3.2f, 5.65f),
                new Vector2(3.2f, 5.65f),
                new Vector2(9.5f, 5.65f)
            };

            Vector2 pos = spawnPoints[Random.Range(0, spawnPoints.Length)];
            pos += new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.15f, 0.15f));
            AmmoType supplyAmmo = kind == EnemyKind.Supply ? ChooseSupplyAmmo(_round) : AmmoType.Basic;

            string name = kind == EnemyKind.Boss ? $"Boss_R{_round:000}" : kind == EnemyKind.Supply ? $"SUPPLY_{supplyAmmo}" : $"Enemy_{kind}";
            var go = new GameObject(name);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = pos;
            var enemy = go.AddComponent<EnemyTank>();
            enemy.Initialize(this, kind, _round, supplyAmmo);
            _aliveEnemies++;
        }

        public void SpawnProjectile(Vector2 position, Vector2 direction, Team team, int damage, float speed, Color color, AmmoType ammo = AmmoType.Basic)
        {
            if (_worldRoot == null) return;
            var go = new GameObject(team == Team.Player ? $"PlayerProjectile_{ammo}" : "EnemyProjectile");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            var projectile = go.AddComponent<Projectile>();
            projectile.Initialize(direction, team, damage, speed, color, ammo);
        }

        public void OnEnemyDestroyed(EnemyTank enemy, Vector3 position, EnemyKind kind)
        {
            _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
            int points = kind switch
            {
                EnemyKind.Fast => 150,
                EnemyKind.Heavy => 240,
                EnemyKind.Sniper => 300,
                EnemyKind.Siege => 380,
                EnemyKind.Elite => 520,
                EnemyKind.Supply => 450,
                EnemyKind.Boss => 2500 + _round * 30,
                _ => 100
            };
            _score += points;

            bool huge = kind == EnemyKind.Boss;
            Color blast = huge ? new Color(1f, 0.09f, 0.04f) : kind == EnemyKind.Supply ? AmmoDatabase.Color(enemy.SupplyAmmo) : new Color(1f, 0.38f, 0.10f);
            VisualFactory.Explosion(position, blast, huge ? 2.35f : kind == EnemyKind.Heavy || kind == EnemyKind.Siege ? 1.25f : 1.0f);
            KickCamera(huge ? 0.46f : 0.12f, huge ? 0.26f : 0.075f);
            BattleAudio.PlayGlobal(huge ? SoundCue.ExplosionLarge : SoundCue.ExplosionSmall, huge ? 0.90f : 0.40f, 0.07f);

            if (kind == EnemyKind.Supply)
            {
                SpawnAmmoPickup(position, enemy.SupplyAmmo);
                ShowToast($"SUPPLY TANK DOWN // {AmmoDatabase.DisplayName(enemy.SupplyAmmo)}", 1.45f);
                return;
            }

            if (kind == EnemyKind.Boss)
            {
                SpawnPowerUp(position + new Vector3(-0.45f, 0f, 0f));
                SpawnAmmoPickup(position + new Vector3(0.45f, 0f, 0f), ChooseSupplyAmmo(_round));
                return;
            }

            float dropChance = Mathf.Min(0.16f, 0.065f + _round * 0.0009f);
            if (Random.value < dropChance)
                SpawnPowerUp(position);
        }

        private void SpawnAmmoPickup(Vector3 position, AmmoType kind)
        {
            if (kind == AmmoType.Basic) kind = AmmoType.ArmorPiercing;
            var go = new GameObject("AmmoDrop_" + kind);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            var pickup = go.AddComponent<AmmoPickup>();
            pickup.Initialize(kind, AmmoDatabase.PickupAmount(kind, _round));
        }

        private void SpawnPowerUp(Vector3 position)
        {
            var go = new GameObject("PowerUp");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            var power = go.AddComponent<PowerUp>();
            power.Initialize((PowerUpKind)Random.Range(0, 5));
        }

        public void OnAmmoCollected(AmmoType kind, int amount)
        {
            _score += 350;
            ShowToast($"AMMO +{amount} // {AmmoDatabase.DisplayName(kind)}", 1.55f);
            BattleAudio.PlayGlobal(SoundCue.AmmoPickup, 0.70f, 0f);
        }

        public void OnPowerUpCollected(PowerUpKind kind)
        {
            _score += 250;
            ShowToast(kind switch
            {
                PowerUpKind.Repair => "FIELD REPAIR // TANK + EAGLE",
                PowerUpKind.RapidFire => "AUTOLOADER UPGRADE",
                PowerUpKind.PowerShot => "CANNON POWER +",
                PowerUpKind.Speed => "ENGINE BOOST +",
                _ => "SHIELD // 6 SEC"
            }, 1.25f);
            BattleAudio.PlayGlobal(SoundCue.Pickup, 0.62f, 0f);
            CapturePlayerLoadout();
        }

        public void OnPlayerDestroyed(Vector3 position)
        {
            if (_state != GameState.Playing) return;
            CapturePlayerLoadout();
            _player = null;
            _lives--;
            VisualFactory.Explosion(position, new Color(0.10f, 0.75f, 1f), 1.45f);
            KickCamera(0.32f, 0.18f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.60f, 0.05f);

            if (_lives <= 0)
            {
                LoseCampaign("YOUR TANK WAS DESTROYED");
            }
            else
            {
                ShowToast($"TANK LOST // {_lives} RESERVE", 1.35f);
                StartCoroutine(RespawnPlayerAfterDelay());
            }
        }

        private IEnumerator RespawnPlayerAfterDelay()
        {
            yield return new WaitForSeconds(1.20f);
            if (_state == GameState.Playing && _player == null) SpawnPlayer();
        }

        private void OnBaseDestroyed(Vector3 position)
        {
            if (_state != GameState.Playing) return;
            _eagleHp = 0;
            _baseObject = null;
            VisualFactory.Explosion(position, new Color(1f, 0.08f, 0.04f), 2.8f);
            KickCamera(0.78f, 0.34f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 1f, 0f);
            LoseCampaign("ORZEŁEK DESTROYED");
        }

        private void LoseCampaign(string reason)
        {
            Time.timeScale = 1f;
            _state = GameState.GameOver;
            BattleAudio.Instance?.SetEngineMoving(false, 0f);
            SaveHighScore();
            ShowToast(reason, 2.0f);
        }

        private void WinCampaign()
        {
            Time.timeScale = 1f;
            _state = GameState.Victory;
            _score += 20000 + _eagleHp * 2500;
            BattleAudio.Instance?.SetEngineMoving(false, 0f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 1f, 0f);
            SaveHighScore();
        }

        private void SaveHighScore()
        {
            if (_score <= _highScore) return;
            _highScore = _score;
            PlayerPrefs.SetInt("TankRevival.HighScore", _highScore);
            PlayerPrefs.Save();
        }

        public void KickCamera(float duration, float amount)
        {
            _shakeUntil = Mathf.Max(_shakeUntil, Time.unscaledTime + duration);
            _shakeAmount = Mathf.Max(_shakeAmount * 0.72f, amount);
        }

        private void ShowToast(string text, float duration)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + duration;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.24f, 0.90f, 1f) }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                normal = { textColor = new Color(0.78f, 0.88f, 0.94f) }
            };
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 29,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                fixedHeight = 48f
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = new Color(0.62f, 0.72f, 0.78f) }
            };
            _warningStyle = new GUIStyle(_hudStyle)
            {
                normal = { textColor = new Color(1f, 0.24f, 0.16f) }
            };
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_state == GameState.Menu)
            {
                DrawPanel(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.5f - 235f, 700f, 470f));
                GUI.Label(new Rect(0, Screen.height * 0.5f - 185f, Screen.width, 70f), "TANK REVIVAL", _titleStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 126f, Screen.width, 38f), "ORZEŁ OVERDRIVE // 100 ROUND DEFENSE", _subtitleStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 79f, Screen.width, 30f), "DEFEND THE EAGLE • HUNT SUPPLY TANKS • UPGRADE YOUR CANNON", _smallStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 42f, Screen.width, 32f), $"HIGH SCORE  {_highScore:N0}", _subtitleStyle);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 18f, 310f, 48f), "START DEFENSE", _buttonStyle)) StartCampaign();
                GUI.Label(new Rect(0, Screen.height * 0.5f + 89f, Screen.width, 27f), "WASD / ARROWS  •  SPACE FIRE  •  Q/E AMMO  •  1–7 DIRECT SELECT  •  P PAUSE", _smallStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f + 121f, Screen.width, 27f), "Supply tanks glow in the color of the ammunition they carry", _smallStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f + 153f, Screen.width, 27f), "Original game and procedural assets — classic top-down tank spirit", _smallStyle);
                return;
            }

            if (_state == GameState.Playing || _state == GameState.Paused)
            {
                DrawHud();
                if (_state == GameState.Paused)
                {
                    DrawPanel(new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 90f, 380f, 180f));
                    GUI.Label(new Rect(0, Screen.height * 0.5f - 40f, Screen.width, 52f), "PAUSED", _centerStyle);
                    GUI.Label(new Rect(0, Screen.height * 0.5f + 20f, Screen.width, 30f), "Press P or ESC to continue", _smallStyle);
                }
                return;
            }

            DrawPanel(new Rect(Screen.width * 0.5f - 350f, Screen.height * 0.5f - 215f, 700f, 430f));
            string headline = _state == GameState.Victory ? "ORZEŁEK SURVIVED 100 ROUNDS" : "DEFENSE FAILED";
            GUI.Label(new Rect(0, Screen.height * 0.5f - 160f, Screen.width, 64f), headline, _titleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 84f, Screen.width, 38f), $"ROUND {_round}   •   SCORE {_score:N0}", _subtitleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 42f, Screen.width, 32f), $"HIGH SCORE {_highScore:N0}", _subtitleStyle);
            if (GUI.Button(new Rect(Screen.width * 0.5f - 155f, Screen.height * 0.5f + 28f, 310f, 48f), "DEFEND AGAIN", _buttonStyle)) StartCampaign();
            GUI.Label(new Rect(0, Screen.height * 0.5f + 100f, Screen.width, 28f), "ENTER / SPACE also restarts", _smallStyle);
        }

        private void DrawHud()
        {
            int playerHp = _player != null && _player.Health != null ? _player.Health.Current : 0;
            int eagleHp = _baseHealth != null ? _baseHealth.Current : _eagleHp;
            int remaining = _aliveEnemies + _enemiesToSpawn + (_bossPending ? 1 : 0);
            AmmoType active = _player != null ? _player.ActiveAmmo : _savedActiveAmmo;
            int activeCount = _player != null ? _player.GetAmmoCount(active) : (active == AmmoType.Basic ? -1 : _savedAmmo[(int)active]);
            string ammoCount = active == AmmoType.Basic ? "∞" : activeCount.ToString();

            GUI.Box(new Rect(14f, 12f, 480f, 108f), string.Empty);
            GUI.Label(new Rect(28f, 19f, 455f, 27f), $"ROUND {_round:000}/100     SCORE {_score:N0}     ENEMY {remaining}", _hudStyle);
            GUI.Label(new Rect(28f, 47f, 455f, 27f), $"LIVES {_lives}   ARMOR {playerHp}   ORZEŁEK {eagleHp}/{EagleMaxHealth}", eagleHp <= 2 ? _warningStyle : _hudStyle);
            GUI.Label(new Rect(28f, 75f, 455f, 27f), $"AMMO {AmmoDatabase.DisplayName(active)}  [{ammoCount}]   •   Q/E switch", _hudStyle);

            if (_player != null)
            {
                string inventory = $"1 STD ∞   2 AP {_player.GetAmmoCount(AmmoType.ArmorPiercing)}   3 HE {_player.GetAmmoCount(AmmoType.Explosive)}   4 FIRE {_player.GetAmmoCount(AmmoType.Incendiary)}   5 EMP {_player.GetAmmoCount(AmmoType.EMP)}   6 TWIN {_player.GetAmmoCount(AmmoType.Twin)}   7 PLASMA {_player.GetAmmoCount(AmmoType.Plasma)}";
                GUI.Box(new Rect(14f, Screen.height - 48f, Mathf.Min(Screen.width - 28f, 930f), 34f), string.Empty);
                GUI.Label(new Rect(24f, Screen.height - 43f, Mathf.Min(Screen.width - 48f, 910f), 25f), inventory, _smallStyle);
            }

            if (Time.unscaledTime < _toastUntil)
                GUI.Label(new Rect(0f, 24f, Screen.width, 46f), _toast, _centerStyle);
        }

        private static void DrawPanel(Rect rect)
        {
            GUI.color = new Color(0.035f, 0.050f, 0.072f, 0.97f);
            GUI.Box(rect, string.Empty);
            GUI.color = Color.white;
        }
    }
}
