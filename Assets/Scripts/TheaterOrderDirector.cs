using System;
using UnityEngine;

namespace TankRevival
{
    public enum TheaterOrderKind
    {
        None,
        Assault,
        Interdiction,
        Fortify
    }

    [DefaultExecutionOrder(585)]
    public sealed class TheaterOrderDirector : MonoBehaviour
    {
        public const int FirstDecisionRound = 42;
        public const int DecisionInterval = 14;
        public const int OrderDurationRounds = 4;
        public const float DecisionSeconds = 9f;
        public const int AssaultShellsEarly = 1;
        public const int AssaultShellsLate = 2;
        public const int MaxAssaultShellsPerRound = 2;
        public const int InterdictionShotsPerRound = 1;
        public const int FortifyPlayerHeal = 1;
        public const int FortifyEagleHeal = 1;
        public const int OperationRewardBonus = 4;

        private static TheaterOrderDirector _instance;
        private TankGame _game;
        private int _round = -1;
        private TheaterOrderKind _activeOrder;
        private int _activeThroughRound;
        private bool _decisionOpen;
        private float _decisionEndsAt;
        private float _effectAt;
        private bool _roundEffectApplied;
        private int _lastRewardedCommandRound = -1;
        private string _status = string.Empty;
        private float _statusUntil;
        private GUIStyle _header;
        private GUIStyle _body;

        public static TheaterOrderDirector Instance => _instance;
        public TheaterOrderKind ActiveOrder => _activeOrder;
        public int ActiveThroughRound => _activeThroughRound;
        public bool DecisionOpen => _decisionOpen;

        public static bool ConfigurationValid =>
            FirstDecisionRound >= 40 && FirstDecisionRound <= 50 &&
            DecisionInterval >= 12 && DecisionInterval <= 16 &&
            OrderDurationRounds >= 3 && OrderDurationRounds <= 5 &&
            DecisionSeconds >= 6f && DecisionSeconds <= 12f &&
            AssaultShellsEarly >= 1 && AssaultShellsLate <= MaxAssaultShellsPerRound &&
            MaxAssaultShellsPerRound <= 2 && InterdictionShotsPerRound == 1 &&
            FortifyPlayerHeal == 1 && FortifyEagleHeal == 1 &&
            OperationRewardBonus >= 2 && OperationRewardBonus <= 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TheaterOrderDirector>() != null) return;
            GameObject go = new GameObject("TheaterOrderDirector_v10_1");
            DontDestroyOnLoad(go);
            go.AddComponent<TheaterOrderDirector>();
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

            int current = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (current != _round)
            {
                _round = current;
                _roundEffectApplied = false;
                _effectAt = Time.time + 2.1f;

                if (_activeOrder != TheaterOrderKind.None && current > _activeThroughRound)
                {
                    _activeOrder = TheaterOrderKind.None;
                    _activeThroughRound = 0;
                }

                if (HasDecisionForRound(current)) OpenDecision();
            }

