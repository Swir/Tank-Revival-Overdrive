using UnityEngine;

namespace TankRevival
{
    public enum CounterOrderOutcome
    {
        None = 0,
        Success = 1,
        Stalemate = 2,
        Failure = 3
    }

    public enum CounterOffensivePlan
    {
        None = 0,
        LocalOffensive = 1,
        FeintExploit = 2,
        CommandCollapse = 3,
        EnemyRecovery = 4
    }

    [DefaultExecutionOrder(556)]
    public sealed class CounterOffensiveCampaignDirector : MonoBehaviour
    {
        public const float AssessmentDelay = 7.5f;
        public const int MaxOperationalMomentum = 3;
        public const int MinOperationalMomentum = -2;
        public const int MaxCapturedIntel = 2;
        public const int MaxPlanRounds = 3;
        public const int MaxSupportShellsPerRound = 2;
        public const int MaxRecoveryHealsPerRound = 1;
        public const int MaxSuccessRewardBonds = 5;

        private static CounterOffensiveCampaignDirector _instance;
        private TankGame _game;
        private int _round = -1;
        private int _trackedOperationSector = -1;
        private int _sourceSector = -1;
        private int _lastPlanRound = -1;
        private bool _assessmentActive;
        private bool _assessmentResolved;
        private float _assessmentStarted;
        private PlayerCounterOrder _trackedOrder;
        private int _startHighValue;
        private int _startEnemies;
        private float _startEagleRatio;
        private DeceptionAxis _mainAxis;
        private DeceptionAxis _feintAxis;
        private CounterOrderOutcome _lastOutcome;
        private CounterOffensivePlan _pendingPlan;
        private int _operationalMomentum;
        private int _capturedIntel;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static CounterOffensiveCampaignDirector Instance => _instance;
        public CounterOrderOutcome LastOutcome => _lastOutcome;
        public CounterOffensivePlan PendingPlan => _pendingPlan;
        public int OperationalMomentum => _operationalMomentum;
        public int CapturedIntel => _capturedIntel;
        public bool AssessmentActive => _assessmentActive;

