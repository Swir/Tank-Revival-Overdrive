using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v4.4 procedural battle mix. Layers caliber/body impacts, metallic stress, kill confirmation,
    /// distant battlefield rumble and critical-state pulse over the existing BattleAudio system.
    /// All sounds are generated at runtime, so the project remains asset-light and deterministic.
    /// </summary>
    [DefaultExecutionOrder(8800)]
    public sealed class EnhancedBattleAudioDirector : MonoBehaviour
    {
        private const int SampleRate = 22050;
        public static EnhancedBattleAudioDirector Instance { get; private set; }

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _impact;
        private AudioSource _confirm;
        private AudioSource _rumble;
        private AudioSource _danger;
        private float _dangerTarget;
        private float _battleTarget;
        private float _scanAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnhancedBattleAudioDirector>() != null) return;
            var go = new GameObject("EnhancedBattleAudioDirector_v4_4");
            DontDestroyOnLoad(go);
            go.AddComponent<EnhancedBattleAudioDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _impact = AddSource(0.82f, false);
            _confirm = AddSource(0.64f, false);
            _rumble = AddSource(0f, true);
            _danger = AddSource(0f, true);

            BuildClips();
            _rumble.clip = _clips["rumble"];
            _danger.clip = _clips["danger"];
            _rumble.Play();
            _danger.Play();
        }

        private void OnEnable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.ShotSpawned3D += OnShot;
            Projectile.Impact3D -= OnImpact;
            Projectile.Impact3D += OnImpact;
            Projectile.DamageResolved -= OnDamageResolved;
            Projectile.DamageResolved += OnDamageResolved;
        }

        private void OnDisable()
        {
            Projectile.ShotSpawned3D -= OnShot;
            Projectile.Impact3D -= OnImpact;
            Projectile.DamageResolved -= OnDamageResolved;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource AddSource(float volume, bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _rumble.volume = Mathf.MoveTowards(_rumble.volume, _battleTarget * 0.22f, dt * 0.16f);
            _danger.volume = Mathf.MoveTowards(_danger.volume, _dangerTarget * 0.23f, dt * 0.50f);
            _danger.pitch = 0.93f + _dangerTarget * 0.09f;
            _rumble.pitch = 0.92f + _battleTarget * 0.10f;

            if (Time.unscaledTime < _scanAt) return;
            _scanAt = Time.unscaledTime + 0.45f;

            int enemies = 0;
            int heavy = 0;
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                enemies++;
                if (enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege || enemy.Kind == EnemyKind.Boss) heavy++;
            }
            _battleTarget = Mathf.Clamp01(enemies / 12f + heavy * 0.055f);
        }

        public static void SetDangerPressure(float pressure)
        {
            if (Instance != null) Instance._dangerTarget = Mathf.Clamp01(pressure);
        }

        public static void PlayConfirmation(bool killed, float volume)
        {
            if (Instance == null || Instance._confirm == null) return;
            string key = killed ? "kill" : "hit";
            if (!Instance._clips.TryGetValue(key, out AudioClip clip) || clip == null) return;
            Instance._confirm.pitch = killed ? UnityEngine.Random.Range(0.97f, 1.035f) : UnityEngine.Random.Range(0.98f, 1.05f);
            Instance._confirm.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public static void PlayArmorStress(float volume)
        {
            if (Instance == null || Instance._impact == null) return;
            if (!Instance._clips.TryGetValue("armorStress", out AudioClip clip) || clip == null) return;
            Instance._impact.pitch = UnityEngine.Random.Range(0.90f, 1.06f);
            Instance._impact.panStereo = UnityEngine.Random.Range(-0.18f, 0.18f);
            Instance._impact.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void OnShot(Projectile projectile, Vector3 position, Vector2 direction, Team team, AmmoType ammo)
        {
            string key;
            float volume;
            switch (ammo)
            {
                case AmmoType.Explosive:
                    key = "cannonHeavy";
                    volume = 0.74f;
                    break;
                case AmmoType.ArmorPiercing:
                    key = "cannonHeavy";
                    volume = 0.63f;
                    break;
                case AmmoType.Plasma:
                    key = "energyHeavy";
                    volume = 0.61f;
                    break;
                case AmmoType.EMP:
                    key = "energyPulse";
                    volume = 0.52f;
                    break;
                default:
                    key = team == Team.Player ? "cannonLight" : "cannonEnemy";
                    volume = team == Team.Player ? 0.39f : 0.27f;
                    break;
            }
            PlayAt(key, position, volume, team == Team.Player ? 0.02f : 0.06f);
        }

        private void OnImpact(Projectile projectile, Vector3 position, Team team, AmmoType ammo, bool explosive, bool ricochet)
        {
            if (ricochet)
            {
                PlayAt("metalPing", position, 0.38f, 0.11f);
                return;
            }

            if (explosive)
            {
                PlayAt("impactHeavy", position, 0.78f, 0.05f);
                return;
            }

            string key = ammo == AmmoType.Plasma || ammo == AmmoType.EMP ? "energyImpact" : "impactBody";
            PlayAt(key, position, ammo == AmmoType.Plasma ? 0.48f : 0.33f, 0.07f);
        }

        private void OnDamageResolved(Projectile projectile, Health target, int amount, bool killed)
        {
            if (target == null || amount <= 0) return;
            if (killed)
            {
                EnemyTank enemy = target.GetComponent<EnemyTank>();
                if (enemy != null)
                {
                    float volume = enemy.Kind == EnemyKind.Boss ? 0.90f :
                                   enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Siege ? 0.72f : 0.52f;
                    PlayAt(enemy.Kind == EnemyKind.Boss ? "destructionBoss" : "destruction", target.transform.position, volume, 0.04f);
                }
            }
        }

        private void PlayAt(string key, Vector3 position, float volume, float jitter)
        {
            if (_impact == null || !_clips.TryGetValue(key, out AudioClip clip) || clip == null) return;

            PlayerTank player = CombatRoster.Player;
            float distanceAttenuation = 1f;
            float pan = 0f;
            if (player != null)
            {
                Vector3 delta = position - player.transform.position;
                distanceAttenuation = Mathf.Lerp(1f, 0.30f, Mathf.Clamp01(delta.magnitude / 15f));
                pan = Mathf.Clamp(delta.x / 8f, -0.72f, 0.72f);
            }

            _impact.pitch = 1f + UnityEngine.Random.Range(-jitter, jitter);
            _impact.panStereo = pan;
            _impact.PlayOneShot(clip, Mathf.Clamp01(volume * distanceAttenuation));
        }

        private void BuildClips()
        {
            _clips["cannonLight"] = BuildCannon("v44_CannonLight", 0.20f, 108f, 0.78f, 101);
            _clips["cannonEnemy"] = BuildCannon("v44_CannonEnemy", 0.18f, 82f, 0.66f, 103);
            _clips["cannonHeavy"] = BuildCannon("v44_CannonHeavy", 0.37f, 55f, 1.00f, 107);
            _clips["impactBody"] = BuildImpact("v44_ImpactBody", 0.22f, 0.66f, 109);
            _clips["impactHeavy"] = BuildImpact("v44_ImpactHeavy", 0.55f, 1.00f, 113);
            _clips["metalPing"] = BuildMetal("v44_MetalPing", 0.24f, 1510f, 0.52f);
            _clips["armorStress"] = BuildMetal("v44_ArmorStress", 0.46f, 210f, 0.68f);
            _clips["energyHeavy"] = BuildEnergy("v44_EnergyHeavy", 0.34f, 270f, 1050f, 0.65f);
            _clips["energyPulse"] = BuildEnergy("v44_EnergyPulse", 0.29f, 880f, 170f, 0.56f);
            _clips["energyImpact"] = BuildEnergy("v44_EnergyImpact", 0.24f, 1240f, 310f, 0.49f);
            _clips["hit"] = BuildConfirmation("v44_HitConfirm", false);
            _clips["kill"] = BuildConfirmation("v44_KillConfirm", true);
            _clips["destruction"] = BuildImpact("v44_Destruction", 0.72f, 0.88f, 127);
            _clips["destructionBoss"] = BuildImpact("v44_DestructionBoss", 1.10f, 1.00f, 131);
            _clips["rumble"] = BuildRumbleLoop();
            _clips["danger"] = BuildDangerLoop();
        }

        private static AudioClip BuildCannon(string name, float duration, float baseFrequency, float gain, int seed)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            var rng = new System.Random(seed);
            float low = 0f;
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / duration);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = low * 0.925f + noise * 0.075f;
                float freq = baseFrequency * Mathf.Lerp(1.25f, 0.62f, u);
                phase += Mathf.PI * 2f * freq / SampleRate;
                float body = Mathf.Sin(phase) * 0.58f + Mathf.Sin(phase * 0.49f) * 0.31f;
                float transient = t < 0.018f ? noise * (1f - t / 0.018f) : 0f;
                float env = Mathf.Exp(-t * (duration > 0.3f ? 7.0f : 12f));
                data[i] = Mathf.Clamp((body + low * 0.44f + transient * 0.60f) * env * gain, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildImpact(string name, float duration, float gain, int seed)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            var rng = new System.Random(seed);
            float low = 0f;
            float low2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = low * 0.88f + n * 0.12f;
                low2 = low2 * 0.975f + n * 0.025f;
                float boom = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Lerp(67f, 34f, t / duration));
                float crack = t < 0.025f ? n * (1f - t / 0.025f) : 0f;
                float env = Mathf.Exp(-t * 5.1f);
                data[i] = Mathf.Clamp((low * 0.42f + low2 * 0.72f + boom * 0.37f + crack * 0.52f) * env * gain, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildMetal(string name, float duration, float frequency, float gain)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (frequency > 500f ? 17f : 7.2f));
                float wobble = 1f + Mathf.Sin(t * 53f) * 0.018f;
                float a = Mathf.Sin(t * Mathf.PI * 2f * frequency * wobble);
                float b = Mathf.Sin(t * Mathf.PI * 2f * frequency * 1.71f) * 0.34f;
                float c = Mathf.Sin(t * Mathf.PI * 2f * frequency * 2.43f) * 0.16f;
                data[i] = Mathf.Clamp((a + b + c) * env * gain, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildEnergy(string name, float duration, float from, float to, float gain)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            float p1 = 0f;
            float p2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / duration);
                float freq = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, u));
                p1 += Mathf.PI * 2f * freq / SampleRate;
                p2 += Mathf.PI * 2f * freq * 1.50f / SampleRate;
                float env = Mathf.Sin(Mathf.PI * u) * Mathf.Exp(-u * 0.34f);
                data[i] = Mathf.Clamp((Mathf.Sin(p1) * 0.72f + Mathf.Sin(p2) * 0.25f) * env * gain, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildConfirmation(string name, bool kill)
        {
            float duration = kill ? 0.18f : 0.095f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / duration);
                float baseFreq = kill ? Mathf.Lerp(610f, 890f, u) : Mathf.Lerp(920f, 760f, u);
                float env = Mathf.Sin(Mathf.PI * u) * Mathf.Exp(-u * 0.8f);
                float tone = Mathf.Sin(t * Mathf.PI * 2f * baseFreq) + Mathf.Sin(t * Mathf.PI * 2f * baseFreq * 2f) * 0.18f;
                data[i] = tone * env * (kill ? 0.34f : 0.24f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildRumbleLoop()
        {
            float duration = 1.8f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            var rng = new System.Random(149);
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = low * 0.988f + n * 0.012f;
                float engines = Mathf.Sin(t * Mathf.PI * 2f * 38f) * 0.16f + Mathf.Sin(t * Mathf.PI * 2f * 57f) * 0.08f;
                data[i] = Mathf.Clamp(low * 0.42f + engines, -0.42f, 0.42f);
            }
            return MakeClip("v44_BattleRumble", data);
        }

        private static AudioClip BuildDangerLoop()
        {
            float duration = 1.0f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 1.0f)), 7f);
                float low = Mathf.Sin(t * Mathf.PI * 2f * 54f) * 0.38f + Mathf.Sin(t * Mathf.PI * 2f * 108f) * 0.12f;
                data[i] = low * pulse * 0.62f;
            }
            return MakeClip("v44_DangerPulse", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
