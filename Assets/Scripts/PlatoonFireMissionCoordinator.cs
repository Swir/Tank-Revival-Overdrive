using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.4 bounded target-assignment layer. It never fires weapons or applies damage;
    /// it only tells existing EnemyTank authority whether Player or Orzelek is the better target.
    /// </summary>
    public static class PlatoonFireMissionCoordinator
    {
        private sealed class Reservation
        {
            public int ActorId;
            public bool PlayerTarget;
            public bool Precision;
            public float ExpiresAt;
        }

        public const int MaxActors = 24;
        public const int MaxPrecisionCommitmentsPerTarget = 2;
        public const float ReservationSeconds = 1.35f;
        private static readonly List<Reservation> Reservations = new List<Reservation>(MaxActors);

        public static bool ConfigurationValid => MaxActors <= 24 && MaxActors >= 12 && MaxPrecisionCommitmentsPerTarget == 2 && ReservationSeconds >= 0.8f && ReservationSeconds <= 1.6f;
        public static int ActiveReservations { get { Prune(); return Reservations.Count; } }

        public static bool PreferPlayer(EnemyTank actor, EnemyKind kind, int round, bool localPreference, bool counterFire)
        {
            if (actor == null || kind == EnemyKind.Boss || kind == EnemyKind.Supply || kind == EnemyKind.Basic || kind == EnemyKind.Fast)
                return localPreference;

            Prune();
            int id = actor.GetInstanceID();
            Reservation own = Find(id);
            if (own != null)
            {
                own.ExpiresAt = Time.time + ReservationSeconds;
                return own.PlayerTarget;
            }

            bool precision = IsPrecision(kind);
            float playerScore = localPreference ? 2.0f : 0.65f;
            float baseScore = localPreference ? 0.70f : 1.75f;

            // v11.3 incoming-fire memory is the strongest legitimate reason to retaliate.
            if (counterFire) playerScore += 3.0f;
            if (AdvancedGunneryDoctrineDirector.PreferPlayer(kind, round)) playerScore += 1.6f;

            // v9.5 fire-mission rounds make Siege/Heavy more valuable against Orzelek pressure,
            // while Sniper/Elite remain the hunter screen protecting that mission.
            if (FireMissionNetworkDirector.IsFireMissionRound(round))
            {
                if (kind == EnemyKind.Siege) baseScore += 2.1f;
                else if (kind == EnemyKind.Heavy) baseScore += 1.1f;
                else if (kind == EnemyKind.Sniper || kind == EnemyKind.Elite) playerScore += 0.9f;
            }

            int playerCommitments = Count(true, precisionOnly: true);
            int baseCommitments = Count(false, precisionOnly: true);
            if (precision)
            {
                playerScore -= playerCommitments * 1.35f;
                baseScore -= baseCommitments * 1.35f;
                if (playerCommitments >= MaxPrecisionCommitmentsPerTarget) playerScore -= 4.0f;
                if (baseCommitments >= MaxPrecisionCommitmentsPerTarget) baseScore -= 4.0f;
            }

            bool player = playerScore >= baseScore;
            Reserve(id, player, precision);
            return player;
        }

        public static void Release(EnemyTank actor)
        {
            if (actor == null) return;
            int id = actor.GetInstanceID();
            for (int i = Reservations.Count - 1; i >= 0; i--)
                if (Reservations[i].ActorId == id) Reservations.RemoveAt(i);
        }

        public static void Reset()
        {
            Reservations.Clear();
        }

        private static bool IsPrecision(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
        }

        private static Reservation Find(int actorId)
        {
            for (int i = 0; i < Reservations.Count; i++) if (Reservations[i].ActorId == actorId) return Reservations[i];
            return null;
        }

        private static int Count(bool playerTarget, bool precisionOnly)
        {
            int count = 0;
            for (int i = 0; i < Reservations.Count; i++)
                if (Reservations[i].PlayerTarget == playerTarget && (!precisionOnly || Reservations[i].Precision)) count++;
            return count;
        }

        private static void Reserve(int actorId, bool playerTarget, bool precision)
        {
            if (Reservations.Count >= MaxActors)
            {
                int oldest = 0;
                for (int i = 1; i < Reservations.Count; i++) if (Reservations[i].ExpiresAt < Reservations[oldest].ExpiresAt) oldest = i;
                Reservations.RemoveAt(oldest);
            }
            Reservations.Add(new Reservation { ActorId = actorId, PlayerTarget = playerTarget, Precision = precision, ExpiresAt = Time.time + ReservationSeconds });
        }

        private static void Prune()
        {
            float now = Time.time;
            for (int i = Reservations.Count - 1; i >= 0; i--) if (Reservations[i].ExpiresAt <= now) Reservations.RemoveAt(i);
        }
    }
}
