using System;
using UnityEngine;

namespace TankRevival
{
    public sealed class Projectile : MonoBehaviour
    {
        public Team OwnerTeam { get; private set; }
        public int Damage { get; private set; }
        public AmmoType Ammo { get; private set; }

        /// <summary>
        /// Presentation/combat telemetry hooks. Gameplay authority remains inside Projectile/Health.
        /// </summary>
        public static event Action<Projectile, Vector3, Vector2, Team, AmmoType> ShotSpawned3D;
        public static event Action<Projectile, Vector3, Team, AmmoType, bool, bool> Impact3D;
        public static event Action<Projectile, Health, int, bool> DamageResolved;

        private Rigidbody2D _body;
        private float _dieAt;
        private int _penetrations;
        private Color _color;
        private float _nextTrail;

        public void Initialize(Vector2 direction, Team ownerTeam, int damage, float speed, Color color, AmmoType ammo = AmmoType.Basic)
        {
            OwnerTeam = ownerTeam;
            Damage = Mathf.Max(1, damage);
            Ammo = ammo;
            _color = color;
            _penetrations = ammo == AmmoType.Plasma ? 3 : ammo == AmmoType.ArmorPiercing ? 1 : 0;

            float scale = ammo == AmmoType.Plasma ? 1.42f : ammo == AmmoType.Explosive ? 1.18f : 1f;
            VisualFactory.Disc("ProjectileGlow", transform, new Vector2(0.30f, 0.30f) * scale, new Color(color.r, color.g, color.b, 0.25f), Vector3.zero, 34);
            VisualFactory.Disc("ProjectileCore", transform, new Vector2(0.115f, 0.20f) * scale, color, Vector3.zero, 35);
            if (ammo == AmmoType.Plasma || ammo == AmmoType.EMP)
                VisualFactory.RingObject("ProjectileRing", transform, new Vector2(0.27f, 0.27f) * scale, new Color(color.r, color.g, color.b, 0.74f), Vector3.zero, 33);

            Vector2 shotDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            float angle = Mathf.Atan2(shotDirection.y, shotDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = ammo == AmmoType.Plasma ? 0.13f : 0.095f;
            collider.isTrigger = true;

            _body = gameObject.AddComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.linearVelocity = shotDirection * speed;

            _dieAt = Time.time + 5f;
            _nextTrail = Time.time;
            ShotSpawned3D?.Invoke(this, transform.position, shotDirection, OwnerTeam, Ammo);
        }

        private void Update()
        {
            if (Time.time >= _dieAt)
            {
                Destroy(gameObject);
                return;
            }

            if (Ammo != AmmoType.Basic && Time.time >= _nextTrail)
            {
                _nextTrail = Time.time + (Ammo == AmmoType.Plasma ? 0.025f : 0.045f);
                VisualFactory.ProjectileAfterglow(transform.position, _color, Ammo == AmmoType.Plasma ? 0.42f : 0.28f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponent<Health>();
            if (health != null)
            {
                if (health.Team == OwnerTeam && OwnerTeam != Team.Neutral) return;

                int resolvedDamage = Damage;
                var armor = other.GetComponent<ArmorSystem>();
                if (armor != null)
                {
                    Vector2 velocity = _body != null ? _body.linearVelocity : (Vector2)transform.up;
                    resolvedDamage = armor.ResolveIncoming(Damage, Ammo, velocity, transform.position, out bool ricochet, out bool critical);
                    if (ricochet)
                    {
                        Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, false, true);
                        Destroy(gameObject);
                        return;
                    }

                    if (critical)
                    {
                        VisualFactory.MicroBurst(transform.position, new Color(1f, 0.12f, 0.04f), 1.0f);
                        VisualFactory.RingPulse(transform.position, new Color(1f, 0.24f, 0.06f), 0.88f);
                    }
                }

                if (!health.Damage(resolvedDamage, OwnerTeam)) return;
                bool killed = health.IsDead;
                DamageResolved?.Invoke(this, health, resolvedDamage, killed);
                ApplyStatus(health);

                bool explosive = Ammo == AmmoType.Explosive;
                if (explosive)
                    Detonate(health);
                else
                    VisualFactory.MicroBurst(transform.position, _color, Ammo == AmmoType.Plasma ? 0.72f : 0.46f);

                Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, explosive, false);

                if (CanPenetrate())
                {
                    _penetrations--;
                    return;
                }

                Destroy(gameObject);
                return;
            }

            var obstacle = other.GetComponent<Obstacle>();
            if (obstacle == null) return;
            if (obstacle.Kind == ObstacleKind.Water) return;

            bool steel = obstacle.Kind == ObstacleKind.Steel;
            obstacle.Hit(Damage, transform.position);

            if (Ammo == AmmoType.Explosive)
            {
                Detonate(null);
                Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, true, false);
                Destroy(gameObject);
                return;
            }

            if (CanPenetrate())
            {
                _penetrations--;
                VisualFactory.MicroBurst(transform.position, _color, 0.36f);
                Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, false, false);
                return;
            }

            if (steel)
                BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.42f, 0.08f);

            Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, false, steel);
            Destroy(gameObject);
        }

        private bool CanPenetrate()
        {
            return _penetrations > 0 && (Ammo == AmmoType.ArmorPiercing || Ammo == AmmoType.Plasma);
        }

        private void ApplyStatus(Health target)
        {
            if (target == null || target.IsDead) return;

            if (Ammo == AmmoType.EMP || Ammo == AmmoType.Incendiary)
            {
                var status = target.GetComponent<CombatStatus>();
                if (status == null) status = target.gameObject.AddComponent<CombatStatus>();

                if (Ammo == AmmoType.EMP)
                    status.ApplyEmp(2.4f);
                else
                    status.ApplyBurn(OwnerTeam, 3.2f, 1, 0.78f);
            }
        }

        private void Detonate(Health primary)
        {
            const float radius = 1.05f;
            VisualFactory.Explosion(transform.position, _color, 0.95f);
            BattleAudio.PlayGlobal(SoundCue.ExplosionSmall, 0.62f, 0.05f);

            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in hits)
            {
                var health = hit.GetComponent<Health>();
                if (health != null && health != primary && health.Team != OwnerTeam)
                {
                    int splash = Mathf.Max(1, Damage - 1);
                    if (health.Damage(splash, OwnerTeam))
                        DamageResolved?.Invoke(this, health, splash, health.IsDead);
                }

                var obstacle = hit.GetComponent<Obstacle>();
                if (obstacle != null && obstacle.Kind == ObstacleKind.Brick)
                    obstacle.Hit(Mathf.Max(1, Damage), transform.position);
            }
        }
    }
}
