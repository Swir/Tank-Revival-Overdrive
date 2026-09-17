using UnityEngine;

namespace TankRevival
{
    public enum EncounterAct
    {
        Mobilization = 0,
        Breakthrough = 1,
        Siege = 2,
        Counteroffensive = 3,
        Overdrive = 4
    }

    public enum EncounterDoctrine
    {
        ArmoredAssault = 0,
        HunterKiller = 1,
        ArtillerySiege = 2,
        LogisticsInterdiction = 3,
        ElectronicSuppression = 4,
        RouteBreakthrough = 5,
        CombinedArms = 6,
        BossGauntlet = 7
    }

    /// <summary>
    /// Immutable-by-convention round plan generated without Unity scene state.
    /// TankGame remains the round/spawn authority; this plan is the deterministic v13.0 doctrine contract.
    /// </summary>
    public struct EncounterPlan
    {
        public int Round;
        public EncounterAct Act;
        public EncounterDoctrine Doctrine;
        public EnemyKind PrimaryEnemy;
        public int EnemyCount;
        public int MaxAlive;
        public float SpawnInterval;
        public bool BossRound;
        public int BossPhaseCount;
        public float EaglePressure;
        public float RoutePressure;
        public float LogisticsPressure;
        public float EwPressure;
        public float SigintWindow;
        public int Signature;

        public string CompactLabel
        {
            get { return Act + " / " + Doctrine; }
        }
    }

    /// <summary>
    /// Pure deterministic planner for all 100 campaign rounds. No scene lookups, random state, reflection or authority writes.
    /// </summary>
    public static class EncounterPlannerV130
    {
        public const int PlannedRounds = 100;
        public const int MaxEnemyBudget = 62;
        public const int MaxConcurrentEnemies = 14;
        public const float MinSpawnInterval = 0.28f;
        public const float MaxSpawnInterval = 0.76f;

        public static bool ConfigurationValid
        {
            get
            {
                return PlannedRounds == 100 && MaxEnemyBudget <= 62 && MaxConcurrentEnemies <= 14 &&
                       MinSpawnInterval >= 0.24f && MaxSpawnInterval <= 0.90f;
            }
        }

        public static EncounterPlan PlanForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            int actIndex = Mathf.Clamp((round - 1) / 20, 0, 4);
            bool boss = round % 10 == 0;
            EncounterDoctrine doctrine = boss
                ? EncounterDoctrine.BossGauntlet
                : (EncounterDoctrine)((round * 5 + actIndex * 3 + round / 10) % 7);

            float progress = (round - 1f) / (PlannedRounds - 1f);
            float doctrinePressure = DoctrinePressure(doctrine);
            float eaglePressure = Mathf.Clamp01(0.18f + progress * 0.68f + doctrinePressure * 0.14f + (boss ? 0.10f : 0f));
            float routePressure = Mathf.Clamp01(0.12f + progress * 0.56f + RouteBias(doctrine));
            float logisticsPressure = Mathf.Clamp01(0.10f + progress * 0.52f + LogisticsBias(doctrine));
            float ewPressure = Mathf.Clamp01(0.06f + progress * 0.48f + EwBias(doctrine));
            float sigintWindow = Mathf.Clamp(0.92f - progress * 0.37f - ewPressure * 0.12f, 0.34f, 0.92f);

            int baseEnemies = 6 + Mathf.CeilToInt(round * 0.46f);
            int doctrineExtra = doctrine == EncounterDoctrine.ArmoredAssault || doctrine == EncounterDoctrine.CombinedArms ? 3 :
                                doctrine == EncounterDoctrine.RouteBreakthrough ? 2 :
                                doctrine == EncounterDoctrine.BossGauntlet ? -2 : 0;
            int enemyCount = Mathf.Clamp(baseEnemies + doctrineExtra, 6, MaxEnemyBudget);
            int maxAlive = Mathf.Clamp(4 + round / 11 + (doctrine == EncounterDoctrine.ArmoredAssault ? 1 : 0), 4, MaxConcurrentEnemies);
            float interval = Mathf.Clamp(0.76f - progress * 0.35f - doctrinePressure * 0.08f, MinSpawnInterval, MaxSpawnInterval);
            int phases = boss ? BossPhaseWarfareV130.PhaseCountForRound(round) : 0;
            EnemyKind primary = PrimaryEnemyFor(doctrine, round);

