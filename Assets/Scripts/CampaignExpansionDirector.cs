using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum ExpansionSectorOperation
    {
        CrossfireGrid = 0,
        SiegeLock = 1,
        EmpStorm = 2,
        AttritionFront = 3
    }

    public enum ExpansionChallengeContract
    {
        None = 0,
        EagleGuard = 1,
        BlitzClock = 2,
        ArmorQuarry = 3,
        SpecialistPurge = 4
    }

    public enum ExpansionBossContract
    {
        IronOath = 0,
        StormCage = 1,
        LastStandProtocol = 2
    }

    /// <summary>
    /// v5.3 replayability layer. It does not replace TankGame spawning, BossLegendDirector,
    /// Health, Projectile or WarEconomy authority. Instead it attaches bounded pressure packages
    /// to actors already created by the authoritative campaign and tracks optional challenge contracts.
    /// </summary>
    [DefaultExecutionOrder(320)]
    public sealed class CampaignExpansionDirector : MonoBehaviour
    {
        private const string SeedKey = "TankRevival.V53.RunSeed";
        private TankGame _game;
        private int _round;
        private int _sector;
        private int _runSeed;
        private bool _wasPlaying;
        private ExpansionSectorOperation _operation;
        private ExpansionChallengeContract _challenge;
        private ExpansionBossContract _bossContract;
        private readonly HashSet<int> _processedEnemies = new HashSet<int>();
        private readonly HashSet<int> _trackedKills = new HashSet<int>();
        private int _challengeKills;
        private int _challengeTarget;
        private float _challengeStarted;
        private bool _challengeFailed;
        private Health _trackedEagle;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _accent;

        public static CampaignExpansionDirector Instance { get; private set; }
        public static int SectorOperationCount => Enum.GetValues(typeof(ExpansionSectorOperation)).Length;
        public static int ChallengeContractCount => Enum.GetValues(typeof(ExpansionChallengeContract)).Length - 1;
        public static int BossContractCount => Enum.GetValues(typeof(ExpansionBossContract)).Length;
        public ExpansionSectorOperation ActiveOperation => _operation;
        public ExpansionChallengeContract ActiveChallenge => _challenge;
        public ExpansionBossContract ActiveBossContract => _bossContract;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CampaignExpansionDirector>() != null) return;
            GameObject go = new GameObject("CampaignExpansionDirector_v5_3");
            DontDestroyOnLoad(go);
            go.AddComponent<CampaignExpansionDirector>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            UnhookEagle();
            if (Instance == this) Instance = null;
        }

        public static bool ValidateCatalog(out string reason)
        {
            if (SectorOperationCount < 4)
            {
                reason = "sector operation catalog incomplete";
                return false;
            }
            if (ChallengeContractCount < 4)
            {
                reason = "challenge contract catalog incomplete";
                return false;
            }
            if (BossContractCount < 3)
            {
                reason = "boss contract catalog incomplete";
                return false;
            }
            reason = $"operations={SectorOperationCount} challenges={ChallengeContractCount} bossContracts={BossContractCount}";
            return true;
        }

        private void Update()
        {
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying)
            {
                if (_wasPlaying) EndRun();
                _wasPlaying = false;
                return;
            }

            if (!_wasPlaying)
            {
                _wasPlaying = true;
                BeginRun();
            }

            int currentRound = _game.CurrentRound;
            if (currentRound != _round)
            {
                if (_round > 0) ResolveChallenge(true);
                BeginRound(currentRound);
            }

            ApplyOperationToNewEnemies();
            UpdateChallengeLiveState();
        }

        private void BeginRun()
        {
            _runSeed = UnityEngine.Random.Range(10000, 999999);
            PlayerPrefs.SetInt(SeedKey, _runSeed);
            PlayerPrefs.Save();
            _round = 0;
            _sector = 0;
            _processedEnemies.Clear();
            _trackedKills.Clear();
            _challenge = ExpansionChallengeContract.None;
            _challengeFailed = false;
        }

        private void EndRun()
        {
            ResolveChallenge(false);
            UnhookEagle();
            _round = 0;
            _sector = 0;
            _processedEnemies.Clear();
            _trackedKills.Clear();
        }

        private void BeginRound(int round)
        {
            UnhookEagle();
            _round = round;
            _processedEnemies.Clear();
            _trackedKills.Clear();
            _challengeKills = 0;
            _challengeTarget = 0;
            _challengeFailed = false;
            _challengeStarted = Time.time;

            int sector = Mathf.Clamp((round - 1) / 10 + 1, 1, 10);
            if (sector != _sector)
            {
                _sector = sector;
                _operation = (ExpansionSectorOperation)PositiveMod(_runSeed + sector * 37, SectorOperationCount);
                Announce($"SECTOR OPERATION // {OperationName(_operation)}");
            }

            _challenge = ChooseChallenge(round);
            if (_challenge != ExpansionChallengeContract.None)
            {
                ConfigureChallenge(_challenge);
                Announce($"OPTIONAL CONTRACT // {ChallengeName(_challenge)} // +{ChallengeReward()} BONDS");
            }

            if (round % 10 == 0)
            {
                _bossContract = (ExpansionBossContract)PositiveMod(_runSeed + round * 19, BossContractCount);
                Announce($"BOSS CONTRACT // {BossContractName(_bossContract)}");
            }
        }

        private ExpansionChallengeContract ChooseChallenge(int round)
        {
            if (round % 10 == 0 || round < 5 || round % 5 != 0) return ExpansionChallengeContract.None;
            if (round < 25)
                return PositiveMod(_runSeed + round * 11, 2) == 0 ? ExpansionChallengeContract.EagleGuard : ExpansionChallengeContract.BlitzClock;

            int pick = PositiveMod(_runSeed + round * 11, ChallengeContractCount);
            return (ExpansionChallengeContract)(pick + 1);
        }

        private void ConfigureChallenge(ExpansionChallengeContract contract)
        {
            if (contract == ExpansionChallengeContract.EagleGuard)
            {
                _trackedEagle = RuntimeBattleRegistry.Eagle;
                if (_trackedEagle != null) _trackedEagle.Damaged += OnTrackedEagleDamaged;
            }
            else if (contract == ExpansionChallengeContract.ArmorQuarry)
            {
                _challengeTarget = Mathf.Clamp(2 + _sector / 3, 3, 5);
            }
            else if (contract == ExpansionChallengeContract.SpecialistPurge)
            {
                _challengeTarget = Mathf.Clamp(2 + _sector / 4, 2, 4);
            }
        }

        private void ApplyOperationToNewEnemies()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                int id = enemy.GetInstanceID();
                if (!_processedEnemies.Add(id)) continue;

                TrackChallengeKill(enemy);
                if (enemy.Kind == EnemyKind.Boss)
                {
                    InstallBossContract(enemy);
                    continue;
                }

                if (!EligibleForOperation(enemy.Kind, _operation)) continue;
                int hpBonus = Mathf.Clamp((_sector - 1) / 2, 0, 4);
                if (_operation == ExpansionSectorOperation.SiegeLock || _operation == ExpansionSectorOperation.AttritionFront)
                    hpBonus++;
                if (hpBonus > 0) enemy.Health.SetMaximum(enemy.Health.Maximum + hpBonus, true);

                ExpansionPressureAgent pressure = enemy.GetComponent<ExpansionPressureAgent>();
                if (pressure == null) pressure = enemy.gameObject.AddComponent<ExpansionPressureAgent>();
                pressure.Initialize(_game, enemy, _operation, _sector);
            }
        }

        private void TrackChallengeKill(EnemyTank enemy)
        {
            if (_challenge == ExpansionChallengeContract.None || enemy == null || enemy.Health == null) return;
            int id = enemy.GetInstanceID();
            if (!_trackedKills.Add(id)) return;
            EnemyKind kind = enemy.Kind;
            enemy.Health.Died += _ => OnChallengeEnemyDied(kind);
        }

        private void OnChallengeEnemyDied(EnemyKind kind)
        {
            if (_challenge == ExpansionChallengeContract.ArmorQuarry &&
                (kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Elite || kind == EnemyKind.Boss))
                _challengeKills++;
            else if (_challenge == ExpansionChallengeContract.SpecialistPurge &&
                     (kind == EnemyKind.Sniper || kind == EnemyKind.Elite || kind == EnemyKind.Siege))
                _challengeKills++;
        }

        private void UpdateChallengeLiveState()
        {
            if (_challenge == ExpansionChallengeContract.None) return;
            if (_challenge == ExpansionChallengeContract.BlitzClock)
            {
                float limit = BlitzLimit();
                if (Time.time - _challengeStarted > limit) _challengeFailed = true;
            }
        }

        private void ResolveChallenge(bool roundCompleted)
        {
            if (_challenge == ExpansionChallengeContract.None) return;

            bool success = roundCompleted && !_challengeFailed;
            if (_challenge == ExpansionChallengeContract.ArmorQuarry || _challenge == ExpansionChallengeContract.SpecialistPurge)
                success &= _challengeKills >= _challengeTarget;
            else if (_challenge == ExpansionChallengeContract.BlitzClock)
                success &= Time.time - _challengeStarted <= BlitzLimit();

            if (success)
            {
                int reward = ChallengeReward();
                WarEconomyDirector.AwardMissionBonds(reward, "V5.3 " + ChallengeName(_challenge));
                Announce($"CONTRACT COMPLETE // {ChallengeName(_challenge)} // +{reward} BONDS");
            }
            else if (roundCompleted)
            {
                Announce($"CONTRACT MISSED // {ChallengeName(_challenge)}");
            }

            UnhookEagle();
            _challenge = ExpansionChallengeContract.None;
        }

        private void InstallBossContract(EnemyTank boss)
        {
            ExpansionBossContractAgent agent = boss.GetComponent<ExpansionBossContractAgent>();
            if (agent == null) agent = boss.gameObject.AddComponent<ExpansionBossContractAgent>();
            agent.Initialize(_game, boss, _bossContract, _sector);
        }

        private void OnTrackedEagleDamaged(Health health, int amount)
        {
            if (_challenge == ExpansionChallengeContract.EagleGuard && amount > 0)
                _challengeFailed = true;
        }

        private void UnhookEagle()
        {
            if (_trackedEagle != null) _trackedEagle.Damaged -= OnTrackedEagleDamaged;
            _trackedEagle = null;
        }

        private int ChallengeReward()
        {
            return 7 + _sector * 2 + (_challenge == ExpansionChallengeContract.ArmorQuarry || _challenge == ExpansionChallengeContract.SpecialistPurge ? 3 : 0);
        }

        private float BlitzLimit()
        {
            return Mathf.Max(45f, 72f - _sector * 2.2f);
        }

        private static bool EligibleForOperation(EnemyKind kind, ExpansionSectorOperation operation)
        {
            return operation switch
            {
                ExpansionSectorOperation.CrossfireGrid => kind == EnemyKind.Sniper || kind == EnemyKind.Elite || kind == EnemyKind.Fast,
                ExpansionSectorOperation.SiegeLock => kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Elite,
                ExpansionSectorOperation.EmpStorm => kind == EnemyKind.Fast || kind == EnemyKind.Sniper || kind == EnemyKind.Elite,
                ExpansionSectorOperation.AttritionFront => kind == EnemyKind.Basic || kind == EnemyKind.Heavy || kind == EnemyKind.Siege,
                _ => false
            };
        }

        private static int PositiveMod(int value, int divisor)
        {
            if (divisor <= 0) return 0;
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        public static string OperationName(ExpansionSectorOperation operation)
        {
            return operation switch
            {
                ExpansionSectorOperation.CrossfireGrid => "CROSSFIRE GRID",
                ExpansionSectorOperation.SiegeLock => "SIEGE LOCK",
                ExpansionSectorOperation.EmpStorm => "EMP STORM",
                ExpansionSectorOperation.AttritionFront => "ATTRITION FRONT",
                _ => "UNKNOWN"
            };
        }

        public static string ChallengeName(ExpansionChallengeContract contract)
        {
            return contract switch
            {
                ExpansionChallengeContract.EagleGuard => "EAGLE GUARD",
                ExpansionChallengeContract.BlitzClock => "BLITZ CLOCK",
                ExpansionChallengeContract.ArmorQuarry => "ARMOR QUARRY",
                ExpansionChallengeContract.SpecialistPurge => "SPECIALIST PURGE",
                _ => "NONE"
            };
        }

        public static string BossContractName(ExpansionBossContract contract)
        {
            return contract switch
            {
                ExpansionBossContract.IronOath => "IRON OATH",
                ExpansionBossContract.StormCage => "STORM CAGE",
                ExpansionBossContract.LastStandProtocol => "LAST STAND PROTOCOL",
                _ => "UNKNOWN"
            };
        }

        private string OperationRule()
        {
            return _operation switch
            {
                ExpansionSectorOperation.CrossfireGrid => "Mobile marksmen add synchronized armor-piercing crossfire.",
                ExpansionSectorOperation.SiegeLock => "Heavy assault groups harden and pressure Orzelek with breach shells.",
                ExpansionSectorOperation.EmpStorm => "Fast specialists layer EMP disruption over the normal assault.",
                ExpansionSectorOperation.AttritionFront => "Line armor hardens and adds incendiary pressure over long fights.",
                _ => string.Empty
            };
        }

        private string ChallengeProgress()
        {
            if (_challenge == ExpansionChallengeContract.None) return "No optional contract this round.";
            if (_challenge == ExpansionChallengeContract.EagleGuard) return _challengeFailed ? "Eagle took damage — contract failed." : "Keep Orzelek completely undamaged.";
            if (_challenge == ExpansionChallengeContract.BlitzClock)
            {
                float left = Mathf.Max(0f, BlitzLimit() - (Time.time - _challengeStarted));
                return _challengeFailed ? "Time expired — contract failed." : $"Clear before timer expires: {left:0}s";
            }
            return $"Priority kills: {_challengeKills}/{_challengeTarget}";
        }

        private void Announce(string text)
        {
            _banner = text;
            _bannerUntil = Time.unscaledTime + 3.4f;
            CampaignEncounterDirector.Instance?.Broadcast(text);
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.22f, 0.02f);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.96f, 0.66f, 0.20f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.84f, 0.90f, 0.96f) } };
            _accent = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.72f, 0.24f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            EnsureStyles();

            float y = 562f;
            GUI.color = new Color(0.035f, 0.027f, 0.014f, 0.91f);
            GUI.Box(new Rect(14f, y, 455f, 78f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, y + 6f, 425f, 18f), $"OVERDRIVE OPS // {OperationName(_operation)}", _header);
            GUI.Label(new Rect(28f, y + 27f, 425f, 20f), OperationRule(), _body);
            GUI.Label(new Rect(28f, y + 49f, 425f, 20f), ChallengeProgress(), _body);

            if (Time.unscaledTime < _bannerUntil)
            {
                GUI.color = new Color(0.035f, 0.027f, 0.014f, 0.96f);
                GUI.Box(new Rect(Screen.width * 0.5f - 390f, Screen.height * 0.27f, 780f, 48f), string.Empty);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * 0.5f - 380f, Screen.height * 0.27f + 6f, 760f, 36f), _banner, _accent);
            }
        }
    }

    public sealed class ExpansionPressureAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _enemy;
        private Health _health;
        private ExpansionSectorOperation _operation;
        private int _sector;
        private float _nextShot;

        public void Initialize(TankGame game, EnemyTank enemy, ExpansionSectorOperation operation, int sector)
        {
            _game = game;
            _enemy = enemy;
            _health = enemy != null ? enemy.Health : null;
            _operation = operation;
            _sector = Mathf.Clamp(sector, 1, 10);
            _nextShot = Time.time + UnityEngine.Random.Range(3.0f, 5.0f);
        }

        private void Update()
        {
            if (_game == null || !_game.IsPlaying || _enemy == null || _health == null || _health.IsDead) return;
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + Mathf.Max(2.2f, 5.2f - _sector * 0.18f + UnityEngine.Random.Range(-0.3f, 0.5f));

            Vector2 origin = transform.position;
            Vector2 target = _operation == ExpansionSectorOperation.SiegeLock ? _game.BasePosition : _game.PlayerPosition;
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.02f) direction = Vector2.down;
            direction.Normalize();

            AmmoType ammo;
            int damage = 1;
            Color color;
            switch (_operation)
            {
                case ExpansionSectorOperation.CrossfireGrid:
                    ammo = AmmoType.ArmorPiercing;
                    color = new Color(1f, 0.74f, 0.22f);
                    break;
                case ExpansionSectorOperation.SiegeLock:
                    ammo = _sector >= 6 ? AmmoType.Explosive : AmmoType.ArmorPiercing;
                    damage = _sector >= 8 ? 2 : 1;
                    color = new Color(1f, 0.24f, 0.10f);
                    break;
                case ExpansionSectorOperation.EmpStorm:
                    ammo = AmmoType.EMP;
                    color = new Color(0.25f, 0.72f, 1f);
                    break;
                default:
                    ammo = AmmoType.Incendiary;
                    color = new Color(1f, 0.42f, 0.10f);
                    break;
            }

            float speed = 8.5f + _sector * 0.28f;
            _game.SpawnProjectile(origin + direction * 0.76f, direction, Team.Enemy, damage, speed, color, ammo);
            if (_operation == ExpansionSectorOperation.CrossfireGrid && _sector >= 5)
            {
                Vector2 side = new Vector2(-direction.y, direction.x) * 0.10f;
                _game.SpawnProjectile(origin + direction * 0.72f, (direction + side).normalized, Team.Enemy, 1, speed * 0.96f, color, ammo);
            }
            VisualFactory.MuzzleFlash(origin + direction * 0.68f, color, 0.72f);
        }
    }

    public sealed class ExpansionBossContractAgent : MonoBehaviour
    {
        private TankGame _game;
        private EnemyTank _boss;
        private Health _health;
        private ExpansionBossContract _contract;
        private int _sector;
        private float _nextAttack;
        private float _nextRecovery;
        private bool _configured;

        public ExpansionBossContract Contract => _contract;
        public bool IsConfigured => _configured;

        public void Initialize(TankGame game, EnemyTank boss, ExpansionBossContract contract, int sector)
        {
            _game = game;
            _boss = boss;
            _health = boss != null ? boss.Health : null;
            _contract = contract;
            _sector = Mathf.Clamp(sector, 1, 10);
            if (_health == null) return;

            float multiplier = contract == ExpansionBossContract.IronOath ? 1.30f : contract == ExpansionBossContract.StormCage ? 1.20f : 1.42f;
            int boosted = Mathf.CeilToInt(_health.Maximum * multiplier);
            _health.SetMaximum(boosted, true);
            _nextAttack = Time.time + 3.2f;
            _nextRecovery = Time.time + 7.5f;
            _configured = true;

            Color pulse = contract == ExpansionBossContract.StormCage ? new Color(0.24f, 0.72f, 1f) : new Color(1f, 0.28f, 0.12f);
            VisualFactory.RingPulse(transform.position, pulse, 1.65f);
        }

        private void Update()
        {
            if (!_configured || _game == null || !_game.IsPlaying || _boss == null || _health == null || _health.IsDead) return;

            if (_contract == ExpansionBossContract.LastStandProtocol && _health.Current <= _health.Maximum / 2 && Time.time >= _nextRecovery)
            {
                _nextRecovery = Time.time + 8.5f;
                _health.Heal(Mathf.Max(1, _sector / 4));
                VisualFactory.RingPulse(transform.position, new Color(1f, 0.18f, 0.08f), 1.25f);
            }

            if (Time.time < _nextAttack) return;
            _nextAttack = Time.time + Mathf.Max(2.0f, 4.8f - _sector * 0.18f);

            Vector2 origin = transform.position;
            Vector2 direction = _game.PlayerPosition - origin;
            if (direction.sqrMagnitude < 0.02f) direction = Vector2.down;
            direction.Normalize();

            if (_contract == ExpansionBossContract.IronOath)
            {
                Fire(origin, direction, AmmoType.ArmorPiercing, 2, new Color(1f, 0.70f, 0.18f), 10.6f);
                Vector2 side = new Vector2(-direction.y, direction.x) * 0.14f;
                Fire(origin, (direction + side).normalized, AmmoType.ArmorPiercing, 1, new Color(1f, 0.55f, 0.12f), 10.2f);
                Fire(origin, (direction - side).normalized, AmmoType.ArmorPiercing, 1, new Color(1f, 0.55f, 0.12f), 10.2f);
            }
            else if (_contract == ExpansionBossContract.StormCage)
            {
                for (int i = -2; i <= 2; i++)
                    Fire(origin, Rotate(direction, i * 10f), AmmoType.EMP, 1, new Color(0.25f, 0.74f, 1f), 9.4f);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f + Time.time * 8f;
                    Vector2 radial = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    Fire(origin, radial, AmmoType.Incendiary, 1, new Color(1f, 0.28f, 0.10f), 8.8f);
                }
            }

            BattleAudio.PlayGlobal(SoundCue.HeavyShot, 0.31f, 0.04f);
            VisualFactory.RingPulse(transform.position, new Color(1f, 0.38f, 0.12f), 0.82f);
        }

        private void Fire(Vector2 origin, Vector2 direction, AmmoType ammo, int damage, Color color, float speed)
        {
            _game.SpawnProjectile(origin + direction * 0.82f, direction, Team.Enemy, damage, speed, color, ammo);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized;
        }
    }
}
