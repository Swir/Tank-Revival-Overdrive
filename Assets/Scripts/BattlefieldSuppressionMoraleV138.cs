using System;
using UnityEngine;

namespace TankRevival
{
    public enum BattlefieldMoraleStateV138
    {
        Steady = 0,
        Pressed = 1,
        Suppressed = 2,
        Broken = 3,
        Recovering = 4
    }

    public readonly struct SuppressionIntentV138
    {
        public readonly BattlefieldMoraleStateV138 State;
        public readonly float Pressure;
        public readonly float MovementScale;
        public readonly float ReloadScale;
        public readonly float SpreadScale;

        public SuppressionIntentV138(BattlefieldMoraleStateV138 state, float pressure, float movement, float reload, float spread)
        {
            State = state;
            Pressure = pressure;
            MovementScale = movement;
            ReloadScale = reload;
            SpreadScale = spread;
        }
    }

    /// <summary>
    /// Pure deterministic v13.8 suppression model. It owns no movement, fire, damage, projectile,
    /// spawn or Health authority; it only converts bounded observed pressure into behavior intent.
    /// </summary>
    public static class BattlefieldSuppressionModelV138
    {
        public const int MaxTrackedEnemies = 24;
        public const int MaxHudMarkers = 8;
        public const float SampleCadenceSeconds = 0.25f;
        public const float RetreatCadenceSeconds = 0.50f;
        public const float MaxPressure = 100f;
        public const float PressedEnter = 20f;
        public const float SuppressedEnter = 45f;
        public const float BrokenEnter = 72f;
        public const float PressedExit = 12f;
        public const float SuppressedExit = 32f;
        public const float BrokenExit = 58f;
        public const float MaxBrokenSeconds = 4.5f;
        public const float RecoveryFloorSeconds = 1.25f;
        public const float NearMissRadius = 1.55f;
        public const float NearMissMinForward = 0.35f;
        public const float NearMissMaxForward = 13.5f;

        public static bool ConfigurationValid =>
            MaxTrackedEnemies == 24 && MaxHudMarkers > 0 && MaxHudMarkers <= 8 &&
            SampleCadenceSeconds >= 0.20f && SampleCadenceSeconds <= 0.50f &&
            RetreatCadenceSeconds >= 0.25f && RetreatCadenceSeconds <= 1f &&
            PressedExit < PressedEnter && PressedEnter < SuppressedExit &&
            SuppressedExit < SuppressedEnter && BrokenExit < BrokenEnter &&
            SuppressedEnter < BrokenExit && BrokenEnter < MaxPressure &&
            MaxBrokenSeconds >= 3f && MaxBrokenSeconds <= 6f && RecoveryFloorSeconds >= 1f;

        public static float Resistance(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Boss: return 0.55f;
                case EnemyKind.Heavy: return 0.78f;
                case EnemyKind.Elite: return 0.84f;
                case EnemyKind.Siege: return 0.88f;
                case EnemyKind.Fast: return 1.06f;
                default: return 1f;
            }
        }

        public static float AmmoPressure(AmmoType ammo)
        {
            switch (ammo)
            {
                case AmmoType.Plasma: return 1.45f;
                case AmmoType.ArmorPiercing: return 1.28f;
                case AmmoType.Explosive: return 1.20f;
                case AmmoType.EMP: return 1.15f;
                case AmmoType.Incendiary: return 1.08f;
                case AmmoType.Twin: return 1.06f;
                default: return 1f;
            }
        }

        public static float DensityPressureScale(LateRoundPressureBandV137 band, bool nearMiss)
        {
            if (!nearMiss) return 1f;
            return band == LateRoundPressureBandV137.Critical ? 0.74f :
                   band == LateRoundPressureBandV137.Dense ? 0.86f : 1f;
        }

