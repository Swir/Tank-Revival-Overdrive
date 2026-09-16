using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Read-only tactical memory of recently opened cover lanes. Obstacle remains the only
    /// structural-damage authority; this class only publishes bounded snapshots to AI/presentation.
    /// v11.9 adds explicit resolve/query contracts so combat engineers can close a gap without
    /// creating a second terrain, movement or damage authority.
    /// </summary>
    public static class ReactiveCoverBreachDirector
    {
        public struct BreachSnapshot
        {
            public bool Valid;
            public Vector2 Position;
            public Team AttackerTeam;
            public AmmoType Ordnance;
            public ObstacleKind CoverKind;
            public float OpenedAt;
            public int Sequence;
        }

        public const int MaxRecentBreaches = 16;
        public const float RecentBreachSeconds = 12f;
        public const float ManeuverExploitRange = 7.5f;
        public const float TargetCorridorSlack = 3.0f;

        private static readonly BreachSnapshot[] Recent = new BreachSnapshot[MaxRecentBreaches];
        private static int _nextIndex;
        private static int _sequence;
        private static int _resolvedCount;

        public static bool ConfigurationValid => MaxRecentBreaches >= 8 && MaxRecentBreaches <= 24 && RecentBreachSeconds >= 8f && RecentBreachSeconds <= 18f && ManeuverExploitRange >= 5f && ManeuverExploitRange <= 9f && TargetCorridorSlack <= 4f;
        public static int Sequence => _sequence;
        public static int ResolvedCount => _resolvedCount;

        public static void ReportBreach(Vector2 position, Team attackerTeam, AmmoType ordnance, ObstacleKind coverKind)
        {
            if (coverKind == ObstacleKind.Water) return;
            Recent[_nextIndex] = new BreachSnapshot
            {
                Valid = true,
                Position = position,
                AttackerTeam = attackerTeam,
                Ordnance = ordnance,
                CoverKind = coverKind,
                OpenedAt = Time.time,
                Sequence = ++_sequence
            };
            _nextIndex = (_nextIndex + 1) % MaxRecentBreaches;
        }

        public static int RecentCount
        {
            get
            {
                int count = 0;
                float now = Time.time;
                for (int i = 0; i < Recent.Length; i++)
                {
                    if (Recent[i].Valid && now - Recent[i].OpenedAt <= RecentBreachSeconds) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Marks one exact opening as tactically closed. The object that physically closes the gap is
        /// still an Obstacle; resolving only prevents AI/presentation from treating stale space as open.
        /// If that replacement obstacle is later breached, Obstacle.ReportBreach publishes a new sequence.
        /// </summary>
        public static bool ResolveBreach(int sequence)
        {
            if (sequence <= 0) return false;
            for (int i = 0; i < Recent.Length; i++)
            {
                BreachSnapshot candidate = Recent[i];
                if (!candidate.Valid || candidate.Sequence != sequence) continue;
                candidate.Valid = false;
                Recent[i] = candidate;
                _resolvedCount++;
                return true;
            }
            return false;
        }

        public static bool IsBreachActive(int sequence)
        {
            if (sequence <= 0) return false;
            float now = Time.time;
            for (int i = 0; i < Recent.Length; i++)
            {
                BreachSnapshot candidate = Recent[i];
                if (!candidate.Valid || candidate.Sequence != sequence) continue;
                return now - candidate.OpenedAt <= RecentBreachSeconds;
            }
            return false;
        }

        /// <summary>
        /// Finds the freshest opening made by one side around an engineering anchor. Fixed storage
        /// keeps counter-breach decisions allocation-free and deterministic even during late rounds.
        /// </summary>
        public static bool TryFindRecentBreachNear(Vector2 anchor, Team attackerTeam, float maxDistance, out BreachSnapshot best)
        {
            best = default;
            float bestScore = float.MaxValue;
            float now = Time.time;
            float limit = Mathf.Max(0.5f, maxDistance);

            for (int i = 0; i < Recent.Length; i++)
            {
                BreachSnapshot candidate = Recent[i];
                if (!candidate.Valid || candidate.AttackerTeam != attackerTeam) continue;
                float age = now - candidate.OpenedAt;
                if (age < 0f || age > RecentBreachSeconds) continue;
                float distance = Vector2.Distance(anchor, candidate.Position);
                if (distance > limit) continue;

                // Distance is the main engineering constraint; freshness breaks near-equal ties.
                float score = distance + age * 0.08f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best.Valid;
        }

        /// <summary>
        /// Finds a recent opening that helps the actor progress toward its current target. Friendly
        /// breaches are preferred, but an already-open enemy breach can still be exploited. The fixed
        /// 16-entry scan keeps this deterministic and allocation-free in 100-round mass battles.
        /// </summary>
        public static bool TryFindBestBreach(Vector2 actorPosition, Vector2 targetPosition, Team actorTeam, out BreachSnapshot best)
        {
            best = default;
            float directDistance = Vector2.Distance(actorPosition, targetPosition);
            float bestScore = float.MaxValue;
            float now = Time.time;

            for (int i = 0; i < Recent.Length; i++)
            {
                BreachSnapshot candidate = Recent[i];
                if (!candidate.Valid) continue;
                float age = now - candidate.OpenedAt;
                if (age < 0f || age > RecentBreachSeconds) continue;

                float actorDistance = Vector2.Distance(actorPosition, candidate.Position);
                if (actorDistance > ManeuverExploitRange) continue;

                float targetDistance = Vector2.Distance(candidate.Position, targetPosition);
                if (targetDistance > directDistance + TargetCorridorSlack) continue;

                float friendlyBonus = candidate.AttackerTeam == actorTeam ? 1.35f : 0f;
                float heavyBonus = Obstacle.IsHeavyOrdnance(candidate.Ordnance) ? 0.35f : 0f;
                float freshness = 1f - Mathf.Clamp01(age / RecentBreachSeconds);
                float score = actorDistance + targetDistance * 0.18f - friendlyBonus - heavyBonus - freshness * 0.45f;
                if (score >= bestScore) continue;

                bestScore = score;
                best = candidate;
            }

            return best.Valid;
        }
    }
}
