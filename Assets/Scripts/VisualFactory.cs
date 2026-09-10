using UnityEngine;

namespace TankRevival
{
    public static class VisualFactory
    {
        private static Sprite _square;

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

        public static void BuildTankSkin(Transform parent, Color body, Color accent)
        {
            Rect("Shadow", parent, new Vector2(0.92f, 0.78f), new Color(0f, 0f, 0f, 0.38f), new Vector3(0.07f, -0.08f, 0f), 1);
            Rect("TrackL", parent, new Vector2(0.22f, 0.92f), new Color(0.08f, 0.09f, 0.10f), new Vector3(-0.39f, 0f, 0f), 2);
            Rect("TrackR", parent, new Vector2(0.22f, 0.92f), new Color(0.08f, 0.09f, 0.10f), new Vector3(0.39f, 0f, 0f), 2);
            Rect("TrackHighlightL", parent, new Vector2(0.07f, 0.72f), new Color(0.35f, 0.37f, 0.39f), new Vector3(-0.39f, 0f, 0f), 3);
            Rect("TrackHighlightR", parent, new Vector2(0.07f, 0.72f), new Color(0.35f, 0.37f, 0.39f), new Vector3(0.39f, 0f, 0f), 3);
            Rect("Hull", parent, new Vector2(0.62f, 0.76f), body, Vector3.zero, 4);
            Rect("HullTop", parent, new Vector2(0.48f, 0.16f), accent, new Vector3(0f, 0.23f, 0f), 5);
            Rect("TurretShadow", parent, new Vector2(0.47f, 0.47f), new Color(0f, 0f, 0f, 0.28f), new Vector3(0.035f, -0.025f, 0f), 5);
            Rect("Turret", parent, new Vector2(0.42f, 0.42f), accent, Vector3.zero, 6);
            Rect("Barrel", parent, new Vector2(0.13f, 0.56f), accent * 0.88f, new Vector3(0f, 0.39f, 0f), 6);
            Rect("BarrelTip", parent, new Vector2(0.18f, 0.10f), Color.Lerp(accent, Color.white, 0.25f), new Vector3(0f, 0.67f, 0f), 7);
            Rect("Glint", parent, new Vector2(0.13f, 0.09f), new Color(1f, 1f, 1f, 0.60f), new Vector3(-0.10f, 0.08f, 0f), 7);
        }

        public static void Explosion(Vector3 worldPosition, Color color, float scale = 1f)
        {
            var go = new GameObject("ExplosionFX");
            go.transform.position = worldPosition;
            var fx = go.AddComponent<ExplosionFx>();
            fx.Initialize(color, scale);
        }
    }

    public sealed class ExplosionFx : MonoBehaviour
    {
        private Transform[] _pieces;
        private SpriteRenderer[] _renderers;
        private Vector2[] _velocity;
        private float _age;
        private float _life = 0.48f;

        public void Initialize(Color color, float scale)
        {
            const int count = 14;
            _pieces = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _velocity = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.18f, 0.18f);
                float speed = Random.Range(1.8f, 4.5f) * scale;
                float size = Random.Range(0.08f, 0.22f) * scale;
                var c = i % 3 == 0 ? Color.white : (i % 2 == 0 ? Color.Lerp(color, Color.yellow, 0.45f) : color);
                var p = VisualFactory.Rect("Spark", transform, new Vector2(size, size), c, Vector3.zero, 50 + i);
                _pieces[i] = p.transform;
                _renderers[i] = p.GetComponent<SpriteRenderer>();
                _velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

            VisualFactory.Rect("Flash", transform, Vector2.one * (0.62f * scale), new Color(1f, 0.92f, 0.45f, 0.9f), Vector3.zero, 70);
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / _life);

            for (int i = 0; i < _pieces.Length; i++)
            {
                _pieces[i].localPosition += (Vector3)(_velocity[i] * Time.unscaledDeltaTime);
                _velocity[i] *= 0.94f;
                var c = _renderers[i].color;
                c.a = 1f - t;
                _renderers[i].color = c;
            }

            transform.localScale = Vector3.one * (1f + t * 0.35f);
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
