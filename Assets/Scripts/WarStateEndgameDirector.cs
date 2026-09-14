using UnityEngine;

namespace TankRevival
{
    public enum FinalWarState
    {
        Contested = 0,
        Advantage = 1,
        Crisis = 2
    }

    public enum EndgamePlan
    {
        None = 0,
        CommandCollapse = 1,
        BreakthroughPursuit = 2,
        DesperateDefense = 3
    }

    public enum EndgamePhase
    {
        None = 0,
        Intelligence = 1,
        Interdiction = 2,
        Breakthrough = 3,
        FinalBattle = 4
    }

    [DefaultExecutionOrder(558)]
    public sealed class WarStateEndgameDirector : MonoBehaviour
    {
        public const int EndgameStartRound = 90;
        public const int FinalRound = 100;
        public const int MaxSupportShellsPerRound = 2;
        public const int MaxRecoveryPerRound = 1;
        public const int MaxEndgameRewardBonds = 8;
        public const int AdvantageThreshold = 2;
        public const int CrisisThreshold = -1;

        private static WarStateEndgameDirector _instance;
        private TankGame _game;
        private int _round = -1;
        private int _lastEffectRound = -1;
        private bool _finalResolved;
        private FinalWarState _warState = FinalWarState.Contested;
        private EndgamePlan _plan = EndgamePlan.None;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static WarStateEndgameDirector Instance => _instance;
        public FinalWarState WarState => _warState;
        public EndgamePlan Plan => _plan;

        public static bool ConfigurationValid =>
            EndgameStartRound == 90 && FinalRound == 100 &&
            MaxSupportShellsPerRound >= 1 && MaxSupportShellsPerRound <= 2 &&
            MaxRecoveryPerRound == 1 && MaxEndgameRewardBonds >= 6 && MaxEndgameRewardBonds <= 8 &&
            AdvantageThreshold == 2 && CrisisThreshold == -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarStateEndgameDirector>() != null) return;
            GameObject go = new GameObject("WarStateEndgameDirector_v10_7");
            DontDestroyOnLoad(go);
            go.AddComponent<WarStateEndgameDirector>();
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
            if (round == _round) return;
            _round = round;

            CounterOffensiveCampaignDirector counter = CounterOffensiveCampaignDirector.Instance;
            if (counter != null)
            {
                _warState = ResolveWarState(counter.OperationalMomentum, counter.CapturedIntel, counter.LastOutcome);
                _plan = ResolveEndgamePlan(_warState, counter.CapturedIntel, counter.LastOutcome);
            }

