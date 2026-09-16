using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.6 movement-intent layer. Returns bounded cardinal steering suggestions only;
    /// EnemyTank Rigidbody2D remains the sole movement authority.
    /// </summary>
    public static class AdaptivePlatoonManeuverDirector
    {
        public const float HunterFlankBand = 4.8f;
        public const float SiegeMinStandoff = 5.4f;
        public const float SiegeMaxStandoff = 8.6f;
        public const float CounterFireDisplaceBand = 7.2f;
        public const float ReorgSeconds = 3.5f;
        public const int MaxTrackedActors = 24;

        private static int _lossEpoch;
        private static float _lastLossTime = -99f;

        public static bool ConfigurationValid => HunterFlankBand >= 3.5f && HunterFlankBand <= 6.5f && SiegeMinStandoff >= 4f && SiegeMaxStandoff > SiegeMinStandoff && SiegeMaxStandoff <= 10f && CounterFireDisplaceBand >= SiegeMinStandoff && MaxTrackedActors == PlatoonFireMissionCoordinator.MaxActors;
        public static int LossEpoch => _lossEpoch;
        public static bool Reorganizing => Time.time - _lastLossTime < ReorgSeconds;

        public static void NotifyLoss(EnemyKind kind)
        {
            if (kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite)
            {
                _lossEpoch++;
                _lastLossTime = Time.time;
            }
        }

        public static Vector2 DesiredDirection(EnemyTank actor, EnemyKind kind, Vector2 actorPosition, Vector2 playerPosition, Vector2 eaglePosition, bool targetPlayer, bool counterFire, int round)
        {
            if (actor == null || kind == EnemyKind.Boss || kind == EnemyKind.Supply || kind == EnemyKind.Basic || kind == EnemyKind.Fast)
                return Cardinal((targetPlayer ? playerPosition : eaglePosition) - actorPosition);

            PlatoonFireMissionCoordinator.PlatoonRole role = PlatoonFireMissionCoordinator.RoleFor(actor, kind);
            Vector2 target = targetPlayer ? playerPosition : eaglePosition;
            Vector2 toTarget = target - actorPosition;
            float distance = toTarget.magnitude;
            Vector2 forward = distance > .01f ? toTarget / distance : Vector2.down;
            Vector2 side = new Vector2(-forward.y, forward.x);
            int parity = (actor.GetInstanceID() ^ (_lossEpoch * 397) ^ round) & 1;
            if (parity == 0) side = -side;

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.Hunter)
            {
                // Hunters/Elites build opposing flank lanes around the player and periodically cross the axis.
                float phase = Mathf.Sin((Time.time + (actor.GetInstanceID() & 7)) * .55f);
                Vector2 flankPoint = playerPosition + side * HunterFlankBand + forward * phase * 1.4f;
                return Cardinal(flankPoint - actorPosition);
            }

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.FireSupport)
            {
                // Siege never camps indefinitely: close units withdraw, distant units close the gap,
                // and real Counter-Fire forces a lateral displacement inside a bounded band.
                if (counterFire)
                {
                    Vector2 displacePoint = target + side * CounterFireDisplaceBand - forward * 1.2f;
                    return Cardinal(displacePoint - actorPosition);
                }
                if (distance < SiegeMinStandoff) return Cardinal(-forward + side * .25f);
                if (distance > SiegeMaxStandoff) return Cardinal(forward + side * .15f);
                return Cardinal(side);
            }

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.Breacher)
            {
                // Breachers preserve frontal pressure with a small deterministic lane split.
                return Cardinal(forward + side * .22f);
            }

            // Commanders flex during casualty reorganization instead of stacking behind Breachers.
            if (Reorganizing) return Cardinal(side + forward * .35f);
            return Cardinal(forward + side * .12f);
        }

        private static Vector2 Cardinal(Vector2 v)
        {
            if (v.sqrMagnitude < .001f) return Vector2.down;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y) ? new Vector2(Mathf.Sign(v.x), 0f) : new Vector2(0f, Mathf.Sign(v.y));
        }
    }
}
