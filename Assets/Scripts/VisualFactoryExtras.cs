using UnityEngine;

namespace TankRevival
{
    public static partial class VisualFactory
    {
        public static void BuildEagleStronghold(Transform parent)
        {
            Color steelDark = new Color(0.08f, 0.10f, 0.13f);
            Color steel = new Color(0.24f, 0.30f, 0.36f);
            Color steelLight = new Color(0.48f, 0.58f, 0.66f);
            Color red = new Color(0.72f, 0.055f, 0.075f);
            Color white = new Color(0.94f, 0.97f, 1f);
            Color gold = new Color(1f, 0.72f, 0.12f);

            Disc("EagleBunkerShadow", parent, new Vector2(1.48f, 1.08f), new Color(0f, 0f, 0f, 0.42f), new Vector3(0.08f, -0.08f, 0f), 2);
            Rect("EagleBunker", parent, new Vector2(1.28f, 0.92f), steelDark, Vector3.zero, 3);
            Rect("EagleArmorPlate", parent, new Vector2(1.16f, 0.80f), steel, new Vector3(0f, 0.02f, 0f), 4);
            Rect("EagleArmorTop", parent, new Vector2(0.98f, 0.11f), steelLight, new Vector3(-0.03f, 0.31f, 0f), 5);

            for (int i = -2; i <= 2; i++)
                Disc("BunkerBolt" + i, parent, new Vector2(0.07f, 0.07f), new Color(0.72f, 0.78f, 0.82f), new Vector3(i * 0.23f, -0.30f, 0f), 6);

            Disc("ShieldGlow", parent, new Vector2(0.82f, 0.72f), new Color(1f, 0.10f, 0.13f, 0.15f), new Vector3(0f, 0.02f, 0f), 6);
            Disc("Shield", parent, new Vector2(0.68f, 0.62f), red, new Vector3(0f, 0.02f, 0f), 7);
            Rect("ShieldStem", parent, new Vector2(0.42f, 0.32f), red, new Vector3(0f, -0.16f, 0f), 7);

            // Stylized white eagle silhouette: body, spread wings, head and gold crown/beak.
            Disc("EagleBody", parent, new Vector2(0.22f, 0.31f), white, new Vector3(0f, 0.00f, 0f), 10);
            Disc("EagleChest", parent, new Vector2(0.28f, 0.20f), white, new Vector3(0f, -0.04f, 0f), 10);

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                RectRotated("EagleWingA" + side, parent, new Vector2(0.28f, 0.095f), white, new Vector3(0.16f * s, 0.08f, 0f), -22f * s, 10);
                RectRotated("EagleWingB" + side, parent, new Vector2(0.27f, 0.085f), white, new Vector3(0.27f * s, 0.015f, 0f), -36f * s, 10);
                RectRotated("EagleWingC" + side, parent, new Vector2(0.22f, 0.075f), white, new Vector3(0.33f * s, -0.08f, 0f), -52f * s, 10);
                RectRotated("EagleTail" + side, parent, new Vector2(0.16f, 0.06f), white, new Vector3(0.075f * s, -0.22f, 0f), 18f * s, 10);
            }

            Disc("EagleHead", parent, new Vector2(0.14f, 0.14f), white, new Vector3(0.055f, 0.18f, 0f), 11);
            RectRotated("EagleBeak", parent, new Vector2(0.10f, 0.045f), gold, new Vector3(0.125f, 0.17f, 0f), -16f, 12);
            Disc("EagleEye", parent, new Vector2(0.032f, 0.032f), new Color(0.08f, 0.08f, 0.09f), new Vector3(0.078f, 0.205f, 0f), 13);
            Rect("CrownBase", parent, new Vector2(0.16f, 0.035f), gold, new Vector3(0.045f, 0.255f, 0f), 12);
            RectRotated("CrownL", parent, new Vector2(0.035f, 0.09f), gold, new Vector3(-0.005f, 0.292f, 0f), -15f, 12);
            Rect("CrownM", parent, new Vector2(0.035f, 0.10f), gold, new Vector3(0.045f, 0.305f, 0f), 12);
            RectRotated("CrownR", parent, new Vector2(0.035f, 0.09f), gold, new Vector3(0.095f, 0.292f, 0f), 15f, 12);

