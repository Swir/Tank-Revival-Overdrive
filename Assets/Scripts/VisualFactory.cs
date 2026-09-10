using UnityEngine;

namespace TankRevival
{
    public static partial class VisualFactory
    {
        private static Sprite _square;
        private static Sprite _circle;
        private static Sprite _ring;

        public static Sprite Square
        {
            get
            {
                if (_square != null) return _square;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.name = "RuntimeSquareTexture";
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.HideAndDontSave;
                _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                _square.name = "RuntimeSquareSprite";
                return _square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                _circle = CreateRadialSprite("RuntimeCircleSprite", 64, 0f, 0.48f, false);
                return _circle;
            }
        }

        public static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                _ring = CreateRadialSprite("RuntimeRingSprite", 96, 0.32f, 0.48f, true);
                return _ring;
            }
        }

        private static Sprite CreateRadialSprite(string name, int size, float inner, float outer, bool hollow)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name + "Texture";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;

            var pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) * inv;
                    float edge = Mathf.Clamp01((outer - d) * size * 0.55f);
                    float alpha = edge;
                    if (hollow)
                    {
                        float innerEdge = Mathf.Clamp01((d - inner) * size * 0.55f);
                        alpha *= innerEdge;
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

        public static GameObject Rect(string name, Transform parent, Vector2 size, Color color, Vector3 localPosition, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        public static GameObject RectRotated(string name, Transform parent, Vector2 size, Color color, Vector3 localPosition, float angle, int sortingOrder = 0)
        {
            var go = Rect(name, parent, size, color, localPosition, sortingOrder);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return go;
        }

        public static GameObject Disc(string name, Transform parent, Vector2 size, Color color, Vector3 localPosition, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Circle;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        public static GameObject RingObject(string name, Transform parent, Vector2 size, Color color, Vector3 localPosition, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Ring;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        public static void BuildTankSkin(Transform parent, Color body, Color accent)
        {
            Color dark = Color.Lerp(body, Color.black, 0.56f);
            Color mid = Color.Lerp(body, accent, 0.35f);
            Color metal = new Color(0.18f, 0.20f, 0.23f, 1f);
            Color trackRubber = new Color(0.055f, 0.06f, 0.07f, 1f);

            Disc("TankShadow", parent, new Vector2(1.02f, 0.84f), new Color(0f, 0f, 0f, 0.34f), new Vector3(0.08f, -0.08f, 0f), 0);
            Rect("TrackBaseL", parent, new Vector2(0.25f, 0.96f), trackRubber, new Vector3(-0.39f, 0f, 0f), 2);
            Rect("TrackBaseR", parent, new Vector2(0.25f, 0.96f), trackRubber, new Vector3(0.39f, 0f, 0f), 2);
            Rect("TrackInnerL", parent, new Vector2(0.11f, 0.86f), metal, new Vector3(-0.39f, 0f, 0f), 3);
            Rect("TrackInnerR", parent, new Vector2(0.11f, 0.86f), metal, new Vector3(0.39f, 0f, 0f), 3);

            for (int i = 0; i < 6; i++)
            {
                float y = -0.375f + i * 0.15f;
                Rect("TrackSegmentL_" + i, parent, new Vector2(0.22f, 0.045f), new Color(0.48f, 0.51f, 0.55f, 0.92f), new Vector3(-0.39f, y, 0f), 4);
                Rect("TrackSegmentR_" + i, parent, new Vector2(0.22f, 0.045f), new Color(0.48f, 0.51f, 0.55f, 0.92f), new Vector3(0.39f, y, 0f), 4);
            }

            Rect("HullShadow", parent, new Vector2(0.69f, 0.78f), new Color(0f, 0f, 0f, 0.30f), new Vector3(0.035f, -0.035f, 0f), 4);
            Rect("Hull", parent, new Vector2(0.66f, 0.78f), dark, Vector3.zero, 5);
            Rect("HullCenter", parent, new Vector2(0.56f, 0.65f), body, new Vector3(0f, 0.015f, 0f), 6);
            RectRotated("NoseLeft", parent, new Vector2(0.24f, 0.26f), mid, new Vector3(-0.16f, 0.30f, 0f), -13f, 7);
            RectRotated("NoseRight", parent, new Vector2(0.24f, 0.26f), mid, new Vector3(0.16f, 0.30f, 0f), 13f, 7);
            Rect("RearArmor", parent, new Vector2(0.50f, 0.12f), Color.Lerp(dark, body, 0.35f), new Vector3(0f, -0.30f, 0f), 7);
            Rect("EngineDeck", parent, new Vector2(0.36f, 0.14f), new Color(0.12f, 0.14f, 0.16f), new Vector3(0f, -0.19f, 0f), 8);

            for (int i = -1; i <= 1; i++)
                Rect("EngineVent_" + i, parent, new Vector2(0.055f, 0.11f), new Color(0.38f, 0.42f, 0.45f), new Vector3(i * 0.095f, -0.19f, 0f), 9);

            Disc("TurretShadow", parent, new Vector2(0.52f, 0.48f), new Color(0f, 0f, 0f, 0.34f), new Vector3(0.035f, -0.025f, 0f), 9);
            Disc("TurretBase", parent, new Vector2(0.49f, 0.49f), dark, Vector3.zero, 10);
            Disc("Turret", parent, new Vector2(0.42f, 0.42f), accent, new Vector3(0f, 0.015f, 0f), 11);
            Disc("Hatch", parent, new Vector2(0.18f, 0.18f), Color.Lerp(accent, Color.white, 0.18f), new Vector3(-0.08f, -0.035f, 0f), 12);
            Disc("HatchCore", parent, new Vector2(0.09f, 0.09f), Color.Lerp(accent, Color.black, 0.30f), new Vector3(-0.08f, -0.035f, 0f), 13);
            Rect("GunMantlet", parent, new Vector2(0.28f, 0.14f), dark, new Vector3(0f, 0.20f, 0f), 12);
            Rect("BarrelShadow", parent, new Vector2(0.16f, 0.62f), new Color(0f, 0f, 0f, 0.26f), new Vector3(0.025f, 0.47f, 0f), 11);
            Rect("Barrel", parent, new Vector2(0.13f, 0.63f), Color.Lerp(accent, Color.black, 0.18f), new Vector3(0f, 0.47f, 0f), 13);
            Rect("BarrelHighlight", parent, new Vector2(0.035f, 0.54f), new Color(1f, 1f, 1f, 0.28f), new Vector3(-0.035f, 0.47f, 0f), 14);
            Rect("MuzzleBrake", parent, new Vector2(0.23f, 0.12f), Color.Lerp(accent, Color.black, 0.35f), new Vector3(0f, 0.79f, 0f), 14);

            Color lamp = new Color(0.78f, 0.97f, 1f, 0.95f);
            Disc("LampL", parent, new Vector2(0.105f, 0.105f), lamp, new Vector3(-0.23f, 0.31f, 0f), 15);
            Disc("LampR", parent, new Vector2(0.105f, 0.105f), lamp, new Vector3(0.23f, 0.31f, 0f), 15);
            Disc("LampGlowL", parent, new Vector2(0.20f, 0.20f), new Color(lamp.r, lamp.g, lamp.b, 0.18f), new Vector3(-0.23f, 0.31f, 0f), 14);
            Disc("LampGlowR", parent, new Vector2(0.20f, 0.20f), new Color(lamp.r, lamp.g, lamp.b, 0.18f), new Vector3(0.23f, 0.31f, 0f), 14);
            RectRotated("HullGlint", parent, new Vector2(0.23f, 0.035f), new Color(1f, 1f, 1f, 0.36f), new Vector3(-0.10f, 0.15f, 0f), -8f, 15);
            Disc("TurretGlint", parent, new Vector2(0.10f, 0.065f), new Color(1f, 1f, 1f, 0.48f), new Vector3(-0.09f, 0.10f, 0f), 15);

            if (parent.GetComponent<TankTrackAnimator>() == null)
                parent.gameObject.AddComponent<TankTrackAnimator>();
        }

        public static void Explosion(Vector3 worldPosition, Color color, float scale = 1f)
        {
            var go = new GameObject("ExplosionFX");
            go.transform.position = worldPosition;
            var fx = go.AddComponent<ExplosionFx>();
            fx.Initialize(color, scale);
        }
    }

    public sealed class TankTrackAnimator : MonoBehaviour
    {
        private Transform[] _left;
        private Transform[] _right;
        private Vector3 _lastPosition;
        private float _phase;

        private void Start()
        {
            _left = FindSegments("TrackSegmentL_");
            _right = FindSegments("TrackSegmentR_");
            _lastPosition = transform.position;
        }

        private Transform[] FindSegments(string prefix)
        {
            var result = new Transform[6];
            for (int i = 0; i < result.Length; i++)
                result[i] = transform.Find(prefix + i);
            return result;
        }

        private void LateUpdate()
        {
            float distance = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
            if (distance < 0.0001f) return;

            _phase += distance * 5.5f;
            AnimateSide(_left, _phase);
            AnimateSide(_right, _phase);
        }

        private static void AnimateSide(Transform[] segments, float phase)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == null) continue;
                float y = -0.375f + Mathf.Repeat(i * 0.15f + phase * 0.15f, 0.90f);
                var p = segment.localPosition;
                p.y = y;
                segment.localPosition = p;
            }
        }
    }

    public sealed class ExplosionFx : MonoBehaviour
    {
        private Transform[] _pieces;
        private SpriteRenderer[] _renderers;
        private Vector2[] _velocity;
        private Transform[] _smoke;
        private SpriteRenderer[] _smokeRenderers;
        private Vector2[] _smokeVelocity;
        private Transform _flash;
        private SpriteRenderer _flashRenderer;
        private Transform _ring;
        private SpriteRenderer _ringRenderer;
        private float _age;
        private float _life = 0.72f;
        private float _scale;

        public void Initialize(Color color, float scale)
        {
            _scale = scale;
            const int sparkCount = 20;
            const int smokeCount = 8;
            _pieces = new Transform[sparkCount];
            _renderers = new SpriteRenderer[sparkCount];
            _velocity = new Vector2[sparkCount];
            _smoke = new Transform[smokeCount];
            _smokeRenderers = new SpriteRenderer[smokeCount];
            _smokeVelocity = new Vector2[smokeCount];

            for (int i = 0; i < sparkCount; i++)
            {
                float angle = (Mathf.PI * 2f * i / sparkCount) + Random.Range(-0.20f, 0.20f);
                float speed = Random.Range(2.2f, 6.2f) * scale;
                Vector2 size = new Vector2(Random.Range(0.05f, 0.11f), Random.Range(0.14f, 0.34f)) * scale;
                Color c = i % 4 == 0 ? Color.white : (i % 2 == 0 ? Color.Lerp(color, new Color(1f, 0.72f, 0.18f), 0.60f) : color);
                var p = VisualFactory.Rect("Spark", transform, size, c, Vector3.zero, 80 + i);
                p.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                _pieces[i] = p.transform;
                _renderers[i] = p.GetComponent<SpriteRenderer>();
                _velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

            for (int i = 0; i < smokeCount; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0.05f, 0.30f) * scale;
                float size = Random.Range(0.20f, 0.42f) * scale;
                Vector3 pos = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                Color smokeColor = new Color(0.13f, 0.14f, 0.16f, Random.Range(0.30f, 0.52f));
                var s = VisualFactory.Disc("Smoke", transform, new Vector2(size, size), smokeColor, pos, 58 + i);
                _smoke[i] = s.transform;
                _smokeRenderers[i] = s.GetComponent<SpriteRenderer>();
                _smokeVelocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(0.25f, 0.85f) * scale;
            }

            var flash = VisualFactory.Disc("Flash", transform, Vector2.one * (0.72f * scale), new Color(1f, 0.93f, 0.56f, 0.96f), Vector3.zero, 110);
            _flash = flash.transform;
            _flashRenderer = flash.GetComponent<SpriteRenderer>();
            var ring = VisualFactory.RingObject("ShockRing", transform, Vector2.one * (0.60f * scale), new Color(1f, 0.72f, 0.24f, 0.72f), Vector3.zero, 105);
            _ring = ring.transform;
            _ringRenderer = ring.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / _life);

            for (int i = 0; i < _pieces.Length; i++)
            {
                _pieces[i].localPosition += (Vector3)(_velocity[i] * Time.unscaledDeltaTime);
                _velocity[i] *= Mathf.Pow(0.90f, Time.unscaledDeltaTime * 60f);
                var c = _renderers[i].color;
                c.a = Mathf.Clamp01(1f - t * 1.15f);
                _renderers[i].color = c;
                _pieces[i].localScale *= 1f + Time.unscaledDeltaTime * 0.55f;
            }

            for (int i = 0; i < _smoke.Length; i++)
            {
                _smoke[i].localPosition += (Vector3)(_smokeVelocity[i] * Time.unscaledDeltaTime);
                float grow = 1f + Time.unscaledDeltaTime * (0.75f + i * 0.03f);
                _smoke[i].localScale *= grow;
                var c = _smokeRenderers[i].color;
                c.a = Mathf.Clamp01((1f - t) * 0.48f);
                _smokeRenderers[i].color = c;
            }

            if (_flash != null)
            {
                float ft = Mathf.Clamp01(_age / 0.16f);
                _flash.localScale = Vector3.one * (1f + ft * 1.45f);
                var c = _flashRenderer.color;
                c.a = 1f - ft;
                _flashRenderer.color = c;
            }

            if (_ring != null)
            {
                float rt = Mathf.Clamp01(_age / 0.42f);
                _ring.localScale = Vector3.one * (1f + rt * 3.4f);
                var c = _ringRenderer.color;
                c.a = (1f - rt) * 0.72f;
                _ringRenderer.color = c;
            }

            transform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f * _scale);
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
