using UnityEngine;

namespace TankRevival
{
    public enum ImpactMaterialKind : byte
    {
        Unknown,
        Organic,
        Brick,
        Steel,
        Terrain
    }

    public enum DamageVisualState : byte
    {
        Healthy,
        Damaged,
        Critical,
        Burning
    }

    public struct CombatFeedbackBudget
    {
        public readonly int MaxActiveCues;
        public readonly int MaxParticlesPerCue;
        public readonly int RingSegments;
        public readonly float MinImpactInterval;
        public readonly float DamagePulseCadence;
        public readonly float AudioCooldown;

        public CombatFeedbackBudget(int maxActiveCues, int maxParticlesPerCue, int ringSegments,
            float minImpactInterval, float damagePulseCadence, float audioCooldown)
        {
            MaxActiveCues = maxActiveCues;
            MaxParticlesPerCue = maxParticlesPerCue;
            RingSegments = ringSegments;
            MinImpactInterval = minImpactInterval;
            DamagePulseCadence = damagePulseCadence;
            AudioCooldown = audioCooldown;
        }
    }

    public struct CombatImpactStyle
    {
        public readonly Color Primary;
        public readonly Color Secondary;
        public readonly float Radius;
        public readonly float Lifetime;
        public readonly float ParticleSize;
        public readonly int Particles;
        public readonly int Priority;
        public readonly SoundCue AudioCue;
        public readonly bool HasAudio;

        public CombatImpactStyle(Color primary, Color secondary, float radius, float lifetime,
            float particleSize, int particles, int priority, SoundCue audioCue, bool hasAudio)
        {
            Primary = primary;
            Secondary = secondary;
            Radius = radius;
            Lifetime = lifetime;
            ParticleSize = particleSize;
            Particles = particles;
            Priority = priority;
            AudioCue = audioCue;
            HasAudio = hasAudio;
        }
    }

    /// <summary>
    /// v12.7 presentation-only combat language. Projectile and Health remain authoritative; this layer
    /// consumes their read-only events/state and reuses a fixed pool for shockwaves, sparks, debris,
    /// smoke and fire pulses. No impact path allocates a new GameObject and late-wave pressure only
    /// reduces presentation density, never gameplay work.
    /// </summary>
    [DefaultExecutionOrder(770)]
    public sealed class CinematicCombatFeedbackDirector : MonoBehaviour
    {
        public const int PoolCapacity = 16;
        public const int MaxParticlesPerCue = 20;
        public const int MaxRingSegments = 22;
        public const int MinRingSegments = 10;
        public const float DamagedThreshold = 0.66f;
        public const float CriticalThreshold = 0.40f;
        public const float BurningThreshold = 0.20f;

        private sealed class CueSlot
        {
            public GameObject Root;
            public LineRenderer Ring;
            public ParticleSystem Particles;
            public float StartedAt;
            public float Lifetime;
            public float StartRadius;
            public float EndRadius;
            public int Priority;
            public bool Active;
            public Color Primary;
            public Color Secondary;
        }

        private static CinematicCombatFeedbackDirector _instance;
        private readonly CueSlot[] _pool = new CueSlot[PoolCapacity];
        private readonly Vector3[] _ringPoints = new Vector3[MaxRingSegments + 1];
        private Material _ringMaterial;
        private Material _particleMaterial;
        private TankGame _game;
        private CombatFeedbackBudget _budget;
        private float _nextImpactAt;
        private float _nextAudioAt;
        private int _lastAudioPriority;
        private float _lastAudioAt;
        private float _recentFirePressure;

