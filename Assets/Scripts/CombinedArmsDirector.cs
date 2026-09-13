using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum ReinforcementDoctrine
    {
        EscortScreen,
        HunterKiller,
        SiegeRelief
    }

    public sealed class CombinedArmsDirector : MonoBehaviour
    {
        public const int EarliestRound = 26;
        public const int OperationInterval = 8;
        public const int CommandPostHealthMin = 6;
        public const int CommandPostHealthMax = 12;
        public const int ReinforcementWavesMin = 2;
        public const int ReinforcementWavesMax = 3;
        public const int ReinforcementWaveSizeMin = 2;
        public const int ReinforcementWaveSizeMax = 4;
        public const float ReinforcementCadenceMin = 7.0f;
        public const float ReinforcementCadenceMax = 10.0f;
        public const int MaxLiveEnemyPressure = 16;
        public const int MaxSupportCharges = 2;
        public const int RewardMin = 10;
        public const int RewardMax = 22;
        public const float SupportTelegraphSeconds = 1.15f;
        public const int ArtilleryShells = 5;
        public const int AirSupportPasses = 3;

        private static readonly MethodInfo SpawnEnemyMethod = typeof(TankGame).GetMethod("SpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);

        private TankGame _game;
        private int _round = -1;
        private ReinforcementDoctrine _doctrine;
        private GameObject _commandPost;
        private Health _commandPostHealth;
        private float _nextWave;
        private int _wavesDeployed;
        private int _supportCharges;
        private bool _resolved;
        private string _status = string.Empty;
        private float _supportFireAt = -1f;
        private bool _airSupportPending;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static bool ConfigurationValid =>
            EarliestRound >= 20 && EarliestRound <= 35 &&
            OperationInterval >= 6 && OperationInterval <= 10 &&
            CommandPostHealthMin >= 4 && CommandPostHealthMax <= 16 && CommandPostHealthMin < CommandPostHealthMax &&
            ReinforcementWavesMin >= 1 && ReinforcementWavesMax <= 4 && ReinforcementWavesMin <= ReinforcementWavesMax &&
            ReinforcementWaveSizeMin >= 1 && ReinforcementWaveSizeMax <= 4 && ReinforcementWaveSizeMin <= ReinforcementWaveSizeMax &&
            ReinforcementCadenceMin >= 5f && ReinforcementCadenceMax <= 12f && ReinforcementCadenceMin < ReinforcementCadenceMax &&
            MaxLiveEnemyPressure >= 12 && MaxLiveEnemyPressure <= 18 &&
            MaxSupportCharges >= 1 && MaxSupportCharges <= 3 &&
            RewardMin >= 8 && RewardMax <= 26 && RewardMin < RewardMax &&
            SupportTelegraphSeconds >= 0.8f && SupportTelegraphSeconds <= 2.0f &&
            ArtilleryShells >= 3 && ArtilleryShells <= 6 &&
            AirSupportPasses >= 2 && AirSupportPasses <= 4 &&
            SpawnEnemyMethod != null;

        public static bool HasOperationForRound(int round)
        {
            if (round < EarliestRound || round % 10 == 0) return false;
            if ((round - EarliestRound) % OperationInterval != 0) return false;
            return !MultiStageOperationDirector.HasOperationForRound(round);
        }

        public static ReinforcementDoctrine DoctrineForRound(int round)
        {
            int slot = Mathf.Abs((round - EarliestRound) / OperationInterval) % 3;
            return (ReinforcementDoctrine)slot;
        }

        public static int CommandPostHealthForRound(int round)
        {
            return Mathf.Clamp(CommandPostHealthMin + Mathf.Clamp(round, 1, 100) / 18, CommandPostHealthMin, CommandPostHealthMax);
        }

        public static int RewardForRound(int round)
        {
            return Mathf.Clamp(RewardMin + Mathf.Clamp(round, 1, 100) / 9, RewardMin, RewardMax);
        }

        public static int WaveCountForRound(int round)
        {
            return Mathf.Clamp(ReinforcementWavesMin + (round >= 70 ? 1 : 0), ReinforcementWavesMin, ReinforcementWavesMax);
        }

        public static int WaveSizeForRound(int round)
        {
            return Mathf.Clamp(ReinforcementWaveSizeMin + (round >= 55 ? 1 : 0) + (round >= 88 ? 1 : 0), ReinforcementWaveSizeMin, ReinforcementWaveSizeMax);
        }

        public static EnemyKind[] CompositionForRound(int round, ReinforcementDoctrine doctrine)
        {
            switch (doctrine)
            {
                case ReinforcementDoctrine.EscortScreen:
                    return round >= 60
                        ? new[] { EnemyKind.Heavy, EnemyKind.Fast, EnemyKind.Elite, EnemyKind.Fast }
                        : new[] { EnemyKind.Heavy, EnemyKind.Fast, EnemyKind.Fast };
                case ReinforcementDoctrine.HunterKiller:
                    return round >= 65
                        ? new[] { EnemyKind.Sniper, EnemyKind.Elite, EnemyKind.Fast, EnemyKind.Sniper }
                        : new[] { EnemyKind.Sniper, EnemyKind.Fast, EnemyKind.Sniper };
                default:
                    return round >= 55
                        ? new[] { EnemyKind.Siege, EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Fast }
                        : new[] { EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Fast };
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombinedArmsDirector>() != null) return;
            var go = new GameObject("CombinedArmsDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CombinedArmsDirector>();
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetOperation();
                return;
            }

            if (_game.CurrentRound != _round)
            {
                ResetOperation();
                _round = _game.CurrentRound;
                if (HasOperationForRound(_round)) BeginOperation();
            }

            if (!HasOperationForRound(_round) || _resolved) return;

            if (_commandPostHealth == null || _commandPostHealth.IsDead)
            {
                if (!_resolved) ResolveCommandPostDestroyed();
                return;
            }

            if (_wavesDeployed < WaveCountForRound(_round) && Time.time >= _nextWave)
                DeployReinforcementWave();

            if (_supportCharges > 0 && _supportFireAt < 0f)
            {
                if (Input.GetKeyDown(KeyCode.F9)) QueueSupport(false);
                else if (Input.GetKeyDown(KeyCode.F10)) QueueSupport(true);
            }

            if (_supportFireAt > 0f && Time.time >= _supportFireAt)
                ExecuteSupportStrike();
        }

        private void BeginOperation()
        {
            _doctrine = DoctrineForRound(_round);
            _wavesDeployed = 0;
            _supportCharges = 0;
            _resolved = false;
            _supportFireAt = -1f;
            _nextWave = Time.time + 3.8f;
            SpawnCommandPost();
            _status = "REINFORCEMENT NODE ACTIVE // DESTROY IT BEFORE RELIEF WAVES ARRIVE";
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.30f, -0.08f);
        }

        private void SpawnCommandPost()
        {
            var rng = new System.Random(_round * 7919 + (int)_doctrine * 503 + 211);
            float x = (float)(rng.NextDouble() * 13.0 - 6.5);
            float y = (float)(rng.NextDouble() * 2.0 + 2.4);

            _commandPost = new GameObject("REINFORCEMENT_COMMAND_POST");
            _commandPost.transform.position = new Vector3(x, y, 0f);
            Color core = DoctrineColor(_doctrine);
            VisualFactory.Rect("PostBase", _commandPost.transform, new Vector2(1.18f, 0.88f), new Color(0.16f, 0.18f, 0.22f), Vector3.zero, 8);
            VisualFactory.Rect("PostCore", _commandPost.transform, new Vector2(0.68f, 0.56f), core, new Vector3(0f, 0.06f, 0f), 9);
            VisualFactory.Rect("Antenna", _commandPost.transform, new Vector2(0.08f, 0.88f), Color.white, new Vector3(0.30f, 0.62f, 0f), 10);
            VisualFactory.Disc("Beacon", _commandPost.transform, new Vector2(0.22f, 0.22f), core, new Vector3(0.30f, 1.06f, 0f), 11);

            var collider = _commandPost.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.12f, 0.82f);
            _commandPostHealth = _commandPost.AddComponent<Health>();
            _commandPostHealth.Initialize(Team.Enemy, CommandPostHealthForRound(_round));
            _commandPostHealth.Damaged += OnCommandPostDamaged;
            _commandPostHealth.Died += OnCommandPostDied;
            VisualFactory.RingPulse(_commandPost.transform.position, core, 1.55f);
        }

        private void OnCommandPostDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            _status = "COMMAND POST UNDER FIRE // " + health.Current + "/" + health.Maximum + " HP";
            VisualFactory.RingPulse(health.transform.position, DoctrineColor(_doctrine), 0.62f);
        }

        private void OnCommandPostDied(Health health)
        {
            ResolveCommandPostDestroyed();
        }

        private void ResolveCommandPostDestroyed()
        {
            if (_resolved) return;
            _resolved = true;
            _supportCharges = Mathf.Min(MaxSupportCharges, _supportCharges + 1);
            int reward = RewardForRound(_round);
            WarEconomyDirector.AwardMissionBonds(reward, "REINFORCEMENT NODE DESTROYED");
            _status = "NODE DESTROYED // SUPPORT CHARGE ACQUIRED";
            if (_commandPost != null)
            {
                VisualFactory.Explosion(_commandPost.transform.position, DoctrineColor(_doctrine), 1.45f);
                _commandPost = null;
            }
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.46f, 0f);
        }

        private void DeployReinforcementWave()
        {
            _nextWave = Time.time + Mathf.Lerp(ReinforcementCadenceMax, ReinforcementCadenceMin, Mathf.Clamp01((_round - EarliestRound) / 74f));
            if (RuntimeBattleRegistry.EnemySnapshot.Length >= MaxLiveEnemyPressure)
            {
                _status = "REINFORCEMENT HOLD // BATTLEFIELD SATURATED";
                return;
            }

            EnemyKind[] composition = CompositionForRound(_round, _doctrine);
            int requested = WaveSizeForRound(_round);
            int spawned = 0;
            for (int i = 0; i < requested; i++)
            {
                if (RuntimeBattleRegistry.EnemySnapshot.Length + spawned >= MaxLiveEnemyPressure) break;
                EnemyKind kind = composition[i % composition.Length];
                if (SpawnEnemyMethod == null) break;
                try
                {
                    SpawnEnemyMethod.Invoke(_game, new object[] { kind });
                    spawned++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TankRevival] Reinforcement spawn bridge failed: " + ex.GetType().Name);
                    break;
                }
            }

            if (spawned > 0)
            {
                _wavesDeployed++;
                _status = DoctrineLabel(_doctrine) + " // WAVE " + _wavesDeployed + "/" + WaveCountForRound(_round) + " DEPLOYED";
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.18f, -0.10f);
            }
        }

        private void QueueSupport(bool airSupport)
        {
            if (_supportCharges <= 0 || _supportFireAt > 0f) return;
            _supportCharges--;
            _airSupportPending = airSupport;
            _supportFireAt = Time.time + SupportTelegraphSeconds;
            _status = airSupport ? "CAS RUN INBOUND // MARKED HOSTILES" : "ARTILLERY FIRE MISSION // IMPACT INBOUND";

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length && i < 4; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                VisualFactory.RingPulse(enemy.transform.position, airSupport ? new Color(0.20f, 0.82f, 1f) : new Color(1f, 0.52f, 0.10f), 0.95f);
            }
        }

        private void ExecuteSupportStrike()
        {
            _supportFireAt = -1f;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies.Length == 0)
            {
                _status = "SUPPORT ABORTED // NO HOSTILES";
                return;
            }

            int shots = _airSupportPending ? AirSupportPasses : ArtilleryShells;
            int fired = 0;
            for (int i = 0; i < enemies.Length && fired < shots; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                Vector2 target = enemy.transform.position;
                if (_airSupportPending)
                {
                    Vector2 origin = target + new Vector2(-4.2f, 2.2f + fired * 0.22f);
                    Vector2 dir = (target - origin).normalized;
                    _game.SpawnProjectile(origin, dir, Team.Player, 2, 13.5f, new Color(0.20f, 0.82f, 1f), AmmoType.Plasma);
                }
                else
                {
                    Vector2 origin = target + new Vector2((fired % 2 == 0 ? -0.35f : 0.35f), 4.6f);
                    _game.SpawnProjectile(origin, Vector2.down, Team.Player, 2, 10.8f, new Color(1f, 0.52f, 0.10f), AmmoType.Explosive);
                }
                fired++;
            }

            _status = (_airSupportPending ? "CAS COMPLETE" : "ARTILLERY COMPLETE") + " // " + fired + " ATTACK RUNS";
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.44f, _airSupportPending ? 0.08f : -0.05f);
        }

        private void ResetOperation()
        {
            if (_commandPostHealth != null)
            {
                _commandPostHealth.Damaged -= OnCommandPostDamaged;
                _commandPostHealth.Died -= OnCommandPostDied;
            }
            if (_commandPost != null) Destroy(_commandPost);
            _commandPost = null;
            _commandPostHealth = null;
            _supportCharges = 0;
            _supportFireAt = -1f;
            _resolved = false;
            _wavesDeployed = 0;
            _status = string.Empty;
            _round = -1;
        }

        private static string DoctrineLabel(ReinforcementDoctrine doctrine)
        {
            switch (doctrine)
            {
                case ReinforcementDoctrine.EscortScreen: return "ESCORT SCREEN";
                case ReinforcementDoctrine.HunterKiller: return "HUNTER-KILLER TEAM";
                default: return "SIEGE RELIEF GROUP";
            }
        }

        private static Color DoctrineColor(ReinforcementDoctrine doctrine)
        {
            switch (doctrine)
            {
                case ReinforcementDoctrine.EscortScreen: return new Color(1f, 0.62f, 0.12f);
                case ReinforcementDoctrine.HunterKiller: return new Color(0.86f, 0.18f, 0.62f);
                default: return new Color(1f, 0.22f, 0.10f);
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.58f, 0.16f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _warning = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.26f, 0.12f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !HasOperationForRound(_game.CurrentRound)) return;
            EnsureStyles();

            float hpRatio = _commandPostHealth != null && _commandPostHealth.Maximum > 0
                ? (float)_commandPostHealth.Current / _commandPostHealth.Maximum
                : 0f;
            GUI.color = new Color(0.045f, 0.025f, 0.020f, 0.94f);
            GUI.Box(new Rect(Screen.width - 440f, 146f, 426f, 116f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 426f, 153f, 398f, 22f), "COMBINED ARMS // " + DoctrineLabel(_doctrine), _header);
            GUI.Label(new Rect(Screen.width - 426f, 178f, 398f, 20f), "NODE HP " + Mathf.RoundToInt(hpRatio * 100f) + "%   WAVES " + _wavesDeployed + "/" + WaveCountForRound(_round), _body);
            GUI.Label(new Rect(Screen.width - 426f, 201f, 398f, 20f), "SUPPORT " + _supportCharges + "/" + MaxSupportCharges + "   F9 ARTILLERY   F10 CAS", _body);
            GUI.Label(new Rect(Screen.width - 426f, 224f, 398f, 29f), _status, _resolved ? _body : _warning);
        }
    }
}
