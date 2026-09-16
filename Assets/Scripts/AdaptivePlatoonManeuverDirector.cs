using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.6 bounded maneuver-intent layer. It coordinates specialist movement only;
    /// EnemyTank/Rigidbody2D remain the sole movement authority.
    /// v11.7 adds read-only presentation snapshots so HUD/VFX can describe this authority
    /// without gaining write access to movement, damage or projectile state.
    /// </summary>
    public static class AdaptivePlatoonManeuverDirector
    {
        public enum ManeuverPresentationState
        {
            Advance,
            Envelopment,
            Standoff,
            CounterFireDisplacement,
            Reorganizing
        }

        public struct ManeuverPresentationSnapshot
        {
            public int PhaseIndex;
            public float Phase01;
            public bool Reorganizing;
            public float CasualtyPressure01;
            public int LossEpoch;
            public int SpecialistLosses;
            public GunneryDoctrine Doctrine;
        }

        public const float HunterFlankBand = 4.8f;
        public const float SiegeMinStandoff = 5.4f;
        public const float SiegeMaxStandoff = 8.6f;
        public const float CounterFireDisplaceBand = 7.2f;
        public const float ReorgSeconds = 3.5f;
        public const float EncirclementPhaseSeconds = 6.0f;
        public const int MaxTrackedActors = 24;

        private static int _lossEpoch;
        private static float _lastLossTime = -99f;
        private static readonly Dictionary<EnemyKind, int> Losses = new Dictionary<EnemyKind, int>();

        public static bool ConfigurationValid => HunterFlankBand >= 3.5f && HunterFlankBand <= 6.5f && SiegeMinStandoff >= 4f && SiegeMaxStandoff > SiegeMinStandoff && SiegeMaxStandoff <= 10f && CounterFireDisplaceBand >= SiegeMinStandoff && MaxTrackedActors == PlatoonFireMissionCoordinator.MaxActors && ReorgSeconds > 0f && EncirclementPhaseSeconds >= ReorgSeconds;
        public static int LossEpoch => _lossEpoch;
        public static bool Reorganizing => Time.time - _lastLossTime < ReorgSeconds;
        public static int LossesOf(EnemyKind kind) => Losses.TryGetValue(kind, out int count) ? count : 0;
        public static int SpecialistLosses => LossesOf(EnemyKind.Heavy) + LossesOf(EnemyKind.Sniper) + LossesOf(EnemyKind.Siege) + LossesOf(EnemyKind.Elite);
        public static float CasualtyPressure01 => Mathf.Clamp01(SpecialistLosses / 8f);

        public static bool SupportsPresentation(EnemyKind kind)
        {
            return IsSpecialist(kind);
        }

        public static ManeuverPresentationSnapshot ReadPresentationSnapshot(int round)
        {
            int phase = Mathf.FloorToInt(Time.time / EncirclementPhaseSeconds);
            GunneryDoctrine doctrine = AdvancedGunneryDoctrineDirector.Instance != null
                ? AdvancedGunneryDoctrineDirector.Instance.ActiveDoctrine
                : (round >= 70 ? GunneryDoctrine.CounterFire : round >= 35 ? GunneryDoctrine.HunterKiller : GunneryDoctrine.Standard);
            return new ManeuverPresentationSnapshot
            {
                PhaseIndex = phase,
                Phase01 = Mathf.Repeat(Time.time, EncirclementPhaseSeconds) / EncirclementPhaseSeconds,
                Reorganizing = Reorganizing,
                CasualtyPressure01 = CasualtyPressure01,
                LossEpoch = _lossEpoch,
                SpecialistLosses = SpecialistLosses,
                Doctrine = doctrine
            };
        }

        public static ManeuverPresentationState PresentationStateFor(EnemyTank actor, EnemyKind kind, bool counterFire)
        {
            if (Reorganizing) return ManeuverPresentationState.Reorganizing;
            PlatoonFireMissionCoordinator.PlatoonRole role = PlatoonFireMissionCoordinator.RoleFor(actor, kind);
            if (role == PlatoonFireMissionCoordinator.PlatoonRole.FireSupport)
                return counterFire ? ManeuverPresentationState.CounterFireDisplacement : ManeuverPresentationState.Standoff;
            if (role == PlatoonFireMissionCoordinator.PlatoonRole.Hunter)
                return ManeuverPresentationState.Envelopment;
            return ManeuverPresentationState.Advance;
        }

        public static void NotifyLoss(EnemyKind kind)
        {
            if (!IsSpecialist(kind)) return;
            _lossEpoch++;
            _lastLossTime = Time.time;
            Losses[kind] = LossesOf(kind) + 1;
        }

        public static Vector2 DesiredDirection(EnemyTank actor, EnemyKind kind, Vector2 actorPosition, Vector2 playerPosition, Vector2 eaglePosition, bool targetPlayer, bool counterFire, int round)
        {
            if (actor == null || !IsSpecialist(kind))
                return Cardinal((targetPlayer ? playerPosition : eaglePosition) - actorPosition);

            PlatoonFireMissionCoordinator.PlatoonRole role = PlatoonFireMissionCoordinator.RoleFor(actor, kind);
            Vector2 target = targetPlayer ? playerPosition : eaglePosition;
            Vector2 toTarget = target - actorPosition;
            float distance = toTarget.magnitude;
            Vector2 forward = distance > .01f ? toTarget / distance : Vector2.down;
            Vector2 side = new Vector2(-forward.y, forward.x);

            // A six-second encirclement clock gives the platoon a shared maneuver rhythm.
            // Casualties flip the parity so survivors do not keep marching into a collapsed lane.
            int phase = Mathf.FloorToInt(Time.time / EncirclementPhaseSeconds);
            int parity = (actor.GetInstanceID() ^ (_lossEpoch * 397) ^ round ^ phase) & 1;
            if (parity == 0) side = -side;

            int hunterLosses = LossesOf(EnemyKind.Sniper) + LossesOf(EnemyKind.Elite);
            int breachLosses = LossesOf(EnemyKind.Heavy);
            int supportLosses = LossesOf(EnemyKind.Siege);
            float casualtyPressure = Mathf.Clamp01((hunterLosses + breachLosses + supportLosses) / 8f);

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.Hunter)
            {
                // Hunters form alternating left/right hooks. After hunter losses, surviving flankers
                // widen and rotate their lane instead of repeatedly entering the broken flank.
                float recoveryWidth = Mathf.Min(1.8f, hunterLosses * .35f);
                float sweep = Mathf.Sin((Time.time + (actor.GetInstanceID() & 7)) * .55f) * 1.4f;
                Vector2 flankPoint = playerPosition + side * (HunterFlankBand + recoveryWidth) + forward * sweep;
                if (Reorganizing) flankPoint += side * (1.0f + casualtyPressure);
                return Cardinal(flankPoint - actorPosition);
            }

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.FireSupport)
            {
                // Counter-fire always wins over normal standoff behavior. Reorganization also moves
                // surviving support laterally so artillery does not remain in the lane where a unit died.
                if (counterFire || (Reorganizing && supportLosses > 0))
                {
                    float displacement = CounterFireDisplaceBand + Mathf.Min(1.2f, supportLosses * .3f);
                    Vector2 displacePoint = target + side * displacement - forward * (1.2f + casualtyPressure);
                    return Cardinal(displacePoint - actorPosition);
                }
                if (distance < SiegeMinStandoff) return Cardinal(-forward + side * .25f);
                if (distance > SiegeMaxStandoff) return Cardinal(forward + side * .15f);
                return Cardinal(side);
            }

            if (role == PlatoonFireMissionCoordinator.PlatoonRole.Breacher)
            {
                // Heavy losses make remaining breachers spread rather than stack on the same frontal lane.
                float lane = .22f + Mathf.Min(.38f, breachLosses * .08f);
                if (Reorganizing) lane += .20f;
                return Cardinal(forward + side * lane);
            }

            // Commander is the formation recovery pivot. During casualty recovery it shifts across the
            // line while retaining forward pressure, then resumes the normal command lane.
            if (Reorganizing)
            {
                float lateral = .85f + casualtyPressure * .55f;
                return Cardinal(side * lateral + forward * .35f);
            }
            return Cardinal(forward + side * .12f);
        }

        private static bool IsSpecialist(EnemyKind kind)
        {
            return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
        }

        private static Vector2 Cardinal(Vector2 v)
        {
            if (v.sqrMagnitude < .001f) return Vector2.down;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y) ? new Vector2(Mathf.Sign(v.x), 0f) : new Vector2(0f, Mathf.Sign(v.y));
        }
    }
}