            int signature = 17;
            signature = signature * 31 + round;
            signature = signature * 31 + actIndex;
            signature = signature * 31 + (int)doctrine;
            signature = signature * 31 + (int)primary;
            signature = signature * 31 + enemyCount;
            signature = signature * 31 + maxAlive;
            signature = signature * 31 + Mathf.RoundToInt(interval * 1000f);
            signature = signature * 31 + phases;
            signature = signature * 31 + Mathf.RoundToInt(eaglePressure * 1000f);
            signature = signature * 31 + Mathf.RoundToInt(routePressure * 1000f);
            signature = signature * 31 + Mathf.RoundToInt(logisticsPressure * 1000f);
            signature = signature * 31 + Mathf.RoundToInt(ewPressure * 1000f);

            return new EncounterPlan
            {
                Round = round,
                Act = (EncounterAct)actIndex,
                Doctrine = doctrine,
                PrimaryEnemy = primary,
                EnemyCount = enemyCount,
                MaxAlive = maxAlive,
                SpawnInterval = interval,
                BossRound = boss,
                BossPhaseCount = phases,
                EaglePressure = eaglePressure,
                RoutePressure = routePressure,
                LogisticsPressure = logisticsPressure,
                EwPressure = ewPressure,
                SigintWindow = sigintWindow,
                Signature = signature
            };
        }

        /// <summary>
        /// Deterministic support-wave composition consumed by TankGame. No Unity random state, allocations or authority writes.
        /// Boss rounds keep the boss itself in TankGame's existing boss path while this helper produces its escort wave.
        /// </summary>
        public static EnemyKind EnemyForSpawn(EncounterPlan plan, int spawnOrdinal)
        {
            int ordinal = Mathf.Max(0, spawnOrdinal);
            int roll = PositiveMod(unchecked(plan.Signature * 397 + ordinal * 101 + plan.Round * 53), 100);

            if (plan.BossRound)
            {
                if (roll < 24) return plan.Round >= 60 ? EnemyKind.Elite : EnemyKind.Heavy;
                if (roll < 44) return EnemyKind.Siege;
                if (roll < 61) return EnemyKind.Sniper;
                if (roll < 75) return EnemyKind.Fast;
                if (roll < 84) return EnemyKind.Supply;
                return plan.Round >= 40 ? EnemyKind.Heavy : EnemyKind.Basic;
            }

            if (roll < 52) return plan.PrimaryEnemy;
            if (roll < 62 && plan.Round >= 3) return EnemyKind.Supply;

            switch (plan.Doctrine)
            {
                case EncounterDoctrine.ArmoredAssault: return roll < 84 ? EnemyKind.Heavy : EnemyKind.Siege;
                case EncounterDoctrine.HunterKiller: return roll < 82 ? EnemyKind.Fast : EnemyKind.Sniper;
                case EncounterDoctrine.ArtillerySiege: return roll < 84 ? EnemyKind.Siege : EnemyKind.Heavy;
                case EncounterDoctrine.LogisticsInterdiction: return roll < 82 ? EnemyKind.Fast : EnemyKind.Supply;
                case EncounterDoctrine.ElectronicSuppression: return roll < 84 ? EnemyKind.Sniper : EnemyKind.Elite;
                case EncounterDoctrine.RouteBreakthrough: return roll < 84 ? EnemyKind.Fast : EnemyKind.Heavy;
                case EncounterDoctrine.CombinedArms:
                    if (roll < 74) return EnemyKind.Heavy;
                    if (roll < 88) return EnemyKind.Sniper;
                    return plan.Round >= 60 ? EnemyKind.Elite : EnemyKind.Fast;
                default: return EnemyKind.Basic;
            }
        }