            if (_decisionOpen)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SelectOrder(TheaterOrderKind.Assault);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectOrder(TheaterOrderKind.Interdiction);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectOrder(TheaterOrderKind.Fortify);
                else if (Time.time >= _decisionEndsAt) SelectOrder(DefaultOrderForMomentum(CurrentMomentum()));
            }

            if (!_roundEffectApplied && IsOrderActiveForRound(_activeOrder, _round, _activeThroughRound) && Time.time >= _effectAt)
            {
                ApplyRoundOrderEffect();
                _roundEffectApplied = true;
            }

            TryRewardResolvedCommandOperation();
        }

        public static bool HasDecisionForRound(int round)
        {
            if (round < FirstDecisionRound || round > 100 || round % 10 == 0) return false;
            return (round - FirstDecisionRound) % DecisionInterval == 0;
        }

        public static int EligibleDecisionCount()
        {
            int count = 0;
            for (int round = 1; round <= 100; round++) if (HasDecisionForRound(round)) count++;
            return count;
        }

        public static int ActiveUntilForDecisionRound(int round)
        {
            return Mathf.Clamp(round + OrderDurationRounds, 1, 100);
        }

        public static bool IsOrderActiveForRound(TheaterOrderKind kind, int round, int throughRound)
        {
            return kind != TheaterOrderKind.None && round >= 1 && round <= throughRound;
        }

        public static int AssaultShellCount(int round)
        {
            return Mathf.Clamp(round >= 70 ? AssaultShellsLate : AssaultShellsEarly, 1, MaxAssaultShellsPerRound);
        }

        public static TheaterOrderKind DefaultOrderForMomentum(int momentum)
        {
            if (momentum <= -1) return TheaterOrderKind.Fortify;
            if (momentum >= 2) return TheaterOrderKind.Assault;
            return TheaterOrderKind.Interdiction;
        }

        public static int CommandSupportBonus(TheaterOrderKind order)
        {
            return order == TheaterOrderKind.Assault ? 1 : 0;
        }

        public static int CommandRelayPressureBonus(TheaterOrderKind order)
        {
            return order == TheaterOrderKind.Interdiction ? 1 : 0;
        }

        public static int CommandRewardBonus(TheaterOrderKind order)
        {
            return order == TheaterOrderKind.None ? 0 : OperationRewardBonus;
        }

        private void OpenDecision()
        {
            _decisionOpen = true;
            _decisionEndsAt = Time.time + DecisionSeconds;
            _status = "THEATER ORDER // [1] ASSAULT  [2] INTERDICTION  [3] FORTIFY";
            _statusUntil = _decisionEndsAt;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.24f, -0.08f);
        }

        private void SelectOrder(TheaterOrderKind order)
        {
            if (!_decisionOpen || order == TheaterOrderKind.None) return;
            _decisionOpen = false;
            _activeOrder = order;
            _activeThroughRound = ActiveUntilForDecisionRound(_round);
            _roundEffectApplied = false;
            _effectAt = Time.time + 0.8f;
            _status = "ORDER CONFIRMED // " + OrderLabel(order) + " // ACTIVE THROUGH ROUND " + _activeThroughRound;
            _statusUntil = Time.time + 4.5f;
            VisualFactory.RingPulse(CombatRoster.Player != null ? (Vector2)CombatRoster.Player.transform.position : Vector2.zero, OrderColor(order), 1.35f);
            BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.34f, 0f);
        }

        private void ApplyRoundOrderEffect()
        {
            switch (_activeOrder)
            {
                case TheaterOrderKind.Assault:
                    ApplyAssault();
                    break;
                case TheaterOrderKind.Interdiction:
                    ApplyInterdiction();
                    break;
                case TheaterOrderKind.Fortify:
                    ApplyFortify();
                    break;
            }
        }

        private void ApplyAssault()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int shells = AssaultShellCount(_round);
            int fired = 0;
            for (int pass = 0; pass < 3 && fired < shells; pass++)
            {
                for (int i = 0; i < enemies.Length && fired < shells; i++)
                {
                    EnemyTank enemy = enemies[i];
                    if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                    bool priority = pass == 0 ? (enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Heavy) :
                                    pass == 1 ? (enemy.Kind == EnemyKind.Elite || enemy.Kind == EnemyKind.Sniper) : true;
                    if (!priority) continue;
                    Vector2 target = enemy.transform.position;
                    Vector2 origin = target + new Vector2(fired == 0 ? -0.26f : 0.26f, 4.9f);
                    _game.SpawnProjectile(origin, Vector2.down, Team.Player, 2, 11.0f, new Color(1f, 0.58f, 0.18f), AmmoType.Explosive);
                    VisualFactory.RingPulse(target, new Color(1f, 0.58f, 0.18f), 0.70f);
                    fired++;
                }
            }
            _status = "ASSAULT ORDER // " + fired + " FIRE-SUPPORT SHELL" + (fired == 1 ? string.Empty : "S") + " COMMITTED";
            _statusUntil = Time.time + 2.8f;
        }

        private void ApplyInterdiction()
        {
            Health target = FindInfrastructureTarget();
            if (target == null || target.IsDead)
            {
                _status = "INTERDICTION ORDER // NO LIVE COMMAND/LOGISTICS TARGET";
                _statusUntil = Time.time + 2.4f;
                return;
            }

            Vector2 targetPos = target.transform.position;
            Vector2 origin = targetPos + new Vector2(-3.4f, 2.8f);
            Vector2 direction = (targetPos - origin).normalized;
            _game.SpawnProjectile(origin, direction, Team.Player, 2, 12.2f, new Color(0.22f, 0.82f, 1f), AmmoType.ArmorPiercing);
            VisualFactory.RingPulse(targetPos, new Color(0.22f, 0.82f, 1f), 0.82f);
            _status = "INTERDICTION ORDER // FIRE MISSION ON " + target.name;
            _statusUntil = Time.time + 2.8f;
        }

        private void ApplyFortify()
        {
            PlayerTank player = CombatRoster.Player;
            Health eagle = CombatRoster.Eagle;
            if (player != null && player.Health != null && !player.Health.IsDead)
                player.Health.Heal(FortifyPlayerHeal);
            if (eagle != null && !eagle.IsDead)
                eagle.Heal(FortifyEagleHeal);

            if (player != null) VisualFactory.RingPulse(player.transform.position, new Color(0.24f, 1f, 0.48f), 1.0f);
            if (eagle != null) VisualFactory.RingPulse(eagle.transform.position, new Color(0.24f, 1f, 0.48f), 1.2f);
            _status = "FORTIFY ORDER // FIELD REPAIR + ORZEŁEK SUSTAIN";
            _statusUntil = Time.time + 2.8f;
        }

        private Health FindInfrastructureTarget()
        {
            Health[] health = RuntimeBattleRegistry.HealthSnapshot;
            Health logistics = null;
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || h.IsDead || h.Team != Team.Enemy) continue;
                string n = h.gameObject.name;
                if (n == "ENEMY_THEATER_COMMAND_RELAY_V100") return h;
                if (n.StartsWith("ENEMY_LOGISTICS_", StringComparison.Ordinal)) logistics = h;
            }
            return logistics;
        }

        private void TryRewardResolvedCommandOperation()
        {
            CombinedArmsCampaignCommandDirector command = CombinedArmsCampaignCommandDirector.Instance;
            if (command == null || command.CurrentPhase != CampaignCommandPhase.Resolved) return;
            if (_lastRewardedCommandRound == _round) return;
            if (!IsOrderActiveForRound(_activeOrder, _round, _activeThroughRound)) return;

            _lastRewardedCommandRound = _round;
            int bonus = CommandRewardBonus(_activeOrder);
            WarEconomyDirector.AwardMissionBonds(bonus, "THEATER ORDER " + OrderLabel(_activeOrder));
            _status = "COMMAND OPERATION COMPLETE // " + OrderLabel(_activeOrder) + " BONUS +" + bonus + " BONDS";
            _statusUntil = Time.time + 3.2f;
        }

        private static int CurrentMomentum()
        {
            CombinedArmsCampaignCommandDirector command = CombinedArmsCampaignCommandDirector.Instance;
            return command != null ? command.CommandMomentum : 0;
        }

        private void ResetRun()
        {
            _round = -1;
            _activeOrder = TheaterOrderKind.None;
            _activeThroughRound = 0;
            _decisionOpen = false;
            _roundEffectApplied = false;
            _lastRewardedCommandRound = -1;
            _status = string.Empty;
        }

        private static string OrderLabel(TheaterOrderKind order)
        {
            switch (order)
            {
                case TheaterOrderKind.Assault: return "ASSAULT";
                case TheaterOrderKind.Interdiction: return "INTERDICTION";
                case TheaterOrderKind.Fortify: return "FORTIFY";
                default: return "NONE";
            }
        }

        private static Color OrderColor(TheaterOrderKind order)
        {
            switch (order)
            {
                case TheaterOrderKind.Assault: return new Color(1f, 0.38f, 0.16f);
                case TheaterOrderKind.Interdiction: return new Color(0.20f, 0.82f, 1f);
                case TheaterOrderKind.Fortify: return new Color(0.24f, 1f, 0.48f);
                default: return Color.white;
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.28f, 0.88f, 1f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            bool show = _decisionOpen || Time.time < _statusUntil || _activeOrder != TheaterOrderKind.None;
            if (!show) return;
            EnsureStyles();

            Rect box = new Rect(14f, Screen.height - 112f, 560f, 96f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 12f, box.y + 8f, 530f, 22f), "THEATER COMMAND // v10.1", _header);
            if (_decisionOpen)
            {
                float left = Mathf.Max(0f, _decisionEndsAt - Time.time);
                GUI.Label(new Rect(box.x + 12f, box.y + 34f, 530f, 20f), "Choose doctrine: 1 ASSAULT  •  2 INTERDICTION  •  3 FORTIFY", _body);
                GUI.Label(new Rect(box.x + 12f, box.y + 56f, 530f, 20f), "Decision window " + left.ToString("0.0") + "s // default adapts to command momentum", _body);
            }
            else
            {
                GUI.Label(new Rect(box.x + 12f, box.y + 34f, 530f, 20f), "ACTIVE ORDER: " + OrderLabel(_activeOrder) + " // THROUGH ROUND " + _activeThroughRound, _body);
                GUI.Label(new Rect(box.x + 12f, box.y + 56f, 530f, 20f), _status, _body);
            }
        }
    }
}