        public static float ImpactPressure(AmmoType ammo, EnemyKind kind, int damage)
        {
            float baseValue = 11f + Mathf.Clamp(damage, 1, 8) * 2.4f;
            return Mathf.Clamp(baseValue * AmmoPressure(ammo) * Resistance(kind), 4f, 34f);
        }

        public static float NearMissPressure(AmmoType ammo, EnemyKind kind, LateRoundPressureBandV137 band)
        {
            return Mathf.Clamp(7.5f * AmmoPressure(ammo) * Resistance(kind) * DensityPressureScale(band, true), 2.5f, 14f);
        }

        public static int RetreatPhase(int slot, float stateAgeSeconds)
        {
            int safeSlot = Mathf.Clamp(slot, 0, MaxTrackedEnemies - 1);
            int cadenceStep = Mathf.FloorToInt(Mathf.Max(0f, stateAgeSeconds) / RetreatCadenceSeconds);
            return (safeSlot + cadenceStep) & 3;
        }

        public static BattlefieldMoraleStateV138 ResolveState(
            BattlefieldMoraleStateV138 previous, float pressure, float stateAgeSeconds)
        {
            float p = Mathf.Clamp(pressure, 0f, MaxPressure);
            switch (previous)
            {
                case BattlefieldMoraleStateV138.Broken:
                    if (stateAgeSeconds >= MaxBrokenSeconds || p < BrokenExit)
                        return BattlefieldMoraleStateV138.Recovering;
                    return BattlefieldMoraleStateV138.Broken;
                case BattlefieldMoraleStateV138.Recovering:
                    if (p >= BrokenEnter) return BattlefieldMoraleStateV138.Broken;
                    if (p >= SuppressedEnter) return BattlefieldMoraleStateV138.Suppressed;
                    if (stateAgeSeconds < RecoveryFloorSeconds) return BattlefieldMoraleStateV138.Recovering;
                    if (p >= PressedEnter) return BattlefieldMoraleStateV138.Pressed;
                    return BattlefieldMoraleStateV138.Steady;
                case BattlefieldMoraleStateV138.Suppressed:
                    if (p >= BrokenEnter) return BattlefieldMoraleStateV138.Broken;
                    if (p < SuppressedExit) return p >= PressedEnter ? BattlefieldMoraleStateV138.Pressed : BattlefieldMoraleStateV138.Steady;
                    return BattlefieldMoraleStateV138.Suppressed;
                case BattlefieldMoraleStateV138.Pressed:
                    if (p >= BrokenEnter) return BattlefieldMoraleStateV138.Broken;
                    if (p >= SuppressedEnter) return BattlefieldMoraleStateV138.Suppressed;
                    if (p < PressedExit) return BattlefieldMoraleStateV138.Steady;
                    return BattlefieldMoraleStateV138.Pressed;
                default:
                    if (p >= BrokenEnter) return BattlefieldMoraleStateV138.Broken;
                    if (p >= SuppressedEnter) return BattlefieldMoraleStateV138.Suppressed;
                    if (p >= PressedEnter) return BattlefieldMoraleStateV138.Pressed;
                    return BattlefieldMoraleStateV138.Steady;
            }
        }

