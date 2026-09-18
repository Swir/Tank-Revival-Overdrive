using System;
using UnityEngine;

namespace TankRevival
{
    public enum LateRoundPressureBandV137
    {
        Normal = 0,
        Dense = 1,
        Critical = 2
    }

    public readonly struct LateRoundPerformanceProfileV137
    {
        public readonly LateRoundPressureBandV137 Band;
        public readonly int TrailTokens;
        public readonly int MicroTokens;
        public readonly int TacticalTokens;
        public readonly int ExplosionSparkCap;
        public readonly int ExplosionSmokeCap;
        public readonly float PresentationDensity;

        public LateRoundPerformanceProfileV137(
            LateRoundPressureBandV137 band,
            int trailTokens,
            int microTokens,
            int tacticalTokens,
            int explosionSparkCap,
            int explosionSmokeCap,
            float presentationDensity)
        {
            Band = band;
            TrailTokens = trailTokens;
            MicroTokens = microTokens;
            TacticalTokens = tacticalTokens;
            ExplosionSparkCap = explosionSparkCap;
            ExplosionSmokeCap = explosionSmokeCap;
            PresentationDensity = presentationDensity;
        }
    }

    /// <summary>
    /// Pure v13.7 performance planner. It classifies presentation pressure only; it never changes
    /// enemy count, spawn cadence, movement, targeting, damage, Health, Projectile or economy state.
    /// Existing WarfarePerformanceGovernor remains the frame-pressure/graphics-floor authority.
    /// </summary>
    public static class LateRoundPerformancePlannerV137
    {
        public const int PlannedRounds = 100;
        public const float SampleCadenceSeconds = 0.50f;
        public const float RecoveryHoldSeconds = 4.00f;
        public const int DenseRoundFloor = 80;
        public const int CriticalRoundFloor = 90;
        public const int DenseEnemyFloor = 20;
        public const int CriticalEnemyFloor = 32;
        public const int DenseUnitFloor = 38;
        public const int CriticalUnitFloor = 54;
        public const int DenseExplosionFloor = 5;
        public const int CriticalExplosionFloor = 9;

        public static bool ConfigurationValid =>
            PlannedRounds == 100 &&
            SampleCadenceSeconds >= 0.35f && SampleCadenceSeconds <= 0.75f &&
            RecoveryHoldSeconds >= 3f && RecoveryHoldSeconds <= 7f &&
            DenseRoundFloor >= 70 && CriticalRoundFloor > DenseRoundFloor && CriticalRoundFloor <= 95 &&
            DenseEnemyFloor >= 16 && CriticalEnemyFloor > DenseEnemyFloor &&
            DenseUnitFloor >= 32 && CriticalUnitFloor > DenseUnitFloor &&
            DenseExplosionFloor >= 4 && CriticalExplosionFloor > DenseExplosionFloor;

        public static LateRoundPressureBandV137 Classify(
            int round,
            int livingEnemies,
            int registeredUnits,
            int activeExplosions,
            WarfarePerformanceGovernor.BudgetTier governorTier)
        {
            int r = Mathf.Clamp(round, 1, PlannedRounds);
            int enemies = Mathf.Max(0, livingEnemies);
            int units = Mathf.Max(0, registeredUnits);
            int explosions = Mathf.Max(0, activeExplosions);

            if (governorTier == WarfarePerformanceGovernor.BudgetTier.Survival ||
                enemies >= CriticalEnemyFloor || units >= CriticalUnitFloor || explosions >= CriticalExplosionFloor ||
                (r >= CriticalRoundFloor && enemies >= 26) ||
                (r >= 96 && (units >= 44 || explosions >= 6)))
                return LateRoundPressureBandV137.Critical;

            if (governorTier == WarfarePerformanceGovernor.BudgetTier.Balanced ||
                enemies >= DenseEnemyFloor || units >= DenseUnitFloor || explosions >= DenseExplosionFloor ||
                (r >= DenseRoundFloor && enemies >= 15) ||
                (r >= CriticalRoundFloor && units >= 30))
                return LateRoundPressureBandV137.Dense;

            return LateRoundPressureBandV137.Normal;
        }

        public static LateRoundPerformanceProfileV137 ProfileForBand(LateRoundPressureBandV137 band)
        {
            switch (band)
            {
                case LateRoundPressureBandV137.Critical:
                    return new LateRoundPerformanceProfileV137(band, 2, 3, 4, 8, 3, 0.42f);
                case LateRoundPressureBandV137.Dense:
                    return new LateRoundPerformanceProfileV137(band, 5, 7, 8, 13, 5, 0.68f);
                default:
                    return new LateRoundPerformanceProfileV137(band, 10, 14, 14, 20, 8, 1.00f);
            }
        }

        public static LateRoundPerformanceProfileV137 Plan(
            int round,
            int livingEnemies,
            int registeredUnits,
            int activeExplosions,
            WarfarePerformanceGovernor.BudgetTier governorTier)
        {
            return ProfileForBand(Classify(round, livingEnemies, registeredUnits, activeExplosions, governorTier));
        }
    }

