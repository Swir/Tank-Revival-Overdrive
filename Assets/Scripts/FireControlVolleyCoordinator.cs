using UnityEngine;

namespace TankRevival
{
    /// <summary>Synchronizes precision-capable enemies into short, bounded fire windows without spawning extra shots.</summary>
    public static class FireControlVolleyCoordinator
    {
        public const float VolleyPeriod = 3.20f;
        public const float MaxHoldSeconds = 0.72f;

        public static bool IsEligible(EnemyKind kind, int round)
        {
            return round >= 35 && (kind == EnemyKind.Sniper || kind == EnemyKind.Elite || kind == EnemyKind.Heavy);
        }

        public static float HoldForWindow(float now, int instanceId, EnemyKind kind, int round)
        {
            if (!IsEligible(kind, round)) return 0f;
            float squadOffset = Mathf.Abs(instanceId % 3) * 0.12f;
            float phase = Mathf.Repeat(now + squadOffset, VolleyPeriod);
            if (phase <= FireControlBallisticsDirector.CoordinatedVolleyWindow) return 0f;
            float wait = VolleyPeriod - phase;
            return wait <= MaxHoldSeconds ? wait : 0f;
        }
    }
}
