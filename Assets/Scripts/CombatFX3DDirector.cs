using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.5 3D COMBAT FX.
    /// Converts weapon fire, projectile travel, impacts, tank destruction and the Orzelek loss
    /// event into lightweight mesh-based 3D effects while the existing 2D combat remains authoritative.
    /// </summary>
    public sealed class CombatFX3DDirector : MonoBehaviour
    {
        private const int MaxTransientFx = 210;
        private static int _activeTransientFx;

        private readonly HashSet<Health> _observedHealth = new HashSet<Health>();
        private readonly HashSet<WreckDecay> _observedWrecks = new HashSet<WreckDecay>();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatFX3DDirector>() != null) return;
            var go = new GameObject("CombatFX3DDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatFX3DDirector>();
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
            Projectile.ShotSpawned3D += OnShotSpawned;
            Projectile.Impact3D -= OnProjectileImpact;
            Projectile.Impact3D += OnProjectileImpact;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
            Projectile.Impact3D -= OnProjectileImpact;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.26f;
            HookHealth();
            UpgradeWrecks();
        }

        private void HookHealth()
        {
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Health health = all[i];
                if (health == null || _observedHealth.Contains(health)) continue;
                _observedHealth.Add(health);
                health.Died -= OnHealthDied;
                health.Died += OnHealthDied;
                health.Damaged -= OnHealthDamaged;
                health.Damaged += OnHealthDamaged;
            }
            _observedHealth.RemoveWhere(h => h == null);
        }

        private void UpgradeWrecks()
        {
            WreckDecay[] wrecks = FindObjectsByType<WreckDecay>(FindObjectsSortMode.None);
            for (int i = 0; i < wrecks.Length; i++)
            {
                WreckDecay wreck = wrecks[i];
                if (wreck == null || _observedWrecks.Contains(wreck)) continue;
                _observedWrecks.Add(wreck);
                if (wreck.GetComponent<Wreck3DPresentation>() == null)
                    wreck.gameObject.AddComponent<Wreck3DPresentation>().Initialize();
            }
            _observedWrecks.RemoveWhere(w => w == null);
        }

        private static void OnShotSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            Color color = ResolveAmmoColor(ammo, team);
            float force = ammo == AmmoType.Plasma ? 1.85f : ammo == AmmoType.Explosive || ammo == AmmoType.ArmorPiercing ? 1.38f : 1f;
            SpawnMuzzleFlash(position, direction, color, force);
            VehicleMotion3DDirector.NotifyShot(position, team, force);

            if (projectile != null && projectile.GetComponent<ProjectileTrail3D>() == null)
            {
                var trail = projectile.gameObject.AddComponent<ProjectileTrail3D>();
                trail.Initialize(ammo, color);
            }
        }

        private static void OnProjectileImpact(Projectile projectile, Vector3 position, Team team, AmmoType ammo, bool explosive, bool ricochet)
        {
            Color color = ricochet ? new Color(0.88f, 0.94f, 1f) : ResolveAmmoColor(ammo, team);
            SpawnImpact(position, color, explosive, ricochet);
        }

        private static Color ResolveAmmoColor(AmmoType ammo, Team team)
        {
            if (ammo == AmmoType.Basic)
                return team == Team.Player ? new Color(0.55f, 0.94f, 1f) : new Color(1f, 0.28f, 0.12f);
            return AmmoDatabase.Color(ammo);
        }

        private void OnHealthDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0) return;
            string objectName = health.gameObject.name;
            bool eagle = objectName.Contains("ORZELEK");
            if (!eagle) return;

            Vector3 point = health.transform.position + new Vector3(Random.Range(-0.20f, 0.20f), Random.Range(-0.16f, 0.22f), 0f);
            SpawnImpact(point, new Color(1f, 0.10f, 0.035f), false, false);
            SpawnShockwave(health.transform.position, new Color(1f, 0.08f, 0.025f), 0.42f, 0.95f, 0.24f);
        }

        private void OnHealthDied(Health health)
        {
            if (health == null) return;
            Vector3 position = health.transform.position;
            string objectName = health.gameObject.name;

            if (objectName.Contains("ORZELEK"))
            {
                SpawnEagleCollapse(position);
                return;
            }

            EnemyTank enemy = health.GetComponent<EnemyTank>();
            if (enemy != null)
            {
                float scale = enemy.Kind == EnemyKind.Boss ? 1.75f : enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege ? 1.30f : 1f;
                Color tint = enemy.Kind == EnemyKind.Boss ? new Color(1f, 0.24f, 0.03f) : new Color(1f, 0.46f, 0.08f);
                SpawnDestructionBurst(position, tint, scale, enemy.Kind == EnemyKind.Boss ? 12 : 7);
                return;
            }

            if (health.GetComponent<PlayerTank>() != null)
            {
                SpawnDestructionBurst(position, new Color(0.22f, 0.80f, 1f), 1.18f, 8);
                return;
            }

            // Player-owned fortress modules and tactical deployables get a smaller physical break-up.
            if (health.Team == Team.Player)
                SpawnDestructionBurst(position, new Color(0.34f, 0.82f, 1f), 0.62f, 4);
        }

        private static void SpawnMuzzleFlash(Vector3 position, Vector2 direction, Color color, float force)
        {
            if (!TryReserveFx()) return;
            var root = new GameObject("MuzzleFlash3D");
            root.transform.position = new Vector3(position.x, position.y, 0f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            Runtime3DFactory.Box("MuzzleCore3D", root.transform, new Vector3(0f, 0.11f, -0.52f),
                new Vector3(0.12f * force, 0.38f * force, 0.12f * force), Color.Lerp(color, Color.white, 0.55f), 0.02f, 0.92f);
            Runtime3DFactory.Box("MuzzleFlare3D", root.transform, new Vector3(0f, 0.23f, -0.50f),
                new Vector3(0.30f * force, 0.26f * force, 0.08f), color, 0.02f, 0.82f);
            Runtime3DFactory.Cylinder("MuzzleRing3D", root.transform, new Vector3(0f, 0.03f, -0.48f),
                0.24f * force, 0.045f, Color.Lerp(color, Color.white, 0.25f), 0.02f, 0.88f);

            root.AddComponent<TransientScale3D>().Initialize(0.13f, 1.10f, 0.08f, Random.Range(-45f, 45f), true);
        }

        private static void SpawnImpact(Vector3 position, Color color, bool explosive, bool ricochet)
        {
            float scale = explosive ? 1.40f : ricochet ? 0.72f : 0.82f;
            SpawnShockwave(position, color, explosive ? 0.42f : 0.18f, explosive ? 1.85f : 0.92f, explosive ? 0.34f : 0.20f);

            if (TryReserveFx())
            {
                var flash = new GameObject(explosive ? "ExplosionCore3D" : "ImpactFlash3D");
                flash.transform.position = new Vector3(position.x, position.y, 0f);
                flash.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
                Runtime3DFactory.Cylinder("ImpactCore3D", flash.transform, new Vector3(0f, 0f, -0.53f),
                    0.34f * scale, 0.10f * scale, Color.Lerp(color, Color.white, explosive ? 0.60f : 0.36f), 0.02f, 0.86f);
                Runtime3DFactory.Box("ImpactCrossA3D", flash.transform, new Vector3(0f, 0f, -0.56f),
                    new Vector3(0.10f * scale, 0.62f * scale, 0.07f), color, 0.02f, 0.72f);
                Runtime3DFactory.Box("ImpactCrossB3D", flash.transform, new Vector3(0f, 0f, -0.55f),
                    new Vector3(0.62f * scale, 0.10f * scale, 0.07f), color, 0.02f, 0.72f);
                flash.AddComponent<TransientScale3D>().Initialize(explosive ? 0.30f : 0.18f, 0.55f, explosive ? 1.55f : 1.12f, 100f, true);
            }

            int fragments = explosive ? 10 : ricochet ? 6 : 4;
            SpawnFragments(position, color, fragments, explosive ? 3.7f : ricochet ? 3.0f : 2.1f, explosive ? 0.70f : 0.42f);
        }

        private static void SpawnShockwave(Vector3 position, Color color, float startScale, float endScale, float life)
        {
            if (!TryReserveFx()) return;
            var root = new GameObject("Shockwave3D");
            root.transform.position = new Vector3(position.x, position.y, 0f);
            Runtime3DFactory.Cylinder("ShockRing3D", root.transform, new Vector3(0f, 0f, -0.48f),
                0.82f, 0.035f, Color.Lerp(color, Color.white, 0.18f), 0.01f, 0.82f);
            root.AddComponent<TransientScale3D>().Initialize(life, Mathf.Max(0.05f, startScale), Mathf.Max(startScale + 0.02f, endScale), 24f, true);
        }

        private static void SpawnDestructionBurst(Vector3 position, Color color, float scale, int fragments)
        {
            SpawnShockwave(position, color, 0.50f * scale, 2.10f * scale, 0.48f);
            SpawnFragments(position, color, fragments, 4.2f * scale, 0.95f);

            if (!TryReserveFx()) return;
            var root = new GameObject("VehicleDestruction3D");
            root.transform.position = new Vector3(position.x, position.y, 0f);
            Runtime3DFactory.Cylinder("FireballCore3D", root.transform, new Vector3(0f, 0f, -0.62f),
                0.58f * scale, 0.20f * scale, Color.Lerp(color, Color.white, 0.46f), 0.02f, 0.84f);
            Runtime3DFactory.Cylinder("FireballOuter3D", root.transform, new Vector3(0f, 0f, -0.50f),
                0.88f * scale, 0.10f * scale, Color.Lerp(color, new Color(0.95f, 0.12f, 0.02f), 0.45f), 0.01f, 0.55f);
            root.AddComponent<TransientScale3D>().Initialize(0.48f, 0.48f, 1.75f, 70f, true);
        }

        private static void SpawnFragments(Vector3 position, Color color, int count, float energy, float lifetime)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryReserveFx()) return;
                var fragment = new GameObject("CombatFragment3D");
                fragment.transform.position = new Vector3(position.x, position.y, -0.44f);
                fragment.transform.rotation = Random.rotation;
                Runtime3DFactory.Box("FragmentMesh3D", fragment.transform, Vector3.zero,
                    new Vector3(Random.Range(0.035f, 0.10f), Random.Range(0.10f, 0.26f), Random.Range(0.025f, 0.07f)),
                    Color.Lerp(color, new Color(0.28f, 0.25f, 0.22f), Random.Range(0.10f, 0.48f)), 0.56f, 0.30f);

                Vector2 planar = Random.insideUnitCircle.normalized * Random.Range(energy * 0.38f, energy);
                Vector3 velocity = new Vector3(planar.x, planar.y, -Random.Range(0.50f, 1.85f));
                fragment.AddComponent<Ballistic3DFragment>().Initialize(velocity, Random.Range(-520f, 520f), lifetime * Random.Range(0.70f, 1.18f), true);
            }
        }

        private static void SpawnEagleCollapse(Vector3 position)
        {
            Color gold = new Color(0.94f, 0.68f, 0.12f);
            Color core = new Color(0.75f, 0.08f, 0.025f);
            SpawnShockwave(position, new Color(1f, 0.10f, 0.025f), 0.55f, 3.4f, 0.72f);
            SpawnShockwave(position, gold, 0.30f, 2.45f, 0.50f);
            SpawnFragments(position, gold, 14, 5.2f, 1.25f);
            SpawnFragments(position, core, 10, 4.4f, 1.00f);

            var wreck = new GameObject("ORZELEK_3D_WRECK");
            wreck.transform.position = new Vector3(position.x, position.y, 0f);
            wreck.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));
            Color charred = new Color(0.085f, 0.075f, 0.070f);
            Runtime3DFactory.Box("BrokenPlinth3D", wreck.transform, new Vector3(0f, -0.03f, -0.12f),
                new Vector3(1.18f, 0.82f, 0.38f), charred, 0.35f, 0.16f);
            GameObject brokenCore = Runtime3DFactory.Box("BrokenCore3D", wreck.transform, new Vector3(0.12f, 0.06f, -0.40f),
                new Vector3(0.42f, 0.40f, 0.22f), new Color(0.30f, 0.055f, 0.035f), 0.32f, 0.20f);
            brokenCore.transform.localRotation = Quaternion.Euler(8f, -12f, 17f);
            GameObject wingL = Runtime3DFactory.Box("FallenWingL3D", wreck.transform, new Vector3(-0.48f, -0.18f, -0.36f),
                new Vector3(0.52f, 0.10f, 0.065f), Color.Lerp(gold, Color.black, 0.45f), 0.52f, 0.30f);
            wingL.transform.localRotation = Quaternion.Euler(0f, 0f, -48f);
            GameObject wingR = Runtime3DFactory.Box("FallenWingR3D", wreck.transform, new Vector3(0.52f, -0.22f, -0.34f),
                new Vector3(0.50f, 0.10f, 0.065f), Color.Lerp(gold, Color.black, 0.52f), 0.52f, 0.30f);
            wingR.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);
            GameObject ember = Runtime3DFactory.Cylinder("EagleEmber3D", wreck.transform, new Vector3(0.08f, 0.04f, -0.57f),
                0.22f, 0.06f, new Color(1f, 0.08f, 0.02f), 0.02f, 0.82f);
            wreck.AddComponent<EagleWreck3D>().Initialize(ember.transform);
        }

        internal static bool TryReserveFx()
        {
            if (_activeTransientFx >= MaxTransientFx) return false;
            _activeTransientFx++;
            return true;
        }

        internal static void ReleaseFx()
        {
            _activeTransientFx = Mathf.Max(0, _activeTransientFx - 1);
        }
    }

    public sealed class ProjectileTrail3D : MonoBehaviour
    {
        private AmmoType _ammo;
        private Color _color;
        private float _nextTrail;
        private bool _ready;

        public void Initialize(AmmoType ammo, Color color)
        {
            _ammo = ammo;
            _color = color;
            _ready = true;
            _nextTrail = Time.time;
        }

        private void Update()
        {
            if (!_ready || Time.time < _nextTrail) return;
            float interval = _ammo == AmmoType.Plasma ? 0.026f : _ammo == AmmoType.EMP ? 0.040f : _ammo == AmmoType.Basic ? 0.075f : 0.050f;
            _nextTrail = Time.time + interval;
            if (!CombatFX3DDirector.TryReserveFx()) return;

            var shard = new GameObject("ProjectileTrail3D");
            shard.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            shard.transform.rotation = transform.rotation;
            float size = _ammo == AmmoType.Plasma ? 1.45f : _ammo == AmmoType.Explosive ? 1.15f : 1f;
            Runtime3DFactory.Box("TrailMesh3D", shard.transform, new Vector3(0f, -0.06f, -0.43f),
                new Vector3(0.045f * size, 0.18f * size, 0.035f), Color.Lerp(_color, Color.white, 0.16f), 0.02f, 0.78f);
            shard.AddComponent<TransientScale3D>().Initialize(_ammo == AmmoType.Plasma ? 0.20f : 0.14f, 1f, 0.15f, 0f, true);
        }
    }

    public sealed class TransientScale3D : MonoBehaviour
    {
        private float _born;
        private float _life;
        private float _startScale;
        private float _endScale;
        private float _spin;
        private bool _releaseBudget;
        private bool _released;

        public void Initialize(float life, float startScale, float endScale, float spinDegreesPerSecond, bool releaseBudget)
        {
            _born = Time.unscaledTime;
            _life = Mathf.Max(0.03f, life);
            _startScale = startScale;
            _endScale = endScale;
            _spin = spinDegreesPerSecond;
            _releaseBudget = releaseBudget;
            transform.localScale = Vector3.one * _startScale;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / _life);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, eased);
            if (Mathf.Abs(_spin) > 0.01f)
                transform.Rotate(0f, 0f, _spin * Time.unscaledDeltaTime, Space.Self);
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!_releaseBudget || _released) return;
            _released = true;
            CombatFX3DDirector.ReleaseFx();
        }
    }

    public sealed class Ballistic3DFragment : MonoBehaviour
    {
        private Vector3 _velocity;
        private float _spin;
        private float _born;
        private float _life;
        private Vector3 _initialScale;
        private bool _releaseBudget;
        private bool _released;

        public void Initialize(Vector3 velocity, float spinDegreesPerSecond, float life, bool releaseBudget)
        {
            _velocity = velocity;
            _spin = spinDegreesPerSecond;
            _born = Time.unscaledTime;
            _life = Mathf.Max(0.10f, life);
            _initialScale = transform.localScale;
            _releaseBudget = releaseBudget;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / _life);
            _velocity.z += 3.8f * dt;
            _velocity.x *= Mathf.Pow(0.94f, dt * 60f);
            _velocity.y *= Mathf.Pow(0.94f, dt * 60f);
            transform.position += _velocity * dt;
            transform.Rotate(_spin * 0.31f * dt, _spin * 0.57f * dt, _spin * dt, Space.Self);
            transform.localScale = _initialScale * Mathf.Lerp(1f, 0.18f, t);
            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!_releaseBudget || _released) return;
            _released = true;
            CombatFX3DDirector.ReleaseFx();
        }
    }

    public sealed class Wreck3DPresentation : MonoBehaviour
    {
        private Transform _ember;
        private Vector3 _emberBaseScale;
        private bool _ready;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;
            Runtime3DFactory.HideLegacySprites(transform);

            string n = gameObject.name;
            float scale = n.Contains("Boss") ? 1.65f : n.Contains("Heavy") || n.Contains("Siege") ? 1.24f : 0.94f;
            Color charred = new Color(0.075f, 0.070f, 0.064f);
            Color rust = new Color(0.24f, 0.095f, 0.040f);

            GameObject hull = Runtime3DFactory.Box("WreckHull3D", transform, new Vector3(0f, 0f, -0.22f),
                new Vector3(0.82f, 0.62f, 0.26f) * scale, charred, 0.34f, 0.16f);
            hull.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
            GameObject turret = Runtime3DFactory.Cylinder("WreckTurret3D", transform,
                new Vector3(Random.Range(-0.13f, 0.13f), Random.Range(-0.11f, 0.11f), -0.40f),
                0.42f * scale, 0.16f * scale, Color.Lerp(charred, rust, 0.22f), 0.42f, 0.18f);
            turret.transform.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-8f, 8f), Random.Range(-35f, 35f));
            GameObject barrel = Runtime3DFactory.Box("BrokenBarrel3D", turret.transform, new Vector3(0f, 0.32f, -0.02f),
                new Vector3(0.10f, 0.58f, 0.09f), rust, 0.55f, 0.18f);
            barrel.transform.localRotation = Quaternion.Euler(0f, Random.Range(-12f, 12f), Random.Range(-18f, 18f));
            Runtime3DFactory.Box("ScorchPlate3D", transform, new Vector3(0.15f, -0.08f, -0.38f),
                new Vector3(0.34f, 0.20f, 0.04f) * scale, rust, 0.25f, 0.12f);
            GameObject ember = Runtime3DFactory.Cylinder("WreckEmber3D", transform, new Vector3(-0.10f, 0.04f, -0.48f),
                0.12f * scale, 0.04f, new Color(1f, 0.12f, 0.025f), 0.01f, 0.78f);
            _ember = ember.transform;
            _emberBaseScale = _ember.localScale;
        }

        private void LateUpdate()
        {
            if (_ember == null) return;
            float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 7.8f) * 0.22f;
            _ember.localScale = _emberBaseScale * pulse;
        }
    }

    public sealed class EagleWreck3D : MonoBehaviour
    {
        private Transform _ember;
        private Vector3 _baseScale;
        private float _born;
        private const float Lifetime = 26f;

        public void Initialize(Transform ember)
        {
            _ember = ember;
            if (_ember != null) _baseScale = _ember.localScale;
            _born = Time.unscaledTime;
        }

        private void Update()
        {
            if (_ember != null)
            {
                float pulse = 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9.5f));
                _ember.localScale = _baseScale * pulse;
                _ember.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 28f);
            }

            if (Time.unscaledTime - _born >= Lifetime)
                Destroy(gameObject);
        }
    }
}
