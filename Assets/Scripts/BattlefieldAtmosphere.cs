using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Ten-sector atmospheric renderer for the 100-round campaign.
    /// Uses a fixed particle budget and reuses the same runtime sprites to keep late-game cost predictable.
    /// </summary>
    public sealed class BattlefieldAtmosphere : MonoBehaviour
    {
        private enum AtmosphereKind
        {
            BorderDust,
            IronHaze,
            AshCrossroads,
            StormRain,
            RiverMist,
            FrozenSpear,
            FortressDust,
            NightSparks,
            BurningGate,
            OverdriveStorm
        }

        private TankGame _game;
        private AtmosphereKind _kind;
        private int _lastSector = -1;
        private Transform[] _particles;
        private SpriteRenderer[] _renderers;
        private Vector2[] _velocity;
        private float[] _phase;
        private float[] _baseAlpha;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<BattlefieldAtmosphere>() != null) return;
            var go = new GameObject("BattlefieldAtmosphere");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldAtmosphere>();
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;

            int sector = Mathf.Clamp((_game.CurrentRound - 1) / 10, 0, 9);
            if (sector != _lastSector)
            {
                _lastSector = sector;
                _kind = (AtmosphereKind)sector;
                Rebuild();
            }

            AnimateParticles();
        }

        private void Rebuild()
        {
            ClearParticles();
            int count = ParticleBudget(_kind);
            _particles = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _velocity = new Vector2[count];
            _phase = new float[count];
            _baseAlpha = new float[count];

            Color baseColor = BaseColor(_kind);
            for (int i = 0; i < count; i++)
            {
                bool disc;
                Vector2 size;
                ConfigureParticle(i, out size, out disc);
                Vector3 position = new Vector3(Random.Range(-12.2f, 12.2f), Random.Range(-7.0f, 7.0f), 0f);
                Color color = baseColor;
                color.a *= Random.Range(0.68f, 1.08f);

                GameObject particle = disc
                    ? VisualFactory.Disc("AtmosphereParticle", transform, size, color, position, 95)
                    : VisualFactory.Rect("AtmosphereParticle", transform, size, color, position, 95);

                _particles[i] = particle.transform;
                _renderers[i] = particle.GetComponent<SpriteRenderer>();
                _phase[i] = Random.Range(0f, Mathf.PI * 2f);
                _baseAlpha[i] = color.a;
            }

            Camera cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = BackgroundColor(_kind);
        }

        private static int ParticleBudget(AtmosphereKind kind)
        {
            switch (kind)
            {
                case AtmosphereKind.StormRain: return 58;
                case AtmosphereKind.FrozenSpear: return 50;
                case AtmosphereKind.BurningGate: return 56;
                case AtmosphereKind.OverdriveStorm: return 62;
                case AtmosphereKind.RiverMist: return 34;
                case AtmosphereKind.NightSparks: return 38;
                default: return 42;
            }
        }

        private void ConfigureParticle(int index, out Vector2 size, out bool disc)
        {
            disc = false;
            switch (_kind)
            {
                case AtmosphereKind.StormRain:
                    size = new Vector2(Random.Range(0.018f, 0.032f), Random.Range(0.40f, 0.78f));
                    _velocity[index] = new Vector2(-1.1f, Random.Range(-9.4f, -6.8f));
                    break;
                case AtmosphereKind.FrozenSpear:
                    float snow = Random.Range(0.035f, 0.10f);
                    size = Vector2.one * snow;
                    disc = true;
                    _velocity[index] = new Vector2(Random.Range(-0.32f, 0.32f), Random.Range(-1.05f, -0.45f));
                    break;
                case AtmosphereKind.AshCrossroads:
                    float ash = Random.Range(0.025f, 0.080f);
                    size = Vector2.one * ash;
                    disc = true;
                    _velocity[index] = new Vector2(Random.Range(-0.25f, 0.36f), Random.Range(-0.40f, 0.20f));
                    break;
                case AtmosphereKind.RiverMist:
                    size = new Vector2(Random.Range(0.55f, 1.55f), Random.Range(0.05f, 0.12f));
                    _velocity[index] = new Vector2(Random.Range(0.20f, 0.50f), Random.Range(-0.04f, 0.04f));
                    break;
                case AtmosphereKind.NightSparks:
                    size = Vector2.one * Random.Range(0.025f, 0.060f);
                    disc = true;
                    _velocity[index] = new Vector2(Random.Range(-0.12f, 0.18f), Random.Range(0.35f, 0.95f));
                    break;
                case AtmosphereKind.BurningGate:
                    size = new Vector2(Random.Range(0.025f, 0.060f), Random.Range(0.12f, 0.28f));
                    _velocity[index] = new Vector2(Random.Range(0.30f, 1.0f), Random.Range(1.2f, 2.8f));
                    break;
                case AtmosphereKind.OverdriveStorm:
                    size = index % 3 == 0 ? new Vector2(0.025f, Random.Range(0.35f, 0.70f)) : Vector2.one * Random.Range(0.025f, 0.065f);
                    disc = index % 3 != 0;
                    _velocity[index] = new Vector2(Random.Range(-0.9f, 0.9f), index % 3 == 0 ? Random.Range(-6.8f, -4.8f) : Random.Range(0.55f, 1.8f));
                    break;
                case AtmosphereKind.FortressDust:
                    size = new Vector2(Random.Range(0.04f, 0.13f), Random.Range(0.025f, 0.07f));
                    _velocity[index] = new Vector2(Random.Range(-0.55f, 0.55f), Random.Range(-0.12f, 0.22f));
                    break;
                case AtmosphereKind.IronHaze:
                    size = new Vector2(Random.Range(0.08f, 0.24f), Random.Range(0.02f, 0.05f));
                    _velocity[index] = new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(0.02f, 0.12f));
                    break;
                default:
                    float dust = Random.Range(0.018f, 0.050f);
                    size = Vector2.one * dust;
                    disc = true;
                    _velocity[index] = new Vector2(Random.Range(-0.14f, 0.14f), Random.Range(0.03f, 0.20f));
                    break;
            }
        }

        private static Color BaseColor(AtmosphereKind kind)
        {
            switch (kind)
            {
                case AtmosphereKind.IronHaze: return new Color(0.64f, 0.70f, 0.74f, 0.13f);
                case AtmosphereKind.AshCrossroads: return new Color(0.76f, 0.64f, 0.56f, 0.22f);
                case AtmosphereKind.StormRain: return new Color(0.46f, 0.74f, 1f, 0.23f);
                case AtmosphereKind.RiverMist: return new Color(0.40f, 0.76f, 0.86f, 0.09f);
                case AtmosphereKind.FrozenSpear: return new Color(0.92f, 0.97f, 1f, 0.34f);
                case AtmosphereKind.FortressDust: return new Color(0.72f, 0.68f, 0.56f, 0.18f);
                case AtmosphereKind.NightSparks: return new Color(0.44f, 0.48f, 1f, 0.25f);
                case AtmosphereKind.BurningGate: return new Color(1f, 0.34f, 0.08f, 0.42f);
                case AtmosphereKind.OverdriveStorm: return new Color(1f, 0.18f, 0.16f, 0.34f);
                default: return new Color(0.52f, 0.70f, 0.78f, 0.14f);
            }
        }

        private static Color BackgroundColor(AtmosphereKind kind)
        {
            switch (kind)
            {
                case AtmosphereKind.IronHaze: return new Color(0.028f, 0.034f, 0.040f);
                case AtmosphereKind.AshCrossroads: return new Color(0.042f, 0.031f, 0.028f);
                case AtmosphereKind.StormRain: return new Color(0.014f, 0.026f, 0.042f);
                case AtmosphereKind.RiverMist: return new Color(0.018f, 0.036f, 0.045f);
                case AtmosphereKind.FrozenSpear: return new Color(0.030f, 0.044f, 0.058f);
                case AtmosphereKind.FortressDust: return new Color(0.035f, 0.034f, 0.030f);
                case AtmosphereKind.NightSparks: return new Color(0.010f, 0.012f, 0.034f);
                case AtmosphereKind.BurningGate: return new Color(0.055f, 0.020f, 0.018f);
                case AtmosphereKind.OverdriveStorm: return new Color(0.048f, 0.010f, 0.018f);
                default: return new Color(0.020f, 0.027f, 0.040f);
            }
        }

        private void AnimateParticles()
        {
            if (_particles == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < _particles.Length; i++)
            {
                Transform p = _particles[i];
                if (p == null) continue;
                Vector2 velocity = _velocity[i];
                if (_kind == AtmosphereKind.FrozenSpear || _kind == AtmosphereKind.AshCrossroads || _kind == AtmosphereKind.RiverMist)
                    velocity.x += Mathf.Sin(Time.time * 1.35f + _phase[i]) * 0.16f;

                p.localPosition += (Vector3)(velocity * dt);
                Vector3 pos = p.localPosition;
                if (pos.y < -7.4f) pos.y = 7.4f;
                if (pos.y > 7.4f) pos.y = -7.4f;
                if (pos.x < -12.6f) pos.x = 12.6f;
                if (pos.x > 12.6f) pos.x = -12.6f;
                p.localPosition = pos;

                if (_kind == AtmosphereKind.BurningGate || _kind == AtmosphereKind.AshCrossroads || _kind == AtmosphereKind.OverdriveStorm)
                    p.Rotate(0f, 0f, (18f + i % 7 * 5f) * dt);

                SpriteRenderer renderer = _renderers[i];
                if (renderer != null && _kind != AtmosphereKind.StormRain)
                {
                    Color c = renderer.color;
                    c.a = _baseAlpha[i] * (0.76f + Mathf.Sin(Time.time * 1.8f + _phase[i]) * 0.20f);
                    renderer.color = c;
                }
            }
        }

        private void ClearParticles()
        {
            if (_particles != null)
            {
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i] != null) Destroy(_particles[i].gameObject);
            }
            _particles = null;
            _renderers = null;
            _velocity = null;
            _phase = null;
            _baseAlpha = null;
        }

        private void OnDestroy()
        {
            ClearParticles();
        }
    }
}