        public static EagleDefensePlanV130 EagleDefenseFor(EncounterPlan plan)
        {
            float pressure = Mathf.Clamp01(plan.EaglePressure);
            int tier = pressure >= 0.78f ? 3 : pressure >= 0.52f ? 2 : 1;
            int wallHp = tier == 3 ? 4 : tier == 2 ? 3 : 2;
            return new EagleDefensePlanV130
            {
                Tier = tier,
                WallHitPoints = wallHp,
                SteelSides = tier >= 2,
                SteelCrown = tier >= 3
            };
        }

        public static void BuildAll(EncounterPlan[] destination)
        {
            if (destination == null || destination.Length < PlannedRounds) return;
            for (int i = 0; i < PlannedRounds; i++) destination[i] = PlanForRound(i + 1);
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static float DoctrinePressure(EncounterDoctrine doctrine)
        {
            switch (doctrine)
            {
                case EncounterDoctrine.ArmoredAssault: return 0.82f;
                case EncounterDoctrine.HunterKiller: return 0.70f;
                case EncounterDoctrine.ArtillerySiege: return 0.76f;
                case EncounterDoctrine.LogisticsInterdiction: return 0.62f;
                case EncounterDoctrine.ElectronicSuppression: return 0.66f;
                case EncounterDoctrine.RouteBreakthrough: return 0.78f;
                case EncounterDoctrine.CombinedArms: return 0.90f;
                default: return 1.0f;
            }
        }

        private static float RouteBias(EncounterDoctrine doctrine)
        {
            return doctrine == EncounterDoctrine.RouteBreakthrough ? 0.28f : doctrine == EncounterDoctrine.HunterKiller ? 0.16f : 0f;
        }

        private static float LogisticsBias(EncounterDoctrine doctrine)
        {
            return doctrine == EncounterDoctrine.LogisticsInterdiction ? 0.32f : doctrine == EncounterDoctrine.CombinedArms ? 0.14f : 0f;
        }

        private static float EwBias(EncounterDoctrine doctrine)
        {
            return doctrine == EncounterDoctrine.ElectronicSuppression ? 0.36f : doctrine == EncounterDoctrine.CombinedArms ? 0.16f : 0f;
        }

        private static EnemyKind PrimaryEnemyFor(EncounterDoctrine doctrine, int round)
        {
            switch (doctrine)
            {
                case EncounterDoctrine.HunterKiller: return round >= 30 ? EnemyKind.Elite : EnemyKind.Fast;
                case EncounterDoctrine.ArtillerySiege: return EnemyKind.Siege;
                case EncounterDoctrine.LogisticsInterdiction: return EnemyKind.Supply;
                case EncounterDoctrine.ElectronicSuppression: return round >= 55 ? EnemyKind.Elite : EnemyKind.Sniper;
                case EncounterDoctrine.RouteBreakthrough: return EnemyKind.Fast;
                case EncounterDoctrine.CombinedArms: return round >= 45 ? EnemyKind.Heavy : EnemyKind.Basic;
                case EncounterDoctrine.BossGauntlet: return EnemyKind.Boss;
                default: return round >= 25 ? EnemyKind.Heavy : EnemyKind.Basic;
            }
        }
    }

    public struct EagleDefensePlanV130
    {
        public int Tier;
        public int WallHitPoints;
        public bool SteelSides;
        public bool SteelCrown;
    }

    /// <summary>Pure phase policy used by the real boss weapon authority.</summary>
    public static class BossPhaseWarfareV130
    {
        public const int MaxPhases = 4;

