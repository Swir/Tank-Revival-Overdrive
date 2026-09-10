using UnityEngine;

namespace TankRevival
{
    public sealed class BattlefieldAtmosphere : MonoBehaviour
    {
        private enum AtmosphereKind
        {
            ColdDust,
            Rain,
            Ash,
            Snow,
            EmberStorm
        }

        private TankGame _game;
        private AtmosphereKind _kind;
        private int _lastSector = -1;
        private Transform[] _particles;
        private SpriteRenderer[] _renderers;
        private Vector2[] _velocity;
        private float[] _phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindAnyObjectByType<BattlefieldAtmosphere>() != null) return;
            var go = new GameObject("BattlefieldAtmosphere");
            go.AddComponent<BattlefieldAtmosphere>();
        }

        private void Update()
        {
            if (_game == null)
                _game = FindAnyObjectByType<TankGame>();
            if (_game == null) return;

            int sector = Mathf.Clamp((_game.CurrentRound - 1) / 20, 0, 4);
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

            int count = _kind switch
            {
                AtmosphereKind.ColdDust => 28,
                AtmosphereKind.Rain => 54,
                AtmosphereKind.Ash => 44,
                AtmosphereKind.Snow => 48,
                _ => 60
            };

            _particles = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _velocity = new Vector2[count];
            _phase = new float[count];

            Color color = GetBaseColor();
            for (int i = 0; i < count; i++)
            {
                Vector2 size;
                bool disc = false;
                switch (_kind)
                {
                    case AtmosphereKind.Rain:
                        size = new Vector2(Random.Range(0.018f, 0.032f), Random.Range(0.35f, 0.72f));
                        _velocity[i] = new Vector2(-0.9f, Random.Range(-8.5f, -6.2f));
                        break;
                    case AtmosphereKind.Snow:
                        float snowSize = Random.Range(0.035f, 0.095f);
                        size = Vector2.one * snowSize;
                        disc = true;
                        _velocity[i] = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.95f, -0.48f));
                        break;
                    case AtmosphereKind.Ash:
                        float ashSize = Random.Range(0.025f, 0.075f);
                        size = Vector2.one * ashSize;
                        disc = true;
                        _velocity[i] = new Vector2(Random.Range(-0.24f, 0.34f), Random.Range(-0.42f, 0.18f));
                        break;
                    case AtmosphereKind.EmberStorm:
                        size = new Vector2(Random.Range(0.025f, 0.055f), Random.Range(0.12f, 0.26f));
                        _velocity[i] = new Vector2(Random.Range(0.30f, 0.95f), Random.Range(1.1f, 2.6f));
                        break;
                    default:
                        float dustSize = Random.Range(0.018f, 0.045f);
                        size = Vector2.one * dustSize;
                        disc = true;
                        _velocity[i] = new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.03f, 0.18f));
                        break;
                }

                Vector3 position = new Vector3(Random.Range(-12.2f, 12.2f), Random.Range(-7.0f, 7.0f), 0f);
                GameObject particle = disc
                    ? VisualFactory.Disc("AtmosphereParticle", transform, size, color, position, 95)
                    : VisualFactory.Rect("AtmosphereParticle", transform, size, color, position, 95);

                _particles[i] = particle.transform;
                _renderers[i] = particle.GetComponent<SpriteRenderer>();
                _phase[i] = Random.Range(0f, Mathf.PI * 2f);
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = _kind switch
                {
                    AtmosphereKind.Rain => new Color(0.016f, 0.028f, 0.042f),
                    AtmosphereKind.Ash => new Color(0.040f, 0.033f, 0.036f),
                    AtmosphereKind.Snow => new Color(0.030f, 0.044f, 0.058f),
                    AtmosphereKind.EmberStorm => new Color(0.055f, 0.020f, 0.018f),
                    _ => new Color(0.020f, 0.027f, 0.040f)
                };
            }
        }

        private Color GetBaseColor()
        {
            return _kind switch
            {
                AtmosphereKind.Rain => new Color(0.46f, 0.74f, 1f, 0.22f),
                AtmosphereKind.Ash => new Color(0.72f, 0.66f, 0.62f, 0.20f),
                AtmosphereKind.Snow => new Color(0.92f, 0.97f, 1f, 0.34f),
                AtmosphereKind.EmberStorm => new Color(1f, 0.34f, 0.08f, 0.42f),
                _ => new Color(0.52f, 0.70f, 0.78f, 0.14f)
            };
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
                if (_kind == AtmosphereKind.Snow || _kind == AtmosphereKind.Ash)
                    velocity.x += Mathf.Sin(Time.time * 1.4f + _phase[i]) * 0.16f;

                p.localPosition += (Vector3)(velocity * dt);
                Vector3 pos = p.localPosition;

                if (pos.y < -7.4f) pos.y = 7.4f;
                if (pos.y > 7.4f) pos.y = -7.4f;
                if (pos.x < -12.6f) pos.x = 12.6f;
                if (pos.x > 12.6f) pos.x = -12.6f;
                p.localPosition = pos;

                if (_kind == AtmosphereKind.EmberStorm || _kind == AtmosphereKind.Ash)
                    p.Rotate(0f, 0f, (18f + i % 7 * 5f) * dt);

                if (_renderers[i] != null && _kind != AtmosphereKind.Rain)
                {
                    Color c = _renderers[i].color;
                    float baseAlpha = GetBaseColor().a;
                    c.a = baseAlpha * (0.72f + Mathf.Sin(Time.time * 2f + _phase[i]) * 0.22f);
                    _renderers[i].color = c;
                }
            }
        }

        private void ClearParticles()
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i] != null)
                    Destroy(_particles[i].gameObject);
            }
            _particles = null;
            _renderers = null;
            _velocity = null;
            _phase = null;
        }

        private void OnDestroy()
        {
            ClearParticles();
        }
    }
}
