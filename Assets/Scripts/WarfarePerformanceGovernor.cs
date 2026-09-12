using System;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Production hardening layer. Converts measured frame pressure into shared runtime budgets.
    /// v4.6 also publishes budget transitions and exposes registry/FX telemetry for mass-battle tuning.
    /// It never changes combat damage, AI decisions, spawn authority or campaign state.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class WarfarePerformanceGovernor : MonoBehaviour
    {
        public enum BudgetTier
        {
            Full,
            Balanced,
            Survival
        }

        public static WarfarePerformanceGovernor Instance { get; private set; }
        public static BudgetTier Tier => Instance != null ? Instance._tier : BudgetTier.Full;
        public static int TrackMarkCap => Tier == BudgetTier.Survival ? 64 : Tier == BudgetTier.Balanced ? 108 : 180;
        public static float TrackMarkDistance => Tier == BudgetTier.Survival ? 0.74f : Tier == BudgetTier.Balanced ? 0.50f : 0.36f;
        public static float TrackMarkInterval => Tier == BudgetTier.Survival ? 0.13f : Tier == BudgetTier.Balanced ? 0.085f : 0.055f;
        public static float WearSparkMultiplier => Tier == BudgetTier.Survival ? 0.45f : Tier == BudgetTier.Balanced ? 0.72f : 1f;
        public static bool AllowAmbientWear => Tier != BudgetTier.Survival;
        public static float FxDensityMultiplier => Tier == BudgetTier.Survival ? 0.42f : Tier == BudgetTier.Balanced ? 0.68f : 1f;
        public static event Action<BudgetTier> BudgetChanged;

        private const float WindowSeconds = 1.35f;
        private BudgetTier _tier = BudgetTier.Full;
        private float _windowTime;
        private float _windowFrameTime;
        private int _windowFrames;
        private float _smoothedFps = 60f;
        private float _smoothedMs = 16.67f;
        private float _lastTierChange;
        private float _stableSince;
        private int _spikes;
        private bool _showTelemetry;

        public float SmoothedFps => _smoothedFps;
        public float SmoothedFrameMs => _smoothedMs;
        public int RecentSpikeCount => _spikes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarfarePerformanceGovernor>() != null) return;
            var go = new GameObject("WarfarePerformanceGovernor_v4_6");
            DontDestroyOnLoad(go);
            go.AddComponent<WarfarePerformanceGovernor>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _stableSince = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) _showTelemetry = !_showTelemetry;

            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.0001f, 0.25f);
            _windowTime += dt;
            _windowFrameTime += dt;
            _windowFrames++;
            if (dt > 0.055f) _spikes++;

            if (_windowTime < WindowSeconds) return;

            float avg = _windowFrameTime / Mathf.Max(1, _windowFrames);
            float fps = 1f / Mathf.Max(0.0001f, avg);
            _smoothedFps = Mathf.Lerp(_smoothedFps, fps, 0.45f);
            _smoothedMs = Mathf.Lerp(_smoothedMs, avg * 1000f, 0.45f);
            EvaluateBudget();

            _windowTime = 0f;
            _windowFrameTime = 0f;
            _windowFrames = 0;
            _spikes = 0;
        }

        private void EvaluateBudget()
        {
            float now = Time.unscaledTime;
            int enemies = CombatRoster.LivingEnemyCount;
            int units = RuntimeBattleRegistry.RegisteredHealthCount;
            bool highLoad = _smoothedFps < 48f || _smoothedMs > 22f || enemies >= 26 || units >= 44;
            bool criticalLoad = _smoothedFps < 34f || _smoothedMs > 31f || enemies >= 38 || units >= 60;
            bool healthy = _smoothedFps > 57f && _smoothedMs < 18.5f && enemies < 24 && units < 40;

            BudgetTier desired = criticalLoad ? BudgetTier.Survival : highLoad ? BudgetTier.Balanced : BudgetTier.Full;

            if ((int)desired > (int)_tier)
            {
                SetTier(desired, now);
                _stableSince = now;
                return;
            }

            if (!healthy)
            {
                _stableSince = now;
                return;
            }

            if (now - _stableSince < 5.5f || now - _lastTierChange < 4f) return;
            if (_tier == BudgetTier.Survival) SetTier(BudgetTier.Balanced, now);
            else if (_tier == BudgetTier.Balanced) SetTier(BudgetTier.Full, now);
        }

        private void SetTier(BudgetTier tier, float now)
        {
            if (_tier == tier) return;
            _tier = tier;
            _lastTierChange = now;
            BudgetChanged?.Invoke(_tier);
        }

        private void OnGUI()
        {
            if (!_showTelemetry) return;

            float width = 336f;
            float height = 158f;
            Rect panel = new Rect(Screen.width - width - 18f, 18f, width, height);
            GUI.Box(panel, string.Empty);

            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 9f, width - 24f, height - 18f));
            GUILayout.Label("v4.6 MASS-BATTLE TELEMETRY  [F3]");
            GUILayout.Label("FPS  " + _smoothedFps.ToString("0.0") + "   FRAME  " + _smoothedMs.ToString("0.0") + " ms");
            GUILayout.Label("BUDGET  " + _tier.ToString().ToUpperInvariant() + "   FX  " + FxDensityMultiplier.ToString("0.00"));
            GUILayout.Label("REGISTRY  " + RuntimeBattleRegistry.RegisteredHealthCount + " units / " + RuntimeBattleRegistry.RegisteredEnemyCount + " enemies   REV " + RuntimeBattleRegistry.Revision);
            GUILayout.Label("ROSTER  " + CombatRoster.LivingEnemyCount + " alive   TRACK CAP  " + TrackMarkCap);
            GUILayout.Label("FX TRAILS  +" + MassBattleFxBudget.TrailsAccepted + " / -" + MassBattleFxBudget.TrailsRejected +
                            "   MICRO  +" + MassBattleFxBudget.MicroAccepted + " / -" + MassBattleFxBudget.MicroRejected);
            GUILayout.EndArea();
        }
    }
}
