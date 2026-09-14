using System;
using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum CampaignCommandPhase
    {
        None,
        Reconnaissance,
        Interdiction,
        DecisiveAction,
        Resolved
    }

    [DefaultExecutionOrder(570)]
    public sealed class CombinedArmsCampaignCommandDirector : MonoBehaviour
    {
        public const int EarliestRound = 40;
        public const int FirstOperationRound = 41;
        public const int OperationInterval = 7;
        public const int MomentumMin = -2;
        public const int MomentumMax = 3;
        public const int RelayHealthMin = 8;
        public const int RelayHealthMax = 15;
        public const int BaseResponseWaves = 2;
        public const int MaxResponseWaves = 3;
        public const int BaseWaveSize = 2;
        public const int MaxWaveSize = 4;
        public const int MaxLiveEnemyPressure = 17;
        public const float ReconSeconds = 4.5f;
        public const float InterdictionSeconds = 24f;
        public const float DecisiveHoldSeconds = 6f;
        public const float ResponseCadenceSeconds = 7.5f;
        public const int RewardMin = 14;
        public const int RewardMax = 28;
        public const int MaxFriendlySupportShells = 4;

        private static readonly MethodInfo SpawnEnemyMethod = typeof(TankGame).GetMethod("SpawnEnemy", BindingFlags.Instance | BindingFlags.NonPublic);
        private static CombinedArmsCampaignCommandDirector _instance;

        private TankGame _game;
        private int _round = -1;
        private int _momentum;
        private CampaignCommandPhase _phase;
        private float _phaseEndsAt;
        private GameObject _relay;
        private Health _relayHealth;
        private int _wavesDeployed;
        private float _nextWaveAt;
        private bool _supportCommitted;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;
        private GUIStyle _warning;

        public static CombinedArmsCampaignCommandDirector Instance => _instance;
        public int CommandMomentum => _momentum;
        public CampaignCommandPhase CurrentPhase => _phase;
        public static bool ConfigurationValid =>
            EarliestRound >= 35 && EarliestRound <= 50 &&
            FirstOperationRound > EarliestRound && OperationInterval >= 6 && OperationInterval <= 9 &&
            MomentumMin <= -2 && MomentumMax >= 2 && MomentumMax <= 4 &&
            RelayHealthMin >= 6 && RelayHealthMax <= 18 && RelayHealthMin < RelayHealthMax &&
            BaseResponseWaves >= 1 && MaxResponseWaves <= 3 && BaseResponseWaves <= MaxResponseWaves &&
            BaseWaveSize >= 2 && MaxWaveSize <= 4 && BaseWaveSize <= MaxWaveSize &&
            MaxLiveEnemyPressure >= 14 && MaxLiveEnemyPressure <= 18 &&
            ReconSeconds >= 3f && ReconSeconds <= 6f &&
            InterdictionSeconds >= 18f && InterdictionSeconds <= 30f &&
            DecisiveHoldSeconds >= 4f && DecisiveHoldSeconds <= 8f &&
            ResponseCadenceSeconds >= 6f && ResponseCadenceSeconds <= 10f &&
            RewardMin >= 12 && RewardMax <= 30 && RewardMin < RewardMax &&
            MaxFriendlySupportShells >= 2 && MaxFriendlySupportShells <= 4 &&
            SpawnEnemyMethod != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombinedArmsCampaignCommandDirector>() != null) return;
            GameObject go = new GameObject("CombinedArmsCampaignCommandDirector_v10_0");
            DontDestroyOnLoad(go);
            go.AddComponent<CombinedArmsCampaignCommandDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ClearRelay();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                if (_round >= 0) ResetRun();
                return;
            }

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round)
            {
                ResetOperationOnly();
                _round = current;
                if (HasCommandOperationForRound(current)) BeginOperation();
            }

            if (!HasCommandOperationForRound(_round) || _phase == CampaignCommandPhase.Resolved || _phase == CampaignCommandPhase.None) return;

            if (_relayHealth == null || _relayHealth.IsDead)
            {
                if (_phase != CampaignCommandPhase.DecisiveAction) BeginDecisiveAction();
            }

            switch (_phase)
            {
                case CampaignCommandPhase.Reconnaissance:
                    if (Time.time >= _phaseEndsAt) BeginInterdiction();
                    break;
                case CampaignCommandPhase.Interdiction:
                    UpdateInterdiction();
                    break;
                case CampaignCommandPhase.DecisiveAction:
                    if (Time.time >= _phaseEndsAt) ResolveSuccess();
                    break;
            }
        }

        public static bool HasCommandOperationForRound(int round)
        {
            if (round < FirstOperationRound || round > 100 || round % 10 == 0) return false;
            if ((round - FirstOperationRound) % OperationInterval != 0) return false;
            if (MultiStageOperationDirector.HasOperationForRound(round)) return false;
            if (CombinedArmsDirector.HasOperationForRound(round)) return false;
            return true;
        }

        public static int RelayHealthForRound(int round, int momentum)
        {
            int normalized = Mathf.Clamp(momentum, MomentumMin, MomentumMax);
            int lateWar = Mathf.Clamp(round, 1, 100) / 18;
            int initiativePressure = Mathf.Max(0, -normalized);
            return Mathf.Clamp(RelayHealthMin + lateWar + initiativePressure, RelayHealthMin, RelayHealthMax);
        }

        public static int ResponseWaveCount(int momentum)
        {
            int pressure = momentum < 0 ? 1 : 0;
            return Mathf.Clamp(BaseResponseWaves + pressure, BaseResponseWaves, MaxResponseWaves);
        }

        public static int ResponseWaveSize(int round, int momentum)
        {
            int size = BaseWaveSize;
            if (round >= 70) size++;
            if (momentum <= -2) size++;
            if (momentum >= 2) size--;
            return Mathf.Clamp(size, BaseWaveSize, MaxWaveSize);
        }

        public static int RewardForRound(int round, int momentum)
        {
            int reward = RewardMin + Mathf.Clamp(round, 1, 100) / 10 + Mathf.Max(0, momentum) * 2;
            return Mathf.Clamp(reward, RewardMin, RewardMax);
        }

        public static int FriendlySupportShells(int momentum)
        {
            return Mathf.Clamp(2 + Mathf.Max(0, momentum), 2, MaxFriendlySupportShells);
        }

        public static int NextMomentum(int current, bool success)
        {
            return Mathf.Clamp(current + (success ? 1 : -1), MomentumMin, MomentumMax);
        }

        public static int EligibleOperationCount()
        {
            int count = 0;
            for (int round = 1; round <= 100; round++) if (HasCommandOperationForRound(round)) count++;
            return count;
        }

        public static bool CoreIntegrationTypesAvailable()
        {
            return typeof(LogisticsNetworkDirector) != null &&
                   typeof(SupplyRouteWarfareDirector) != null &&
                   typeof(DynamicFrontlineTerritoryDirector) != null &&
                   typeof(FortificationNetworkDirector) != null &&
                   typeof(FireMissionNetworkDirector) != null;
        }

        private void BeginOperation()
        {
            _phase = CampaignCommandPhase.Reconnaissance;
            _wavesDeployed = 0;
            _supportCommitted = false;
            SpawnRelay();

            float recon = ReconSeconds;
            FireMissionNetworkDirector fireNet = FireMissionNetworkDirector.Instance;
            if (fireNet != null && fireNet.HasTargetLock) recon = Mathf.Max(3f, ReconSeconds - 1.5f);
            _phaseEndsAt = Time.time + recon;
            _status = "THEATER COMMAND // RECON → INTERDICTION → DECISIVE HOLD";
            _statusUntil = Time.time + 4f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.28f, -0.06f);
        }

        private void SpawnRelay()
        {
            int lane = Mathf.Abs((_round / OperationInterval) + _momentum) % 3;
            Vector2 lanePosition = DynamicFrontlineTerritoryDirector.LanePosition(lane);
            Vector2 pos = new Vector2(lanePosition.x, 3.55f + ((_round % 3) - 1) * 0.34f);

            _relay = new GameObject("ENEMY_THEATER_COMMAND_RELAY_V100");
            _relay.transform.position = pos;
            Color core = MomentumColor(_momentum);
            VisualFactory.Rect("RelayBase", _relay.transform, new Vector2(1.30f, 0.92f), new Color(0.12f, 0.14f, 0.19f), Vector3.zero, 8);
            VisualFactory.Rect("RelayCore", _relay.transform, new Vector2(0.72f, 0.58f), core, new Vector3(0f, 0.05f, 0f), 9);
            VisualFactory.Rect("Mast", _relay.transform, new Vector2(0.10f, 1.06f), Color.white, new Vector3(0.31f, 0.70f, 0f), 10);
            VisualFactory.Disc("Beacon", _relay.transform, new Vector2(0.26f, 0.26f), core, new Vector3(0.31f, 1.26f, 0f), 11);

            BoxCollider2D collider = _relay.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.24f, 0.88f);
            _relayHealth = _relay.AddComponent<Health>();
            _relayHealth.Initialize(Team.Enemy, RelayHealthForRound(_round, _momentum));
            _relayHealth.Damaged += OnRelayDamaged;
            _relayHealth.Died += OnRelayDied;
            VisualFactory.RingPulse(pos, core, 1.55f);
        }

        private void BeginInterdiction()
        {
            if (_phase != CampaignCommandPhase.Reconnaissance) return;
            _phase = CampaignCommandPhase.Interdiction;
            _phaseEndsAt = Time.time + InterdictionSeconds;
            _nextWaveAt = Time.time + 1.5f;
            _status = "INTERDICTION // DESTROY COMMAND RELAY BEFORE RESPONSE NETWORK LOCKS";
            _statusUntil = Time.time + 4f;
        }

        private void UpdateInterdiction()
        {
            if (Time.time >= _phaseEndsAt)
            {
                ResolveFailure();
                return;
            }

            if (_wavesDeployed < ResponseWaveCount(_momentum) && Time.time >= _nextWaveAt)
                DeployResponseWave();
        }

        private void DeployResponseWave()
        {
            _nextWaveAt = Time.time + ResponseCadenceSeconds;
            EnemyTank[] snapshot = RuntimeBattleRegistry.EnemySnapshot;
            if (snapshot.Length >= MaxLiveEnemyPressure)
            {
                _status = "COMMAND RESPONSE HELD // BATTLEFIELD SATURATED";
                _statusUntil = Time.time + 2.5f;
                return;
            }

            int wanted = ResponseWaveSize(_round, _momentum);
            int available = Mathf.Max(0, MaxLiveEnemyPressure - snapshot.Length);
            wanted = Mathf.Min(wanted, available);
            EnemyKind[] composition = ResponseComposition(_round, _momentum);
            int spawned = 0;
            for (int i = 0; i < wanted; i++)
            {
                if (SpawnEnemyMethod == null) break;
                try
                {
                    SpawnEnemyMethod.Invoke(_game, new object[] { composition[i % composition.Length] });
                    spawned++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TankRevival] v10 campaign-command response spawn failed: " + ex.GetType().Name);
                    break;
                }
            }

            if (spawned > 0)
            {
                _wavesDeployed++;
                _status = "COMMAND RESPONSE // WAVE " + _wavesDeployed + "/" + ResponseWaveCount(_momentum) + " // " + spawned + " UNITS";
                _statusUntil = Time.time + 2.8f;
                BattleAudio.PlayGlobal(SoundCue.EnemyShot, 0.18f, -0.10f);
            }
        }

        private static EnemyKind[] ResponseComposition(int round, int momentum)
        {
            if (round >= 76 || momentum <= -2)
                return new[] { EnemyKind.Heavy, EnemyKind.Siege, EnemyKind.Elite, EnemyKind.Sniper };
            if (round >= 55)
                return new[] { EnemyKind.Heavy, EnemyKind.Sniper, EnemyKind.Fast };
            return new[] { EnemyKind.Fast, EnemyKind.Heavy, EnemyKind.Fast };
        }

        private void OnRelayDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            _status = "COMMAND RELAY HIT // " + health.Current + "/" + health.Maximum + " HP";
            _statusUntil = Time.time + 2.2f;
            VisualFactory.RingPulse(health.transform.position, MomentumColor(_momentum), 0.62f);
        }

        private void OnRelayDied(Health health)
        {
            BeginDecisiveAction();
        }

        private void BeginDecisiveAction()
        {
            if (_phase == CampaignCommandPhase.DecisiveAction || _phase == CampaignCommandPhase.Resolved) return;
            Vector2 relayPos = _relay != null ? (Vector2)_relay.transform.position : Vector2.zero;
            if (_relay != null)
            {
                VisualFactory.Explosion(relayPos, new Color(0.18f, 0.88f, 1f), 1.40f);
                Destroy(_relay);
            }
            _relay = null;
            _relayHealth = null;
            _phase = CampaignCommandPhase.DecisiveAction;
            _phaseEndsAt = Time.time + DecisiveHoldSeconds;
            CommitFriendlySupport();
            _status = "RELAY DOWN // DECISIVE HOLD " + DecisiveHoldSeconds.ToString("0") + "s";
            _statusUntil = Time.time + 4f;
            BattleAudio.PlayGlobal(SoundCue.ExplosionLarge, 0.38f, 0f);
        }

        private void CommitFriendlySupport()
        {
            if (_supportCommitted || _game == null) return;
            _supportCommitted = true;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shells = FriendlySupportShells(_momentum);
            int fired = 0;
            for (int i = 0; i < enemies.Length && fired < shells; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                Vector2 target = enemy.transform.position;
                Vector2 origin = target + new Vector2((fired % 2 == 0 ? -0.32f : 0.32f), 4.8f);
                _game.SpawnProjectile(origin, Vector2.down, Team.Player, 2, 10.8f, new Color(0.20f, 0.82f, 1f), AmmoType.Explosive);
                VisualFactory.RingPulse(target, new Color(0.20f, 0.82f, 1f), 0.72f);
                fired++;
            }
        }

        private void ResolveSuccess()
        {
            if (_phase == CampaignCommandPhase.Resolved) return;
            int oldMomentum = _momentum;
            int reward = RewardForRound(_round, oldMomentum);
            _momentum = NextMomentum(_momentum, true);
            WarEconomyDirector.AwardMissionBonds(reward, "THEATER COMMAND OPERATION WON");
            _phase = CampaignCommandPhase.Resolved;
            _status = "OPERATION WON // +" + reward + " BONDS // MOMENTUM " + Signed(_momentum);
            _statusUntil = Time.time + 5f;
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.46f, 0.02f);
        }

        private void ResolveFailure()
        {
            if (_phase == CampaignCommandPhase.Resolved) return;
            _momentum = NextMomentum(_momentum, false);
            if (_relay != null)
            {
                VisualFactory.RingPulse(_relay.transform.position, new Color(1f, 0.22f, 0.10f), 1.2f);
            }
            ClearRelay();
            _phase = CampaignCommandPhase.Resolved;
            _status = "OPERATION LOST // ENEMY INITIATIVE // MOMENTUM " + Signed(_momentum);
            _statusUntil = Time.time + 5f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.30f, -0.12f);
        }

        private void ClearRelay()
        {
            if (_relayHealth != null)
            {
                _relayHealth.Damaged -= OnRelayDamaged;
                _relayHealth.Died -= OnRelayDied;
            }
            if (_relay != null) Destroy(_relay);
            _relay = null;
            _relayHealth = null;
        }

        private void ResetOperationOnly()
        {
            ClearRelay();
            _phase = CampaignCommandPhase.None;
            _wavesDeployed = 0;
            _nextWaveAt = 0f;
            _phaseEndsAt = 0f;
            _supportCommitted = false;
            _status = string.Empty;
        }

        private void ResetRun()
        {
            ResetOperationOnly();
            _round = -1;
            _momentum = 0;
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private static Color MomentumColor(int momentum)
        {
            if (momentum >= 2) return new Color(0.18f, 0.92f, 1f);
            if (momentum < 0) return new Color(1f, 0.25f, 0.10f);
            return new Color(1f, 0.62f, 0.12f);
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.18f, 0.92f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _warning = new GUIStyle(_body) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.50f, 0.12f) } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || !HasCommandOperationForRound(_game.CurrentRound) || _phase == CampaignCommandPhase.None) return;
            EnsureStyles();
            float hp = _relayHealth != null && _relayHealth.Maximum > 0 ? (float)_relayHealth.Current / _relayHealth.Maximum : 0f;
            float remaining = Mathf.Max(0f, _phaseEndsAt - Time.time);
            GUI.color = new Color(0.025f, 0.050f, 0.075f, 0.94f);
            GUI.Box(new Rect(Screen.width - 456f, 270f, 442f, 118f), string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 442f, 277f, 414f, 22f), "THEATER COMMAND // " + _phase.ToString().ToUpperInvariant(), _header);
            GUI.Label(new Rect(Screen.width - 442f, 302f, 414f, 20f), "MOMENTUM " + Signed(_momentum) + "   RELAY " + Mathf.RoundToInt(hp * 100f) + "%   T-" + remaining.ToString("0.0"), _body);
            GUI.Label(new Rect(Screen.width - 442f, 325f, 414f, 20f), "RESPONSE " + _wavesDeployed + "/" + ResponseWaveCount(_momentum) + "   SUPPORT " + FriendlySupportShells(_momentum) + " SHELLS", _body);
            if (!string.IsNullOrEmpty(_status) && Time.time <= _statusUntil)
                GUI.Label(new Rect(Screen.width - 442f, 348f, 414f, 30f), _status, _phase == CampaignCommandPhase.Interdiction ? _warning : _body);
        }
    }
}
