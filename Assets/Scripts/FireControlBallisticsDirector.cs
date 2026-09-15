using UnityEngine;

namespace TankRevival
{
    /// <summary>v11.2 shared fire-control model. Keeps Projectile/Health authoritative and only shapes shot solution.</summary>
    public static class FireControlBallisticsDirector
    {
        public const float MinAccuracy = 0.58f;
        public const float MaxSpreadDegrees = 7.5f;

        public static float Stabilization(float movement01, ArmorSystem armor)
        {
            float module = armor != null ? armor.WeaponFunctionMultiplier : 1f;
            float mobilityPenalty = Mathf.Clamp01(movement01) * 0.26f;
            return Mathf.Clamp(module - mobilityPenalty, MinAccuracy, 1f);
        }

        public static float SpreadDegrees(AmmoType ammo, float movement01, ArmorSystem armor)
        {
            float stabilization = Stabilization(movement01, armor);
            float baseSpread = ammo == AmmoType.ArmorPiercing ? 0.75f : ammo == AmmoType.Plasma ? 1.15f : ammo == AmmoType.Explosive ? 1.55f : 1.0f;
            float movement = Mathf.Clamp01(movement01) * 3.2f;
            float damage = (1f - stabilization) * 5.2f;
            return Mathf.Clamp(baseSpread + movement + damage, 0.35f, MaxSpreadDegrees);
        }

        public static Vector2 ApplySpread(Vector2 direction, float degrees)
        {
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
            float angle = Random.Range(-degrees, degrees);
            return Quaternion.Euler(0f, 0f, angle) * direction.normalized;
        }

        public static Vector2 Lead(Vector2 shooter, Vector2 target, Vector2 targetVelocity, float projectileSpeed)
        {
            float distance = Vector2.Distance(shooter, target);
            float time = distance / Mathf.Max(2f, projectileSpeed);
            return target + targetVelocity * Mathf.Clamp(time, 0f, 1.25f);
        }

        public static bool ConfigurationValid => MinAccuracy >= 0.5f && MaxSpreadDegrees <= 8f;
    }
}
