using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Procedural 3D identity for the selected primary family. Presentation only; the actual
    /// firing remains in WeaponFamilyDirector/Projectile.
    /// </summary>
    public sealed class WeaponFamily3DPresentation : MonoBehaviour
    {
        private Transform _root;
        private Transform _animated;
        private PrimaryWeaponFamily _family;
        private Vector3 _baseScale;
        private float _phase;

        private void Start()
        {
            _family = WeaponFamilyDirector.Selected;
            Build();
        }

        private void Build()
        {
            var go = new GameObject("PrimaryWeaponFamily3D");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, -0.54f);
            _root = go.transform;

            Color color = WeaponFamilyDirector.FamilyColor(_family);
            Color dark = Color.Lerp(color, Color.black, 0.62f);
            Color steel = new Color(0.24f, 0.28f, 0.33f);

            switch (_family)
            {
                case PrimaryWeaponFamily.Autocannon:
                {
                    Runtime3DFactory.Box("VulcanMount", _root, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.22f, 0.16f), dark, 0.65f, 0.44f);
                    var cluster = new GameObject("RotaryCluster");
                    cluster.transform.SetParent(_root, false);
                    cluster.transform.localPosition = new Vector3(0f, 0.27f, 0f);
                    _animated = cluster.transform;
                    for (int i = -1; i <= 1; i++)
                        Runtime3DFactory.Box("VulcanBarrel", _animated, new Vector3(i * 0.085f, 0.20f, 0f), new Vector3(0.055f, 0.58f, 0.055f), steel, 0.80f, 0.42f);
                    Runtime3DFactory.Cylinder("VulcanHub", _animated, new Vector3(0f, -0.02f, 0f), 0.20f, 0.10f, color, 0.55f, 0.50f);
                    break;
                }
                case PrimaryWeaponFamily.HeavyCannon:
                {
                    Runtime3DFactory.Box("SiegeBreech", _root, new Vector3(0f, 0.05f, 0f), new Vector3(0.44f, 0.32f, 0.22f), dark, 0.72f, 0.38f);
                    _animated = Runtime3DFactory.Box("SiegeBarrel", _root, new Vector3(0f, 0.48f, 0f), new Vector3(0.18f, 0.92f, 0.18f), steel, 0.84f, 0.40f).transform;
                    Runtime3DFactory.Box("SiegeBrake", _animated, new Vector3(0f, 0.46f, 0f), new Vector3(0.32f, 0.15f, 0.24f), color, 0.65f, 0.46f);
                    Runtime3DFactory.Box("SiegeArmorL", _root, new Vector3(-0.30f, 0.08f, 0f), new Vector3(0.12f, 0.36f, 0.20f), dark, 0.72f, 0.34f);
                    Runtime3DFactory.Box("SiegeArmorR", _root, new Vector3(0.30f, 0.08f, 0f), new Vector3(0.12f, 0.36f, 0.20f), dark, 0.72f, 0.34f);
                    break;
                }
                case PrimaryWeaponFamily.Railgun:
                {
                    Runtime3DFactory.Box("RailSpine", _root, new Vector3(0f, 0.38f, 0f), new Vector3(0.10f, 1.10f, 0.10f), steel, 0.92f, 0.58f);
                    Runtime3DFactory.Box("RailL", _root, new Vector3(-0.12f, 0.40f, 0f), new Vector3(0.055f, 1.02f, 0.08f), color, 0.76f, 0.72f);
                    Runtime3DFactory.Box("RailR", _root, new Vector3(0.12f, 0.40f, 0f), new Vector3(0.055f, 1.02f, 0.08f), color, 0.76f, 0.72f);
                    _animated = Runtime3DFactory.Cylinder("RailCapacitor", _root, new Vector3(0f, -0.02f, -0.05f), 0.32f, 0.12f, color, 0.36f, 0.82f).transform;
                    break;
                }
                case PrimaryWeaponFamily.PlasmaRepeater:
                {
                    Runtime3DFactory.Box("ArcHousing", _root, new Vector3(0f, 0.18f, 0f), new Vector3(0.38f, 0.62f, 0.20f), dark, 0.55f, 0.48f);
                    Runtime3DFactory.Box("ArcEmitter", _root, new Vector3(0f, 0.55f, 0f), new Vector3(0.13f, 0.66f, 0.13f), steel, 0.75f, 0.55f);
                    _animated = Runtime3DFactory.Cylinder("ArcCore", _root, new Vector3(0f, 0.12f, -0.15f), 0.30f, 0.10f, color, 0.20f, 0.92f).transform;
                    Runtime3DFactory.Cylinder("ArcCoilL", _root, new Vector3(-0.20f, 0.22f, -0.02f), 0.12f, 0.16f, color, 0.30f, 0.82f);
                    Runtime3DFactory.Cylinder("ArcCoilR", _root, new Vector3(0.20f, 0.22f, -0.02f), 0.12f, 0.16f, color, 0.30f, 0.82f);
                    break;
                }
            }

            if (_animated != null) _baseScale = _animated.localScale;
        }

        private void LateUpdate()
        {
            if (_animated == null) return;
            _phase += Time.deltaTime;
            WeaponFamilyDirector director = WeaponFamilyDirector.Instance;
            float mastery = director != null ? director.MasteryLevel / 5f : 0f;

            if (_family == PrimaryWeaponFamily.Autocannon)
                _animated.localRotation = Quaternion.Euler(0f, 0f, -_phase * Mathf.Lerp(160f, 410f, mastery));
            else if (_family == PrimaryWeaponFamily.Railgun)
            {
                float pulse = 1f + Mathf.Sin(_phase * 5f) * (0.04f + mastery * 0.04f);
                _animated.localScale = _baseScale * pulse;
            }
            else if (_family == PrimaryWeaponFamily.PlasmaRepeater)
            {
                float pulse = 1f + Mathf.Sin(_phase * 8f) * 0.10f;
                _animated.localScale = _baseScale * pulse;
            }
            else
            {
                float breathe = 1f + Mathf.Sin(_phase * 2.4f) * 0.018f;
                _animated.localScale = _baseScale * breathe;
            }
        }
    }
}