        public static CinematicCombatFeedbackDirector Instance => _instance;
        public CombatFeedbackBudget CurrentBudget => _budget;
        public int ActiveCueCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i] != null && _pool[i].Active) count++;
                return count;
            }
        }

        public static bool ConfigurationValid =>
            PoolCapacity == 16 && MaxParticlesPerCue == 20 && MaxRingSegments <= 24 && MinRingSegments >= 8 &&
            BurningThreshold < CriticalThreshold && CriticalThreshold < DamagedThreshold && DamagedThreshold < 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<CinematicCombatFeedbackDirector>() != null) return;
            GameObject go = new GameObject("CinematicCombatFeedbackDirector_v12_7");
            DontDestroyOnLoad(go);
            go.AddComponent<CinematicCombatFeedbackDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreatePool();
            _budget = ComputeBudget(1, 0f, 0);
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D += OnShotSpawned;
            Projectile.ImpactMaterial3D += OnImpact;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShotSpawned;
            Projectile.ImpactMaterial3D -= OnImpact;
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null) Destroy(_ringMaterial);
            if (_particleMaterial != null) Destroy(_particleMaterial);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            int round = _game != null ? Mathf.Clamp(_game.CurrentRound, 1, 100) : 1;
            float density = _game != null ? Mathf.Clamp01(_game.LateBattleDensity01) : 0f;
            int pressure = Mathf.Clamp(Mathf.CeilToInt(_recentFirePressure), 0, 12);
            _budget = ComputeBudget(round, density, pressure);
            _recentFirePressure = Mathf.MoveTowards(_recentFirePressure, 0f, Time.unscaledDeltaTime * 4.5f);
            UpdatePool();
        }

        private void OnShotSpawned(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            _recentFirePressure = Mathf.Min(12f, _recentFirePressure + (ammo == AmmoType.Explosive || ammo == AmmoType.Plasma ? 1.35f : 0.72f));
        }

        private void OnImpact(Projectile projectile, Vector3 position, Team team, AmmoType ammo,
            ImpactMaterialKind material, bool explosive, bool ricochet)
        {
            CombatImpactStyle style = ResolveImpactStyle(ammo, material, explosive, ricochet);
            bool highPriority = style.Priority >= 5;
            if (Time.unscaledTime >= _nextImpactAt || highPriority)
            {
                if (TryAcquireCue(style.Priority, out CueSlot slot))
                {
                    ActivateImpactCue(slot, position, style);
                    _nextImpactAt = Time.unscaledTime + _budget.MinImpactInterval;
                }
            }
            TryPlayImpactAudio(style, ricochet, explosive);
        }

        public static bool RequestDamagePulse(Vector3 position, float healthRatio)
        {
            if (_instance == null) return false;
            DamageVisualState state = ResolveDamageState(healthRatio);
            if (state == DamageVisualState.Healthy) return false;
            return _instance.EmitDamagePulse(position, state);
        }

        public static float DamagePulseCadence(float healthRatio, float pressure01)
        {
            DamageVisualState state = ResolveDamageState(healthRatio);
            float baseCadence = state == DamageVisualState.Burning ? 0.22f : state == DamageVisualState.Critical ? 0.31f : 0.48f;
            return baseCadence * Mathf.Lerp(1f, 1.85f, Mathf.Clamp01(pressure01));
        }

        private bool EmitDamagePulse(Vector3 position, DamageVisualState state)
        {
            int priority = state == DamageVisualState.Burning ? 5 : state == DamageVisualState.Critical ? 4 : 2;
            if (!TryAcquireCue(priority, out CueSlot slot)) return false;

            Color primary = state == DamageVisualState.Burning
                ? new Color(1f, 0.22f, 0.035f, 0.94f)
                : state == DamageVisualState.Critical
                    ? new Color(0.95f, 0.43f, 0.08f, 0.82f)
                    : new Color(0.28f, 0.31f, 0.34f, 0.66f);
            Color secondary = state == DamageVisualState.Burning
                ? new Color(0.16f, 0.12f, 0.10f, 0.72f)
                : new Color(0.10f, 0.11f, 0.12f, 0.62f);

            CombatImpactStyle style = new CombatImpactStyle(primary, secondary,
                state == DamageVisualState.Burning ? 0.62f : 0.45f,
                state == DamageVisualState.Burning ? 0.72f : 0.58f,
                state == DamageVisualState.Burning ? 0.12f : 0.16f,
                state == DamageVisualState.Burning ? 12 : state == DamageVisualState.Critical ? 9 : 6,
                priority, SoundCue.ImpactSoft, false);
            ActivateDamageCue(slot, position, style, state);
            return true;
        }

        public static DamageVisualState ResolveDamageState(float healthRatio)
        {
            float ratio = Mathf.Clamp01(healthRatio);
            if (ratio <= BurningThreshold) return DamageVisualState.Burning;
            if (ratio <= CriticalThreshold) return DamageVisualState.Critical;
            if (ratio <= DamagedThreshold) return DamageVisualState.Damaged;
            return DamageVisualState.Healthy;
        }

        public static CombatFeedbackBudget ComputeBudget(int round, float density01, int recentFirePressure)
        {
            int r = Mathf.Clamp(round, 1, 100);
            float density = Mathf.Clamp01(density01);
            int pressure = Mathf.Clamp(recentFirePressure, 0, 12);
            int tier = 0;
            if (r >= 55 || density >= 0.45f || pressure >= 5) tier = 1;
            if (r >= 82 || density >= 0.76f || pressure >= 9) tier = 2;

            if (tier == 0) return new CombatFeedbackBudget(14, 18, 22, 0.030f, 0.30f, 0.075f);
            if (tier == 1) return new CombatFeedbackBudget(10, 12, 16, 0.055f, 0.42f, 0.115f);
            return new CombatFeedbackBudget(7, 7, 10, 0.090f, 0.58f, 0.170f);
        }

        public static CombatImpactStyle ResolveImpactStyle(AmmoType ammo, ImpactMaterialKind material, bool explosive = false, bool ricochet = false)
        {
            Color primary;
            Color secondary;
            int materialWeight;
            SoundCue audio;

            switch (material)
            {
                case ImpactMaterialKind.Organic:
                    primary = new Color(1.00f, 0.34f, 0.12f, 0.96f);
                    secondary = new Color(0.42f, 0.08f, 0.025f, 0.76f);
                    materialWeight = 1;
                    audio = SoundCue.ImpactSoft;
                    break;
                case ImpactMaterialKind.Brick:
                    primary = new Color(0.94f, 0.42f, 0.12f, 0.96f);
                    secondary = new Color(0.32f, 0.075f, 0.026f, 0.82f);
                    materialWeight = 2;
                    audio = SoundCue.ImpactHard;
                    break;
                case ImpactMaterialKind.Steel:
                    primary = new Color(0.82f, 0.91f, 1.00f, 0.98f);
                    secondary = new Color(0.22f, 0.32f, 0.44f, 0.88f);
                    materialWeight = 3;
                    audio = ricochet ? SoundCue.Ricochet : SoundCue.ImpactHard;
                    break;
                case ImpactMaterialKind.Terrain:
                    primary = new Color(0.78f, 0.64f, 0.40f, 0.88f);
                    secondary = new Color(0.22f, 0.18f, 0.13f, 0.70f);
                    materialWeight = 1;
                    audio = SoundCue.ImpactSoft;
                    break;
                default:
                    primary = Color.white;
                    secondary = new Color(0.42f, 0.46f, 0.52f, 0.72f);
                    materialWeight = 1;
                    audio = SoundCue.ImpactSoft;
                    break;
            }

            int ammoWeight = 0;
            float radius = 0.42f;
            float lifetime = 0.34f;
            float particleSize = 0.085f;
            int particles = 5;

            switch (ammo)
            {
                case AmmoType.ArmorPiercing:
                    ammoWeight = 2; radius = 0.58f; lifetime = 0.42f; particleSize = 0.075f; particles = 10; break;
                case AmmoType.Explosive:
                    ammoWeight = 4; radius = 1.02f; lifetime = 0.62f; particleSize = 0.15f; particles = 18; audio = SoundCue.ExplosionSmall; break;
                case AmmoType.Plasma:
                    ammoWeight = 4; radius = 0.88f; lifetime = 0.52f; particleSize = 0.12f; particles = 15;
                    primary = Color.Lerp(primary, new Color(0.20f, 0.86f, 1f, 1f), 0.68f); audio = SoundCue.Plasma; break;
                case AmmoType.EMP:
                    ammoWeight = 3; radius = 0.80f; lifetime = 0.50f; particleSize = 0.10f; particles = 12;
                    primary = new Color(0.40f, 0.68f, 1f, 0.98f); secondary = new Color(0.18f, 0.26f, 0.94f, 0.74f); audio = SoundCue.Emp; break;
                case AmmoType.Incendiary:
                    ammoWeight = 2; radius = 0.62f; lifetime = 0.50f; particleSize = 0.11f; particles = 10;
                    primary = new Color(1f, 0.26f, 0.035f, 0.98f); secondary = new Color(0.56f, 0.06f, 0.015f, 0.82f); break;
                case AmmoType.Twin:
                    ammoWeight = 1; radius = 0.46f; lifetime = 0.32f; particleSize = 0.075f; particles = 6; break;
            }

            if (ricochet) { radius *= 0.78f; particles = Mathf.Max(5, particles - 2); ammoWeight = Mathf.Max(ammoWeight, 2); }
            if (explosive) ammoWeight = Mathf.Max(ammoWeight, 4);
            int priority = Mathf.Clamp(materialWeight + ammoWeight, 1, 7);
            return new CombatImpactStyle(primary, secondary, radius, lifetime, particleSize, particles, priority, audio, true);
        }

        private void CreatePool()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _ringMaterial = shader != null ? new Material(shader) : null;
            _particleMaterial = shader != null ? new Material(shader) : null;

            for (int i = 0; i < PoolCapacity; i++)
            {
                GameObject root = new GameObject("CombatCue_" + i);
                root.transform.SetParent(transform, false);
                root.SetActive(false);

                LineRenderer ring = root.AddComponent<LineRenderer>();
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = MinRingSegments;
                ring.widthMultiplier = 0.045f;
                ring.numCapVertices = 1;
                if (_ringMaterial != null) ring.sharedMaterial = _ringMaterial;
                ring.sortingOrder = 78;

                ParticleSystem ps = root.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = MaxParticlesPerCue;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;
                ParticleSystem.ShapeModule shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.08f;
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (_particleMaterial != null) renderer.sharedMaterial = _particleMaterial;
                renderer.sortingOrder = 79;

                _pool[i] = new CueSlot { Root = root, Ring = ring, Particles = ps };
            }
        }

        private bool TryAcquireCue(int priority, out CueSlot slot)
        {
            slot = null;
            int active = 0;
            CueSlot replacement = null;
            for (int i = 0; i < _pool.Length; i++)
            {
                CueSlot candidate = _pool[i];
                if (!candidate.Active)
                {
                    if (slot == null) slot = candidate;
                    continue;
                }
                active++;
                if (replacement == null || candidate.Priority < replacement.Priority ||
                    (candidate.Priority == replacement.Priority && candidate.StartedAt < replacement.StartedAt))
                    replacement = candidate;
            }

            if (active < _budget.MaxActiveCues && slot != null) return true;
            if (replacement != null && priority > replacement.Priority)
            {
                Deactivate(replacement);
                slot = replacement;
                return true;
            }
            slot = null;
            return false;
        }

        private void ActivateImpactCue(CueSlot slot, Vector3 position, CombatImpactStyle style)
        {
            ConfigureCue(slot, position, style, false);
        }

        private void ActivateDamageCue(CueSlot slot, Vector3 position, CombatImpactStyle style, DamageVisualState state)
        {
            ConfigureCue(slot, position + Vector3.up * (state == DamageVisualState.Burning ? 0.16f : 0.10f), style, true);
        }

        private void ConfigureCue(CueSlot slot, Vector3 position, CombatImpactStyle style, bool damagePulse)
        {
            slot.Root.transform.position = position;
            slot.Root.SetActive(true);
            slot.Active = true;
            slot.StartedAt = Time.unscaledTime;
            slot.Lifetime = Mathf.Max(0.15f, style.Lifetime);
            slot.StartRadius = damagePulse ? style.Radius * 0.35f : style.Radius * 0.18f;
            slot.EndRadius = style.Radius;
            slot.Priority = style.Priority;
            slot.Primary = style.Primary;
            slot.Secondary = style.Secondary;

            slot.Ring.enabled = !damagePulse || style.Priority >= 4;
            slot.Ring.startColor = style.Primary;
            slot.Ring.endColor = style.Secondary;
            slot.Ring.widthMultiplier = damagePulse ? 0.025f : 0.045f + style.Priority * 0.004f;

            ParticleSystem.MainModule main = slot.Particles.main;
            main.startLifetime = damagePulse ? Mathf.Min(0.72f, style.Lifetime) : Mathf.Min(0.52f, style.Lifetime);
            main.startSpeed = damagePulse ? new ParticleSystem.MinMaxCurve(0.18f, 0.55f) : new ParticleSystem.MinMaxCurve(0.8f, 2.1f + style.Priority * 0.11f);
            main.startSize = new ParticleSystem.MinMaxCurve(style.ParticleSize * 0.65f, style.ParticleSize * 1.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(style.Primary, style.Secondary);
            main.gravityModifier = damagePulse ? -0.055f : 0.10f;

            ParticleSystem.ShapeModule shape = slot.Particles.shape;
            shape.radius = damagePulse ? 0.15f : 0.07f;
            int count = Mathf.Clamp(style.Particles, 1, Mathf.Min(MaxParticlesPerCue, _budget.MaxParticlesPerCue));
            slot.Particles.Clear(true);
            slot.Particles.Emit(count);
            UpdateRing(slot, 0f);
        }

        private void UpdatePool()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pool.Length; i++)
            {
                CueSlot slot = _pool[i];
                if (!slot.Active) continue;
                float t = Mathf.Clamp01((now - slot.StartedAt) / slot.Lifetime);
                UpdateRing(slot, t);
                if (t >= 1f && !slot.Particles.IsAlive(true)) Deactivate(slot);
            }
        }

        private void UpdateRing(CueSlot slot, float t)
        {
            if (!slot.Ring.enabled) return;
            int segments = Mathf.Clamp(_budget.RingSegments, MinRingSegments, MaxRingSegments);
            slot.Ring.positionCount = segments;
            float radius = Mathf.Lerp(slot.StartRadius, slot.EndRadius, 1f - (1f - t) * (1f - t));
            float alpha = 1f - t;
            Color start = slot.Primary; start.a *= alpha;
            Color end = slot.Secondary; end.a *= alpha;
            slot.Ring.startColor = start;
            slot.Ring.endColor = end;
            Vector3 center = slot.Root.transform.position;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                _ringPoints[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.62f, 0f);
                slot.Ring.SetPosition(i, _ringPoints[i]);
            }
        }

        private static void Deactivate(CueSlot slot)
        {
            slot.Active = false;
            slot.Priority = 0;
            slot.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Root.SetActive(false);
        }

        private void TryPlayImpactAudio(CombatImpactStyle style, bool ricochet, bool explosive)
        {
            if (!style.HasAudio) return;
            float now = Time.unscaledTime;
            bool canPreempt = style.Priority >= 6 && (now - _lastAudioAt) >= 0.045f && style.Priority > _lastAudioPriority;
            if (now < _nextAudioAt && !canPreempt) return;

            // Projectile already owns the core HE detonation and explicit steel ricochet cues. The v12.7
            // hierarchy supplements material impacts without double-firing those authoritative sounds.
            if (explosive || ricochet) return;

            float volume = Mathf.Lerp(0.18f, 0.55f, style.Priority / 7f);
            BattleAudio.PlayGlobal(style.AudioCue, volume, 0.025f);
            _lastAudioPriority = style.Priority;
            _lastAudioAt = now;
            _nextAudioAt = now + _budget.AudioCooldown;
        }
    }
}