        public static SuppressionIntentV138 Intent(BattlefieldMoraleStateV138 state, float pressure)
        {
            switch (state)
            {
                case BattlefieldMoraleStateV138.Pressed:
                    return new SuppressionIntentV138(state, pressure, 0.98f, 1.05f, 1.12f);
                case BattlefieldMoraleStateV138.Suppressed:
                    return new SuppressionIntentV138(state, pressure, 0.91f, 1.15f, 1.28f);
                case BattlefieldMoraleStateV138.Broken:
                    return new SuppressionIntentV138(state, pressure, 0.82f, 1.28f, 1.48f);
                case BattlefieldMoraleStateV138.Recovering:
                    return new SuppressionIntentV138(state, pressure, 0.94f, 1.10f, 1.20f);
                default:
                    return new SuppressionIntentV138(state, pressure, 1f, 1f, 1f);
            }
        }
    }

    [DefaultExecutionOrder(-8875)]
    public sealed class BattlefieldSuppressionMoraleDirector : MonoBehaviour
    {
        private sealed class Entry
        {
            public bool Occupied;
            public EnemyTank Enemy;
            public EnemyKind Kind;
            public float Pressure;
            public float StateSince;
            public float LastPressureAt;
            public float LastHitAt;
            public BattlefieldMoraleStateV138 State;
            public string Label = string.Empty;
        }

        public static BattlefieldSuppressionMoraleDirector Instance { get; private set; }
        private readonly Entry[] _entries = new Entry[BattlefieldSuppressionModelV138.MaxTrackedEnemies];
        private int _count;
        private float _nextSample;
        private GUIStyle _markerStyle;
        private GUIStyle _globalStyle;
        private TankGame _game;
        private string _globalLabel = "MORALE  STEADY";
        private float _globalPressure;
        private int _stateTransitions;
        private int _brokenRecoveries;

        public int TrackedCount => _count;
        public float GlobalPressure => _globalPressure;
        public int StateTransitions => _stateTransitions;
        public int BrokenRecoveries => _brokenRecoveries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static BattlefieldSuppressionMoraleDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            BattlefieldSuppressionMoraleDirector existing = FindAnyObjectByType<BattlefieldSuppressionMoraleDirector>();
            if (existing != null) { Instance = existing; return existing; }
            GameObject go = new GameObject("BattlefieldSuppressionMoraleDirector_v13_8");
            DontDestroyOnLoad(go);
            return go.AddComponent<BattlefieldSuppressionMoraleDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < _entries.Length; i++) _entries[i] = new Entry();
            _nextSample = Time.unscaledTime;
            _game = FindAnyObjectByType<TankGame>();
            Projectile.DamageResolved += OnDamageResolved;
            Projectile.ShotSpawned3D += OnShotSpawned;
        }

        private void OnDestroy()
        {
            Projectile.DamageResolved -= OnDamageResolved;
            Projectile.ShotSpawned3D -= OnShotSpawned;
            if (Instance == this) Instance = null;
        }

        public void Register(EnemyTank enemy, EnemyKind kind)
        {
            if (enemy == null || Find(enemy) >= 0) return;
            int slot = FirstFree();
            if (slot < 0) return;
            Entry e = _entries[slot];
            e.Occupied = true;
            e.Enemy = enemy;
            e.Kind = kind;
            e.Pressure = 0f;
            e.State = BattlefieldMoraleStateV138.Steady;
            e.StateSince = Time.unscaledTime;
            e.LastPressureAt = e.StateSince;
            e.LastHitAt = -100f;
            e.Label = "STEADY";
            _count++;
        }

        public void Unregister(EnemyTank enemy)
        {
            int slot = Find(enemy);
            if (slot < 0) return;
            Clear(_entries[slot]);
            _count = Mathf.Max(0, _count - 1);
        }

        public static float MovementScale(EnemyTank enemy) => TryIntent(enemy, out SuppressionIntentV138 i) ? i.MovementScale : 1f;
        public static float ReloadScale(EnemyTank enemy) => TryIntent(enemy, out SuppressionIntentV138 i) ? i.ReloadScale : 1f;
        public static float SpreadScale(EnemyTank enemy) => TryIntent(enemy, out SuppressionIntentV138 i) ? i.SpreadScale : 1f;

        public static bool TryIntent(EnemyTank enemy, out SuppressionIntentV138 intent)
        {
            if (Instance != null)
            {
                int slot = Instance.Find(enemy);
                if (slot >= 0)
                {
                    Entry e = Instance._entries[slot];
                    intent = BattlefieldSuppressionModelV138.Intent(e.State, e.Pressure);
                    return true;
                }
            }
            intent = BattlefieldSuppressionModelV138.Intent(BattlefieldMoraleStateV138.Steady, 0f);
            return false;
        }

        public static Vector2 AdjustDirection(EnemyTank enemy, Vector2 position, Vector2 playerPosition, Vector2 current)
        {
            if (Instance == null || enemy == null) return current;
            int slot = Instance.Find(enemy);
            if (slot < 0) return current;
            Entry e = Instance._entries[slot];
            SuppressionIntentV138 intent = BattlefieldSuppressionModelV138.Intent(e.State, e.Pressure);
            if (intent.State != BattlefieldMoraleStateV138.Broken && intent.State != BattlefieldMoraleStateV138.Recovering) return current;
            // Existing cohesion gets first say. If it is already constraining movement, do not compete with it.
            if (BattlefieldCohesionDirector.MovementScale(enemy) < 0.94f) return current;
            Vector2 away = position - playerPosition;
            if (away.sqrMagnitude < 0.25f) return current;
            Vector2 fallback = Mathf.Abs(away.x) > Mathf.Abs(away.y)
                ? new Vector2(Mathf.Sign(away.x), 0f)
                : new Vector2(0f, Mathf.Sign(away.y));
            int phase = BattlefieldSuppressionModelV138.RetreatPhase(slot, Time.unscaledTime - e.StateSince);
            return phase == 0 ? current : fallback;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextSample) return;
            _nextSample = now + BattlefieldSuppressionModelV138.SampleCadenceSeconds;
            Sample(now);
        }

        private void Sample(float now)
        {
            float sum = 0f;
            int live = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry e = _entries[i];
                if (!e.Occupied) continue;
                if (e.Enemy == null)
                {
                    Clear(e);
                    _count = Mathf.Max(0, _count - 1);
                    continue;
                }

                float cohesionStress = Mathf.Clamp(BattlefieldCohesionDirector.SpreadScale(e.Enemy) - 1f, 0f, 0.45f);
                float decay = (e.Kind == EnemyKind.Boss ? 11f : 8.5f) * (1f - cohesionStress * 0.35f);
                if (e.State == BattlefieldMoraleStateV138.Suppressed ||
                    e.State == BattlefieldMoraleStateV138.Broken ||
                    e.State == BattlefieldMoraleStateV138.Recovering)
                {
                    decay *= BattlefieldSmokeScreenDirectorV143.SuppressionRecoveryScaleAt(e.Enemy.transform.position, e.Kind);
                }
                e.Pressure = Mathf.Max(0f, e.Pressure - decay * BattlefieldSuppressionModelV138.SampleCadenceSeconds);
                BattlefieldMoraleStateV138 next = BattlefieldSuppressionModelV138.ResolveState(e.State, e.Pressure, now - e.StateSince);
                if (next != e.State)
                {
                    if (e.State == BattlefieldMoraleStateV138.Broken && next == BattlefieldMoraleStateV138.Recovering) _brokenRecoveries++;
                    e.State = next;
                    e.StateSince = now;
                    e.Label = next.ToString().ToUpperInvariant();
                    _stateTransitions++;
                }
                sum += e.Pressure;
                live++;
            }
            _globalPressure = live > 0 ? sum / live : 0f;
            _globalLabel = "MORALE PRESSURE  " + Mathf.RoundToInt(_globalPressure) + "%";
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (projectile == null || target == null || projectile.OwnerTeam != Team.Player) return;
            EnemyTank enemy = target.GetComponent<EnemyTank>();
            if (enemy == null) return;
            int slot = Find(enemy);
            if (slot < 0) { Register(enemy, enemy.Kind); slot = Find(enemy); }
            if (slot < 0) return;

            Entry e = _entries[slot];
            float now = Time.unscaledTime;
            float sustained = now - e.LastHitAt <= 1.35f ? 1.18f : 1f;
            AddPressure(e, BattlefieldSuppressionModelV138.ImpactPressure(projectile.Ammo, e.Kind, damage) * sustained, now);
            e.LastHitAt = now;
            if (killed) BroadcastAllyLoss(enemy.transform.position, now);
        }

        private void OnShotSpawned(Projectile projectile, Vector3 origin3, Vector2 direction, Team owner, AmmoType ammo)
        {
            if (projectile == null || owner != Team.Player || direction.sqrMagnitude < 0.1f) return;
            Vector2 origin = origin3;
            Vector2 dir = direction.normalized;
            LateRoundPressureBandV137 band = LateRoundPerformanceDirector.CurrentProfile.Band;
            float now = Time.unscaledTime;

            // Near-miss pressure only visits the fixed, deterministically slotted v13.8 roster.
            // This avoids HashSet snapshot ordering and per-shot register/find churn under 100-round density.
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry e = _entries[i];
                if (!e.Occupied || e.Enemy == null) continue;
                EnemyTank enemy = e.Enemy;
                Vector2 delta = (Vector2)enemy.transform.position - origin;
                float forward = Vector2.Dot(delta, dir);
                if (forward < BattlefieldSuppressionModelV138.NearMissMinForward || forward > BattlefieldSuppressionModelV138.NearMissMaxForward) continue;
                float lateral = Mathf.Abs(delta.x * dir.y - delta.y * dir.x);
                if (lateral > BattlefieldSuppressionModelV138.NearMissRadius || lateral < 0.26f) continue;
                AddPressure(e, BattlefieldSuppressionModelV138.NearMissPressure(ammo, e.Kind, band), now);
            }
        }

        private void BroadcastAllyLoss(Vector2 position, float now)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry e = _entries[i];
                if (!e.Occupied || e.Enemy == null) continue;
                float sqr = ((Vector2)e.Enemy.transform.position - position).sqrMagnitude;
                if (sqr > 49f) continue;
                AddPressure(e, e.Kind == EnemyKind.Boss ? 2.5f : 5.5f, now);
            }
        }

        private static void AddPressure(Entry e, float amount, float now)
        {
            e.Pressure = Mathf.Clamp(e.Pressure + Mathf.Max(0f, amount), 0f, BattlefieldSuppressionModelV138.MaxPressure);
            e.LastPressureAt = now;
        }

        private int Find(EnemyTank enemy)
        {
            if (enemy == null) return -1;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Occupied && _entries[i].Enemy == enemy) return i;
            return -1;
        }

        private int FirstFree()
        {
            for (int i = 0; i < _entries.Length; i++) if (!_entries[i].Occupied) return i;
            return -1;
        }

        private static void Clear(Entry e)
        {
            e.Occupied = false;
            e.Enemy = null;
            e.Kind = EnemyKind.Basic;
            e.Pressure = 0f;
            e.State = BattlefieldMoraleStateV138.Steady;
            e.StateSince = 0f;
            e.LastPressureAt = 0f;
            e.LastHitAt = -100f;
            e.Label = string.Empty;
        }

        private void OnGUI()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_globalStyle == null)
            {
                _globalStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
                _globalStyle.normal.textColor = new Color(0.38f, 0.90f, 1f, 0.92f);
                _markerStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter };
                _markerStyle.normal.textColor = new Color(1f, 0.72f, 0.26f, 0.90f);
            }
            GUI.Label(new Rect(18f, 118f, 230f, 24f), _globalLabel, _globalStyle);

            int cap = Mathf.Min(BattlefieldSuppressionModelV138.MaxHudMarkers, LateRoundPerformanceDirector.CurrentProfile.TacticalTokens);
            int shown = 0;
            for (int i = 0; i < _entries.Length && shown < cap; i++)
            {
                Entry e = _entries[i];
                if (!e.Occupied || e.Enemy == null || e.State == BattlefieldMoraleStateV138.Steady) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.Enemy.transform.position + Vector3.up * 0.8f);
                if (sp.z <= 0f) continue;
                GUI.Label(new Rect(sp.x - 42f, Screen.height - sp.y - 10f, 84f, 20f), e.Label, _markerStyle);
                shown++;
            }
        }
    }
}
