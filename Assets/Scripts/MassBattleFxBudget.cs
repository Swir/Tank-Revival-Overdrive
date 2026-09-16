using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared transient-FX budget for mass battles. Gameplay events are never suppressed; only
    /// optional presentation density (projectile afterglow, explosion spark/smoke counts and
    /// v11.7 tactical maneuver cues) scales with the performance governor and live FX pressure.
    /// </summary>
    public static class MassBattleFxBudget
    {
        private static int _frame = -1;
        private static int _trailTokens;
        private static int _microTokens;
        private static int _tacticalTokens;
        private static int _activeExplosions;
        private static int _trailsAccepted;
        private static int _trailsRejected;
        private static int _microAccepted;
        private static int _microRejected;
        private static int _tacticalAccepted;
        private static int _tacticalRejected;

        public static int ActiveExplosions => _activeExplosions;
        public static int TrailsAccepted => _trailsAccepted;
        public static int TrailsRejected => _trailsRejected;
        public static int MicroAccepted => _microAccepted;
        public static int MicroRejected => _microRejected;
        public static int TacticalAccepted => _tacticalAccepted;
        public static int TacticalRejected => _tacticalRejected;

        public static int ExplosionSparkCount
        {
            get
            {
                int baseCount = WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival ? 8 :
                                WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced ? 13 : 20;
                if (_activeExplosions >= 8) baseCount = Mathf.Max(6, baseCount - 5);
                return baseCount;
            }
        }

        public static int ExplosionSmokeCount
        {
            get
            {
                int baseCount = WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival ? 3 :
                                WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced ? 5 : 8;
                if (_activeExplosions >= 8) baseCount = Mathf.Max(2, baseCount - 2);
                return baseCount;
            }
        }

        public static bool TryConsumeProjectileTrail(bool priority)
        {
            BeginFrame();
            if (priority)
            {
                _trailsAccepted++;
                return true;
            }

            if (_trailTokens > 0)
            {
                _trailTokens--;
                _trailsAccepted++;
                return true;
            }

            _trailsRejected++;
            return false;
        }

        public static bool TryConsumeMicroFx(bool priority)
        {
            BeginFrame();
            if (priority)
            {
                _microAccepted++;
                return true;
            }

            if (_microTokens > 0)
            {
                _microTokens--;
                _microAccepted++;
                return true;
            }

            _microRejected++;
            return false;
        }

        /// <summary>
        /// Presentation-only budget used by v11.7 world-space maneuver cues. Priority cues are
        /// Commander, Counter-Fire displacement or active reorganization cues; gameplay is never gated.
        /// </summary>
        public static bool TryConsumeTacticalCue(bool priority)
        {
            BeginFrame();
            if (priority)
            {
                _tacticalAccepted++;
                return true;
            }

            if (_tacticalTokens > 0)
            {
                _tacticalTokens--;
                _tacticalAccepted++;
                return true;
            }

            _tacticalRejected++;
            return false;
        }

        public static void RegisterExplosion()
        {
            _activeExplosions++;
        }

        public static void UnregisterExplosion()
        {
            _activeExplosions = Mathf.Max(0, _activeExplosions - 1);
        }

        private static void BeginFrame()
        {
            int frame = Time.frameCount;
            if (_frame == frame) return;
            _frame = frame;

            switch (WarfarePerformanceGovernor.Tier)
            {
                case WarfarePerformanceGovernor.BudgetTier.Survival:
                    _trailTokens = 2;
                    _microTokens = 3;
                    _tacticalTokens = 4;
                    break;
                case WarfarePerformanceGovernor.BudgetTier.Balanced:
                    _trailTokens = 5;
                    _microTokens = 7;
                    _tacticalTokens = 8;
                    break;
                default:
                    _trailTokens = 10;
                    _microTokens = 14;
                    _tacticalTokens = 14;
                    break;
            }
        }
    }
}
