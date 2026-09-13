using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class CombatVfxReforgeDirector : MonoBehaviour
    {
        public const int AmmoSignatureCount = 7;
        public const int MaxTrackedTrails = 96;
        public const int MaxLayerBurstsPerSecond = 28;
        public const float MinTrailInterval = 0.028f;
        public const float MaxShockwaveScale = 1.85f;
        public const float MaxImpactScale = 1.35f;
        public static bool UsesProjectileEventAuthority => true;
        public static bool ConfigurationValid => AmmoSignatureCount == AmmoDatabase.AmmoTypeCount && MaxTrackedTrails <= 96 && MaxLayerBurstsPerSecond <= 32 && MinTrailInterval >= 0.025f && MaxShockwaveScale <= 2f;

        private sealed class TrailState
        {
            public Projectile Projectile;
            public AmmoType Ammo;
            public float NextEmit;
            public int Id;
        }

        private readonly List<TrailState> _trails = new List<TrailState>(MaxTrackedTrails);
        private readonly HashSet<int> _trailIds = new HashSet<int>();
        private float _burstWindowStart;
        private int _burstsThisWindow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CombatVfxReforgeDirector>() != null) return;
            var go = new GameObject("CombatVfxReforgeDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatVfxReforgeDirector>();
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D += OnShotSpawned;
            Projectile.Impact3D += OnImpact;
            Projectile.DamageResolved += OnDamageResolved;
            _burstWindowStart = Time.unscaledTime;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
            Projectile.Impact3D -= OnImpact;
            Projectile.DamageResolved -= OnDamageResolved;
            _trails.Clear();
            _trailIds.Clear();
        }

        private void Update()
        {
            ResetBurstBudgetIfNeeded();
            for (int i = _trails.Count - 1; i >= 0; i--)
            {
                TrailState state = _trails[i];
                Projectile projectile = state.Projectile;
                if (projectile == null || !projectile.gameObject.activeInHierarchy)
                {
                    _trailIds.Remove(state.Id);
                    _trails.RemoveAt(i);
                    continue;
                }
                if (Time.time < state.NextEmit) continue;
                float interval = TrailInterval(state.Ammo);
                state.NextEmit = Time.time + interval;
                if (!IsHighValueTrail(state.Ammo) || !TryConsumeBurst(false)) continue;
                Color color = AmmoDatabase.Color(state.Ammo);
                float size = state.Ammo == AmmoType.Plasma ? 0.46f : state.Ammo == AmmoType.EMP ? 0.38f : state.Ammo == AmmoType.Incendiary ? 0.32f : 0.27f;
                VisualFactory.ProjectileAfterglow(projectile.transform.position, color, size);
            }
        }

        private void OnShotSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team owner, AmmoType ammo)
        {
            if (projectile == null) return;
            int id = projectile.GetInstanceID();
            if (_trailIds.Add(id))
            {
                if (_trails.Count >= MaxTrackedTrails)
                {
                    _trailIds.Remove(_trails[0].Id);
                    _trails.RemoveAt(0);
                }
                _trails.Add(new TrailState { Projectile = projectile, Ammo = ammo, NextEmit = Time.time, Id = id });
            }

            if (!TryConsumeBurst(owner == Team.Player)) return;
            Color color = AmmoDatabase.Color(ammo);
            float scale = SpawnSignatureScale(ammo);
            VisualFactory.RingPulse(position, new Color(color.r, color.g, color.b, 0.72f), 0.28f + scale * 0.20f);
            if (ammo == AmmoType.Plasma || ammo == AmmoType.Explosive)
                VisualFactory.MicroBurst(position, Color.Lerp(color, Color.white, 0.42f), 0.30f + scale * 0.22f);
        }

        private void OnImpact(Projectile projectile, Vector3 position, Team owner, AmmoType ammo, bool explosive, bool ricochet)
        {
            bool priority = owner == Team.Player || explosive || ammo == AmmoType.Plasma;
            if (!TryConsumeBurst(priority)) return;
            Color color = AmmoDatabase.Color(ammo);
            float scale = Mathf.Min(MaxImpactScale, ImpactScale(ammo));

            if (ricochet)
            {
                VisualFactory.MicroBurst(position, new Color(0.92f, 0.95f, 1f), 0.34f);
                VisualFactory.RingPulse(position, new Color(0.72f, 0.84f, 1f, 0.72f), 0.38f);
                return;
            }

            switch (ammo)
            {
                case AmmoType.ArmorPiercing:
                    VisualFactory.MicroBurst(position, new Color(0.90f, 0.96f, 1f), 0.62f * scale);
                    VisualFactory.RectRotated("APImpactSlash", null, new Vector2(0.07f, 0.72f), new Color(0.90f, 0.96f, 1f, 0.75f), position, 48f, 41).AddComponent<CombatVfxTransient>().Initialize(0.16f, 1.45f);
                    break;
                case AmmoType.Explosive:
                    LayeredExplosion(position, color, 1.00f, true);
                    break;
                case AmmoType.Incendiary:
                    VisualFactory.MicroBurst(position, color, 0.78f * scale);
                    SpawnDiscPulse("IncendiaryBloom", position, color, 0.30f, 0.36f, 1.9f);
                    break;
                case AmmoType.EMP:
                    SpawnRing("EMPShockRing", position, color, 0.34f, 0.42f, 2.65f);
                    SpawnRing("EMPShockRing2", position, Color.Lerp(color, Color.white, 0.38f), 0.22f, 0.34f, 2.15f);
                    break;
                case AmmoType.Twin:
                    VisualFactory.MicroBurst(position, color, 0.48f * scale);
                    SpawnRing("TwinImpact", position, color, 0.20f, 0.24f, 1.55f);
                    break;
                case AmmoType.Plasma:
                    SpawnDiscPulse("PlasmaCore", position, Color.Lerp(color, Color.white, 0.55f), 0.36f, 0.30f, 2.45f);
                    SpawnRing("PlasmaWave", position, color, 0.40f, 0.42f, 2.85f);
                    VisualFactory.MicroBurst(position, color, 0.84f * scale);
                    break;
                default:
                    VisualFactory.MicroBurst(position, color, 0.42f * scale);
                    break;
            }
        }

        private void OnDamageResolved(Projectile projectile, Health target, int damage, bool killed)
        {
            if (!killed || target == null || projectile == null) return;
            bool major = target.Maximum >= 18 || projectile.Ammo == AmmoType.Explosive || projectile.Ammo == AmmoType.Plasma;
            if (!TryConsumeBurst(true)) return;
            Color color = AmmoDatabase.Color(projectile.Ammo);
            LayeredExplosion(target.transform.position, color, major ? 1.28f : 0.88f, major);
        }

        private void LayeredExplosion(Vector3 position, Color color, float scale, bool heavy)
        {
            scale = Mathf.Min(MaxImpactScale, scale);
            VisualFactory.Explosion(position, color, scale);
            SpawnDiscPulse("ExplosionFlash", position, Color.Lerp(color, Color.white, 0.72f), 0.32f * scale, 0.20f, 2.2f);
            SpawnRing("ExplosionShockwave", position, Color.Lerp(color, Color.white, 0.26f), 0.42f * scale, 0.42f, heavy ? MaxShockwaveScale : 1.45f);
            if (heavy && TryConsumeBurst(true))
                VisualFactory.MicroBurst(position, new Color(1f, 0.72f, 0.28f), 1.0f * scale);
        }

        private static void SpawnRing(string name, Vector3 position, Color color, float startScale, float life, float growth)
        {
            GameObject go = VisualFactory.RingObject(name, null, Vector2.one * startScale, new Color(color.r, color.g, color.b, 0.76f), position, 40);
            go.AddComponent<CombatVfxTransient>().Initialize(life, growth);
        }

        private static void SpawnDiscPulse(string name, Vector3 position, Color color, float startScale, float life, float growth)
        {
            GameObject go = VisualFactory.Disc(name, null, Vector2.one * startScale, new Color(color.r, color.g, color.b, 0.66f), position, 42);
            go.AddComponent<CombatVfxTransient>().Initialize(life, growth);
        }

        private bool TryConsumeBurst(bool priority)
        {
            ResetBurstBudgetIfNeeded();
            int tierLimit = WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival ? 10 : WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced ? 18 : MaxLayerBurstsPerSecond;
            if (_burstsThisWindow >= tierLimit && !priority) return false;
            if (_burstsThisWindow >= tierLimit + 5) return false;
            _burstsThisWindow++;
            return true;
        }

        private void ResetBurstBudgetIfNeeded()
        {
            if (Time.unscaledTime - _burstWindowStart < 1f) return;
            _burstWindowStart = Time.unscaledTime;
            _burstsThisWindow = 0;
        }

        private static bool IsHighValueTrail(AmmoType ammo) => ammo == AmmoType.Incendiary || ammo == AmmoType.EMP || ammo == AmmoType.Plasma || ammo == AmmoType.Explosive;

        private static float TrailInterval(AmmoType ammo)
        {
            float interval = ammo == AmmoType.Plasma ? MinTrailInterval : ammo == AmmoType.EMP ? 0.040f : 0.052f;
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced) interval *= 1.45f;
            else if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival) interval *= 2.15f;
            return interval;
        }

        private static float SpawnSignatureScale(AmmoType ammo) => ammo == AmmoType.Plasma ? 1.25f : ammo == AmmoType.Explosive ? 1.12f : ammo == AmmoType.EMP ? 1.04f : 0.78f;
        private static float ImpactScale(AmmoType ammo) => ammo == AmmoType.Plasma ? 1.32f : ammo == AmmoType.Explosive ? 1.22f : ammo == AmmoType.EMP ? 1.10f : ammo == AmmoType.ArmorPiercing ? 0.92f : 0.82f;
    }

    public sealed class CombatVfxTransient : MonoBehaviour
    {
        private float _life;
        private float _age;
        private float _growth;
        private SpriteRenderer _renderer;
        private Vector3 _baseScale;

        public void Initialize(float life, float growth)
        {
            _life = Mathf.Max(0.05f, life);
            _growth = Mathf.Max(1f, growth);
            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _life);
            transform.localScale = _baseScale * Mathf.Lerp(1f, _growth, t);
            if (_renderer != null)
            {
                Color c = _renderer.color;
                c.a *= 1f - t;
                _renderer.color = c;
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