            var beacon = RingObject("EagleDefenseBeacon", parent, new Vector2(1.02f, 0.88f), new Color(0.20f, 0.82f, 1f, 0.30f), Vector3.zero, 1);
            beacon.AddComponent<BeaconPulse>();
        }

        public static void BuildSupplyMarker(Transform parent, AmmoType ammo)
        {
            Color c = AmmoDatabase.Color(ammo);
            var outer = RingObject("SupplyAura", parent, new Vector2(1.20f, 1.20f), new Color(c.r, c.g, c.b, 0.58f), Vector3.zero, 18);
            var inner = RingObject("SupplyRing", parent, new Vector2(0.68f, 0.68f), new Color(1f, 1f, 1f, 0.55f), Vector3.zero, 18);
            outer.AddComponent<SupplyMarkerSpin>().Speed = 62f;
            inner.AddComponent<SupplyMarkerSpin>().Speed = -90f;
            Rect("SupplyStripe", parent, new Vector2(0.58f, 0.08f), c, new Vector3(0f, -0.27f, 0f), 19);
        }

        public static void BuildBossArmor(Transform parent, int round)
        {
            Color red = new Color(1f, 0.12f, 0.06f, 0.92f);
            Color gold = new Color(1f, 0.68f, 0.08f, 0.95f);
            RingObject("BossThreatRing", parent, new Vector2(1.20f, 1.20f), new Color(red.r, red.g, red.b, 0.38f), Vector3.zero, 18).AddComponent<BeaconPulse>();
            RectRotated("BossArmorL", parent, new Vector2(0.18f, 0.52f), Color.Lerp(red, Color.black, 0.35f), new Vector3(-0.32f, 0.03f, 0f), -8f, 17);
            RectRotated("BossArmorR", parent, new Vector2(0.18f, 0.52f), Color.Lerp(red, Color.black, 0.35f), new Vector3(0.32f, 0.03f, 0f), 8f, 17);
            Disc("BossCore", parent, new Vector2(0.18f, 0.18f), gold, new Vector3(0f, -0.08f, 0f), 19);
            if (round >= 50)
            {
                Disc("BossCoreGlow", parent, new Vector2(0.36f, 0.36f), new Color(gold.r, gold.g, gold.b, 0.20f), new Vector3(0f, -0.08f, 0f), 18);
            }
        }

        public static void MuzzleFlash(Vector3 worldPosition, Color color, float scale)
        {
            var go = new GameObject("MuzzleFlashFX");
            go.transform.position = worldPosition;
            var fx = go.AddComponent<QuickFlashFx>();
            fx.Initialize(color, scale, 0.12f, true);
        }

        public static void MicroBurst(Vector3 worldPosition, Color color, float scale)
        {
            var go = new GameObject("MicroBurstFX");
            go.transform.position = worldPosition;
            var fx = go.AddComponent<QuickFlashFx>();
            fx.Initialize(color, scale, 0.20f, false);
        }

        public static void RingPulse(Vector3 worldPosition, Color color, float scale)
        {
            var go = new GameObject("RingPulseFX");
            go.transform.position = worldPosition;
            var fx = go.AddComponent<RingPulseFx>();
            fx.Initialize(color, scale);
        }

