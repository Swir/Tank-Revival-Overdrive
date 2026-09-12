using System;
using UnityEngine;

namespace TankRevival
{
    public sealed class Projectile : MonoBehaviour
    {
        public Team OwnerTeam { get; private set; }
        public int Damage { get; private set; }
        public AmmoType Ammo { get; private set; }

        public static event Action<Projectile, Vector3, Vector2, Team, AmmoType> ShotSpawned3D;
        public static event Action<Projectile, Vector3, Team, AmmoType, bool, bool> Impact3D;
        public static event Action<Projectile, Health, int, bool> DamageResolved;

        private Rigidbody2D _body;
        private CircleCollider2D _collider;
        private SpriteRenderer _glow;
        private SpriteRenderer _core;
        private SpriteRenderer _ring;
        private float _dieAt;
        private int _penetrations;
        private Color _color;
        private float _nextTrail;
        private bool _initialized;
        private bool _releasing;

        private void Awake()
        {
            EnsureRuntimeParts();
        }

        private void EnsureRuntimeParts()
        {
            if (_collider == null)
            {
                _collider = GetComponent<CircleCollider2D>();
                if (_collider == null) _collider = gameObject.AddComponent<CircleCollider2D>();
                _collider.isTrigger = true;
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
                if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
                _body.gravityScale = 0f;
                _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (_glow == null)
                _glow = VisualFactory.Disc("ProjectileGlow", transform, new Vector2(0.30f, 0.30f), Color.white, Vector3.zero, 34).GetComponent<SpriteRenderer>();
            if (_core == null)
                _core = VisualFactory.Disc("ProjectileCore", transform, new Vector2(0.115f, 0.20f), Color.white, Vector3.zero, 35).GetComponent<SpriteRenderer>();
            if (_ring == null)
                _ring = VisualFactory.RingObject("ProjectileRing", transform, new Vector2(0.27f, 0.27f), Color.white, Vector3.zero, 33).GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector2 direction, Team ownerTeam, int damage, float speed, Color color, AmmoType ammo = AmmoType.Basic)
        {
            EnsureRuntimeParts();
            _releasing = false;
            _initialized = true;
            OwnerTeam = ownerTeam;
            Damage = Mathf.Max(1, damage);
            Ammo = ammo;
            _color = color;
            _penetrations = ammo == AmmoType.Plasma ? 3 : ammo == AmmoType.ArmorPiercing ? 1 : 0;

            float scale = ammo == AmmoType.Plasma ? 1.42f : ammo == AmmoType.Explosive ? 1.18f : 1f;
            _glow.transform.localScale = new Vector3(0.30f * scale, 0.30f * scale, 1f);
            _core.transform.localScale = new Vector3(0.115f * scale, 0.20f * scale, 1f);
            _ring.transform.localScale = new Vector3(0.27f * scale, 0.27f * scale, 1f);
            _glow.color = new Color(color.r, color.g, color.b, 0.25f);
            _core.color = color;
            _ring.color = new Color(color.r, color.g, color.b, 0.74f);
            _ring.gameObject.SetActive(ammo == AmmoType.Plasma || ammo == AmmoType.EMP);

            _collider.radius = ammo == AmmoType.Plasma ? 0.13f : 0.095f;
            _collider.enabled = true;
            _body.simulated = true;

            Vector2 shotDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            float angle = Mathf.Atan2(shotDirection.y, shotDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _body.linearVelocity = shotDirection * speed;
            _body.angularVelocity = 0f;

            _dieAt = Time.time + 5f;
            _nextTrail = Time.time;
            ShotSpawned3D?.Invoke(this, transform.position, shotDirection, OwnerTeam, Ammo);
        }

        public void PrepareForPool()
        {
            _initialized = false;
            _releasing = true;
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.simulated = false;
            }
            if (_collider != null) _collider.enabled = false;
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_initialized) return;
            if (Time.time >= _dieAt)
            {
                Recycle();
                return;
            }

            if (Ammo != AmmoType.Basic && Time.time >= _nextTrail)
            {
                float baseInterval = Ammo == AmmoType.Plasma ? 0.025f : 0.045f;
                if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced) baseInterval *= 1.45f;
                else if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival) baseInterval *= 2.2f;
                _nextTrail = Time.time + baseInterval;

                bool priority = OwnerTeam == Team.Player && Ammo == AmmoType.Plasma;
                if (MassBattleFxBudget.TryConsumeProjectileTrail(priority))
                    VisualFactory.ProjectileAfterglow(transform.position, _color, Ammo == AmmoType.Plasma ? 0.42f : 0.28f);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _releasing) return;

            var weakPoint = other.GetComponent<BossWeakPoint>();
            if (weakPoint != null && OwnerTeam == Team.Player)
            {
                if (!weakPoint.IsAvailable) return;
                if (weakPoint.ResolveHit(Damage, OwnerTeam, out int weakDamage))
                {
                    Health target = weakPoint.TargetHealth;
                    if (target != null)
                        DamageResolved?.Invoke(this, target, weakDamage, target.IsDead);
                    if (MassBattleFxBudget.TryConsumeMicroFx(true))
                        VisualFactory.MicroBurst(transform.position, Color.Lerp(_color, Color.white, 0.48f), 0.92f);
                    Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, Ammo == AmmoType.Explosive, false);
                    Recycle();
                }
                return;
            }

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
                        Recycle();
                        return;
                    }

                    if (critical && MassBattleFxBudget.TryConsumeMicroFx(true))
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
                else if (MassBattleFxBudget.TryConsumeMicroFx(killed || OwnerTeam == Team.Player))
                    VisualFactory.MicroBurst(transform.position, _color, Ammo == AmmoType.Plasma ? 0.72f : 0.46f);

                Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, explosive, false);

                if (CanPenetrate())
                {
                    _penetrations--;
                    return;
                }

                Recycle();
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
                Recycle();
                return;
            }

            if (CanPenetrate())
            {
                _penetrations--;
                if (MassBattleFxBudget.TryConsumeMicroFx(false))
                    VisualFactory.MicroBurst(transform.position, _color, 0.36f);
                Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, false, false);
                return;
            }

            if (steel)
                BattleAudio.PlayGlobal(SoundCue.Ricochet, 0.42f, 0.08f);

            Impact3D?.Invoke(this, transform.position, OwnerTeam, Ammo, false, steel);
            Recycle();
        }

        private void Recycle()
        {
            if (_releasing) return;
            _releasing = true;
            ProjectilePool.Release(this);
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

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (Collider2D hit in hits)
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

        private void OnDestroy()
        {
            ProjectilePool.Forget(this);
        }
    }
}
