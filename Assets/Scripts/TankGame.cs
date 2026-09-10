using System;
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
        private int _aliveEnemies;
        private int _enemiesToSpawn;
        private int _maxAlive;
        private bool _bossPending;
        private float _nextSpawn;
        private float _roundClearAt = -1f;

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

        public bool IsPlaying => _state == GameState.Playing;
        public Vector2 PlayerPosition => _player != null ? (Vector2)_player.transform.position : BasePosition;
        public Vector2 BasePosition => _baseObject != null ? (Vector2)_baseObject.transform.position : new Vector2(0f, -6f);

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
            _camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
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
            {
                TogglePause();
            }

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
                        _round++;
                        BeginRound(_round);
                    }
                }
                return;
            }

            HandleSpawning();

            if (_enemiesToSpawn <= 0 && _aliveEnemies <= 0 && !_bossPending)
            {
                _roundClearAt = Time.time + 1.8f;
                ShowToast(_round == FinalRound ? "FINAL ARENA CLEARED" : $"ROUND {_round} CLEARED", 1.7f);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            Vector3 basePosition = new Vector3(0f, 0f, -10f);
            if (Time.unscaledTime < _shakeUntil)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * _shakeAmount;
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
            _state = GameState.Playing;
            BeginRound(_round);
        }

        private void BeginRound(int round)
        {
            _roundClearAt = -1f;
            _aliveEnemies = 0;
            _enemiesToSpawn = Mathf.Min(68, 5 + Mathf.CeilToInt(round * 0.55f));
            _maxAlive = Mathf.Min(12, 4 + round / 12);
            _bossPending = round % 10 == 0;
            _nextSpawn = Time.time + 0.75f;
            BuildArena(round);
            ShowToast(round % 10 == 0 ? $"ROUND {round}  //  BOSS" : $"ROUND {round}", 1.5f);
        }

        private void BuildArena(int round)
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            var root = new GameObject("World");
            _worldRoot = root.transform;
            _player = null;
            _baseObject = null;
            _baseHealth = null;

            BuildGround(round);
            BuildPerimeter();
            BuildBase();
            BuildObstacles(round);
            SpawnPlayer();
        }

        private void BuildGround(int round)
        {
            VisualFactory.Rect("Ground", _worldRoot, new Vector2(24.8f, 14.5f), new Color(0.055f, 0.075f, 0.090f), Vector3.zero, -60);

            for (int x = -12; x <= 12; x++)
            {
                var c = x % 2 == 0 ? new Color(0.10f, 0.14f, 0.16f, 0.35f) : new Color(0.08f, 0.11f, 0.13f, 0.25f);
                VisualFactory.Rect("GridV", _worldRoot, new Vector2(0.025f, 14f), c, new Vector3(x, 0f, 0f), -58);
            }
            for (int y = -7; y <= 7; y++)
            {
                var c = y % 2 == 0 ? new Color(0.10f, 0.14f, 0.16f, 0.35f) : new Color(0.08f, 0.11f, 0.13f, 0.25f);
                VisualFactory.Rect("GridH", _worldRoot, new Vector2(24f, 0.025f), c, new Vector3(0f, y, 0f), -58);
            }

            var rng = new System.Random(round * 173 + 91);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)(rng.NextDouble() * 22.0 - 11.0);
                float y = (float)(rng.NextDouble() * 12.0 - 6.0);
                float s = (float)(0.05 + rng.NextDouble() * 0.11);
                VisualFactory.Rect("GroundSpark", _worldRoot, new Vector2(s, s), new Color(0.20f, 0.30f, 0.32f, 0.30f), new Vector3(x, y, 0f), -57);
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

        private void BuildBase()
        {
            var go = new GameObject("BaseCore");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = new Vector3(0f, -5.95f, 0f);
            _baseObject = go;

            VisualFactory.Rect("BaseShadow", go.transform, new Vector2(1.25f, 0.95f), new Color(0f, 0f, 0f, 0.38f), new Vector3(0.07f, -0.07f, 0f), 2);
            VisualFactory.Rect("BaseBody", go.transform, new Vector2(1.10f, 0.82f), new Color(0.18f, 0.24f, 0.30f), Vector3.zero, 3);
            VisualFactory.Rect("BaseCoreGlow", go.transform, new Vector2(0.58f, 0.50f), new Color(0.10f, 0.86f, 1f), Vector3.zero, 4);
            VisualFactory.Rect("BaseCore", go.transform, new Vector2(0.28f, 0.28f), new Color(0.82f, 0.98f, 1f), Vector3.zero, 5);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.0f, 0.76f);

            _baseHealth = go.AddComponent<Health>();
            _baseHealth.Initialize(Team.Player, 4);
            _baseHealth.Damaged += (_, __) => KickCamera(0.16f, 0.10f);
            _baseHealth.Died += _ => OnBaseDestroyed(go.transform.position);

            CreateObstacle(new Vector2(-1.15f, -5.85f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), 2);
            CreateObstacle(new Vector2(1.15f, -5.85f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), 2);
            CreateObstacle(new Vector2(-0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), 2);
            CreateObstacle(new Vector2(0.75f, -5.05f), ObstacleKind.Brick, new Vector2(0.75f, 0.75f), 2);
        }

        private void BuildObstacles(int round)
        {
            var rng = new System.Random(round * 7919 + 17);
            int count = 24 + Mathf.Min(18, round / 5);

            for (int i = 0; i < count; i++)
            {
                int gx = rng.Next(-10, 11);
                int gy = rng.Next(-4, 5);
                Vector2 pos = new Vector2(gx, gy);

                if (Mathf.Abs(pos.x) < 2.0f && pos.y < -3.0f) continue;
                if (pos.y > 3.9f && (Mathf.Abs(pos.x) > 7f || Mathf.Abs(pos.x) < 2f)) continue;

                double roll = rng.NextDouble();
                ObstacleKind kind;
                if (round >= 6 && roll < Mathf.Min(0.18f, 0.06f + round * 0.0012f))
                    kind = ObstacleKind.Water;
                else if (round >= 18 && roll > 0.86)
                    kind = ObstacleKind.Steel;
                else
                    kind = ObstacleKind.Brick;

                int hp = kind == ObstacleKind.Brick ? 1 + (round >= 45 && rng.NextDouble() < 0.32 ? 1 : 0) : 1;
                CreateObstacle(pos, kind, new Vector2(0.88f, 0.88f), hp);
            }

            int grassCount = 10 + round / 10;
            for (int i = 0; i < grassCount; i++)
            {
                float x = (float)(rng.NextDouble() * 20.0 - 10.0);
                float y = (float)(rng.NextDouble() * 9.0 - 4.0);
                VisualFactory.Rect("Grass", _worldRoot, new Vector2(0.85f, 0.85f), new Color(0.12f, 0.32f, 0.19f, 0.60f), new Vector3(x, y, 0f), 14);
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
                VisualFactory.Rect("BrickShadow", root.transform, size * 0.98f, new Color(0.18f, 0.055f, 0.025f), new Vector3(0.05f, -0.05f, 0f), 0);
                VisualFactory.Rect("Brick", root.transform, size * 0.90f, new Color(0.68f, 0.19f, 0.07f), Vector3.zero, 1);
                VisualFactory.Rect("BrickTop", root.transform, new Vector2(size.x * 0.72f, size.y * 0.17f), new Color(1f, 0.43f, 0.13f), new Vector3(-0.04f, size.y * 0.20f, 0f), 2);
                VisualFactory.Rect("Mortar", root.transform, new Vector2(size.x * 0.12f, size.y * 0.72f), new Color(0.31f, 0.075f, 0.035f), new Vector3(0.10f, 0f, 0f), 2);
            }
            else if (kind == ObstacleKind.Steel)
            {
                VisualFactory.Rect("SteelShadow", root.transform, size * 0.98f, new Color(0.07f, 0.09f, 0.12f), new Vector3(0.04f, -0.04f, 0f), 0);
                VisualFactory.Rect("Steel", root.transform, size * 0.90f, new Color(0.38f, 0.47f, 0.57f), Vector3.zero, 1);
                VisualFactory.Rect("SteelTop", root.transform, new Vector2(size.x * 0.70f, size.y * 0.18f), new Color(0.76f, 0.86f, 0.94f), new Vector3(-0.04f, size.y * 0.20f, 0f), 2);
            }
            else
            {
                VisualFactory.Rect("Water", root.transform, size * 0.94f, new Color(0.04f, 0.32f, 0.55f), Vector3.zero, -2);
                VisualFactory.Rect("WaterShine", root.transform, new Vector2(size.x * 0.74f, size.y * 0.12f), new Color(0.18f, 0.78f, 0.95f, 0.80f), new Vector3(0f, size.y * 0.18f, 0f), -1);
            }

            return root;
        }

        private void SpawnPlayer()
        {
            if (_state != GameState.Playing) return;
            var go = new GameObject("PlayerTank");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = new Vector3(0f, -4.15f, 0f);
            _player = go.AddComponent<PlayerTank>();
            _player.Initialize(this);
            _player.Health.InvulnerableUntil = Time.time + 1.7f;
        }

        private void HandleSpawning()
        {
            if (_aliveEnemies >= _maxAlive || Time.time < _nextSpawn) return;

            if (_enemiesToSpawn > 0)
            {
                _enemiesToSpawn--;
                SpawnEnemy(ChooseEnemyKind(_round));
                _nextSpawn = Time.time + Mathf.Max(0.34f, 1.15f - _round * 0.0065f);
                return;
            }

            if (_bossPending)
            {
                _bossPending = false;
                SpawnEnemy(EnemyKind.Boss);
                ShowToast($"BOSS // ROUND {_round}", 2.0f);
                KickCamera(0.35f, 0.15f);
            }
        }

        private EnemyKind ChooseEnemyKind(int round)
        {
            float r = UnityEngine.Random.value;
            if (round >= 31 && r < Mathf.Min(0.24f, 0.08f + round * 0.0012f)) return EnemyKind.Sniper;
            if (round >= 21 && r < Mathf.Min(0.50f, 0.18f + round * 0.0021f)) return EnemyKind.Heavy;
            if (round >= 11 && r < Mathf.Min(0.76f, 0.40f + round * 0.0023f)) return EnemyKind.Fast;
            return EnemyKind.Basic;
        }

        private void SpawnEnemy(EnemyKind kind)
        {
            Vector2[] spawnPoints =
            {
                new Vector2(-9.5f, 5.65f),
                new Vector2(0f, 5.65f),
                new Vector2(9.5f, 5.65f)
            };

            Vector2 pos = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            pos += new Vector2(UnityEngine.Random.Range(-0.28f, 0.28f), UnityEngine.Random.Range(-0.18f, 0.18f));

            var go = new GameObject(kind == EnemyKind.Boss ? $"Boss_R{_round}" : $"Enemy_{kind}");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = pos;
            var enemy = go.AddComponent<EnemyTank>();
            enemy.Initialize(this, kind, _round);
            _aliveEnemies++;
        }

        public void SpawnProjectile(Vector2 position, Vector2 direction, Team team, int damage, float speed, Color color)
        {
            if (_worldRoot == null) return;
            var go = new GameObject(team == Team.Player ? "PlayerProjectile" : "EnemyProjectile");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            var projectile = go.AddComponent<Projectile>();
            projectile.Initialize(direction, team, damage, speed, color);
        }

        public void OnEnemyDestroyed(EnemyTank enemy, Vector3 position, EnemyKind kind)
        {
            _aliveEnemies = Mathf.Max(0, _aliveEnemies - 1);
            int points = kind switch
            {
                EnemyKind.Fast => 150,
                EnemyKind.Heavy => 240,
                EnemyKind.Sniper => 300,
                EnemyKind.Boss => 2500 + _round * 25,
                _ => 100
            };
            _score += points;

            Color blast = kind == EnemyKind.Boss ? new Color(1f, 0.12f, 0.08f) : new Color(1f, 0.42f, 0.12f);
            VisualFactory.Explosion(position, blast, kind == EnemyKind.Boss ? 2.2f : 1.0f);
            KickCamera(kind == EnemyKind.Boss ? 0.42f : 0.11f, kind == EnemyKind.Boss ? 0.24f : 0.075f);

            float dropChance = Mathf.Min(0.23f, 0.11f + _round * 0.0012f);
            if (kind == EnemyKind.Boss || UnityEngine.Random.value < dropChance)
            {
                SpawnPowerUp(position);
            }
        }

        private void SpawnPowerUp(Vector3 position)
        {
            var go = new GameObject("PowerUp");
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = position;
            var power = go.AddComponent<PowerUp>();
            power.Initialize((PowerUpKind)UnityEngine.Random.Range(0, 5));
        }

        public void OnPowerUpCollected(PowerUpKind kind)
        {
            _score += 250;
            ShowToast(kind switch
            {
                PowerUpKind.Repair => "REPAIR +2",
                PowerUpKind.RapidFire => "RAPID FIRE +",
                PowerUpKind.PowerShot => "CANNON POWER +",
                PowerUpKind.Speed => "ENGINE BOOST +",
                _ => "SHIELD // 6 SEC"
            }, 1.2f);
        }

        public void OnPlayerDestroyed(Vector3 position)
        {
            if (_state != GameState.Playing) return;
            _player = null;
            _lives--;
            VisualFactory.Explosion(position, new Color(0.12f, 0.82f, 1f), 1.4f);
            KickCamera(0.30f, 0.18f);

            if (_lives <= 0)
            {
                LoseCampaign("TANK DESTROYED");
            }
            else
            {
                StartCoroutine(RespawnPlayerAfterDelay());
            }
        }

        private IEnumerator RespawnPlayerAfterDelay()
        {
            yield return new WaitForSeconds(1.25f);
            if (_state == GameState.Playing && _player == null) SpawnPlayer();
        }

        private void OnBaseDestroyed(Vector3 position)
        {
            if (_state != GameState.Playing) return;
            _baseObject = null;
            VisualFactory.Explosion(position, new Color(1f, 0.12f, 0.05f), 2.5f);
            KickCamera(0.70f, 0.32f);
            LoseCampaign("BASE DESTROYED");
        }

        private void LoseCampaign(string reason)
        {
            Time.timeScale = 1f;
            _state = GameState.GameOver;
            SaveHighScore();
            ShowToast(reason, 2.0f);
        }

        private void WinCampaign()
        {
            Time.timeScale = 1f;
            _state = GameState.Victory;
            _score += 10000;
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
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.25f, 0.92f, 1f) }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                normal = { textColor = new Color(0.78f, 0.88f, 0.94f) }
            };
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 30,
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
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (_state == GameState.Menu)
            {
                DrawPanel(new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.5f - 220f, 660f, 440f));
                GUI.Label(new Rect(0, Screen.height * 0.5f - 165f, Screen.width, 70f), "TANK REVIVAL", _titleStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 105f, Screen.width, 40f), "OVERDRIVE // 100 ROUND CAMPAIGN", _subtitleStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f - 52f, Screen.width, 32f), $"HIGH SCORE  {_highScore:N0}", _subtitleStyle);
                if (GUI.Button(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f + 12f, 300f, 48f), "START CAMPAIGN", _buttonStyle)) StartCampaign();
                GUI.Label(new Rect(0, Screen.height * 0.5f + 82f, Screen.width, 28f), "WASD / ARROWS  •  SPACE FIRE  •  P PAUSE", _smallStyle);
                GUI.Label(new Rect(0, Screen.height * 0.5f + 112f, Screen.width, 28f), "Original prototype build — no ripped game assets", _smallStyle);
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

            DrawPanel(new Rect(Screen.width * 0.5f - 330f, Screen.height * 0.5f - 205f, 660f, 410f));
            string headline = _state == GameState.Victory ? "100 ROUNDS CLEARED" : "MISSION FAILED";
            GUI.Label(new Rect(0, Screen.height * 0.5f - 150f, Screen.width, 60f), headline, _titleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 78f, Screen.width, 38f), $"ROUND {_round}   •   SCORE {_score:N0}", _subtitleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f - 36f, Screen.width, 32f), $"HIGH SCORE {_highScore:N0}", _subtitleStyle);
            if (GUI.Button(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f + 30f, 300f, 48f), "PLAY AGAIN", _buttonStyle)) StartCampaign();
            GUI.Label(new Rect(0, Screen.height * 0.5f + 102f, Screen.width, 28f), "ENTER / SPACE also restarts", _smallStyle);
        }

        private void DrawHud()
        {
            int playerHp = _player != null && _player.Health != null ? _player.Health.Current : 0;
            int baseHp = _baseHealth != null ? _baseHealth.Current : 0;
            int remaining = _aliveEnemies + _enemiesToSpawn + (_bossPending ? 1 : 0);

            GUI.Box(new Rect(14f, 12f, 360f, 86f), string.Empty);
            GUI.Label(new Rect(28f, 20f, 340f, 28f), $"ROUND {_round:000}/100     SCORE {_score:N0}", _hudStyle);
            GUI.Label(new Rect(28f, 50f, 340f, 28f), $"LIVES {_lives}   ARMOR {playerHp}   BASE {baseHp}   ENEMY {remaining}", _hudStyle);

            if (Time.unscaledTime < _toastUntil)
            {
                GUI.Label(new Rect(0f, 22f, Screen.width, 46f), _toast, _centerStyle);
            }
        }

        private static void DrawPanel(Rect rect)
        {
            GUI.color = new Color(0.04f, 0.06f, 0.085f, 0.96f);
            GUI.Box(rect, string.Empty);
            GUI.color = Color.white;
        }
    }
}
