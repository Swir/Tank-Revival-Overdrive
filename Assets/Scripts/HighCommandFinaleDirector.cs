using System.Reflection;
using UnityEngine;

namespace TankRevival
{
    public enum FinalObjectiveType
    {
        None = 0,
        HqAssault = 1,
        CommandIsolation = 2,
        EvacuationDenial = 3
    }

    public enum CampaignEpilogueOutcome
    {
        None = 0,
        DecisiveVictory = 1,
        HardWonVictory = 2,
        PyrrhicVictory = 3,
        FightingRetreat = 4,
        CommandEscaped = 5,
        Defeat = 6
    }

    [DefaultExecutionOrder(557)]
    public sealed class HighCommandFinaleDirector : MonoBehaviour
    {
        public const int ObjectiveStartRound = 97;
        public const int ObjectiveEndRound = 99;
        public const int FinalRound = 100;
        public const int AdvantageHqHealth = 16;
        public const int ContestedHqHealth = 20;
        public const int CrisisHqHealth = 24;
        public const int MaxSupportShellsPerRound = 2;
        public const int MaxEnemyPressureShellsPerRound = 1;
        public const int MaxHqRelocations = 2;
        public const int MaxFinaleRewardBonds = 8;
        public const int IsolationHealthPercent = 50;

        private static readonly FieldInfo GameStateField = typeof(TankGame).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
        private static HighCommandFinaleDirector _instance;

        private TankGame _game;
        private GameObject _hq;
        private Health _hqHealth;
        private int _round = -1;
        private int _relocations;
        private int _hqInitialHealth;
        private bool _objectiveResolved;
        private bool _objectiveSuccess;
        private bool _epilogueResolved;
        private bool _sawFinalBattle;
        private FinalWarState _warState = FinalWarState.Contested;
        private FinalObjectiveType _objective = FinalObjectiveType.None;
        private CampaignEpilogueOutcome _epilogue = CampaignEpilogueOutcome.None;
        private string _banner = string.Empty;
        private float _bannerUntil;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static HighCommandFinaleDirector Instance => _instance;
        public FinalObjectiveType Objective => _objective;
        public CampaignEpilogueOutcome Epilogue => _epilogue;
        public bool ObjectiveResolved => _objectiveResolved;
        public bool ObjectiveSucceeded => _objectiveSuccess;
        public Health ActiveHqHealth => _hqHealth;

        public static bool ConfigurationValid =>
            ObjectiveStartRound == 97 && ObjectiveEndRound == 99 && FinalRound == 100 &&
            AdvantageHqHealth >= 14 && AdvantageHqHealth < ContestedHqHealth &&
            ContestedHqHealth < CrisisHqHealth && CrisisHqHealth <= 26 &&
            MaxSupportShellsPerRound >= 1 && MaxSupportShellsPerRound <= 2 &&
            MaxEnemyPressureShellsPerRound == 1 && MaxHqRelocations == 2 &&
            MaxFinaleRewardBonds >= 6 && MaxFinaleRewardBonds <= 8 &&
            IsolationHealthPercent == 50 && GameStateField != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<HighCommandFinaleDirector>() != null) return;
            GameObject go = new GameObject("HighCommandFinaleDirector_v10_8");
            DontDestroyOnLoad(go);
            go.AddComponent<HighCommandFinaleDirector>();
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
            ClearHq(false);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;

            if (!_game.IsPlaying)
            {
                if (_sawFinalBattle && !_epilogueResolved)
                    ResolveEpilogue(IsCampaignVictory(_game));
                else if (_round >= 0 && _round < FinalRound)
                    ResetRun();
                return;
            }

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (round == _round) return;
            _round = round;

            WarStateEndgameDirector endgame = WarStateEndgameDirector.Instance;
            if (endgame != null) _warState = endgame.WarState;
            _objective = ObjectiveForState(_warState);

