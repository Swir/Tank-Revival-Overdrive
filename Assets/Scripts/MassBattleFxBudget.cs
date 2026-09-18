using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Shared transient-FX budget for mass battles. Gameplay events are never suppressed; only
    /// optional presentation density scales with the existing governor plus the proactive v13.7
    /// late-round pressure profile. No enemy, projectile, damage or spawn event is removed here.
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
                LateRoundPerformanceProfileV137 profile = LateRoundPerformanceDirector.CurrentProfile;
                int cap = profile.ExplosionSparkCap;
                if (_activeExplosions >= 8) cap = Mathf.Max(6, cap - 2);
                return cap;
            }
        }

        public static int ExplosionSmokeCount
        {
            get
            {
                LateRoundPerformanceProfileV137 profile = LateRoundPerformanceDirector.CurrentProfile;
                int cap = profile.ExplosionSmokeCap;
                if (_activeExplosions >= 8) cap = Mathf.Max(2, cap - 1);
                return cap;
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

            LateRoundPerformanceProfileV137 profile = LateRoundPerformanceDirector.CurrentProfile;
            _trailTokens = profile.TrailTokens;
            _microTokens = profile.MicroTokens;
            _tacticalTokens = profile.TacticalTokens;
        }
    }
}
