using UnityEngine;

namespace TankRevival
{
    public enum ComponentCasualtyTactic
    {
        FightThrough,
        Screen,
        Hold,
        Disengage,
        Recover
    }

    /// <summary>
    /// Deterministic advisory layer for v12.9 component casualties. It returns intent only;
    /// EnemyTank remains the sole enemy movement/fire authority and EmergencyRepairSystem owns recovery channels.
    /// </summary>
    public static class ComponentCasualtyTactics
    {
        public const float EvaluationInterval = 0.35f;
        public const float MinRecoveryDistance = 5.5f;
        public const float CriticalDisengageDistance = 3.8f;
        public const float ScreenSpeedScale = 0.72f;
        public const float DisengageSpeedScale = 0.88f;

        public static bool ConfigurationValid =>
            EvaluationInterval >= 0.20f && EvaluationInterval <= 0.60f &&
            MinRecoveryDistance >= 5f && CriticalDisengageDistance >= 3f &&
            CriticalDisengageDistance < MinRecoveryDistance &&
            ScreenSpeedScale >= 0.55f && ScreenSpeedScale <= 0.85f &&
            DisengageSpeedScale >= 0.75f && DisengageSpeedScale <= 1f;

        public static ComponentCasualtyTactic Resolve(
            EnemyKind kind,
            bool mobilityCritical,
            bool weaponCritical,
            bool mobilityKilled,
            bool weaponDisabled,
            float playerDistance,
            bool hasRepairCharge)
        {
            float distance = Mathf.Max(0f, playerDistance);

            if (hasRepairCharge && distance >= MinRecoveryDistance &&
                (mobilityKilled || (mobilityCritical && weaponCritical)))
                return ComponentCasualtyTactic.Recover;

            if (weaponDisabled)
                return mobilityKilled ? ComponentCasualtyTactic.Hold : ComponentCasualtyTactic.Disengage;

            if (mobilityKilled)
                return ComponentCasualtyTactic.Hold;

            if (mobilityCritical && weaponCritical)
                return ComponentCasualtyTactic.Disengage;

            if (mobilityCritical)
            {
                if (kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Boss)
                    return ComponentCasualtyTactic.Screen;
                return ComponentCasualtyTactic.Disengage;
            }

            if (weaponCritical)
            {
                if (kind == EnemyKind.Sniper || kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Boss)
                    return ComponentCasualtyTactic.Hold;
                return ComponentCasualtyTactic.Screen;
            }

            return ComponentCasualtyTactic.FightThrough;
        }

        public static ComponentCasualtyTactic Resolve(EnemyKind kind, ArmorSystem armor, float playerDistance, bool hasRepairCharge)
        {
            if (armor == null) return ComponentCasualtyTactic.FightThrough;
            return Resolve(kind, armor.IsMobilityCritical, armor.IsWeaponCritical, armor.IsMobilityKilled,
                armor.IsWeaponDisabled, playerDistance, hasRepairCharge);
        }

        public static float SpeedScale(ComponentCasualtyTactic tactic)
        {
            switch (tactic)
            {
                case ComponentCasualtyTactic.Screen: return ScreenSpeedScale;
                case ComponentCasualtyTactic.Disengage: return DisengageSpeedScale;
                case ComponentCasualtyTactic.Hold:
                case ComponentCasualtyTactic.Recover: return 0f;
                default: return 1f;
            }
        }

        public static bool CanFire(ComponentCasualtyTactic tactic, ArmorSystem armor)
        {
            if (tactic == ComponentCasualtyTactic.Recover) return false;
            return armor == null || !armor.IsWeaponDisabled;
        }

        public static Vector2 AdjustDirection(ComponentCasualtyTactic tactic, Vector2 actorPosition,
            Vector2 playerPosition, Vector2 fallback, int actorKey)
        {
            if (tactic == ComponentCasualtyTactic.Hold || tactic == ComponentCasualtyTactic.Recover)
                return Vector2.zero;
            if (tactic == ComponentCasualtyTactic.FightThrough)
                return Cardinalize(fallback);

            Vector2 away = actorPosition - playerPosition;
            if (away.sqrMagnitude < 0.01f) away = fallback.sqrMagnitude > 0.01f ? fallback : Vector2.down;
            away.Normalize();

            if (tactic == ComponentCasualtyTactic.Disengage)
                return Cardinalize(away);

            Vector2 screen = (actorKey & 1) == 0
                ? new Vector2(-away.y, away.x)
                : new Vector2(away.y, -away.x);
            return Cardinalize(screen);
        }

        private static Vector2 Cardinalize(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return Vector2.zero;
            return Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }
    }
}
