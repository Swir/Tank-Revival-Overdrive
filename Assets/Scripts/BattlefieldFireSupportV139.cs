using System;
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

    public enum FireSupportReactionV139
    {
        Hold = 0,
        Brace = 1,
        Disperse = 2,
        Evade = 3
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

    public readonly struct FireSupportStrikeIntentV139
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Aim;
        public readonly Vector2 Direction;
        public readonly int Damage;
        public readonly float Speed;
        public readonly AmmoType Ammo;
        public readonly FireSupportRoleV139 Role;
        public readonly FireSupportReactionV139 Reaction;
        public readonly int StrikeOrdinal;
        public readonly int RoundSignature;

        public FireSupportStrikeIntentV139(
            Vector2 origin,
            Vector2 aim,
            Vector2 direction,
            int damage,
            float speed,
            AmmoType ammo,
            FireSupportRoleV139 role,
            FireSupportReactionV139 reaction,
            int strikeOrdinal,
            int roundSignature)
        {
            Origin = origin;
            Aim = aim;
            Direction = direction;
            Damage = damage;
            Speed = speed;
            Ammo = ammo;
            Role = role;
            Reaction = reaction;
            StrikeOrdinal = strikeOrdinal;
            RoundSignature = roundSignature;
        }
    }

    /// <summary>
    /// Pure deterministic v13.9 command-window model. It owns no Health, spawn, enemy movement,
    /// terrain damage or projectile resolution authority. Runtime support intent is executed only
    /// through the canonical TankGame projectile path so existing armor, cover and suppression rules win.
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
        public const float MinContactConfidence = BattlefieldSensorFusionPlannerV136.DetectionThreshold;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 && MaxTrackedHostiles == BattlefieldSuppressionModelV138.MaxTrackedEnemies &&
            MaxTrackedHostiles == BattlefieldSensorFusionPlannerV136.MaxTrackedContacts &&
            MaxTelegraphs > 0 && MaxTelegraphs <= BattlefieldSensorFusionPlannerV136.MaxWorldMarkers &&
            MaxStrikes > 0 && MaxStrikes <= MaxTelegraphs &&
            SampleCadenceSeconds >= 0.20f && SampleCadenceSeconds <= 0.50f &&
            ReadyPressure >= 25f && ReadyPressure <= 45f && ChargeSecondsAtFullPressure >= 3.5f &&
            ActiveSeconds > 0f && ActiveSeconds <= 6f && StrikeCadenceSeconds >= 0.65f &&
            MinCooldownSeconds >= 12f && MaxCooldownSeconds <= 24f && MinCooldownSeconds < MaxCooldownSeconds &&
            MinContactConfidence >= 0.20f && MinContactConfidence <= 0.40f;

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

        public static FireSupportReactionV139 ReactionFor(EnemyKind kind)
        {
            switch (RoleFor(kind))
            {
                case FireSupportRoleV139.Scout: return FireSupportReactionV139.Evade;
                case FireSupportRoleV139.Bruiser: return FireSupportReactionV139.Brace;
                case FireSupportRoleV139.Sniper: return FireSupportReactionV139.Disperse;
                case FireSupportRoleV139.Elite: return FireSupportReactionV139.Evade;
                case FireSupportRoleV139.Officer: return FireSupportReactionV139.Hold;
                case FireSupportRoleV139.Boss: return FireSupportReactionV139.Brace;
                default: return FireSupportReactionV139.Disperse;
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

        public static float SensorOpportunity(SensorContactStateV136 state, float confidence)
        {
            float c = Mathf.Clamp01(confidence);
            switch (state)
            {
                case SensorContactStateV136.Verified: return Mathf.Lerp(1.12f, 1.22f, c);
                case SensorContactStateV136.Tracked: return Mathf.Lerp(0.98f, 1.08f, c);
                case SensorContactStateV136.Detected: return Mathf.Lerp(0.82f, 0.96f, c);
                default: return 0f;
            }
        }

        public static float TargetScore(
            EnemyKind kind,
            float pressure,
            float cohesionSpreadScale,
            float terrainOpportunity,
            SensorContactStateV136 contactState,
            float contactConfidence,
            int slot,
            FireSupportProfileV139 profile)
        {
            float sensor = SensorOpportunity(contactState, contactConfidence);
            if (sensor <= 0f) return float.NegativeInfinity;

            float suppression = Mathf.Clamp01(pressure / BattlefieldSuppressionModelV138.MaxPressure);
            float cohesion = Mathf.Clamp01((cohesionSpreadScale - 1f) / 0.50f);
            float classBias = ClassPriority(kind);
            float tieBreak = (Mathf.Clamp(slot, 0, MaxTrackedHostiles - 1) + 1) * 0.0005f;
            return (0.42f + suppression * 0.38f + cohesion * 0.20f) * classBias *
                Mathf.Clamp(terrainOpportunity, 0.55f, 1.20f) * sensor * profile.PriorityBias + tieBreak;
        }

        public static Vector2 AimOffset(FireSupportRoleV139 role, int strikeOrdinal, int roundSignature)
        {
            int h = unchecked(roundSignature * 31 + strikeOrdinal * 97 + (int)role * 53);
            float x = ((h & 7) - 3) * 0.09f;
            float y = (((h >> 3) & 7) - 3) * 0.06f;
            float scale = role == FireSupportRoleV139.Scout ? 1.30f : role == FireSupportRoleV139.Boss ? 0.55f : 1f;
            return new Vector2(x, y) * scale;
        }

        public static FireSupportStrikeIntentV139 BuildIntent(
            EnemyKind kind,
            Vector2 targetPosition,
            int strikeOrdinal,
            int round,
            int roundSignature)
        {
            FireSupportRoleV139 role = RoleFor(kind);
            FireSupportReactionV139 reaction = ReactionFor(kind);
            Vector2 aim = targetPosition + AimOffset(role, strikeOrdinal, roundSignature);
            Vector2 origin = aim + new Vector2((strikeOrdinal & 1) == 0 ? -0.45f : 0.45f, 2.6f);
            Vector2 direction = (aim - origin).normalized;
            int damage = Mathf.Clamp(round, 1, PlannedRounds) >= 70 ? 2 : 1;
            return new FireSupportStrikeIntentV139(
                origin, aim, direction, damage, 11.5f, AmmoType.Explosive,
                role, reaction, strikeOrdinal, roundSignature);
        }
    }

    /// <summary>
    /// Earned player fire-support command. Suppression builds readiness; F commits a short bounded
    /// support window. This director publishes immutable strike intent only. It never creates a
    /// projectile, damages an actor or moves gameplay rigidbodies.
    /// </summary>
    [DefaultExecutionOrder(-8860)]
    public sealed class BattlefieldFireSupportDirector : MonoBehaviour
    {
        public static BattlefieldFireSupportDirector Instance { get; private set; }
        public static event Action<FireSupportStrikeIntentV139> StrikeIntentPublished;

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
        private int _intentsPublished;
        private FireSupportReactionV139 _lastReaction = FireSupportReactionV139.Hold;
        private GUIStyle _hudStyle;

        public FireSupportStateV139 State => _state;
        public float Charge => _charge;
        public int StrikesUsed => _strikesUsed;
        public int CommandsActivated => _commandsActivated;
        public int TargetsEvaluated => _targetsEvaluated;
        public int IntentsPublished => _intentsPublished;
        public FireSupportReactionV139 LastReaction => _lastReaction;
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
            BattlefieldSensorFusionDirector.EnsureInstalled();
            BattlefieldFireSupportExecutionBridgeV139.EnsureInstalled();
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
                    PublishStrikeIntent(_strikesUsed);
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
            BattlefieldSensorFusionDirector sensor = BattlefieldSensorFusionDirector.Instance;
            if (morale == null || sensor == null || morale.TrackedCount <= 0)
            {
                _charge = BattlefieldFireSupportModelV139.AdvanceCharge(
                    _charge, 0f, 0f, BattlefieldFireSupportModelV139.SampleCadenceSeconds);
                return;
            }

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = Mathf.Min(enemies.Length, BattlefieldFireSupportModelV139.MaxTrackedHostiles);
            float cohesionStress = 0f;
            int live = 0;
            int actionableContacts = 0;
            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                cohesionStress += Mathf.Clamp01((BattlefieldCohesionDirector.SpreadScale(enemy) - 1f) / 0.50f);
                live++;
                if (sensor.TryGetContact(enemy, out SensorContactStateV136 state, out float confidence, out _) &&
                    state >= SensorContactStateV136.Detected &&
                    confidence >= BattlefieldFireSupportModelV139.MinContactConfidence)
                {
                    actionableContacts++;
                }
            }
            cohesionStress = live > 0 ? cohesionStress / live : 0f;
            _charge = BattlefieldFireSupportModelV139.AdvanceCharge(
                _charge, morale.GlobalPressure, cohesionStress, BattlefieldFireSupportModelV139.SampleCadenceSeconds);
            if (_charge >= BattlefieldFireSupportModelV139.ReadyCharge && actionableContacts > 0)
                _state = FireSupportStateV139.Ready;
        }

        private void PublishStrikeIntent(int ordinal)
        {
            EnemyTank target = SelectTarget(ordinal);
            if (target == null) return;

            FireSupportStrikeIntentV139 intent = BattlefieldFireSupportModelV139.BuildIntent(
                target.Kind, target.transform.position, ordinal, _round, _profile.Signature);
            _lastReaction = intent.Reaction;
            _intentsPublished++;
            StrikeIntentPublished?.Invoke(intent);
        }

        private EnemyTank SelectTarget(int ordinal)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int count = Mathf.Min(enemies.Length, BattlefieldFireSupportModelV139.MaxTrackedHostiles);
            EnemyTank best = null;
            float bestScore = float.NegativeInfinity;
            TacticalTerrainDirector terrain = TacticalTerrainDirector.Instance;
            TacticalTerrainPlanV134 terrainPlan = terrain != null ? terrain.CurrentPlan : default;
            BattlefieldSensorFusionDirector sensor = BattlefieldSensorFusionDirector.Instance;
            _targetsEvaluated = 0;

            for (int i = 0; i < count; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                _targetsEvaluated++;

                SensorContactStateV136 contactState = SensorContactStateV136.Unknown;
                float contactConfidence = 0f;
                if (sensor == null || !sensor.TryGetContact(enemy, out contactState, out contactConfidence, out _))
                    continue;
                if (contactState < SensorContactStateV136.Detected ||
                    contactConfidence < BattlefieldFireSupportModelV139.MinContactConfidence)
                    continue;

                float pressure = 0f;
                if (BattlefieldSuppressionMoraleDirector.TryIntent(enemy, out SuppressionIntentV138 suppression))
                    pressure = suppression.Pressure;
                float cohesion = BattlefieldCohesionDirector.SpreadScale(enemy);
                float terrainOpportunity = terrain != null
                    ? BattlefieldFireSupportModelV139.TerrainOpportunity(terrainPlan, enemy.transform.position)
                    : 1f;
                float score = BattlefieldFireSupportModelV139.TargetScore(
                    enemy.Kind, pressure, cohesion, terrainOpportunity,
                    contactState, contactConfidence, i, _profile);
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
                case FireSupportStateV139.Active:
                    return "FIRE SUPPORT  ACTIVE  " + _strikesUsed + "/" + _profile.StrikeBudget + "  " + _lastReaction.ToString().ToUpperInvariant();
                case FireSupportStateV139.Cooldown:
                    return "FIRE SUPPORT  COOLDOWN  " + Mathf.CeilToInt(Mathf.Max(0f, _stateUntil - Time.unscaledTime)) + "s";
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
            GUI.Label(new Rect(Screen.width - 350f, 82f, 320f, 26f), BuildHudText(), _hudStyle);
        }
    }

    /// <summary>
    /// Thin canonical execution adapter. It owns no target selection or damage rules: it only forwards
    /// the director's immutable strike intent through TankGame's existing projectile creation path.
    /// </summary>
    [DefaultExecutionOrder(-8850)]
    public sealed class BattlefieldFireSupportExecutionBridgeV139 : MonoBehaviour
    {
        public static BattlefieldFireSupportExecutionBridgeV139 Instance { get; private set; }

        private TankGame _game;
        private int _executedIntents;

        public int ExecutedIntents => _executedIntents;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldFireSupportExecutionBridgeV139 EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldFireSupportExecutionBridgeV139 existing = FindAnyObjectByType<BattlefieldFireSupportExecutionBridgeV139>();
            if (existing != null) { Instance = existing; return existing; }
            GameObject go = new GameObject("BattlefieldFireSupportExecutionBridge_v13_9");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldFireSupportExecutionBridgeV139>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _game = FindAnyObjectByType<TankGame>();
            BattlefieldFireSupportDirector.StrikeIntentPublished += ExecuteIntent;
        }

        private void OnDestroy()
        {
            BattlefieldFireSupportDirector.StrikeIntentPublished -= ExecuteIntent;
            if (Instance == this) Instance = null;
        }

        private void ExecuteIntent(FireSupportStrikeIntentV139 intent)
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            VisualFactory.RingPulse(intent.Aim, new Color(0.20f, 0.82f, 1f), 0.78f);
            _game.SpawnProjectile(
                intent.Origin,
                intent.Direction,
                Team.Player,
                intent.Damage,
                intent.Speed,
                AmmoDatabase.Color(intent.Ammo),
                intent.Ammo);
            _executedIntents++;
        }
    }
}