        public static bool ConfigurationValid =>
            AssessmentDelay >= 6f && AssessmentDelay <= 10f &&
            MinOperationalMomentum == -2 && MaxOperationalMomentum == 3 &&
            MaxCapturedIntel >= 1 && MaxCapturedIntel <= 2 &&
            MaxPlanRounds >= 2 && MaxPlanRounds <= 3 &&
            MaxSupportShellsPerRound >= 1 && MaxSupportShellsPerRound <= 2 &&
            MaxRecoveryHealsPerRound == 1 &&
            MaxSuccessRewardBonds >= 3 && MaxSuccessRewardBonds <= 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CounterOffensiveCampaignDirector>() != null) return;
            GameObject go = new GameObject("CounterOffensiveCampaignDirector_v10_6");
            DontDestroyOnLoad(go);
            go.AddComponent<CounterOffensiveCampaignDirector>();
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

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round != _round)
            {
                _round = round;
                TryExecuteCarryover(round);
            }

            ObserveCounterOrder(round);
            if (_assessmentActive) TryResolveAssessment(round);
        }

        private void ObserveCounterOrder(int round)
        {
            if (!PlayerCounterOrderDirector.IsDecisionRound(round)) return;
            PlayerCounterOrderDirector counter = PlayerCounterOrderDirector.Instance;
            if (counter == null || counter.ActiveOrder == PlayerCounterOrder.None) return;

            int sector = HighCommandDeceptionWarDirector.SectorForRound(round);
            if (_trackedOperationSector == sector) return;

            _trackedOperationSector = sector;
            _sourceSector = sector;
            _trackedOrder = counter.ActiveOrder;
            _mainAxis = counter.KnownMainAxis;
            _feintAxis = counter.KnownFeintAxis;
            _startHighValue = CountHighValueEnemies();
            _startEnemies = CountLiveEnemies();
            _startEagleRatio = EagleRatio();
            _assessmentStarted = Time.time;
            _assessmentActive = true;
            _assessmentResolved = false;
            _banner = "COUNTER-ORDER ASSESSMENT // " + _trackedOrder.ToString().ToUpperInvariant();
            _bannerUntil = Time.unscaledTime + 3.5f;
        }

        private void TryResolveAssessment(int round)
        {
            PlayerCounterOrderDirector counter = PlayerCounterOrderDirector.Instance;
            if (counter == null || counter.BeatsRemaining > 0) return;
            if (Time.time - _assessmentStarted < AssessmentDelay) return;

            int highValueDestroyed = Mathf.Max(0, _startHighValue - CountHighValueEnemies());
            int enemiesDestroyed = Mathf.Max(0, _startEnemies - CountLiveEnemies());
            float eagle = EagleRatio();
            bool eagleAlive = CombatRoster.Eagle != null && !CombatRoster.Eagle.IsDead;

            _lastOutcome = EvaluateOutcome(_trackedOrder, _startEagleRatio, eagle, highValueDestroyed, enemiesDestroyed, eagleAlive);
            _pendingPlan = ResolveNextSectorPlan(_trackedOrder, _lastOutcome);
            _operationalMomentum = Mathf.Clamp(_operationalMomentum + MomentumDelta(_lastOutcome), MinOperationalMomentum, MaxOperationalMomentum);

            if (_trackedOrder == PlayerCounterOrder.DeepStrike && _lastOutcome == CounterOrderOutcome.Success)
                _capturedIntel = Mathf.Min(MaxCapturedIntel, _capturedIntel + 1);

            if (_lastOutcome == CounterOrderOutcome.Success)
            {
                int reward = Mathf.Clamp(3 + Mathf.Max(0, _operationalMomentum), 3, MaxSuccessRewardBonds);
                WarEconomyDirector.AwardMissionBonds(reward, "COUNTER-OFFENSIVE INTELLIGENCE");
            }

            _assessmentActive = false;
            _assessmentResolved = true;
            _banner = "OPERATION " + _lastOutcome.ToString().ToUpperInvariant() + " // NEXT: " + _pendingPlan.ToString().ToUpperInvariant();
            _bannerUntil = Time.unscaledTime + 5f;
            BattleAudio.PlayGlobal(_lastOutcome == CounterOrderOutcome.Success ? SoundCue.RoundClear : SoundCue.Emp, 0.32f, 0.04f);
        }

        private void TryExecuteCarryover(int round)
        {
            int sector = SectorForRound(round);
            if (_sourceSector < 0 || sector <= _sourceSector) return;
            if (!IsPlanRound(round) || _lastPlanRound == round) return;
            if (_pendingPlan == CounterOffensivePlan.None) return;

            int offset = (round - 1) % 10;
            if (offset >= MaxPlanRounds) return;
            _lastPlanRound = round;

            switch (_pendingPlan)
            {
                case CounterOffensivePlan.LocalOffensive:
                    ApplyLocalOffensive(round);
                    break;
                case CounterOffensivePlan.FeintExploit:
                    FirePrioritySupport(AmmoType.Explosive, round >= 80 ? 2 : 1, MaxSupportShellsPerRound);
                    break;
                case CounterOffensivePlan.CommandCollapse:
                    ApplyCommandCollapse(round);
                    break;
                case CounterOffensivePlan.EnemyRecovery:
                    ApplyEnemyRecovery();
                    break;
            }

            if (offset == MaxPlanRounds - 1)
            {
                _banner = "SECTOR CONSEQUENCE COMPLETE // MOMENTUM " + Signed(_operationalMomentum);
                _bannerUntil = Time.unscaledTime + 3f;
                _pendingPlan = CounterOffensivePlan.None;
                _assessmentResolved = false;
            }
        }

        private void ApplyLocalOffensive(int round)
        {
            Health eagle = CombatRoster.Eagle;
            if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum) eagle.Heal(1);
            FirePrioritySupport(AmmoType.ArmorPiercing, round >= 75 ? 2 : 1, 1);
            PulseAxis(_mainAxis, new Color(0.24f, 0.82f, 1f));
        }

        private void ApplyCommandCollapse(int round)
        {
            int damage = _capturedIntel > 0 && round >= 70 ? 2 : 1;
            FirePrioritySupport(AmmoType.ArmorPiercing, damage, MaxSupportShellsPerRound);
            if (_capturedIntel > 0)
            {
                _capturedIntel--;
                _banner = "CAPTURED INTEL EXPLOITED // HIGH COMMAND EXPOSED";
                _bannerUntil = Time.unscaledTime + 2.5f;
            }
        }

        private void ApplyEnemyRecovery()
        {
            if (_capturedIntel > 0)
            {
                _capturedIntel--;
                _banner = "CAPTURED INTEL BLOCKED HIGH COMMAND RECOVERY";
                _bannerUntil = Time.unscaledTime + 2.5f;
                return;
            }

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return;
            int healed = 0;
            for (int i = 0; i < enemies.Length && healed < MaxRecoveryHealsPerRound; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsHighValueLive(enemy)) continue;
                if (enemy.Health.Current >= enemy.Health.Maximum) continue;
                enemy.Health.Heal(1);
                healed++;
                VisualFactory.RingPulse(enemy.transform.position, new Color(1f, 0.28f, 0.18f), 0.65f);
            }
        }

        private int FirePrioritySupport(AmmoType ammo, int damage, int cap)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return 0;
            int fired = 0;
            for (int pass = 0; pass < 5 && fired < cap; pass++)
            {
                EnemyTank target = null;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (!IsHighValueLive(enemy)) continue;
                    if (!MatchesPriority(enemy.Kind, pass)) continue;
                    target = enemy;
                    break;
                }
                if (target == null) continue;
                Vector2 origin = _game.PlayerPosition;
                Vector2 delta = (Vector2)target.transform.position - origin;
                if (delta.sqrMagnitude < 0.25f) continue;
                _game.SpawnProjectile(origin + delta.normalized * 0.65f, delta.normalized, Team.Player, Mathf.Clamp(damage, 1, 2), ammo == AmmoType.Explosive ? 10f : 12f, AmmoDatabase.Color(ammo), ammo);
                VisualFactory.RingPulse(target.transform.position, ammo == AmmoType.Explosive ? new Color(1f, 0.56f, 0.12f) : new Color(0.25f, 0.82f, 1f), 0.72f);
                fired++;
            }
            return fired;
        }

        private static bool MatchesPriority(EnemyKind kind, int pass)
        {
            if (pass == 0) return kind == EnemyKind.Siege;
            if (pass == 1) return kind == EnemyKind.Heavy;
            if (pass == 2) return kind == EnemyKind.Elite;
            if (pass == 3) return kind == EnemyKind.Sniper;
            return kind != EnemyKind.Basic && kind != EnemyKind.Fast;
        }

        private static bool IsHighValueLive(EnemyTank enemy)
        {
            if (enemy == null || enemy.Health == null || enemy.Health.IsDead) return false;
            return enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper;
        }

        private static int CountHighValueEnemies()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return 0;
            int count = 0;
            for (int i = 0; i < enemies.Length; i++) if (IsHighValueLive(enemies[i])) count++;
            return count;
        }

        private static int CountLiveEnemies()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return 0;
            int count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null && enemy.Health != null && !enemy.Health.IsDead) count++;
            }
            return count;
        }

        private static float EagleRatio()
        {
            Health eagle = CombatRoster.Eagle;
            if (eagle == null || eagle.Maximum <= 0) return 1f;
            return Mathf.Clamp01(eagle.Current / (float)eagle.Maximum);
        }

        private static void PulseAxis(DeceptionAxis axis, Color color)
        {
            VisualFactory.RingPulse(DynamicFrontlineTerritoryDirector.LanePosition((int)axis), color, 1.1f);
        }

        private void ResetRun()
        {
            _round = -1;
            _trackedOperationSector = -1;
            _sourceSector = -1;
            _lastPlanRound = -1;
            _assessmentActive = false;
            _assessmentResolved = false;
            _trackedOrder = PlayerCounterOrder.None;
            _lastOutcome = CounterOrderOutcome.None;
            _pendingPlan = CounterOffensivePlan.None;
            _operationalMomentum = 0;
            _capturedIntel = 0;
            _banner = string.Empty;
        }

        public static CounterOrderOutcome EvaluateOutcome(PlayerCounterOrder order, float startEagleRatio, float endEagleRatio, int highValueDestroyed, int enemiesDestroyed, bool eagleAlive)
        {
            if (order == PlayerCounterOrder.None || !eagleAlive) return CounterOrderOutcome.Failure;
            if (order == PlayerCounterOrder.Block)
            {
                if (endEagleRatio >= Mathf.Max(0.35f, startEagleRatio - 0.12f)) return CounterOrderOutcome.Success;
                return endEagleRatio >= 0.24f ? CounterOrderOutcome.Stalemate : CounterOrderOutcome.Failure;
            }
            if (order == PlayerCounterOrder.Counterattack)
            {
                if (highValueDestroyed >= 1 || enemiesDestroyed >= 2) return CounterOrderOutcome.Success;
                return enemiesDestroyed >= 1 ? CounterOrderOutcome.Stalemate : CounterOrderOutcome.Failure;
            }
            if (highValueDestroyed >= 1) return CounterOrderOutcome.Success;
            return enemiesDestroyed >= 1 ? CounterOrderOutcome.Stalemate : CounterOrderOutcome.Failure;
        }

        public static CounterOffensivePlan ResolveNextSectorPlan(PlayerCounterOrder order, CounterOrderOutcome outcome)
        {
            if (outcome == CounterOrderOutcome.Failure) return CounterOffensivePlan.EnemyRecovery;
            if (outcome != CounterOrderOutcome.Success) return CounterOffensivePlan.None;
            if (order == PlayerCounterOrder.Block) return CounterOffensivePlan.LocalOffensive;
            if (order == PlayerCounterOrder.Counterattack) return CounterOffensivePlan.FeintExploit;
            if (order == PlayerCounterOrder.DeepStrike) return CounterOffensivePlan.CommandCollapse;
            return CounterOffensivePlan.None;
        }

        public static int MomentumDelta(CounterOrderOutcome outcome)
        {
            if (outcome == CounterOrderOutcome.Success) return 1;
            if (outcome == CounterOrderOutcome.Failure) return -1;
            return 0;
        }

        public static int SectorForRound(int round) => Mathf.Clamp(((Mathf.Clamp(round, 1, 100) - 1) / 10) + 1, 1, 10);

        public static bool IsPlanRound(int round)
        {
            round = Mathf.Clamp(round, 1, 100);
            int offset = (round - 1) % 10;
            return round % 10 != 0 && offset < MaxPlanRounds;
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime >= _bannerUntil) return;
            EnsureStyles();
            float width = Mathf.Min(590f, Screen.width - 32f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 128f, width, 58f);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f));
            GUILayout.Label("COUNTER-OFFENSIVE COMMAND", _titleStyle);
            GUILayout.Label(_banner + "  //  MOM " + Signed(_operationalMomentum) + "  //  INTEL " + _capturedIntel + "/" + MaxCapturedIntel, _bodyStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }
    }
}
