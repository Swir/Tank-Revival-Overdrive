using UnityEngine;

namespace TankRevival
{
    public enum FireSupportStateV139
    {
        Building = 0,
        Ready = 1,
        Active = 2,
        Cooldown = 3
    }

    public enum FireSupportRoleV139
    {
        Line = 0,
        Scout = 1,
        Bruiser = 2,
        Sniper = 3,
        Elite = 4,
        Officer = 5,
        Boss = 6
    }

    public readonly struct FireSupportProfileV139
    {
        public readonly int Round;
        public readonly int StrikeBudget;
        public readonly float CooldownSeconds;
        public readonly float PriorityBias;
        public readonly int Signature;

        public FireSupportProfileV139(int round, int strikes, float cooldown, float priorityBias, int signature)
        {
            Round = round;
            StrikeBudget = strikes;
            CooldownSeconds = cooldown;
            PriorityBias = priorityBias;
            Signature = signature;
        }
    }

    /// <summary>
    /// Pure deterministic v13.9 command-window model. It owns no Health, spawn, enemy movement,
    /// terrain damage or projectile resolution authority. Runtime support shots are routed through
    /// the canonical ProjectilePool/Projectile path so existing armor, cover and suppression rules win.
    /// </summary>
    public static class BattlefieldFireSupportModelV139
    {
        public const int PlannedRounds = 100;
        public const int MaxTrackedHostiles = 24;
        public const int MaxTelegraphs = 6;
        public const int MaxStrikes = 6;
        public const float SampleCadenceSeconds = 0.25f;
        public const float ReadyPressure = 32f;
        public const float ReadyCharge = 1f;
        public const float ChargeSecondsAtFullPressure = 4.75f;
        public const float ChargeDecayPerSecond = 0.10f;
        public const float ActiveSeconds = 5.50f;
        public const float StrikeCadenceSeconds = 0.82f;
        public const float MinCooldownSeconds = 15f;
        public const float MaxCooldownSeconds = 22f;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 && MaxTrackedHostiles == BattlefieldSuppressionModelV138.MaxTrackedEnemies &&
            MaxTelegraphs > 0 && MaxTelegraphs <= 8 && MaxStrikes > 0 && MaxStrikes <= MaxTelegraphs &&
            SampleCadenceSeconds >= 0.20f && SampleCadenceSeconds <= 0.50f &&
            ReadyPressure >= 25f && ReadyPressure <= 45f && ChargeSecondsAtFullPressure >= 3.5f &&
            ActiveSeconds > 0f && ActiveSeconds <= 6f && StrikeCadenceSeconds >= 0.65f &&
            MinCooldownSeconds >= 12f && MaxCooldownSeconds <= 24f && MinCooldownSeconds < MaxCooldownSeconds;

        public static FireSupportProfileV139 ProfileForRound(int requestedRound)
        {
            int round = Mathf.Clamp(requestedRound, 1, PlannedRounds);
            int band = Mathf.Clamp((round - 1) / 20, 0, 4);
            int strikes = Mathf.Clamp(3 + band / 2 + (round >= 80 ? 1 : 0), 3, MaxStrikes);
            float cooldown = Mathf.Lerp(MaxCooldownSeconds, MinCooldownSeconds, (round - 1f) / 99f);
            float priorityBias = Mathf.Lerp(0.86f, 1.16f, (round - 1f) / 99f);
            int signature = unchecked(139 * 1009 + round * 97 + strikes * 31 + band * 17 + Mathf.RoundToInt(cooldown * 100f));
            return new FireSupportProfileV139(round, strikes, cooldown, priorityBias, signature);
        }

