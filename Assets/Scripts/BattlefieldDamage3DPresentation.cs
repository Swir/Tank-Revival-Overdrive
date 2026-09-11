using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Converts persistent BattlefieldDestruction scars into real mesh-based depressions/heat marks.
    /// The original scar object remains the lifetime/budget owner, so existing cleanup rules still apply.
    /// </summary>
    public sealed class ImpactScar3DPresentation : MonoBehaviour
    {
        private Transform _heat;
        private Vector3 _heatBaseScale;
        private float _born;
        private bool _ready;

        public void Initialize(float size, Color tint)
        {
            if (_ready) return;
            _ready = true;
            _born = Time.unscaledTime;
            Runtime3DFactory.HideLegacySprites(transform);

            float clamped = Mathf.Clamp(size, 0.30f, 1.45f);
            Color outer = new Color(0.020f, 0.016f, 0.012f);
            Color inner = new Color(0.075f, 0.038f, 0.018f);
            Color hot = Color.Lerp(tint, new Color(1f, 0.24f, 0.025f), 0.35f);

            Runtime3DFactory.Cylinder("CraterOuter3D", transform, new Vector3(0f, 0f, 0.272f),
                clamped, 0.030f, outer, 0.01f, 0.06f);
            Runtime3DFactory.Cylinder("CraterInner3D", transform, new Vector3(0f, 0f, 0.255f),
                clamped * 0.63f, 0.022f, inner, 0.01f, 0.08f);
            GameObject heat = Runtime3DFactory.Cylinder("CraterHeat3D", transform, new Vector3(0f, 0f, 0.238f),
                clamped * 0.35f, 0.016f, hot, 0.01f, 0.76f);
            _heat = heat.transform;
            _heatBaseScale = _heat.localScale;

            // Small raised rim chunks add readable depth in the oblique camera without gameplay collision.
            int rimPieces = clamped >= 0.75f ? 7 : 5;
            for (int i = 0; i < rimPieces; i++)
            {
                float angle = i * Mathf.PI * 2f / rimPieces + Random.Range(-0.18f, 0.18f);
                float radius = clamped * Random.Range(0.40f, 0.48f);
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                GameObject bit = Runtime3DFactory.Box("CraterRim3D", transform, new Vector3(x, y, 0.19f),
                    new Vector3(clamped * Random.Range(0.08f, 0.14f), clamped * Random.Range(0.05f, 0.10f), Random.Range(0.035f, 0.075f)),
                    Color.Lerp(outer, new Color(0.19f, 0.13f, 0.08f), 0.42f), 0.08f, 0.10f);
                bit.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-6f, 6f), angle * Mathf.Rad2Deg);
            }
        }

        private void Update()
        {
            if (_heat == null) return;
            float age = Time.unscaledTime - _born;
            float cool = Mathf.Clamp01(1f - age / 7.5f);
            float pulse = 0.82f + Mathf.Sin(Time.unscaledTime * 7f) * 0.18f * cool;
            _heat.localScale = _heatBaseScale * Mathf.Lerp(0.28f, pulse, cool);
            if (age > 8f)
                _heat.gameObject.SetActive(false);
        }
    }
}
