using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum FrontlineMissionType
    {
        StandardDefense,
        EscortConvoy,
        HoldStrongpoints,
        DestroyCommandBunker,
        MinefieldBreakthrough
    }

    /// <summary>
    /// v0.8 campaign variety layer. Special operations are deterministic per round and run
    /// alongside the normal enemy wave so the 100-round campaign gains objectives without
    /// destabilizing the proven TankGame round-completion flow.
    /// </summary>
    public sealed class FrontlineMissionDirector : MonoBehaviour
    {
        private TankGame _game;
        private int _lastRound = -1;
        private FrontlineMissionType _mission = FrontlineMissionType.StandardDefense;
        private Transform _missionRoot;
        private int _objectivesAlive;
        private int _objectivesRequired;
        private int _collected;
        private float _missionDeadline;
        private bool _resolved;
        private bool _success;
        private string _status = string.Empty;
        private GUIStyle _titleStyle;
        private GUIStyle _statusStyle;

        public FrontlineMissionType CurrentMission => _mission;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDirector()
        {
            if (FindAnyObjectByType<FrontlineMissionDirector>() != null) return;
            new GameObject("FrontlineMissionDirector").AddComponent<FrontlineMissionDirector>();
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
                BeginMission(round);

            if (_resolved) return;

            switch (_mission)
            {
                case FrontlineMissionType.HoldStrongpoints:
                    UpdateStrongpointMission();
                    break;
                case FrontlineMissionType.MinefieldBreakthrough:
                    UpdateMinefieldMission();
                    break;
            }
        }

        private void BeginMission(int round)
        {
            _lastRound = round;
            _resolved = false;
            _success = false;
            _objectivesAlive = 0;
            _objectivesRequired = 0;
            _collected = 0;

            if (_missionRoot != null)
                Destroy(_missionRoot.gameObject);

            _missionRoot = new GameObject("FrontlineMission_R" + round.ToString("000")).transform;
            _mission = SelectMission(round);

            switch (_mission)
            {
                case FrontlineMissionType.EscortConvoy:
                    StartEscort(round);
                    break;
                case FrontlineMissionType.HoldStrongpoints:
                    StartStrongpoints(round);
                    break;
                case FrontlineMissionType.DestroyCommandBunker:
                    StartBunkerStrike(round);
                    break;
                case FrontlineMissionType.MinefieldBreakthrough:
                    StartMinefield(round);
                    break;
                default:
                    _status = "Primary objective: defend Orzełek";
                    _resolved = true;
                    break;
            }
        }

        private static FrontlineMissionType SelectMission(int round)
        {
            if (round < 6 || round % 10 == 0) return FrontlineMissionType.StandardDefense;

            int sector = (round - 1) / 10;
            int selector = (round * 7 + sector * 3) % 4;
            return selector switch
            {
                0 => FrontlineMissionType.EscortConvoy,
                1 => FrontlineMissionType.HoldStrongpoints,
                2 => FrontlineMissionType.DestroyCommandBunker,
                _ => FrontlineMissionType.MinefieldBreakthrough
            };
        }

        private void StartEscort(int round)
        {
            _status = "Escort armored supply carrier to extraction";
            var go = new GameObject("ALLIED_ARMORED_CONVOY");
            go.transform.SetParent(_missionRoot, false);
            go.transform.position = new Vector3(-10.2f, Mathf.Lerp(-2.4f, 2.3f, ((round * 13) % 100) / 100f), 0f);
            var convoy = go.AddComponent<ArmoredConvoy>();
            convoy.Initialize(_game, round, OnConvoyResolved);
        }

        private void StartStrongpoints(int round)
        {
            _objectivesRequired = round >= 60 ? 3 : 2;
            _objectivesAlive = _objectivesRequired;
            _missionDeadline = Time.time + Mathf.Lerp(16f, 25f, Mathf.InverseLerp(10f, 100f, round));
            _status = "Hold forward command posts";

            Vector2[] positions =
            {
                new Vector2(-6.3f, 0.3f),
                new Vector2(6.3f, 0.3f),
                new Vector2(0f, 2.8f)
            };

            for (int i = 0; i < _objectivesRequired; i++)
            {
                var go = new GameObject("ALLIED_STRONGPOINT_" + (i + 1));
                go.transform.SetParent(_missionRoot, false);
                go.transform.position = positions[i];
                var point = go.AddComponent<FrontlineStrongpoint>();
                point.Initialize(round, OnStrongpointDestroyed);
            }
        }

        private void StartBunkerStrike(int round)
        {
            _status = "Destroy enemy command bunker";
            var go = new GameObject("ENEMY_COMMAND_BUNKER");
            go.transform.SetParent(_missionRoot, false);
            float x = ((round / 3) % 2 == 0) ? -6.7f : 6.7f;
            go.transform.position = new Vector3(x, 3.65f, 0f);
            var bunker = go.AddComponent<EnemyCommandBunker>();
            bunker.Initialize(_game, round, OnBunkerDestroyed);
        }

        private void StartMinefield(int round)
        {
            _missionDeadline = Time.time + Mathf.Lerp(18f, 28f, Mathf.InverseLerp(10f, 100f, round));
            _status = "Minefield active — use the chaos against enemy armor";

            int count = Mathf.Clamp(8 + round / 12, 8, 16);
            var rng = new System.Random(round * 2377 + 81);
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * 18.0 - 9.0);
                float y = (float)(rng.NextDouble() * 7.2 - 2.4);
                if (Mathf.Abs(x) < 2f && y < -1.5f) x += x < 0f ? -2.2f : 2.2f;

                var go = new GameObject("FRONTLINE_MINE");
                go.transform.SetParent(_missionRoot, false);
                go.transform.position = new Vector3(x, y, 0f);
                var mine = go.AddComponent<FrontlineMine>();
                mine.Initialize(round);
            }
        }

        private void UpdateStrongpointMission()
        {
            if (_objectivesAlive <= 0)
            {
                Resolve(false, "All forward posts lost");
                return;
            }

            float remaining = Mathf.Max(0f, _missionDeadline - Time.time);
            _status = $"Hold command posts: {_objectivesAlive}/{_objectivesRequired} • {remaining:0}s";
            if (remaining <= 0f)
                Resolve(true, "Forward line secured");
        }

        private void UpdateMinefieldMission()
        {
            float remaining = Mathf.Max(0f, _missionDeadline - Time.time);
            int mines = _missionRoot != null ? _missionRoot.GetComponentsInChildren<FrontlineMine>().Length : 0;
            _status = $"Minefield active: {mines} armed • {remaining:0}s";
            if (remaining <= 0f)
                Resolve(true, "Minefield pressure survived");
        }

        private void OnStrongpointDestroyed()
        {
            _objectivesAlive = Mathf.Max(0, _objectivesAlive - 1);
        }

        private void OnConvoyResolved(bool reachedExtraction)
        {
            Resolve(reachedExtraction, reachedExtraction ? "Convoy reached extraction" : "Convoy destroyed");
        }

        private void OnBunkerDestroyed()
        {
            Resolve(true, "Enemy command bunker destroyed");
        }

        private void Resolve(bool success, string result)
        {
            if (_resolved) return;
            _resolved = true;
            _success = success;
            _status = result;

            if (success)
                GrantMissionReward();
        }

        private void GrantMissionReward()
        {
            if (_game == null) return;
            _game.RepairEagle(1);

            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                AmmoType ammo = RewardAmmo(_game.CurrentRound);
                int amount = 3 + Mathf.Clamp(_game.CurrentRound / 25, 0, 3);
                player.AddAmmo(ammo, amount);

                if (_game.CurrentRound >= 30)
                {
                    PowerUpKind kind = (PowerUpKind)((_game.CurrentRound / 5) % 5);
                    player.ApplyPowerUp(kind);
                }

                VisualFactory.RingPulse(player.transform.position, new Color(0.20f, 1f, 0.55f), 1.15f);
            }

            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.55f, 0.08f);
        }

        private static AmmoType RewardAmmo(int round)
        {
            if (round >= 70) return AmmoType.Plasma;
            if (round >= 50) return AmmoType.Twin;
            if (round >= 35) return AmmoType.EMP;
            if (round >= 22) return AmmoType.Incendiary;
            if (round >= 12) return AmmoType.Explosive;
            return AmmoType.ArmorPiercing;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.30f, 0.92f, 1f) }
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.82f, 0.90f, 0.94f) }
            };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || _mission == FrontlineMissionType.StandardDefense) return;
            EnsureStyles();

            float width = Mathf.Min(610f, Screen.width - 40f);
            float x = Screen.width * 0.5f - width * 0.5f;
            GUI.Box(new Rect(x, Screen.height - 91f, width, 38f), string.Empty);
            GUI.Label(new Rect(x + 8f, Screen.height - 88f, width - 16f, 18f), MissionName(_mission) + (_resolved ? (_success ? " // COMPLETE" : " // FAILED") : string.Empty), _titleStyle);
            GUI.Label(new Rect(x + 8f, Screen.height - 71f, width - 16f, 16f), _status, _statusStyle);
        }

        private static string MissionName(FrontlineMissionType mission)
        {
            return mission switch
            {
                FrontlineMissionType.EscortConvoy => "SPECIAL OP // ARMORED ESCORT",
                FrontlineMissionType.HoldStrongpoints => "SPECIAL OP // HOLD THE LINE",
                FrontlineMissionType.DestroyCommandBunker => "SPECIAL OP // DECAPITATION STRIKE",
                FrontlineMissionType.MinefieldBreakthrough => "SPECIAL OP // MINEFIELD",
                _ => "DEFEND ORZEŁEK"
            };
        }
    }

    public sealed class ArmoredConvoy : MonoBehaviour
    {
        private TankGame _game;
        private System.Action<bool> _resolved;
        private Health _health;
        private float _speed;
        private bool _finished;

        public void Initialize(TankGame game, int round, System.Action<bool> resolved)
        {
            _game = game;
            _resolved = resolved;
            _speed = Mathf.Lerp(1.0f, 1.45f, Mathf.InverseLerp(6f, 100f, round));

            VisualFactory.Rect("ConvoyShadow", transform, new Vector2(1.25f, 0.75f), new Color(0f, 0f, 0f, 0.38f), new Vector3(0.07f, -0.08f, 0f), 11);
            VisualFactory.Rect("ConvoyHull", transform, new Vector2(1.18f, 0.68f), new Color(0.16f, 0.52f, 0.30f), Vector3.zero, 12);
            VisualFactory.Rect("Cargo", transform, new Vector2(0.55f, 0.52f), new Color(0.22f, 0.78f, 0.42f), new Vector3(-0.18f, 0f, 0f), 13);
            VisualFactory.Disc("Beacon", transform, new Vector2(0.16f, 0.16f), new Color(0.30f, 1f, 0.65f), new Vector3(0.32f, 0.18f, 0f), 14);

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.12f, 0.62f);
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.bodyType = RigidbodyType2D.Kinematic;

            _health = gameObject.AddComponent<Health>();
            _health.Initialize(Team.Player, Mathf.Clamp(5 + round / 18, 5, 10));
            _health.Died += _ => Fail();
        }

        private void Update()
        {
            if (_finished || _game == null || !_game.IsPlaying) return;
            transform.position += Vector3.right * (_speed * Time.deltaTime);
            if (transform.position.x >= 10.4f)
            {
                _finished = true;
                _resolved?.Invoke(true);
                VisualFactory.RingPulse(transform.position, new Color(0.20f, 1f, 0.55f), 1.1f);
                Destroy(gameObject);
            }
        }

        private void Fail()
        {
            if (_finished) return;
            _finished = true;
            _resolved?.Invoke(false);
            VisualFactory.Explosion(transform.position, new Color(1f, 0.32f, 0.08f), 1.4f);
        }
    }

    public sealed class FrontlineStrongpoint : MonoBehaviour
    {
        private System.Action _destroyed;

        public void Initialize(int round, System.Action destroyed)
        {
            _destroyed = destroyed;
            VisualFactory.Rect("Base", transform, new Vector2(1.15f, 1.15f), new Color(0.10f, 0.28f, 0.34f), Vector3.zero, 10);
            VisualFactory.Rect("Armor", transform, new Vector2(0.90f, 0.90f), new Color(0.18f, 0.58f, 0.70f), Vector3.zero, 11);
            VisualFactory.Disc("Core", transform, new Vector2(0.32f, 0.32f), new Color(0.35f, 0.95f, 1f), Vector3.zero, 12);

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            var health = gameObject.AddComponent<Health>();
            health.Initialize(Team.Player, Mathf.Clamp(4 + round / 24, 4, 8));
            health.Died += _ =>
            {
                _destroyed?.Invoke();
                VisualFactory.Explosion(transform.position, new Color(0.18f, 0.70f, 1f), 1.15f);
            };
        }
    }

    public sealed class EnemyCommandBunker : MonoBehaviour
    {
        private TankGame _game;
        private System.Action _destroyed;
        private float _nextShot;
        private Health _health;

        public void Initialize(TankGame game, int round, System.Action destroyed)
        {
            _game = game;
            _destroyed = destroyed;
            VisualFactory.Rect("BunkerShadow", transform, new Vector2(1.75f, 1.35f), new Color(0f, 0f, 0f, 0.42f), new Vector3(0.08f, -0.08f, 0f), 10);
            VisualFactory.Rect("Bunker", transform, new Vector2(1.65f, 1.25f), new Color(0.32f, 0.21f, 0.17f), Vector3.zero, 11);
            VisualFactory.Rect("ArmorTop", transform, new Vector2(1.28f, 0.30f), new Color(0.64f, 0.34f, 0.20f), new Vector3(0f, 0.33f, 0f), 12);
            VisualFactory.Disc("Gun", transform, new Vector2(0.42f, 0.42f), new Color(0.95f, 0.22f, 0.08f), Vector3.zero, 13);

            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.55f, 1.15f);
            _health = gameObject.AddComponent<Health>();
            _health.Initialize(Team.Enemy, Mathf.Clamp(8 + round / 5, 9, 28));
            _health.Died += _ =>
            {
                _destroyed?.Invoke();
                VisualFactory.Explosion(transform.position, new Color(1f, 0.22f, 0.05f), 2.0f);
            };
            _nextShot = Time.time + 1.5f;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _health == null || _health.IsDead || Time.time < _nextShot) return;
            Vector2 target = _game.PlayerPosition;
            Vector2 direction = (target - (Vector2)transform.position).normalized;
            _game.SpawnProjectile((Vector2)transform.position + direction * 0.78f, direction, Team.Enemy, 1, 6.8f, new Color(1f, 0.26f, 0.08f));
            VisualFactory.MuzzleFlash((Vector2)transform.position + direction * 0.78f, new Color(1f, 0.35f, 0.08f), 1.0f);
            _nextShot = Time.time + Random.Range(1.6f, 2.5f);
        }
    }

    public sealed class FrontlineMine : MonoBehaviour
    {
        private int _damage;
        private bool _armed;

        public void Initialize(int round)
        {
            _damage = round >= 65 ? 3 : round >= 30 ? 2 : 1;
            VisualFactory.Disc("MineShadow", transform, new Vector2(0.58f, 0.58f), new Color(0f, 0f, 0f, 0.38f), new Vector3(0.04f, -0.05f, 0f), 6);
            VisualFactory.Disc("Mine", transform, new Vector2(0.48f, 0.48f), new Color(0.24f, 0.20f, 0.16f), Vector3.zero, 7);
            VisualFactory.Disc("Fuse", transform, new Vector2(0.12f, 0.12f), new Color(1f, 0.24f, 0.08f), Vector3.zero, 8);

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.38f;
            col.isTrigger = true;
            Invoke(nameof(Arm), 0.75f);
        }

        private void Arm()
        {
            _armed = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_armed) return;
            Health health = other.GetComponent<Health>();
            if (health == null || health.IsDead || health.Team == Team.Neutral) return;

            // Mines are intentionally faction-agnostic: they create tactical lanes and can be baited.
            Team source = health.Team == Team.Player ? Team.Enemy : Team.Player;
            if (!health.Damage(_damage, source)) return;

            _armed = false;
            VisualFactory.Explosion(transform.position, new Color(1f, 0.42f, 0.08f), 1.05f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.52f, 0.04f);
            Destroy(gameObject);
        }
    }
}