        public static float AdvanceCharge(float current, float globalPressure, float cohesionStress, float deltaSeconds)
        {
            float charge = Mathf.Clamp01(current);
            float dt = Mathf.Max(0f, deltaSeconds);
            if (globalPressure < ReadyPressure)
                return Mathf.Clamp01(charge - ChargeDecayPerSecond * dt);

            float pressure01 = Mathf.InverseLerp(ReadyPressure, BattlefieldSuppressionModelV138.MaxPressure, globalPressure);
            float cohesionBonus = 1f + Mathf.Clamp01(cohesionStress) * 0.25f;
            float gain = dt * Mathf.Lerp(0.55f, 1f, pressure01) * cohesionBonus / ChargeSecondsAtFullPressure;
            return Mathf.Clamp01(charge + gain);
        }

        public static FireSupportRoleV139 RoleFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return FireSupportRoleV139.Scout;
                case EnemyKind.Heavy: return FireSupportRoleV139.Bruiser;
                case EnemyKind.Sniper: return FireSupportRoleV139.Sniper;
                case EnemyKind.Elite: return FireSupportRoleV139.Elite;
                case EnemyKind.Siege:
                case EnemyKind.Supply: return FireSupportRoleV139.Officer;
                case EnemyKind.Boss: return FireSupportRoleV139.Boss;
                default: return FireSupportRoleV139.Line;
            }
        }

        public static float ClassPriority(EnemyKind kind)
        {
            switch (RoleFor(kind))
            {
                case FireSupportRoleV139.Scout: return 1.08f;
                case FireSupportRoleV139.Bruiser: return 1.02f;
                case FireSupportRoleV139.Sniper: return 1.22f;
                case FireSupportRoleV139.Elite: return 1.18f;
                case FireSupportRoleV139.Officer: return 1.14f;
                case FireSupportRoleV139.Boss: return 0.76f;
                default: return 1f;
            }
        }

        public static float TerrainOpportunity(TacticalTerrainPlanV134 plan, Vector2 position)
        {
            if (TacticalTerrainPlannerV134.IsReservedSafeLane(plan, position)) return 0.62f;
            switch (plan.Doctrine)
            {
                case TacticalTerrainDoctrineV134.FortifiedCorridor:
                case TacticalTerrainDoctrineV134.BreachBelt:
                case TacticalTerrainDoctrineV134.SiegeApproach: return 1.12f;
                case TacticalTerrainDoctrineV134.OpenLanes: return 0.94f;
                default: return 1f;
            }
        }

        public static float TargetScore(
            EnemyKind kind,
            float pressure,
            float cohesionSpreadScale,
            float terrainOpportunity,
            int slot,
            FireSupportProfileV139 profile)
        {
            float suppression = Mathf.Clamp01(pressure / BattlefieldSuppressionModelV138.MaxPressure);
            float cohesion = Mathf.Clamp01((cohesionSpreadScale - 1f) / 0.50f);
            float classBias = ClassPriority(kind);
            float tieBreak = (Mathf.Clamp(slot, 0, MaxTrackedHostiles - 1) + 1) * 0.0005f;
            return (0.42f + suppression * 0.38f + cohesion * 0.20f) * classBias *
                Mathf.Clamp(terrainOpportunity, 0.55f, 1.20f) * profile.PriorityBias + tieBreak;
        }

        public static Vector2 AimOffset(FireSupportRoleV139 role, int strikeOrdinal, int roundSignature)
        {
            int h = unchecked(roundSignature * 31 + strikeOrdinal * 97 + (int)role * 53);
            float x = ((h & 7) - 3) * 0.09f;
            float y = (((h >> 3) & 7) - 3) * 0.06f;
            float scale = role == FireSupportRoleV139.Scout ? 1.30f : role == FireSupportRoleV139.Boss ? 0.55f : 1f;
            return new Vector2(x, y) * scale;
        }
    }

    /// <summary>
    /// Earned player fire-support command. Suppression builds readiness; F commits a short bounded
    /// support window. Targeting consumes existing registry/cohesion/terrain/morale data and every
    /// strike is a normal pooled player projectile, preserving canonical collision and damage authority.
    /// </summary>
    [DefaultExecutionOrder(-8860)]
    public sealed class BattlefieldFireSupportDirector : MonoBehaviour
    {
        public static BattlefieldFireSupportDirector Instance { get; private set; }

        private TankGame _game;
        private FireSupportStateV139 _state = FireSupportStateV139.Building;
        private FireSupportProfileV139 _profile;
        private float _charge;
        private float _stateUntil;
        private float _nextSample;
        private float _nextStrike;
        private int _round = -1;
        private int _strikesUsed;
        private int _commandsActivated;
        private int _targetsEvaluated;
        private GUIStyle _hudStyle;

        public FireSupportStateV139 State => _state;
        public float Charge => _charge;
        public int StrikesUsed => _strikesUsed;
        public int CommandsActivated => _commandsActivated;
        public int TargetsEvaluated => _targetsEvaluated;
        public FireSupportProfileV139 CurrentProfile => _profile;
        public string HudText => BuildHudText();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldFireSupportDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldFireSupportDirector existing = FindAnyObjectByType<BattlefieldFireSupportDirector>();
            if (existing != null) { Instance = existing; return existing; }
            GameObject go = new GameObject("BattlefieldFireSupportDirector_v13_9");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldFireSupportDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _game = FindAnyObjectByType<TankGame>();
            BattlefieldSuppressionMoraleDirector.EnsureInstalled();
            _nextSample = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;
            EnsureRound(_game.CurrentRound);

            if (_state == FireSupportStateV139.Ready && Input.GetKeyDown(KeyCode.F))
                TryActivate();

            float now = Time.unscaledTime;
            if (_state == FireSupportStateV139.Active)
            {
                if (now >= _stateUntil || _strikesUsed >= _profile.StrikeBudget)
                {
                    EnterCooldown(now);
                    return;
                }
                if (now >= _nextStrike)
                {
                    ExecuteStrike(_strikesUsed);
                    _strikesUsed++;
                    _nextStrike = now + BattlefieldFireSupportModelV139.StrikeCadenceSeconds;
                }
                return;
            }

            if (_state == FireSupportStateV139.Cooldown)
            {
                if (now >= _stateUntil)
                {
                    _state = FireSupportStateV139.Building;
                    _charge = 0f;
                }
                return;
            }

            if (now < _nextSample) return;
            _nextSample = now + BattlefieldFireSupportModelV139.SampleCadenceSeconds;
            SampleReadiness();
        }

        public bool TryActivate()
        {
            if (_state != FireSupportStateV139.Ready || _game == null || !_game.IsPlaying) return false;
            float now = Time.unscaledTime;
            _state = FireSupportStateV139.Active;
            _stateUntil = now + BattlefieldFireSupportModelV139.ActiveSeconds;
            _nextStrike = now + 0.18f;
            _strikesUsed = 0;
            _commandsActivated++;
            BattleAudio.PlayGlobal(SoundCue.BossAlarm, 0.38f, 0.08f);
            return true;
        }

        private void EnsureRound(int round)
        {
            int bounded = Mathf.Clamp(round, 1, BattlefieldFireSupportModelV139.PlannedRounds);
            if (_round == bounded) return;
            _round = bounded;
            _profile = BattlefieldFireSupportModelV139.ProfileForRound(bounded);
            _state = FireSupportStateV139.Building;
            _charge = 0f;
            _stateUntil = 0f;
            _nextStrike = 0f;
            _strikesUsed = 0;
        }

        private void SampleReadiness()
        {
            BattlefieldSuppressionMoraleDirector morale = BattlefieldSuppressionMoraleDirector.Instance;
            if (morale == null || morale.TrackedCount <= 0)
            {
                _charge = BattlefieldFireSupportModelV139.AdvanceCharge(
                    _charge, 0f, 0f, BattlefieldFireSupportModelV139.SampleCadenceSeconds);
                return;
            }

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = Mathf.Min(enemies.Length, BattlefieldFireSupportModelV139.MaxTrackedHostiles);
            float cohesionStress = 0f;
            int live = 0;
            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                cohesionStress += Mathf.Clamp01((BattlefieldCohesionDirector.SpreadScale(enemy) - 1f) / 0.50f);
                live++;
            }
            cohesionStress = live > 0 ? cohesionStress / live : 0f;
            _charge = BattlefieldFireSupportModelV139.AdvanceCharge(
                _charge, morale.GlobalPressure, cohesionStress, BattlefieldFireSupportModelV139.SampleCadenceSeconds);
            if (_charge >= BattlefieldFireSupportModelV139.ReadyCharge && live > 0)
                _state = FireSupportStateV139.Ready;
        }

        private void ExecuteStrike(int ordinal)
        {
            EnemyTank target = SelectTarget(ordinal);
            if (target == null) return;

            FireSupportRoleV139 role = BattlefieldFireSupportModelV139.RoleFor(target.Kind);
            Vector2 aim = (Vector2)target.transform.position + BattlefieldFireSupportModelV139.AimOffset(role, ordinal, _profile.Signature);
            Vector2 origin = aim + new Vector2((ordinal & 1) == 0 ? -0.45f : 0.45f, 2.6f);
            Vector2 direction = (aim - origin).normalized;
            int damage = _round >= 70 ? 2 : 1;

            VisualFactory.RingPulse(aim, new Color(0.20f, 0.82f, 1f), 0.78f);
            ProjectilePool.Spawn(origin, direction, Team.Player, damage, 11.5f,
                AmmoDatabase.Color(AmmoType.Explosive), AmmoType.Explosive);
        }

        private EnemyTank SelectTarget(int ordinal)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = Mathf.Min(enemies.Length, BattlefieldFireSupportModelV139.MaxTrackedHostiles);
            EnemyTank best = null;
            float bestScore = float.NegativeInfinity;
            TacticalTerrainDirector terrain = TacticalTerrainDirector.Instance;
            TacticalTerrainPlanV134 terrainPlan = terrain != null ? terrain.CurrentPlan : default;
            _targetsEvaluated = 0;

            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _targetsEvaluated++;
                float pressure = 0f;
                if (BattlefieldSuppressionMoraleDirector.TryIntent(enemy, out SuppressionIntentV138 suppression))
                    pressure = suppression.Pressure;
                float cohesion = BattlefieldCohesionDirector.SpreadScale(enemy);
                float terrainOpportunity = terrain != null
                    ? BattlefieldFireSupportModelV139.TerrainOpportunity(terrainPlan, enemy.transform.position)
                    : 1f;
                float score = BattlefieldFireSupportModelV139.TargetScore(
                    enemy.Kind, pressure, cohesion, terrainOpportunity, i, _profile);
                score += ((ordinal + i + _profile.Signature) & 3) * 0.0001f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = enemy;
            }
            return best;
        }

        private void EnterCooldown(float now)
        {
            _state = FireSupportStateV139.Cooldown;
            _stateUntil = now + _profile.CooldownSeconds;
            _charge = 0f;
        }

        private string BuildHudText()
        {
            switch (_state)
            {
                case FireSupportStateV139.Ready: return "FIRE SUPPORT  READY [F]";
                case FireSupportStateV139.Active: return "FIRE SUPPORT  ACTIVE  " + _strikesUsed + "/" + _profile.StrikeBudget;
                case FireSupportStateV139.Cooldown: return "FIRE SUPPORT  COOLDOWN  " + Mathf.CeilToInt(Mathf.Max(0f, _stateUntil - Time.unscaledTime)) + "s";
                default: return "FIRE SUPPORT  CHARGE  " + Mathf.RoundToInt(_charge * 100f) + "%";
            }
        }

        private void OnGUI()
        {
            if (_game == null || !_game.IsPlaying) return;
            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight
                };
                _hudStyle.normal.textColor = new Color(0.38f, 0.90f, 1f);
            }
            GUI.Label(new Rect(Screen.width - 330f, 82f, 300f, 26f), BuildHudText(), _hudStyle);
        }
    }
}
