using UnityEngine;

namespace TankRevival
{
    public enum ArmorZone
    {
        Front,
        Side,
        Rear
    }

    /// <summary>
    /// Runtime armor model shared by player and enemy tanks. Incoming shells are
    /// evaluated against the tank's hull orientation, producing frontal/side/rear
    /// damage, ricochets and persistent module damage for the lifetime of a tank.
    /// </summary>
    public sealed class ArmorSystem : MonoBehaviour
    {
        public float MobilityMultiplier { get; private set; } = 1f;
        public float ReloadMultiplier { get; private set; } = 1f;
        public ArmorZone LastZone { get; private set; } = ArmorZone.Front;
        public bool LastCritical { get; private set; }

        private float _frontDamage = 0.72f;
        private float _sideDamage = 0.94f;
        private float _rearDamage = 1.34f;
        private float _frontRicochet = 0.22f;
        private float _sideRicochet = 0.08f;
        private float _criticalChance = 0.10f;
        private bool _player;

        public void InitializePlayer()
        {
            _player = true;
            _frontDamage = 0.70f;
            _sideDamage = 0.92f;
            _rearDamage = 1.30f;
            _frontRicochet = 0.26f;
            _sideRicochet = 0.10f;
            _criticalChance = 0.08f;
        }

        public void InitializeEnemy(EnemyKind kind, int round)
        {
            _player = false;
            float progress = Mathf.Clamp01((round - 1f) / 99f);

            switch (kind)
            {
                case EnemyKind.Fast:
                    _frontDamage = 0.86f;
                    _sideDamage = 1.00f;
                    _rearDamage = 1.42f;
                    _frontRicochet = 0.08f;
                    _sideRicochet = 0.02f;
                    break;
                case EnemyKind.Heavy:
                    _frontDamage = 0.54f;
                    _sideDamage = 0.78f;
                    _rearDamage = 1.24f;
                    _frontRicochet = 0.38f;
                    _sideRicochet = 0.16f;
                    break;
                case EnemyKind.Siege:
                    _frontDamage = 0.50f;
                    _sideDamage = 0.75f;
                    _rearDamage = 1.32f;
                    _frontRicochet = 0.42f;
                    _sideRicochet = 0.18f;
                    break;
                case EnemyKind.Elite:
                    _frontDamage = 0.58f;
                    _sideDamage = 0.82f;
                    _rearDamage = 1.28f;
                    _frontRicochet = 0.34f;
                    _sideRicochet = 0.14f;
                    break;
                case EnemyKind.Supply:
                    _frontDamage = 0.78f;
                    _sideDamage = 0.96f;
                    _rearDamage = 1.44f;
                    _frontRicochet = 0.12f;
                    _sideRicochet = 0.04f;
                    break;
                case EnemyKind.Boss:
                    _frontDamage = Mathf.Lerp(0.48f, 0.36f, progress);
                    _sideDamage = Mathf.Lerp(0.72f, 0.58f, progress);
                    _rearDamage = Mathf.Lerp(1.18f, 1.06f, progress);
                    _frontRicochet = Mathf.Lerp(0.42f, 0.55f, progress);
                    _sideRicochet = Mathf.Lerp(0.16f, 0.26f, progress);
                    break;
                default:
                    _frontDamage = 0.76f;
                    _sideDamage = 0.96f;
                    _rearDamage = 1.38f;
                    _frontRicochet = 0.18f;
                    _sideRicochet = 0.07f;
                    break;
            }

            _criticalChance = Mathf.Lerp(0.10f, 0.16f, progress);
        }

        public int ResolveIncoming(int rawDamage, AmmoType ammo, Vector2 projectileVelocity, Vector2 hitPoint, out bool ricochet, out bool critical)
        {
            rawDamage = Mathf.Max(1, rawDamage);
            ricochet = false;
            critical = false;
            LastCritical = false;

            Vector2 incoming = projectileVelocity.sqrMagnitude > 0.001f ? projectileVelocity.normalized : Vector2.down;
            Vector2 hullForward = transform.up;
            float dot = Vector2.Dot(incoming, hullForward);

            if (dot <= -0.48f)
                LastZone = ArmorZone.Front;
            else if (dot >= 0.48f)
                LastZone = ArmorZone.Rear;
            else
                LastZone = ArmorZone.Side;

            float damageMultiplier = LastZone == ArmorZone.Front ? _frontDamage : LastZone == ArmorZone.Side ? _sideDamage : _rearDamage;
            float ricochetChance = LastZone == ArmorZone.Front ? _frontRicochet : LastZone == ArmorZone.Side ? _sideRicochet : 0.01f;

            // Dedicated anti-armor ammunition defeats sloped armor more reliably.
            if (ammo == AmmoType.ArmorPiercing)
            {
                damageMultiplier *= 1.22f;
                ricochetChance *= 0.24f;
            }
            else if (ammo == AmmoType.Plasma)
            {
                damageMultiplier *= 1.32f;
                ricochetChance = 0f;
            }
            else if (ammo == AmmoType.Explosive)
            {
                ricochetChance *= 0.32f;
            }

            // Large raw hits are much less likely to simply bounce away.
            ricochetChance /= Mathf.Max(1f, 0.72f + rawDamage * 0.28f);
            ricochet = Random.value < ricochetChance;
            if (ricochet)
            {
                VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.82f, 0.48f), 0.66f);
                VisualFactory.RingPulse(hitPoint, new Color(1f, 0.62f, 0.18f), 0.45f);
                BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.48f, 0.05f);
                return 0;
            }

            float zoneCrit = LastZone == ArmorZone.Rear ? 0.24f : LastZone == ArmorZone.Side ? 0.10f : 0f;
            float ammoCrit = ammo == AmmoType.ArmorPiercing ? 0.10f : ammo == AmmoType.Plasma ? 0.16f : 0f;
            critical = Random.value < Mathf.Clamp01(_criticalChance + zoneCrit + ammoCrit);
            LastCritical = critical;

            if (critical)
            {
                ApplyModuleDamage(hitPoint);
                damageMultiplier *= 1.25f;
            }

            return Mathf.Max(1, Mathf.RoundToInt(rawDamage * damageMultiplier));
        }

        private void ApplyModuleDamage(Vector2 hitPoint)
        {
            if (Random.value < 0.52f)
            {
                MobilityMultiplier = Mathf.Max(_player ? 0.62f : 0.48f, MobilityMultiplier - (_player ? 0.11f : 0.16f));
                VisualFactory.RingPulse(hitPoint, new Color(1f, 0.34f, 0.08f), 0.78f);
            }
            else
            {
                ReloadMultiplier = Mathf.Min(_player ? 1.55f : 1.80f, ReloadMultiplier + (_player ? 0.13f : 0.19f));
                VisualFactory.RingPulse(hitPoint, new Color(1f, 0.10f, 0.05f), 0.78f);
            }

            VisualFactory.MicroBurst(hitPoint, new Color(1f, 0.18f, 0.05f), 0.90f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.34f, 0.04f);
        }
    }
}
