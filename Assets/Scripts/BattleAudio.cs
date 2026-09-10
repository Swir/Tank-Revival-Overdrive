using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum SoundCue
    {
        PlayerShot,
        EnemyShot,
        HeavyShot,
        Plasma,
        Emp,
        Ricochet,
        ExplosionSmall,
        ExplosionLarge,
        Pickup,
        AmmoPickup,
        BossAlarm,
        EagleAlarm,
        RoundClear
    }

    public sealed class BattleAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;
        public static BattleAudio Instance { get; private set; }

        private readonly Dictionary<SoundCue, AudioClip> _clips = new Dictionary<SoundCue, AudioClip>();
        private AudioSource _fx;
        private AudioSource _engine;
        private float _engineTarget;
        private float _engineSpeed01;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _fx = gameObject.AddComponent<AudioSource>();
            _fx.playOnAwake = false;
            _fx.spatialBlend = 0f;
            _fx.volume = 0.85f;

            _engine = gameObject.AddComponent<AudioSource>();
            _engine.playOnAwake = false;
            _engine.loop = true;
            _engine.spatialBlend = 0f;
            _engine.volume = 0f;
            _engine.clip = BuildEngineLoop();

            BuildClips();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_engine == null) return;
            _engine.volume = Mathf.MoveTowards(_engine.volume, _engineTarget, Time.unscaledDeltaTime * 1.6f);
            _engine.pitch = Mathf.Lerp(0.82f, 1.18f, _engineSpeed01) + Mathf.Sin(Time.unscaledTime * 14f) * 0.015f;

            if (_engineTarget > 0.01f && !_engine.isPlaying)
                _engine.Play();
            else if (_engineTarget <= 0.001f && _engine.volume <= 0.002f && _engine.isPlaying)
                _engine.Stop();
        }

        public void SetEngineMoving(bool moving, float speed01)
        {
            _engineTarget = moving ? 0.23f : 0f;
            _engineSpeed01 = Mathf.Clamp01(speed01);
        }

        public void Play(SoundCue cue, float volume = 1f, float pitchJitter = 0.035f)
        {
            if (_fx == null || !_clips.TryGetValue(cue, out var clip) || clip == null) return;
            float oldPitch = _fx.pitch;
            _fx.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            _fx.PlayOneShot(clip, Mathf.Clamp01(volume));
            _fx.pitch = oldPitch;
        }

        public static void PlayGlobal(SoundCue cue, float volume = 1f, float pitchJitter = 0.035f)
        {
            Instance?.Play(cue, volume, pitchJitter);
        }

        private void BuildClips()
        {
            _clips[SoundCue.PlayerShot] = BuildGunShot("PlayerShot", 0.19f, 115f, 0.92f, 11);
            _clips[SoundCue.EnemyShot] = BuildGunShot("EnemyShot", 0.15f, 92f, 0.70f, 13);
            _clips[SoundCue.HeavyShot] = BuildGunShot("HeavyShot", 0.28f, 72f, 1.15f, 17);
            _clips[SoundCue.Plasma] = BuildSweep("Plasma", 0.28f, 520f, 1220f, 0.68f, true);
            _clips[SoundCue.Emp] = BuildSweep("Emp", 0.34f, 980f, 180f, 0.62f, false);
            _clips[SoundCue.Ricochet] = BuildRicochet();
            _clips[SoundCue.ExplosionSmall] = BuildExplosion("ExplosionSmall", 0.42f, 0.76f, 23);
            _clips[SoundCue.ExplosionLarge] = BuildExplosion("ExplosionLarge", 0.86f, 1.0f, 29);
            _clips[SoundCue.Pickup] = BuildSweep("Pickup", 0.20f, 420f, 860f, 0.45f, true);
            _clips[SoundCue.AmmoPickup] = BuildSweep("AmmoPickup", 0.25f, 360f, 1120f, 0.55f, true);
            _clips[SoundCue.BossAlarm] = BuildAlarm("BossAlarm", 0.90f, 110f, 0.68f);
            _clips[SoundCue.EagleAlarm] = BuildAlarm("EagleAlarm", 0.58f, 180f, 0.58f);
            _clips[SoundCue.RoundClear] = BuildSweep("RoundClear", 0.38f, 330f, 740f, 0.48f, true);
        }

        private static AudioClip BuildGunShot(string name, float duration, float bodyFrequency, float gain, int seed)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var rng = new System.Random(seed);
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Exp(-t * 18f);
                float crack = i < SampleRate * 0.012f ? (1f - i / (SampleRate * 0.012f)) : 0f;
                float freq = bodyFrequency * Mathf.Lerp(1.22f, 0.74f, t / duration);
                phase += Mathf.PI * 2f * freq / SampleRate;
                float body = Mathf.Sin(phase) * 0.62f + Mathf.Sin(phase * 0.47f) * 0.22f;
                data[i] = Mathf.Clamp((body * env + n * (0.42f * env + 0.38f * crack)) * gain, -1f, 1f);
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildExplosion(string name, float duration, float gain, int seed)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var rng = new System.Random(seed);
            float low = 0f;
            float low2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 4.9f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = low * 0.91f + noise * 0.09f;
                low2 = low2 * 0.976f + noise * 0.024f;
                float boom = Mathf.Sin(Mathf.PI * 2f * (54f - t * 19f) * t) * Mathf.Exp(-t * 6.2f);
                float transient = t < 0.035f ? (1f - t / 0.035f) * noise : 0f;
                data[i] = Mathf.Clamp((low * 0.50f + low2 * 0.72f + boom * 0.55f + transient * 0.38f) * env * gain, -1f, 1f);
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildSweep(string name, float duration, float startFreq, float endFreq, float gain, bool addHarmony)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            float phase = 0f;
            float phase2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / duration);
                float freq = Mathf.Lerp(startFreq, endFreq, u * u);
                phase += Mathf.PI * 2f * freq / SampleRate;
                phase2 += Mathf.PI * 2f * (freq * 1.51f) / SampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) * Mathf.Exp(-u * 0.45f);
                float v = Mathf.Sin(phase) * 0.74f;
                if (addHarmony) v += Mathf.Sin(phase2) * 0.22f;
                data[i] = Mathf.Clamp(v * env * gain, -1f, 1f);
            }

            return MakeClip(name, data);
        }

        private static AudioClip BuildRicochet()
        {
            int count = Mathf.CeilToInt(0.19f * SampleRate);
            var data = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / 0.19f;
                float freq = Mathf.Lerp(1800f, 520f, u);
                phase += Mathf.PI * 2f * freq / SampleRate;
                data[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.55f;
            }
            return MakeClip("Ricochet", data);
        }

        private static AudioClip BuildAlarm(string name, float duration, float frequency, float gain)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float mod = 0.58f + 0.42f * Mathf.Sin(t * Mathf.PI * 2f * 3.2f);
                float tone = Mathf.Sin(t * Mathf.PI * 2f * frequency) + Mathf.Sin(t * Mathf.PI * 2f * frequency * 2f) * 0.26f;
                float fade = Mathf.Clamp01(Mathf.Min(t * 8f, (duration - t) * 8f));
                data[i] = Mathf.Clamp(tone * mod * fade * gain * 0.58f, -1f, 1f);
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildEngineLoop()
        {
            float duration = 0.48f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[count];
            var rng = new System.Random(37);
            float low = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = low * 0.94f + noise * 0.06f;
                float pulse = Mathf.Sin(t * Mathf.PI * 2f * 43f) * 0.38f + Mathf.Sin(t * Mathf.PI * 2f * 86f) * 0.18f;
                float tread = Mathf.Sign(Mathf.Sin(t * Mathf.PI * 2f * 18f)) * 0.045f;
                data[i] = Mathf.Clamp((pulse + low * 0.22f + tread) * 0.52f, -1f, 1f);
            }

            return MakeClip("EngineLoop", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