            if (IsEndgameRound(round)) ExecuteEndgameRound(round);
        }

        private void ExecuteEndgameRound(int round)
        {
            if (_lastEffectRound == round) return;
            _lastEffectRound = round;
            EndgamePhase phase = PhaseForRound(round);

            if (round == FinalRound)
            {
                ResolveFinalBattle();
                return;
            }

            switch (_plan)
            {
                case EndgamePlan.CommandCollapse:
                    ExecuteCommandCollapse(round, phase);
                    break;
                case EndgamePlan.BreakthroughPursuit:
                    ExecuteBreakthroughPursuit(round, phase);
                    break;
                case EndgamePlan.DesperateDefense:
                    ExecuteDesperateDefense(round, phase);
                    break;
            }

            _banner = "ENDGAME " + phase.ToString().ToUpperInvariant() + " // " + _plan.ToString().ToUpperInvariant() + " // WAR " + _warState.ToString().ToUpperInvariant();
            _bannerUntil = Time.unscaledTime + 4f;
        }

        private void ExecuteCommandCollapse(int round, EndgamePhase phase)
        {
            int cap = phase == EndgamePhase.Breakthrough ? MaxSupportShellsPerRound : 1;
            FirePlayerSupport(phase == EndgamePhase.Intelligence ? AmmoType.ArmorPiercing : AmmoType.Explosive, cap);
            if ((round == 92 || round == 96) && CombatRoster.Eagle != null && !CombatRoster.Eagle.IsDead)
                CombatRoster.Eagle.Heal(MaxRecoveryPerRound);
        }

        private void ExecuteBreakthroughPursuit(int round, EndgamePhase phase)
        {
            int cap = phase == EndgamePhase.Breakthrough ? MaxSupportShellsPerRound : 1;
            FirePlayerSupport(AmmoType.ArmorPiercing, cap);
            if (round == 94 || round == 98)
                WarEconomyDirector.AwardMissionBonds(2, "ENDGAME BREAKTHROUGH");
        }

        private void ExecuteDesperateDefense(int round, EndgamePhase phase)
        {
            Health eagle = CombatRoster.Eagle;
            if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum)
                eagle.Heal(MaxRecoveryPerRound);

            if (phase == EndgamePhase.Interdiction || phase == EndgamePhase.Breakthrough)
                FireEnemyPressure(1);
        }

        private void ResolveFinalBattle()
        {
            if (_finalResolved) return;
            _finalResolved = true;

            if (_warState == FinalWarState.Advantage)
            {
                Health eagle = CombatRoster.Eagle;
                if (eagle != null && !eagle.IsDead && eagle.Current < eagle.Maximum)
                    eagle.Heal(MaxRecoveryPerRound);
                FirePlayerSupport(AmmoType.ArmorPiercing, 1);
                WarEconomyDirector.AwardMissionBonds(MaxEndgameRewardBonds, "FINAL WAR ADVANTAGE");
                _banner = "FINAL WAR // HIGH COMMAND COLLAPSING // ADVANTAGE";
            }
            else if (_warState == FinalWarState.Crisis)
            {
                FireEnemyPressure(1);
                _banner = "FINAL WAR // DESPERATE DEFENSE // CRISIS";
            }
            else
            {
                _banner = "FINAL WAR // CONTESTED DECISION";
            }

            _bannerUntil = Time.unscaledTime + 7f;
            BattleAudio.PlayGlobal(_warState == FinalWarState.Advantage ? SoundCue.RoundClear : SoundCue.Emp, 0.34f, 0.04f);
        }

        private int FirePlayerSupport(AmmoType ammo, int cap)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return 0;
            int fired = 0;
            for (int pass = 0; pass < 5 && fired < Mathf.Min(cap, MaxSupportShellsPerRound); pass++)
            {
                EnemyTank target = null;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyTank e = enemies[i];
                    if (e == null || e.Health == null || e.Health.IsDead) continue;
                    if (!PriorityMatches(e.Kind, pass)) continue;
                    target = e;
                    break;
                }
                if (target == null) continue;
                Vector2 origin = _game.PlayerPosition;
                Vector2 delta = (Vector2)target.transform.position - origin;
                if (delta.sqrMagnitude < 0.25f) continue;
                _game.SpawnProjectile(origin + delta.normalized * 0.65f, delta.normalized, Team.Player, 1, ammo == AmmoType.Explosive ? 10f : 12f, AmmoDatabase.Color(ammo), ammo);
                VisualFactory.RingPulse(target.transform.position, new Color(0.2f, 0.82f, 1f), 0.72f);
                fired++;
            }
            return fired;
        }

        private int FireEnemyPressure(int cap)
        {
            Health eagle = CombatRoster.Eagle;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (eagle == null || eagle.IsDead || enemies == null) return 0;
            int fired = 0;
            for (int i = 0; i < enemies.Length && fired < Mathf.Min(cap, MaxSupportShellsPerRound); i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                if (e.Kind != EnemyKind.Siege && e.Kind != EnemyKind.Heavy && e.Kind != EnemyKind.Elite) continue;
                Vector2 origin = e.transform.position;
                Vector2 delta = (Vector2)eagle.transform.position - origin;
                if (delta.sqrMagnitude < 0.25f) continue;
                _game.SpawnProjectile(origin + delta.normalized * 0.65f, delta.normalized, Team.Enemy, 1, 9f, AmmoDatabase.Color(AmmoType.Explosive), AmmoType.Explosive);
                fired++;
            }
            return fired;
        }

        private static bool PriorityMatches(EnemyKind kind, int pass)
        {
            if (pass == 0) return kind == EnemyKind.Siege;
            if (pass == 1) return kind == EnemyKind.Heavy;
            if (pass == 2) return kind == EnemyKind.Elite;
            if (pass == 3) return kind == EnemyKind.Sniper;
            return kind != EnemyKind.Basic && kind != EnemyKind.Fast;
        }

        private void ResetRun()
        {
            _round = -1;
            _lastEffectRound = -1;
            _finalResolved = false;
            _warState = FinalWarState.Contested;
            _plan = EndgamePlan.None;
            _banner = string.Empty;
        }

        public static FinalWarState ResolveWarState(int operationalMomentum, int capturedIntel, CounterOrderOutcome lastOutcome)
        {
            int score = Mathf.Clamp(operationalMomentum, CounterOffensiveCampaignDirector.MinOperationalMomentum, CounterOffensiveCampaignDirector.MaxOperationalMomentum);
            score += Mathf.Clamp(capturedIntel, 0, CounterOffensiveCampaignDirector.MaxCapturedIntel);
            if (lastOutcome == CounterOrderOutcome.Success) score++;
            else if (lastOutcome == CounterOrderOutcome.Failure) score--;
            if (score >= AdvantageThreshold) return FinalWarState.Advantage;
            if (score <= CrisisThreshold) return FinalWarState.Crisis;
            return FinalWarState.Contested;
        }

        public static EndgamePlan ResolveEndgamePlan(FinalWarState state, int capturedIntel, CounterOrderOutcome lastOutcome)
        {
            if (state == FinalWarState.Crisis) return EndgamePlan.DesperateDefense;
            if (state == FinalWarState.Advantage && capturedIntel > 0) return EndgamePlan.CommandCollapse;
            if (state == FinalWarState.Advantage || lastOutcome == CounterOrderOutcome.Success) return EndgamePlan.BreakthroughPursuit;
            return EndgamePlan.DesperateDefense;
        }

        public static bool IsEndgameRound(int round) => round >= EndgameStartRound && round <= FinalRound;

        public static EndgamePhase PhaseForRound(int round)
        {
            if (round < EndgameStartRound || round > FinalRound) return EndgamePhase.None;
            if (round == FinalRound) return EndgamePhase.FinalBattle;
            if (round <= 92) return EndgamePhase.Intelligence;
            if (round <= 96) return EndgamePhase.Interdiction;
            return EndgamePhase.Breakthrough;
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying || Time.unscaledTime >= _bannerUntil) return;
            EnsureStyles();
            float width = Mathf.Min(620f, Screen.width - 32f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 92f, width, 60f);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f));
            GUILayout.Label("WAR STATE // CAMPAIGN ENDGAME", _titleStyle);
            GUILayout.Label(_banner, _bodyStyle);
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