    /// <summary>
    /// Bounded v13.7 telemetry/coordinator. Samples pressure twice per second, promotes immediately,
    /// recovers only after a stable hold, and publishes presentation budgets to existing FX systems.
    /// It never removes enemies or suppresses gameplay events.
    /// </summary>
    [DefaultExecutionOrder(-8950)]
    public sealed class LateRoundPerformanceDirector : MonoBehaviour
    {
        public static LateRoundPerformanceDirector Instance { get; private set; }

        private static readonly LateRoundPerformanceProfileV137 NormalProfile =
            LateRoundPerformancePlannerV137.ProfileForBand(LateRoundPressureBandV137.Normal);

        private TankGame _game;
        private LateRoundPerformanceProfileV137 _profile = NormalProfile;
        private float _nextSample;
        private float _recoveryCandidateSince = -1f;
        private long _managedBytes;
        private long _peakManagedBytes;
        private int _gc0;
        private int _gc1;
        private int _gc2;
        private int _samples;
        private GUIStyle _style;

        public static LateRoundPerformanceProfileV137 CurrentProfile => Instance != null ? Instance._profile : NormalProfile;
        public LateRoundPressureBandV137 CurrentBand => _profile.Band;
        public float ManagedMemoryMb => _managedBytes / (1024f * 1024f);
        public float PeakManagedMemoryMb => _peakManagedBytes / (1024f * 1024f);
        public int Gen0Collections => _gc0;
        public int Gen1Collections => _gc1;
        public int Gen2Collections => _gc2;
        public int Samples => _samples;
        public static bool ConfigurationValid => LateRoundPerformancePlannerV137.ConfigurationValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => EnsureInstalled();

        public static LateRoundPerformanceDirector EnsureInstalled()
        {
            if (Instance != null) return Instance;
            LateRoundPerformanceDirector existing = FindAnyObjectByType<LateRoundPerformanceDirector>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }
            GameObject go = new GameObject("LateRoundPerformanceDirector_v13_7");
            DontDestroyOnLoad(go);
            return go.AddComponent<LateRoundPerformanceDirector>();
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
            _nextSample = Time.unscaledTime;
            CaptureMemory();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextSample) return;
            _nextSample = now + LateRoundPerformancePlannerV137.SampleCadenceSeconds;

            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            int round = _game != null ? Mathf.Clamp(_game.CurrentRound, 1, LateRoundPerformancePlannerV137.PlannedRounds) : 1;
            LateRoundPerformanceProfileV137 requested = LateRoundPerformancePlannerV137.Plan(
                round,
                CombatRoster.LivingEnemyCount,
                RuntimeBattleRegistry.RegisteredHealthCount,
                MassBattleFxBudget.ActiveExplosions,
                WarfarePerformanceGovernor.Tier);

            ApplyWithHysteresis(requested, now);
            CaptureMemory();
            _samples++;
        }

        private void ApplyWithHysteresis(LateRoundPerformanceProfileV137 requested, float now)
        {
            if ((int)requested.Band > (int)_profile.Band)
            {
                _profile = requested;
                _recoveryCandidateSince = -1f;
                return;
            }

            if ((int)requested.Band == (int)_profile.Band)
            {
                _recoveryCandidateSince = -1f;
                return;
            }

            if (_recoveryCandidateSince < 0f)
            {
                _recoveryCandidateSince = now;
                return;
            }

            if (now - _recoveryCandidateSince < LateRoundPerformancePlannerV137.RecoveryHoldSeconds) return;

            LateRoundPressureBandV137 oneStep = _profile.Band == LateRoundPressureBandV137.Critical
                ? LateRoundPressureBandV137.Dense
                : LateRoundPressureBandV137.Normal;
            if ((int)oneStep < (int)requested.Band) oneStep = requested.Band;
            _profile = LateRoundPerformancePlannerV137.ProfileForBand(oneStep);
            _recoveryCandidateSince = now;
        }

        private void CaptureMemory()
        {
            _managedBytes = GC.GetTotalMemory(false);
            if (_managedBytes > _peakManagedBytes) _peakManagedBytes = _managedBytes;
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild || !Input.GetKey(KeyCode.F3)) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.55f, 0.92f, 1f) }
                };
            }

            Rect panel = new Rect(Screen.width - 378f, 198f, 360f, 44f);
            GUI.color = new Color(0.02f, 0.04f, 0.065f, 0.88f);
            GUI.Box(panel, string.Empty);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, 340f, 16f),
                "v13.7 DENSITY " + _profile.Band.ToString().ToUpperInvariant() +
                "  FX " + _profile.PresentationDensity.ToString("0.00") +
                "  MEM " + ManagedMemoryMb.ToString("0.0") + "MB", _style);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 21f, 340f, 16f),
                "GC " + _gc0 + "/" + _gc1 + "/" + _gc2 +
                "  TOKENS " + _profile.TrailTokens + "/" + _profile.MicroTokens + "/" + _profile.TacticalTokens,
                _style);
        }
    }
}
