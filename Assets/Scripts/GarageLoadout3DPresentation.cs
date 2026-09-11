using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Physical presentation for v3.1 persistent garage packages. The models are cosmetic only;
    /// authoritative gameplay remains in GarageLoadoutDirector and the existing 2D combat pipeline.
    /// </summary>
    public sealed class GarageLoadout3DPresentation : MonoBehaviour
    {
        private Transform _root;
        private Transform _reactorRing;
        private Transform _leftPod;
        private Transform _rightPod;
        private bool _built;

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            var root = new GameObject("GarageLoadout3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _root = root.transform;

            GaragePrimaryPackage primary = GarageLoadoutDirector.SelectedPrimary;
            GarageReactorPackage reactor = GarageLoadoutDirector.SelectedReactor;
            GarageHullPackage hull = GarageLoadoutDirector.SelectedHull;

            BuildPrimary(primary);
            BuildReactor(reactor);
            BuildHull(hull);
        }

        private void BuildPrimary(GaragePrimaryPackage package)
        {
            if (package == GaragePrimaryPackage.Standard) return;
            Color c = package == GaragePrimaryPackage.VolleyFeed
                ? new Color(0.25f, 0.82f, 1f)
                : new Color(1f, 0.58f, 0.12f);

            GameObject left = Runtime3DFactory.Box("LoadoutPrimaryL", _root, new Vector3(-0.46f, 0.16f, -0.40f), new Vector3(0.16f, 0.42f, 0.14f), c, 0.62f, 0.55f);
            GameObject right = Runtime3DFactory.Box("LoadoutPrimaryR", _root, new Vector3(0.46f, 0.16f, -0.40f), new Vector3(0.16f, 0.42f, 0.14f), c, 0.62f, 0.55f);
            _leftPod = left.transform;
            _rightPod = right.transform;

            if (package == GaragePrimaryPackage.BreachCore)
            {
                Runtime3DFactory.Cylinder("BreachCapL", left.transform, new Vector3(0f, 0.18f, -0.10f), 0.13f, 0.08f, Color.Lerp(c, Color.white, 0.25f), 0.75f, 0.70f);
                Runtime3DFactory.Cylinder("BreachCapR", right.transform, new Vector3(0f, 0.18f, -0.10f), 0.13f, 0.08f, Color.Lerp(c, Color.white, 0.25f), 0.75f, 0.70f);
            }
        }

        private void BuildReactor(GarageReactorPackage package)
        {
            if (package == GarageReactorPackage.Standard) return;
            Color c = package == GarageReactorPackage.CapacitorBank
                ? new Color(0.20f, 0.72f, 1f)
                : new Color(0.20f, 1f, 0.72f);

            GameObject ring = Runtime3DFactory.Cylinder("LoadoutReactorRing", _root, new Vector3(0f, -0.23f, -0.56f), 0.33f, 0.07f, c, 0.42f, 0.82f);
            _reactorRing = ring.transform;
            Runtime3DFactory.Cylinder("LoadoutReactorCore", _root, new Vector3(0f, -0.23f, -0.605f), 0.17f, 0.09f, Color.Lerp(c, Color.white, 0.32f), 0.20f, 0.92f);

            if (package == GarageReactorPackage.ThermalSink)
            {
                for (int i = -2; i <= 2; i++)
                    Runtime3DFactory.Box("ThermalFin", _root, new Vector3(i * 0.11f, -0.36f, -0.52f), new Vector3(0.055f, 0.24f, 0.11f), c, 0.55f, 0.45f);
            }
        }

        private void BuildHull(GarageHullPackage package)
        {
            if (package == GarageHullPackage.Standard) return;
            Color c = package == GarageHullPackage.ReactivePlating
                ? new Color(0.95f, 0.50f, 0.12f)
                : new Color(0.20f, 0.92f, 0.56f);

            Runtime3DFactory.Box("LoadoutHullL", _root, new Vector3(-0.52f, -0.10f, -0.27f), new Vector3(0.10f, 0.62f, 0.24f), c, 0.72f, 0.40f);
            Runtime3DFactory.Box("LoadoutHullR", _root, new Vector3(0.52f, -0.10f, -0.27f), new Vector3(0.10f, 0.62f, 0.24f), c, 0.72f, 0.40f);

            if (package == GarageHullPackage.RepairLattice)
            {
                Runtime3DFactory.Box("RepairSpine", _root, new Vector3(0f, -0.34f, -0.48f), new Vector3(0.42f, 0.10f, 0.08f), c, 0.36f, 0.70f);
                Runtime3DFactory.Cylinder("RepairNodeL", _root, new Vector3(-0.19f, -0.34f, -0.54f), 0.09f, 0.05f, Color.white, 0.20f, 0.88f);
                Runtime3DFactory.Cylinder("RepairNodeR", _root, new Vector3(0.19f, -0.34f, -0.54f), 0.09f, 0.05f, Color.white, 0.20f, 0.88f);
            }
        }

        private void LateUpdate()
        {
            if (!_built) Build();
            PlayerArsenalSystem arsenal = PlayerArsenalSystem.Instance;
            float energy = arsenal != null ? arsenal.EnergyRatio : 0f;
            float heat = arsenal != null ? arsenal.HeatRatio : 0f;

            if (_reactorRing != null)
            {
                _reactorRing.Rotate(0f, 0f, (55f + energy * 135f) * Time.deltaTime, Space.Self);
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * (3f + heat * 8f)) * (0.03f + heat * 0.06f);
                _reactorRing.localScale = new Vector3(0.33f * pulse, 0.33f * pulse, 0.07f);
            }

            if (_leftPod != null && _rightPod != null)
            {
                float kick = arsenal != null && arsenal.OverdriveActive ? Mathf.Sin(Time.unscaledTime * 14f) * 0.025f : 0f;
                _leftPod.localPosition = new Vector3(-0.46f, 0.16f + kick, -0.40f);
                _rightPod.localPosition = new Vector3(0.46f, 0.16f - kick, -0.40f);
            }
        }
    }
}