            if (round == ObjectiveStartRound)
                BeginFinalObjective();
            else if (round >= ObjectiveStartRound && round <= ObjectiveEndRound)
                ExecuteObjectiveRound(round);
            else if (round == FinalRound)
            {
                _sawFinalBattle = true;
                ResolveObjectiveAtFinalBattle();
            }
        }

        private void BeginFinalObjective()
        {
            ClearHq(false);
            _objectiveResolved = false;
            _objectiveSuccess = false;
            _epilogueResolved = false;
            _sawFinalBattle = false;
            _relocations = 0;
            SpawnHighCommandHq();
            ExecuteObjectiveRound(ObjectiveStartRound);
            _banner = "FINAL OBJECTIVE // " + ObjectiveLabel(_objective) + " // HIGH COMMAND HQ LOCATED";
            _bannerUntil = Time.unscaledTime + 6f;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.34f, -0.04f);
        }

        private void SpawnHighCommandHq()
        {
            int lane = InitialLaneForState(_warState);
            Vector2 lanePos = DynamicFrontlineTerritoryDirector.LanePosition(lane);
            Vector2 pos = new Vector2(lanePos.x, 4.55f);

            _hq = new GameObject("ENEMY_HIGH_COMMAND_HQ_V108");
            _hq.transform.position = pos;
            Color accent = StateColor(_warState);
            VisualFactory.Rect("HQBase", _hq.transform, new Vector2(2.00f, 1.28f), new Color(0.09f, 0.10f, 0.13f), Vector3.zero, 8);
            VisualFactory.Rect("HQCore", _hq.transform, new Vector2(1.35f, 0.80f), new Color(0.22f, 0.24f, 0.29f), new Vector3(0f, 0.04f, 0f), 9);
            VisualFactory.Rect("HQLeftBunker", _hq.transform, new Vector2(0.42f, 0.92f), accent, new Vector3(-0.72f, 0.05f, 0f), 10);
            VisualFactory.Rect("HQRightBunker", _hq.transform, new Vector2(0.42f, 0.92f), accent, new Vector3(0.72f, 0.05f, 0f), 10);
            VisualFactory.Rect("HQAntenna", _hq.transform, new Vector2(0.11f, 1.10f), Color.white, new Vector3(0f, 0.95f, 0f), 11);
            VisualFactory.Disc("HQBeacon", _hq.transform, new Vector2(0.34f, 0.34f), accent, new Vector3(0f, 1.55f, 0f), 12);

            BoxCollider2D col = _hq.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.94f, 1.22f);
            _hqHealth = _hq.AddComponent<Health>();
            _hqInitialHealth = HqHealthForState(_warState);
            _hqHealth.Initialize(Team.Enemy, _hqInitialHealth);
            _hqHealth.Damaged += OnHqDamaged;
            _hqHealth.Died += OnHqDestroyed;
            VisualFactory.RingPulse(pos, accent, 1.85f);
        }

        private void ExecuteObjectiveRound(int round)
        {
            if (_objectiveResolved || _hqHealth == null || _hqHealth.IsDead) return;

            if (_objective == FinalObjectiveType.HqAssault)
            {
                FirePlayerSupport(MaxSupportShellsPerRound, AmmoType.ArmorPiercing);
            }
            else if (_objective == FinalObjectiveType.CommandIsolation)
            {
                if (IsHqIsolated(_hqHealth.Current, _hqInitialHealth))
                {
                    ResolveObjective(true, "COMMAND ISOLATED");
                    return;
                }
                FirePlayerSupport(1, AmmoType.ArmorPiercing);
            }
            else if (_objective == FinalObjectiveType.EvacuationDenial)
            {
                if (round > ObjectiveStartRound && _relocations < MaxHqRelocations)
                    RelocateHq(round);
                FireEnemyPressure(MaxEnemyPressureShellsPerRound);
            }
        }

        private void RelocateHq(int round)
        {
            if (_hq == null) return;
            int lane = (InitialLaneForState(_warState) + _relocations + 1) % 3;
            Vector2 lanePos = DynamicFrontlineTerritoryDirector.LanePosition(lane);
            Vector2 next = new Vector2(lanePos.x, 4.25f - 0.25f * _relocations);
            _hq.transform.position = next;
            _relocations++;
            VisualFactory.RingPulse(next, StateColor(_warState), 1.4f);
            _banner = "HQ EVACUATION // RELOCATION " + _relocations + "/" + MaxHqRelocations + " // DENY ESCAPE";
            _bannerUntil = Time.unscaledTime + 4f;
        }

        private void OnHqDamaged(Health health, int amount)
        {
            if (health == null || health.IsDead) return;
            VisualFactory.RingPulse(health.transform.position, StateColor(_warState), 0.62f);
            if (_objective == FinalObjectiveType.CommandIsolation && IsHqIsolated(health.Current, _hqInitialHealth))
            {
                ResolveObjective(true, "COMMAND ISOLATED");
                return;
            }
            _banner = "HIGH COMMAND HQ HIT // " + health.Current + "/" + health.Maximum + " HP";
            _bannerUntil = Time.unscaledTime + 2.4f;
        }

        private void OnHqDestroyed(Health health)
        {
            ResolveObjective(true, _objective == FinalObjectiveType.EvacuationDenial ? "EVACUATION DENIED" : "HIGH COMMAND HQ DESTROYED");
        }

        private void ResolveObjectiveAtFinalBattle()
        {
            if (!_objectiveResolved)
            {
                bool success = false;
                if (_objective == FinalObjectiveType.CommandIsolation && _hqHealth != null && !_hqHealth.IsDead)
                    success = IsHqIsolated(_hqHealth.Current, _hqInitialHealth);
                else if (_hqHealth == null || _hqHealth.IsDead)
                    success = true;
                ResolveObjective(success, success ? "FINAL OBJECTIVE SECURED" : "HIGH COMMAND ENTERS FINAL BATTLE");
            }

            ClearHq(!_objectiveSuccess);
            _banner = _objectiveSuccess
                ? "ROUND 100 // HIGH COMMAND DISRUPTED // BOSS AUTHORITY UNCHANGED"
                : "ROUND 100 // HIGH COMMAND SURVIVED // BOSS AUTHORITY UNCHANGED";
            _bannerUntil = Time.unscaledTime + 6f;
        }

        private void ResolveObjective(bool success, string reason)
        {
            if (_objectiveResolved) return;
            _objectiveResolved = true;
            _objectiveSuccess = success;
            if (success)
            {
                int reward = RewardForObjective(_objective, _warState);
                if (reward > 0) WarEconomyDirector.AwardMissionBonds(reward, "FINAL HIGH COMMAND OBJECTIVE");
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.38f, 0.02f);
            }
            else
            {
                BattleAudio.PlayGlobal(SoundCue.Emp, 0.28f, -0.06f);
            }
            _banner = reason + " // " + (_objectiveSuccess ? "SUCCESS" : "FAILED");
            _bannerUntil = Time.unscaledTime + 5f;
        }

        private void ResolveEpilogue(bool campaignWon)
        {
            _epilogueResolved = true;
            _epilogue = ResolveEpilogueOutcome(campaignWon, _objectiveSuccess, _warState);
            _banner = "CAMPAIGN EPILOGUE // " + EpilogueLabel(_epilogue) + " // " + ObjectiveLabel(_objective);
            _bannerUntil = Time.unscaledTime + 12f;
            if (campaignWon && _objectiveSuccess)
                BattleAudio.PlayGlobal(SoundCue.RoundClear, 0.42f, 0.04f);
        }

        private int FirePlayerSupport(int cap, AmmoType ammo)
        {
            if (_game == null) return 0;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int fired = 0;
            for (int i = 0; i < enemies.Length && fired < Mathf.Min(cap, MaxSupportShellsPerRound); i++)
            {
                EnemyTank e = enemies[i];
                if (e == null || e.Health == null || e.Health.IsDead) continue;
                if (e.Kind != EnemyKind.Siege && e.Kind != EnemyKind.Heavy && e.Kind != EnemyKind.Elite && e.Kind != EnemyKind.Sniper) continue;
                Vector2 target = e.transform.position;
                Vector2 origin = target + new Vector2(fired == 0 ? -0.35f : 0.35f, 4.5f);
                _game.SpawnProjectile(origin, Vector2.down, Team.Player, 1, 11f, AmmoDatabase.Color(ammo), ammo);
                fired++;
            }
            return fired;
        }

        private int FireEnemyPressure(int cap)
        {
            if (_game == null) return 0;
            Health eagle = CombatRoster.Eagle;
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (eagle == null || eagle.IsDead) return 0;
            int fired = 0;
            for (int i = 0; i < enemies.Length && fired < Mathf.Min(cap, MaxEnemyPressureShellsPerRound); i++)
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

        private void ClearHq(bool escaped)
        {
            if (_hqHealth != null)
            {
                _hqHealth.Damaged -= OnHqDamaged;
                _hqHealth.Died -= OnHqDestroyed;
            }
            if (_hq != null)
            {
                if (escaped) VisualFactory.RingPulse(_hq.transform.position, new Color(1f, 0.38f, 0.16f), 1.8f);
                Destroy(_hq);
            }
            _hq = null;
            _hqHealth = null;
        }

        private void ResetRun()
        {
            ClearHq(false);
            _round = -1;
            _relocations = 0;
            _hqInitialHealth = 0;
            _objectiveResolved = false;
            _objectiveSuccess = false;
            _epilogueResolved = false;
            _sawFinalBattle = false;
            _warState = FinalWarState.Contested;
            _objective = FinalObjectiveType.None;
            _epilogue = CampaignEpilogueOutcome.None;
            _banner = string.Empty;
        }

        public static FinalObjectiveType ObjectiveForState(FinalWarState state)
        {
            if (state == FinalWarState.Advantage) return FinalObjectiveType.HqAssault;
            if (state == FinalWarState.Crisis) return FinalObjectiveType.EvacuationDenial;
            return FinalObjectiveType.CommandIsolation;
        }

        public static int HqHealthForState(FinalWarState state)
        {
            if (state == FinalWarState.Advantage) return AdvantageHqHealth;
            if (state == FinalWarState.Crisis) return CrisisHqHealth;
            return ContestedHqHealth;
        }

        public static bool IsFinalObjectiveRound(int round) => round >= ObjectiveStartRound && round <= ObjectiveEndRound;

        public static bool IsHqIsolated(int currentHealth, int maximumHealth)
        {
            if (maximumHealth <= 0) return false;
            return currentHealth * 100 <= maximumHealth * IsolationHealthPercent;
        }

        public static int RewardForObjective(FinalObjectiveType objective, FinalWarState state)
        {
            if (objective == FinalObjectiveType.HqAssault) return Mathf.Min(MaxFinaleRewardBonds, state == FinalWarState.Advantage ? 8 : 6);
            if (objective == FinalObjectiveType.CommandIsolation) return 6;
            if (objective == FinalObjectiveType.EvacuationDenial) return 7;
            return 0;
        }

        public static CampaignEpilogueOutcome ResolveEpilogueOutcome(bool campaignWon, bool objectiveSuccess, FinalWarState state)
        {
            if (!campaignWon)
                return state == FinalWarState.Crisis ? CampaignEpilogueOutcome.Defeat : CampaignEpilogueOutcome.FightingRetreat;
            if (objectiveSuccess && state == FinalWarState.Advantage) return CampaignEpilogueOutcome.DecisiveVictory;
            if (objectiveSuccess) return CampaignEpilogueOutcome.HardWonVictory;
            if (state == FinalWarState.Crisis) return CampaignEpilogueOutcome.PyrrhicVictory;
            return CampaignEpilogueOutcome.CommandEscaped;
        }

        private static int InitialLaneForState(FinalWarState state)
        {
            if (state == FinalWarState.Advantage) return 1;
            if (state == FinalWarState.Crisis) return 2;
            return 0;
        }

        private static bool IsCampaignVictory(TankGame game)
        {
            if (game == null || GameStateField == null) return false;
            object value = GameStateField.GetValue(game);
            return value != null && value.ToString() == "Victory";
        }

        private static string ObjectiveLabel(FinalObjectiveType objective)
        {
            if (objective == FinalObjectiveType.HqAssault) return "HQ ASSAULT";
            if (objective == FinalObjectiveType.CommandIsolation) return "COMMAND ISOLATION";
            if (objective == FinalObjectiveType.EvacuationDenial) return "EVACUATION DENIAL";
            return "NO OBJECTIVE";
        }

        private static string EpilogueLabel(CampaignEpilogueOutcome outcome)
        {
            switch (outcome)
            {
                case CampaignEpilogueOutcome.DecisiveVictory: return "DECISIVE VICTORY — HIGH COMMAND BROKEN";
                case CampaignEpilogueOutcome.HardWonVictory: return "HARD-WON VICTORY — COMMAND NEUTRALIZED";
                case CampaignEpilogueOutcome.PyrrhicVictory: return "PYRRHIC VICTORY — FRONT HELD";
                case CampaignEpilogueOutcome.FightingRetreat: return "FIGHTING RETREAT — WAR CONTINUES";
                case CampaignEpilogueOutcome.CommandEscaped: return "VICTORY — HIGH COMMAND ESCAPED";
                case CampaignEpilogueOutcome.Defeat: return "DEFEAT — HIGH COMMAND SURVIVES";
                default: return "WAR UNRESOLVED";
            }
        }

        private static Color StateColor(FinalWarState state)
        {
            if (state == FinalWarState.Advantage) return new Color(0.18f, 0.86f, 1f);
            if (state == FinalWarState.Crisis) return new Color(1f, 0.28f, 0.14f);
            return new Color(1f, 0.72f, 0.16f);
        }

        private void OnGUI()
        {
            if (Time.unscaledTime >= _bannerUntil) return;
            EnsureStyles();
            float width = Mathf.Min(680f, Screen.width - 32f);
            Rect box = new Rect((Screen.width - width) * 0.5f, 158f, width, 68f);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f));
            GUILayout.Label("HIGH COMMAND FINALE // v10.8", _titleStyle);
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
