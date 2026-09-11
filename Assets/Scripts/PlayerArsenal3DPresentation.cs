using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Runtime 3D feedback for the v3.0 player arsenal. Purely visual: gameplay authority
    /// remains in PlayerArsenalSystem, PlayerTank and the existing projectile/health systems.
    /// </summary>
    public sealed class PlayerArsenal3DPresentation : MonoBehaviour
    {
        private Transform _root;
        private Transform _energyRing;
        private Transform _heatCore;
        private Transform[] _nodes;
        private PlayerArsenalSystem _arsenal;

        private void Start()
        {
            _arsenal = PlayerArsenalSystem.Instance;
            Build();
        }

        private void Build()
        {
            if (_root != null) return;
            var root = new GameObject("ArsenalReactor3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, -0.30f, -0.54f);
            _root = root.transform;

            Color chassis = _arsenal != null ? PlayerArsenalSystem.ChassisColor(_arsenal.Chassis) : new Color(0.25f, 0.82f, 1f);
            GameObject ring = Runtime3DFactory.Cylinder("EnergyRing", _root, Vector3.zero, 0.42f, 0.028f, chassis, 0.12f, 0.82f);
            _energyRing = ring.transform;
            GameObject heat = Runtime3DFactory.Cylinder("HeatCore", _root, new Vector3(0f, 0f, -0.04f), 0.18f, 0.05f, new Color(1f, 0.24f, 0.06f), 0.08f, 0.75f);
            _heatCore = heat.transform;

            _nodes = new Transform[6];
            for (int i = 0; i < _nodes.Length; i++)
            {
                float a = i * Mathf.PI * 2f / _nodes.Length;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.30f, Mathf.Sin(a) * 0.30f, -0.02f);
                GameObject node = Runtime3DFactory.Box("ChargeNode", _root, p, new Vector3(0.075f, 0.075f, 0.05f), chassis, 0.22f, 0.90f);
                _nodes[i] = node.transform;
            }
        }

        private void LateUpdate()
        {
            _arsenal = PlayerArsenalSystem.Instance;
            if (_arsenal == null || _root == null) return;

            float energy = Mathf.Clamp01(_arsenal.EnergyRatio);
            float heat = Mathf.Clamp01(_arsenal.HeatRatio);
            float pulse = _arsenal.OverdriveActive ? 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.14f : 1f;
            _energyRing.localScale = new Vector3(0.42f * Mathf.Lerp(0.70f, 1.08f, energy) * pulse, 0.42f * Mathf.Lerp(0.70f, 1.08f, energy) * pulse, 0.028f);
            _energyRing.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * (_arsenal.OverdriveActive ? 180f : 42f));

            float heatPulse = 0.72f + heat * 0.55f + Mathf.Sin(Time.unscaledTime * (5f + heat * 9f)) * heat * 0.10f;
            _heatCore.localScale = new Vector3(0.18f * heatPulse, 0.18f * heatPulse, 0.05f);

            int lit = Mathf.Clamp(Mathf.CeilToInt(energy * _nodes.Length), 0, _nodes.Length);
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] == null) continue;
                float s = i < lit ? (_arsenal.OverdriveActive ? 1.28f : 1f) : 0.45f;
                _nodes[i].localScale = new Vector3(0.075f, 0.075f, 0.05f) * s;
            }

            float lift = _arsenal.AbilityActive ? -0.06f : 0f;
            _root.localPosition = Vector3.Lerp(_root.localPosition, new Vector3(0f, -0.30f, -0.54f + lift), Time.deltaTime * 8f);
        }
    }
}