        public static int PhaseCountForRound(int round)
        {
            return round >= 50 ? 4 : 3;
        }

        public static int ResolvePhase(int round, float healthRatio, bool mobilityCritical, bool weaponDisabled)
        {
            int count = PhaseCountForRound(round);
            float hp = Mathf.Clamp01(healthRatio);
            int phase = 1;
            if (hp <= 0.70f) phase = 2;
            if (hp <= 0.42f) phase = 3;
            if (count >= 4 && hp <= 0.18f) phase = 4;
            if ((mobilityCritical || weaponDisabled) && phase < count) phase++;
            return Mathf.Clamp(phase, 1, count);
        }

        public static float CadenceScale(int phase)
        {
            switch (Mathf.Clamp(phase, 1, MaxPhases))
            {
                case 2: return 0.86f;
                case 3: return 0.72f;
                case 4: return 0.60f;
                default: return 1f;
            }
        }

        public static int PatternTierBonus(int phase)
        {
            return Mathf.Clamp(phase - 1, 0, 3);
        }

        public static string PhaseLabel(int phase, int phaseCount)
        {
            if (phase >= phaseCount) return "OVERDRIVE";
            if (phase == 3) return "BREAKPOINT";
            if (phase == 2) return "ESCALATION";
            return "CONTACT";
        }
    }

    /// <summary>
    /// Read-mostly runtime service for v13.0 encounter telemetry. It never spawns, damages, moves or awards economy.
    /// BossWeaponController reports phase transitions here so the HUD/integration layer can consume one bounded state object.
    /// </summary>
    public sealed class EncounterWarfareDirector : MonoBehaviour
    {
        public const int MaxTrackedRounds = 100;
        public const int MaxTrackedBosses = 2;
        public static EncounterWarfareDirector Instance { get; private set; }
        public static bool ConfigurationValid { get { return MaxTrackedRounds == 100 && MaxTrackedBosses <= 2 && EncounterPlannerV130.ConfigurationValid; } }

        private TankGame _game;
        private int _observedRound = -1;
        private float _nextSearchAt;
        private int _bossPhase;
        private int _bossPhaseCount;
        private EncounterPlan _currentPlan;

        public EncounterPlan CurrentPlan { get { return _currentPlan; } }
        public int BossPhase { get { return _bossPhase; } }
        public int BossPhaseCount { get { return _bossPhaseCount; } }
        public string BossPhaseLabel { get { return _bossPhase > 0 ? BossPhaseWarfareV130.PhaseLabel(_bossPhase, _bossPhaseCount) : string.Empty; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EncounterWarfareDirector>() != null) return;
            GameObject go = new GameObject("EncounterWarfareDirector_v13_0");
            DontDestroyOnLoad(go);
            go.AddComponent<EncounterWarfareDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_game == null && Time.unscaledTime >= _nextSearchAt)
            {
                _nextSearchAt = Time.unscaledTime + 0.75f;
                _game = FindAnyObjectByType<TankGame>();
            }
            if (_game == null || !_game.IsPlaying) return;
            int round = Mathf.Clamp(_game.CurrentRound, 1, EncounterPlannerV130.PlannedRounds);
            if (round == _observedRound) return;
            _observedRound = round;
            _currentPlan = EncounterPlannerV130.PlanForRound(round);
            _bossPhase = 0;
            _bossPhaseCount = _currentPlan.BossPhaseCount;
        }

        public void ReportBossPhase(int round, int phase, int phaseCount)
        {
            if (round < 1 || round > EncounterPlannerV130.PlannedRounds) return;
            if (_observedRound != round)
            {
                _observedRound = round;
                _currentPlan = EncounterPlannerV130.PlanForRound(round);
            }
            _bossPhaseCount = Mathf.Clamp(phaseCount, 1, BossPhaseWarfareV130.MaxPhases);
            _bossPhase = Mathf.Clamp(phase, 1, _bossPhaseCount);
        }
    }
}