        public static void ProjectileAfterglow(Vector3 worldPosition, Color color, float scale)
        {
            var go = new GameObject("ProjectileTrailFX");
            go.transform.position = worldPosition;
            var disc = Disc("Trail", go.transform, new Vector2(scale, scale), new Color(color.r, color.g, color.b, 0.30f), Vector3.zero, 20);
            var fade = go.AddComponent<AfterglowFx>();
            fade.Renderer = disc.GetComponent<SpriteRenderer>();
        }
    }

    public sealed class SupplyMarkerSpin : MonoBehaviour
    {
        public float Speed = 60f;
        private void Update() => transform.Rotate(0f, 0f, Speed * Time.deltaTime);
    }

    public sealed class BeaconPulse : MonoBehaviour
    {
        private Vector3 _baseScale;
        private float _phase;

        private void Start()
        {
            _baseScale = transform.localScale;
            _phase = Random.Range(0f, 6f);
        }

        private void Update()
        {
            float p = 1f + Mathf.Sin(Time.time * 2.8f + _phase) * 0.08f;
            transform.localScale = _baseScale * p;
        }
    }

    public sealed class QuickFlashFx : MonoBehaviour
    {
        private SpriteRenderer[] _renderers;
        private Transform _ring;
        private float _age;
        private float _life;
        private float _scale;

        public void Initialize(Color color, float scale, float life, bool muzzle)
        {
            _life = life;
            _scale = scale;
            if (muzzle)
            {
                var a = VisualFactory.Disc("FlashCore", transform, Vector2.one * 0.32f * scale, new Color(1f, 0.96f, 0.72f, 0.95f), Vector3.zero, 120);
                var b = VisualFactory.RectRotated("FlashRayA", transform, new Vector2(0.08f, 0.58f) * scale, color, Vector3.zero, 45f, 119);
                var c = VisualFactory.RectRotated("FlashRayB", transform, new Vector2(0.08f, 0.58f) * scale, color, Vector3.zero, -45f, 119);
                _renderers = new[] { a.GetComponent<SpriteRenderer>(), b.GetComponent<SpriteRenderer>(), c.GetComponent<SpriteRenderer>() };
            }
            else
            {
                var a = VisualFactory.Disc("BurstCore", transform, Vector2.one * 0.34f * scale, color, Vector3.zero, 110);
                var b = VisualFactory.RingObject("BurstRing", transform, Vector2.one * 0.30f * scale, new Color(color.r, color.g, color.b, 0.72f), Vector3.zero, 109);
                _ring = b.transform;
                _renderers = new[] { a.GetComponent<SpriteRenderer>(), b.GetComponent<SpriteRenderer>() };
            }
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / _life);
            transform.localScale = Vector3.one * (1f + t * (1.1f + _scale * 0.4f));
            if (_ring != null) _ring.Rotate(0f, 0f, 220f * Time.unscaledDeltaTime);
            if (_renderers != null)
            {
                foreach (var renderer in _renderers)
                {
                    if (renderer == null) continue;
                    var c = renderer.color;
                    c.a *= Mathf.Clamp01(1f - t);
                    renderer.color = c;
                }
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }

    public sealed class RingPulseFx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Transform _ring;
        private float _age;
        private const float Life = 0.42f;

        public void Initialize(Color color, float scale)
        {
            var ring = VisualFactory.RingObject("Pulse", transform, Vector2.one * (0.52f * scale), new Color(color.r, color.g, color.b, 0.80f), Vector3.zero, 115);
            _ring = ring.transform;
            _renderer = ring.GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / Life);
            _ring.localScale = Vector3.one * (1f + t * 3.3f);
            var c = _renderer.color;
            c.a = (1f - t) * 0.8f;
            _renderer.color = c;
            if (_age >= Life) Destroy(gameObject);
        }
    }

    public sealed class AfterglowFx : MonoBehaviour
    {
        public SpriteRenderer Renderer;
        private float _age;
        private const float Life = 0.20f;

        private void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_age / Life);
            transform.localScale = Vector3.one * (1f + t * 0.65f);
            if (Renderer != null)
            {
                var c = Renderer.color;
                c.a = (1f - t) * 0.30f;
                Renderer.color = c;
            }
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
