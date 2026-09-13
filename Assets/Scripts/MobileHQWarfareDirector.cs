using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(449)]
    public sealed class MobileHQWarfareDirector : MonoBehaviour
    {
        public const int MinimumOperationRound = 36;
        public const int OperationRoundCadence = 8;
        public const float MobileHQHealthMultiplier = 1.65f;
        public const float MobileHQRelocationCadence = 5.5f;
        public const float MobileHQStandoff = 5.2f;
        public const int MaxEscortOrders = 5;
        public const int MaxCounterattackShots = 4;
        public const float CounterattackCadence = 3.25f;
        public const float EmergencySuccessionDelay = 2.5f;
        public const float SuccessorHealthMultiplier = 1.28f;
        public const float CommandCollapseDuration = 8.0f;

        private static MobileHQWarfareDirector _instance;
        private TankGame _game;
        private EnemyTank _mobileHq;
        private Health _mobileHqHealth;
        private EnemyTank _successor;
        private Health _successorHealth;
        private int _operationRound = -1;
        private float _nextRelocation;
        private float _nextCounterattack;
        private float _successionAt;
        private float _collapseUntil;
        private bool _hqDestroyedThisOperation;
        private int _operationsStarted;
        private int _hqDestroyed;
        private int _successions;
        private int _successorsDestroyed;
        private int _escortOrders;
        private int _counterattackShots;
        private bool _ownsAdaptiveSuppression;
        private bool _ownsSquadSuppression;
        private bool _ownsBossSuppression;

        public static MobileHQWarfareDirector Instance => _instance;
        public static bool ConfigurationValid =>
            MinimumOperationRound >= 30 && MinimumOperationRound <= 50 &&
            OperationRoundCadence >= 6 && OperationRoundCadence <= 12 &&
            MobileHQHealthMultiplier >= 1.40f && MobileHQHealthMultiplier <= 1.90f &&
            MobileHQRelocationCadence >= 4f && MobileHQRelocationCadence <= 8f &&
            MobileHQStandoff >= 4f && MobileHQStandoff <= 7f &&
            MaxEscortOrders >= 3 && MaxEscortOrders <= 7 &&
            MaxCounterattackShots >= 2 && MaxCounterattackShots <= 5 &&
            CounterattackCadence >= 2.5f && CounterattackCadence <= 5f &&
            EmergencySuccessionDelay >= 1.5f && EmergencySuccessionDelay <= 4f &&
            SuccessorHealthMultiplier >= 1.15f && SuccessorHealthMultiplier <= 1.45f &&
            CommandCollapseDuration >= 6f && CommandCollapseDuration <= 10f;

        public bool OperationActive => _game != null && _game.IsPlaying && _operationRound == _game.CurrentRound;
        public bool HasLiveMobileHQ => IsLive(_mobileHq, _mobileHqHealth);
        public bool HasEmergencySuccessor => IsLive(_successor, _successorHealth);
        public bool CommandCollapseActive => Time.time < _collapseUntil;
        public float CommandCollapseRemaining => Mathf.Max(0f, _collapseUntil - Time.time);
        public int OperationsStarted => _operationsStarted;
        public int MobileHQDestroyed => _hqDestroyed;
        public int EmergencySuccessions => _successions;
        public int SuccessorsDestroyed => _successorsDestroyed;
        public int EscortOrdersIssued => _escortOrders;
        public int CounterattackShotsFired => _counterattackShots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<MobileHQWarfareDirector>() != null) return;
            var go = new GameObject("MobileHQWarfareDirector_v7_9");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileHQWarfareDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ClearOperationBindings();
            RestoreOwnedSuppression(true);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying)
            {
                ClearOperationBindings();
                RestoreOwnedSuppression(true);
                _operationRound = -1;
                _hqDestroyedThisOperation = false;
                _successionAt = 0f;
                _collapseUntil = 0f;
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_operationRound != round)
                EnterRound(round);

            if (!OperationActive)
            {
                if (!CommandCollapseActive) RestoreOwnedSuppression(false);
                return;
            }

            if (HasLiveMobileHQ)
            {
                if (Time.time >= _nextRelocation)
                {
                    _nextRelocation = Time.time + MobileHQRelocationCadence;
                    RelocateMobileHQ(round);
                    OrderEscorts(round);
                }

                if (Time.time >= _nextCounterattack)
                {
                    _nextCounterattack = Time.time + CounterattackCadence;
                    ExecuteCounterattack(round);
                }

                PresentHQ();
            }
            else if (_hqDestroyedThisOperation && !HasEmergencySuccessor && _successionAt > 0f && Time.time >= _successionAt)
            {
                _successionAt = 0f;
                PromoteEmergencySuccessor();
            }

            if (HasEmergencySuccessor)
            {
                PresentSuccessor();
                if (Time.time >= _nextCounterattack)
                {
                    _nextCounterattack = Time.time + CounterattackCadence + 0.8f;
                    ExecuteSuccessorCounterattack(round);
                }
            }

            if (CommandCollapseActive)
                SuppressAdvancedCoordination();
            else
                RestoreOwnedSuppression(false);
        }

        private void EnterRound(int round)
        {
            ClearOperationBindings();
            _operationRound = round;
            _hqDestroyedThisOperation = false;
            _successionAt = 0f;
            _collapseUntil = 0f;
            RestoreOwnedSuppression(true);

            if (!IsOperationRound(round)) return;
            EnemyTank candidate = SelectHQCandidate();
            if (candidate == null) return;

            _mobileHq = candidate;
            _mobileHqHealth = candidate.Health;
            int boosted = Mathf.Max(_mobileHqHealth.Maximum + 3, Mathf.CeilToInt(_mobileHqHealth.Maximum * MobileHQHealthMultiplier));
            _mobileHqHealth.SetMaximum(boosted, true);
            _mobileHqHealth.Died += OnMobileHQDied;
            if (candidate.GetComponent<MobileHQNode>() == null) candidate.gameObject.AddComponent<MobileHQNode>();
            _nextRelocation = Time.time + 1.0f;
            _nextCounterattack = Time.time + 1.8f;
            _operationsStarted++;
            Vector2 pos = candidate.transform.position;
            VisualFactory.RingPulse(pos, new Color(1f, 0.44f, 0.12f), 2.1f);
            VisualFactory.MicroBurst(pos, new Color(0.72f, 0.20f, 1f), 1.25f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.32f, -0.02f);
        }

        private static bool IsOperationRound(int round)
        {
            if (round < MinimumOperationRound) return false;
            return ((round - MinimumOperationRound) % OperationRoundCadence) == 0;
        }

        private EnemyTank SelectHQCandidate()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 player = _game.PlayerPosition;

            for (int i = 0; i < enemies.Length && i < 56; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy, enemy != null ? enemy.Health : null)) continue;
                if (enemy.Kind != EnemyKind.Heavy && enemy.Kind != EnemyKind.Siege && enemy.Kind != EnemyKind.Elite) continue;
                if (enemy.GetComponent<EnemyCommandNode>() != null || enemy.GetComponent<EnemyRelayNode>() != null || enemy.GetComponent<MobileHQNode>() != null) continue;

                float kind = enemy.Kind == EnemyKind.Siege ? 5f : enemy.Kind == EnemyKind.Heavy ? 4.5f : 4f;
                float distance = Mathf.Clamp(Vector2.Distance(enemy.transform.position, player) * 0.06f, 0f, 1.8f);
                float score = kind + distance;
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }
            return best;
        }

        private void RelocateMobileHQ(int round)
        {
            if (!HasLiveMobileHQ) return;
            TacticalNavigationAgent agent = _mobileHq.GetComponent<TacticalNavigationAgent>();
            if (agent == null) return;

            Vector2 player = _game.PlayerPosition;
            Vector2 fromPlayer = ((Vector2)_mobileHq.transform.position - player);
            if (fromPlayer.sqrMagnitude < 0.25f) fromPlayer = Vector2.up;
            Vector2 side = new Vector2(-fromPlayer.y, fromPlayer.x).normalized;
            float sign = (((round + _mobileHq.GetInstanceID()) >> 1) & 1) == 0 ? 1f : -1f;
            Vector2 target = player + fromPlayer.normalized * MobileHQStandoff + side * sign * 2.2f;
            agent.SetOrder(target, MobileHQStandoff, 0.92f, RuntimeBattleRegistry.EnemySnapshot);
            VisualFactory.RingPulse(target, new Color(1f, 0.42f, 0.10f, 0.58f), 0.82f);
        }

        private void OrderEscorts(int round)
        {
            if (!HasLiveMobileHQ) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            Vector2 hq = _mobileHq.transform.position;
            int ordered = 0;

            for (int i = 0; i < enemies.Length && ordered < MaxEscortOrders; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy, enemy != null ? enemy.Health : null) || enemy == _mobileHq) continue;
                if (enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss) continue;
                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent == null) continue;

                float angle = (ordered / (float)MaxEscortOrders) * Mathf.PI * 2f + round * 0.17f;
                Vector2 escort = hq + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (1.8f + (ordered % 2) * 0.7f);
                agent.SetOrder(escort, 1.2f, enemy.Kind == EnemyKind.Fast ? 1.12f : 1.0f, enemies);
                ordered++;
                _escortOrders++;
            }
        }

        private void ExecuteCounterattack(int round)
        {
            if (!HasLiveMobileHQ) return;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            Vector2 player = _game.PlayerPosition;
            int shots = 0;
            float progress = (round - 1f) / 99f;

            for (int i = 0; i < enemies.Length && shots < MaxCounterattackShots; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy, enemy != null ? enemy.Health : null)) continue;
                if (enemy != _mobileHq && enemy.Kind != EnemyKind.Elite && enemy.Kind != EnemyKind.Sniper && enemy.Kind != EnemyKind.Siege) continue;
                Vector2 origin = enemy.transform.position;
                Vector2 delta = player - origin;
                if (delta.sqrMagnitude < 1f) continue;
                Vector2 direction = delta.normalized;
                Vector2 muzzle = origin + direction * 0.78f;
                _game.SpawnProjectile(muzzle, direction, Team.Enemy, 1, Mathf.Lerp(9.2f, 12.2f, progress), new Color(1f, 0.48f, 0.16f), AmmoType.Basic);
                VisualFactory.MuzzleFlash(muzzle, new Color(1f, 0.42f, 0.12f), 0.58f);
                shots++;
                _counterattackShots++;
            }
        }

        private void ExecuteSuccessorCounterattack(int round)
        {
            if (!HasEmergencySuccessor) return;
            Vector2 origin = _successor.transform.position;
            Vector2 delta = _game.PlayerPosition - origin;
            if (delta.sqrMagnitude < 1f) return;
            Vector2 direction = delta.normalized;
            Vector2 muzzle = origin + direction * 0.78f;
            _game.SpawnProjectile(muzzle, direction, Team.Enemy, 1, Mathf.Lerp(9.6f, 12.4f, (round - 1f) / 99f), new Color(0.72f, 0.28f, 1f), AmmoType.Basic);
            VisualFactory.MuzzleFlash(muzzle, new Color(0.70f, 0.26f, 1f), 0.58f);
            _counterattackShots++;
        }

        private void OnMobileHQDied(Health dead)
        {
            if (dead == null || dead != _mobileHqHealth) return;
            Vector2 pos = _mobileHq != null ? (Vector2)_mobileHq.transform.position : Vector2.zero;
            _hqDestroyed++;
            _hqDestroyedThisOperation = true;
            _successionAt = Time.time + EmergencySuccessionDelay;
            VisualFactory.RingPulse(pos, new Color(0.22f, 1f, 0.66f), 2.6f);
            VisualFactory.MicroBurst(pos, new Color(1f, 0.38f, 0.12f), 1.5f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.38f, -0.06f);
            UnsubscribeHQ();
        }

        private void PromoteEmergencySuccessor()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 player = _game.PlayerPosition;

            for (int i = 0; i < enemies.Length && i < 56; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsLive(enemy, enemy != null ? enemy.Health : null) || enemy.Kind != EnemyKind.Elite) continue;
                if (enemy.GetComponent<MobileHQNode>() != null || enemy.GetComponent<EmergencyCommandNode>() != null) continue;
                float score = Vector2.Distance(enemy.transform.position, player);
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }

            if (best == null)
            {
                TriggerCommandCollapse();
                return;
            }

            _successor = best;
            _successorHealth = best.Health;
            int boosted = Mathf.Max(_successorHealth.Maximum + 2, Mathf.CeilToInt(_successorHealth.Maximum * SuccessorHealthMultiplier));
            _successorHealth.SetMaximum(boosted, true);
            _successorHealth.Died += OnSuccessorDied;
            best.gameObject.AddComponent<EmergencyCommandNode>();
            _successions++;
            _nextCounterattack = Time.time + 1.2f;
            Vector2 pos = best.transform.position;
            VisualFactory.RingPulse(pos, new Color(0.72f, 0.28f, 1f), 1.8f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.28f, 0.08f);
        }

        private void OnSuccessorDied(Health dead)
        {
            if (dead == null || dead != _successorHealth) return;
            Vector2 pos = _successor != null ? (Vector2)_successor.transform.position : Vector2.zero;
            _successorsDestroyed++;
            VisualFactory.RingPulse(pos, new Color(0.16f, 1f, 0.70f), 2.4f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.36f, -0.08f);
            UnsubscribeSuccessor();
            TriggerCommandCollapse();
        }

        private void TriggerCommandCollapse()
        {
            _collapseUntil = Mathf.Max(_collapseUntil, Time.time + CommandCollapseDuration);
            SuppressAdvancedCoordination();
            VisualFactory.RingPulse(_game.PlayerPosition, new Color(0.18f, 1f, 0.62f), 3.0f);
        }

        private void SuppressAdvancedCoordination()
        {
            AdaptiveFireControlDirector adaptive = AdaptiveFireControlDirector.Instance;
            if (adaptive != null && adaptive.enabled) { adaptive.enabled = false; _ownsAdaptiveSuppression = true; }
            EnemySquadTacticsDirector squad = EnemySquadTacticsDirector.Instance;
            if (squad != null && squad.enabled) { squad.enabled = false; _ownsSquadSuppression = true; }
            BossCommandTacticsDirector boss = BossCommandTacticsDirector.Instance;
            if (boss != null && boss.enabled) { boss.enabled = false; _ownsBossSuppression = true; }
        }

        private void RestoreOwnedSuppression(bool force)
        {
            if (!force)
            {
                TacticalCounterplayDirector counter = TacticalCounterplayDirector.Instance;
                EnemyElectronicWarfareDirector ew = EnemyElectronicWarfareDirector.Instance;
                CommandNetworkHuntDirector hunt = CommandNetworkHuntDirector.Instance;
                bool externallySuppressed = (counter != null && (counter.NetworkJammed || counter.PlayerInsideSmoke)) ||
                                            (ew != null && ew.TacticalSuperiorityActive) ||
                                            (hunt != null && hunt.NetworkBreakActive) || CommandCollapseActive;
                if (externallySuppressed) return;
            }

            if (_ownsAdaptiveSuppression)
            {
                if (AdaptiveFireControlDirector.Instance != null) AdaptiveFireControlDirector.Instance.enabled = true;
                _ownsAdaptiveSuppression = false;
            }
            if (_ownsSquadSuppression)
            {
                if (EnemySquadTacticsDirector.Instance != null) EnemySquadTacticsDirector.Instance.enabled = true;
                _ownsSquadSuppression = false;
            }
            if (_ownsBossSuppression)
            {
                if (BossCommandTacticsDirector.Instance != null) BossCommandTacticsDirector.Instance.enabled = true;
                _ownsBossSuppression = false;
            }
        }

        private void PresentHQ()
        {
            if (Time.frameCount % 20 != 0 || _mobileHq == null) return;
            VisualFactory.RingPulse(_mobileHq.transform.position, new Color(1f, 0.42f, 0.10f, 0.72f), 1.02f);
        }

        private void PresentSuccessor()
        {
            if (Time.frameCount % 22 != 0 || _successor == null) return;
            VisualFactory.RingPulse(_successor.transform.position, new Color(0.72f, 0.28f, 1f, 0.70f), 0.88f);
        }

        private void ClearOperationBindings()
        {
            UnsubscribeHQ();
            UnsubscribeSuccessor();
        }

        private void UnsubscribeHQ()
        {
            if (_mobileHqHealth != null) _mobileHqHealth.Died -= OnMobileHQDied;
            _mobileHq = null;
            _mobileHqHealth = null;
        }

        private void UnsubscribeSuccessor()
        {
            if (_successorHealth != null) _successorHealth.Died -= OnSuccessorDied;
            _successor = null;
            _successorHealth = null;
        }

        private static bool IsLive(EnemyTank enemy, Health health)
        {
            return enemy != null && health != null && !health.IsDead;
        }

        private void OnGUI()
        {
            if (!OperationActive || _game == null) return;
            float width = Mathf.Min(620f, Screen.width - 28f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 96f, width, 28f);
            string state = HasLiveMobileHQ ? "MOBILE HQ ACTIVE" : HasEmergencySuccessor ? "EMERGENCY COMMAND" : CommandCollapseActive ? $"COMMAND COLLAPSE {CommandCollapseRemaining:0.0}s" : "HQ NEUTRALIZED";
            GUI.Box(rect, state + "     RELAYS " + (CommandNetworkHuntDirector.Instance != null ? CommandNetworkHuntDirector.Instance.LiveRelayCount.ToString() : "-") );
        }
    }

    public sealed class MobileHQNode : MonoBehaviour
    {
        public float PromotedAt { get; private set; }
        private void Awake() => PromotedAt = Time.time;
    }

    public sealed class EmergencyCommandNode : MonoBehaviour
    {
        public float PromotedAt { get; private set; }
        private void Awake() => PromotedAt = Time.time;
    }
}
