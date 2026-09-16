using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.5 bounded platoon command layer. It never fires weapons or applies damage;
    /// it only assigns roles and target intent to existing EnemyTank authority.
    /// </summary>
    public static class PlatoonFireMissionCoordinator
    {
        public enum PlatoonRole { Commander, Hunter, FireSupport, Breacher }
        private sealed class Reservation
        {
            public int ActorId;
            public bool PlayerTarget;
            public bool Precision;
            public int PrioritySignature;
            public float ExpiresAt;
        }

        public const int MaxActors = 24;
        public const int MaxPrecisionCommitmentsPerTarget = 2;
        public const float ReservationSeconds = 1.35f;
        public const float AssaultPhaseSeconds = 8f;
        private static readonly List<Reservation> Reservations = new List<Reservation>(MaxActors);

        public static bool ConfigurationValid => MaxActors <= 24 && MaxActors >= 12 && MaxPrecisionCommitmentsPerTarget == 2 && ReservationSeconds >= 0.8f && ReservationSeconds <= 1.6f && AssaultPhaseSeconds >= 6f;
        public static int ActiveReservations { get { Prune(); return Reservations.Count; } }

        public static PlatoonRole RoleFor(EnemyTank actor, EnemyKind kind)
        {
            if (kind == EnemyKind.Siege) return PlatoonRole.FireSupport;
            if (kind == EnemyKind.Sniper || kind == EnemyKind.Elite) return PlatoonRole.Hunter;
            if (kind == EnemyKind.Heavy) return (actor != null && (actor.GetInstanceID() & 3) == 0) ? PlatoonRole.Commander : PlatoonRole.Breacher;
            return PlatoonRole.Hunter;
        }

        public static bool PreferPlayer(EnemyTank actor, EnemyKind kind, int round, bool localPreference, bool counterFire)
        {
            if (actor == null || kind == EnemyKind.Boss || kind == EnemyKind.Supply || kind == EnemyKind.Basic || kind == EnemyKind.Fast)
                return localPreference;

            Prune();
            int id = actor.GetInstanceID();
            PlatoonRole role = RoleFor(actor, kind);
            bool fireMission = FireMissionNetworkDirector.IsFireMissionRound(round);
            bool doctrinePlayer = AdvancedGunneryDoctrineDirector.PreferPlayer(kind, round);
            int signature = PrioritySignature(localPreference, counterFire, fireMission, doctrinePlayer, role);

            Reservation own = Find(id);
            // v11.5 target handoff: keep a reservation only while the strategic inputs are unchanged.
            // A counter-fire event, doctrine shift or fire-mission transition invalidates it immediately.
            if (own != null && own.PrioritySignature == signature)
                return own.PlayerTarget;
            if (own != null) Remove(id);

            bool precision = IsPrecision(kind);
            float playerScore = localPreference ? 2.0f : 0.65f;
            float baseScore = localPreference ? 0.70f : 1.75f;

            if (counterFire) playerScore += 3.0f;
            if (doctrinePlayer) playerScore += 1.6f;

            // Role doctrine: Hunters screen the player, Breachers and FireSupport pressure Orzelek,
            // Commanders flex toward whichever strategic signal is currently strongest.
            if (role == PlatoonRole.Hunter) playerScore += 1.15f;
            else if (role == PlatoonRole.Breacher) baseScore += 1.10f;
            else if (role == PlatoonRole.FireSupport) baseScore += 1.35f;
            else if (role == PlatoonRole.Commander && counterFire) playerScore += 0.65f;

            if (fireMission)
            {
                if (kind == EnemyKind.Siege) baseScore += 2.1f;
                else if (kind == EnemyKind.Heavy) baseScore += 1.1f;
                else if (kind == EnemyKind.Sniper || kind == EnemyKind.Elite) playerScore += 0.9f;
            }

            // Coordinated assault alternates breach and hunter windows. It changes intent only;
            // existing reload/projectile/damage authority remains untouched.
            bool breachWindow = ((int)(Time.time / AssaultPhaseSeconds) + round) % 2 == 0;
            if (breachWindow)
            {
                if (role == PlatoonRole.Breacher || role == PlatoonRole.FireSupport) baseScore += 0.85f;
                if (role == PlatoonRole.Hunter) playerScore += 0.35f;
            }
            else
            {
                if (role == PlatoonRole.Hunter) playerScore += 0.85f;
                if (role == PlatoonRole.Commander) playerScore += 0.35f;
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
            Reserve(id, player, precision, signature);
            return player;
        }

        public static void Release(EnemyTank actor)
        {
            if (actor != null) Remove(actor.GetInstanceID());
        }

        public static void Reset() { Reservations.Clear(); }

        private static bool IsPrecision(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
        }

        private static int PrioritySignature(bool local, bool counter, bool mission, bool doctrine, PlatoonRole role)
        {
            int value = local ? 1 : 0;
            if (counter) value |= 2;
            if (mission) value |= 4;
            if (doctrine) value |= 8;
            value |= ((int)role + 1) << 4;
            return value;
        }

        private static Reservation Find(int actorId)
        {
            for (int i = 0; i < Reservations.Count; i++) if (Reservations[i].ActorId == actorId) return Reservations[i];
            return null;
        }

        private static void Remove(int actorId)
        {
            for (int i = Reservations.Count - 1; i >= 0; i--) if (Reservations[i].ActorId == actorId) Reservations.RemoveAt(i);
        }

        private static int Count(bool playerTarget, bool precisionOnly)
        {
            int count = 0;
            for (int i = 0; i < Reservations.Count; i++)
                if (Reservations[i].PlayerTarget == playerTarget && (!precisionOnly || Reservations[i].Precision)) count++;
            return count;
        }

        private static void Reserve(int actorId, bool playerTarget, bool precision, int signature)
        {
            if (Reservations.Count >= MaxActors)
            {
                int oldest = 0;
                for (int i = 1; i < Reservations.Count; i++) if (Reservations[i].ExpiresAt < Reservations[oldest].ExpiresAt) oldest = i;
                Reservations.RemoveAt(oldest);
            }
            Reservations.Add(new Reservation { ActorId = actorId, PlayerTarget = playerTarget, Precision = precision, PrioritySignature = signature, ExpiresAt = Time.time + ReservationSeconds });
        }

        private static void Prune()
        {
            float now = Time.time;
            for (int i = Reservations.Count - 1; i >= 0; i--) if (Reservations[i].ExpiresAt <= now) Reservations.RemoveAt(i);
        }
    }
}
