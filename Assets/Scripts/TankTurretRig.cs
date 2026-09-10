using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Detaches the visual turret from the hull so it can aim independently while
    /// the chassis continues to use classic cardinal movement. Also provides a
    /// lightweight recoil impulse without requiring authored animation clips.
    /// </summary>
    public sealed class TankTurretRig : MonoBehaviour
    {
        private static readonly string[] TurretParts =
        {
            "TurretShadow", "TurretBase", "Turret", "Hatch", "HatchCore",
            "GunMantlet", "BarrelShadow", "Barrel", "BarrelHighlight",
            "MuzzleBrake", "TurretGlint"
        };

        private Transform _pivot;
        private readonly List<Transform> _parts = new List<Transform>();
        private Vector2 _aimDirection = Vector2.up;
        private float _recoil;
        private float _recoilVelocity;
        private bool _ready;

        public Vector2 AimDirection => _aimDirection;

        public void Initialize()
        {
            if (_ready) return;

            var pivotObject = new GameObject("TurretPivot");
            _pivot = pivotObject.transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = Vector3.zero;
            _pivot.localRotation = Quaternion.identity;

            for (int i = 0; i < TurretParts.Length; i++)
            {
                Transform part = transform.Find(TurretParts[i]);
                if (part == null) continue;
                _parts.Add(part);
                part.SetParent(_pivot, true);
            }

            _ready = true;
            SetAimDirection(Vector2.up);
        }

        public void SetAimDirection(Vector2 direction)
        {
            if (!_ready) Initialize();
            if (direction.sqrMagnitude < 0.0001f || _pivot == null) return;

            _aimDirection = direction.normalized;
            float worldAngle = Mathf.Atan2(_aimDirection.y, _aimDirection.x) * Mathf.Rad2Deg - 90f;
            _pivot.rotation = Quaternion.Euler(0f, 0f, worldAngle);
        }

        public void KickRecoil(float amount = 1f)
        {
            _recoilVelocity -= Mathf.Clamp(amount, 0.2f, 2.2f) * 1.75f;
        }

        private void LateUpdate()
        {
            if (!_ready || _pivot == null) return;

            // Keep the turret's world orientation independent of hull rotation.
            SetAimDirection(_aimDirection);

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _recoilVelocity += (-_recoil * 34f - _recoilVelocity * 9.5f) * dt;
            _recoil += _recoilVelocity * dt;
            _recoil = Mathf.Clamp(_recoil, -0.18f, 0.035f);

            // Pivot local Y points along the gun's local forward direction.
            _pivot.localPosition = new Vector3(0f, _recoil, 0f);
        }
    }
}
