using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Presentation-only vehicle motion layer. Observes authoritative transforms and Projectile
    /// instances, then animates the procedural 3D shell without touching Rigidbody2D, colliders,
    /// Health, AI, fire cadence or damage.
    /// </summary>
    public sealed class VehicleMotionWeaponAnimationDirector : MonoBehaviour
    {
        public const float MaxHullLeanDegrees = 4.5f;
        public const float MaxSuspensionTravel = 0.055f;
        public const float MaxBarrelRecoil = 0.22f;
        public const float ProjectileObservationInterval = 0.05f;
        public const int MaxTrackedProjectileIds = 192;
        public static bool ConfigurationValid => MaxHullLeanDegrees <= 5f && MaxSuspensionTravel <= 0.06f && MaxBarrelRecoil <= 0.24f;
        public static bool ShotObservationUsesProjectileAuthority => true;

        private readonly List<VehicleMotionRig> _rigs = new List<VehicleMotionRig>(24);
        private readonly HashSet<int> _seenProjectiles = new HashSet<int>();
        private float _nextRigRefresh;
        private float _nextProjectileScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<VehicleMotionWeaponAnimationDirector>() != null) return;
            var go = new GameObject("VehicleMotionWeaponAnimationDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<VehicleMotionWeaponAnimationDirector>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRigRefresh)
            {
                _nextRigRefresh = Time.unscaledTime + 0.40f;
                RefreshRigs();
            }

            if (Time.unscaledTime >= _nextProjectileScan)
            {
                _nextProjectileScan = Time.unscaledTime + ProjectileObservationInterval;
                ObserveAuthoritativeProjectiles();
            }
        }

        private void RefreshRigs()
        {
            for (int i = _rigs.Count - 1; i >= 0; i--)
                if (_rigs[i] == null) _rigs.RemoveAt(i);

            PlayerTank[] players = FindObjectsByType<PlayerTank>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++) EnsureRig(players[i].gameObject, Team.Player, EnemyKind.Basic);

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++) EnsureRig(enemies[i].gameObject, Team.Enemy, enemies[i].Kind);
        }

        private void EnsureRig(GameObject vehicle, Team team, EnemyKind kind)
        {
            VehicleMotionRig rig = vehicle.GetComponent<VehicleMotionRig>();
            if (rig == null) rig = vehicle.AddComponent<VehicleMotionRig>();
            rig.Configure(team, kind);
            if (!_rigs.Contains(rig)) _rigs.Add(rig);
        }

        private void ObserveAuthoritativeProjectiles()
        {
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                Projectile projectile = projectiles[i];
                if (projectile == null) continue;
                int id = projectile.GetInstanceID();
                if (!_seenProjectiles.Add(id)) continue;
                NotifyNearestRig(projectile.transform.position, projectile.OwnerTeam, projectile.Ammo);
            }

            if (_seenProjectiles.Count > MaxTrackedProjectileIds)
            {
                _seenProjectiles.Clear();
                for (int i = 0; i < projectiles.Length; i++)
                    if (projectiles[i] != null) _seenProjectiles.Add(projectiles[i].GetInstanceID());
            }
        }

        private void NotifyNearestRig(Vector3 shotPosition, Team team, AmmoType ammo)
        {
            VehicleMotionRig best = null;
            float bestSqr = 4.0f;
            for (int i = 0; i < _rigs.Count; i++)
            {
                VehicleMotionRig rig = _rigs[i];
                if (rig == null || rig.Team != team) continue;
                float sqr = (rig.transform.position - shotPosition).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = rig;
            }
            if (best != null) best.OnAuthoritativeShotObserved(ammo);
        }
    }

    public sealed class VehicleMotionRig : MonoBehaviour
    {
        public Team Team { get; private set; }
        public EnemyKind Kind { get; private set; }
        public float MotionWeight { get; private set; } = 1f;
        public float RecoilWeight { get; private set; } = 1f;

        private Transform _modelRoot;
        private Transform _turretRoot;
        private Transform _barrel;
        private readonly List<Transform> _wheels = new List<Transform>(8);
        private Vector3 _lastWorldPosition;
        private Vector3 _modelBaseLocalPosition;
        private Quaternion _modelBaseLocalRotation;
        private Vector3 _barrelBaseLocalPosition;
        private bool _bound;
        private float _travelPhase;
        private float _recoil;
        private float _muzzleFlash;
        private GameObject _muzzleFlashObject;

        public void Configure(Team team, EnemyKind kind)
        {
            Team = team;
            Kind = kind;
            ResolveClassWeights();
            if (!_bound) TryBindPresentation();
        }

        private void ResolveClassWeights()
        {
            switch (Kind)
            {
                case EnemyKind.Fast:
                    MotionWeight = 1.35f; RecoilWeight = 0.75f; break;
                case EnemyKind.Heavy:
                    MotionWeight = 0.70f; RecoilWeight = 1.25f; break;
                case EnemyKind.Siege:
                    MotionWeight = 0.58f; RecoilWeight = 1.45f; break;
                case EnemyKind.Boss:
                    MotionWeight = 0.48f; RecoilWeight = 1.60f; break;
                case EnemyKind.Sniper:
                    MotionWeight = 0.82f; RecoilWeight = 1.15f; break;
                default:
                    MotionWeight = 1f; RecoilWeight = 1f; break;
            }
        }

        private void TryBindPresentation()
        {
            _modelRoot = FindChildRecursive(transform, "Tank3D");
            if (_modelRoot == null) return;
            _turretRoot = FindChildRecursive(_modelRoot, "Turret3DRoot");
            _barrel = FindChildRecursive(_modelRoot, "Barrel3D");
            _wheels.Clear();
            CollectNamedChildren(_modelRoot, "RoadWheel", _wheels);
            _modelBaseLocalPosition = _modelRoot.localPosition;
            _modelBaseLocalRotation = _modelRoot.localRotation;
            if (_barrel != null) _barrelBaseLocalPosition = _barrel.localPosition;
            _lastWorldPosition = transform.position;
            BuildMuzzleFlash();
            _bound = true;
        }

        private void BuildMuzzleFlash()
        {
            if (_turretRoot == null || _muzzleFlashObject != null) return;
            _muzzleFlashObject = Runtime3DFactory.Cylinder(
                "WeaponMuzzleFlash3D",
                _turretRoot,
                new Vector3(0f, 0.92f, -0.11f),
                0.18f,
                0.04f,
                new Color(1f, 0.56f, 0.08f, 0.95f),
                0.10f,
                0.85f);
            if (_muzzleFlashObject != null) _muzzleFlashObject.SetActive(false);
        }

        public void OnAuthoritativeShotObserved(AmmoType ammo)
        {
            float ammoKick = ammo == AmmoType.Explosive || ammo == AmmoType.Plasma ? 1.18f : ammo == AmmoType.ArmorPiercing ? 1.08f : 1f;
            _recoil = Mathf.Min(VehicleMotionWeaponAnimationDirector.MaxBarrelRecoil, 0.105f * RecoilWeight * ammoKick);
            _muzzleFlash = ammo == AmmoType.Plasma ? 0.13f : 0.085f;
            if (_muzzleFlashObject != null)
            {
                _muzzleFlashObject.SetActive(true);
                float s = ammo == AmmoType.Explosive || ammo == AmmoType.Plasma ? 1.35f : 1f;
                _muzzleFlashObject.transform.localScale = new Vector3(s, s, s);
            }
        }

        private void LateUpdate()
        {
            if (!_bound)
            {
                TryBindPresentation();
                if (!_bound) return;
            }

            Vector3 world = transform.position;
            Vector3 delta = world - _lastWorldPosition;
            _lastWorldPosition = world;
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            float normalized = Mathf.Clamp01(speed / 5.5f);
            _travelPhase += delta.magnitude * (9f + MotionWeight * 2f);

            float bounce = Mathf.Sin(_travelPhase * 1.7f) * 0.018f * normalized * MotionWeight;
            bounce = Mathf.Clamp(bounce, -VehicleMotionWeaponAnimationDirector.MaxSuspensionTravel, VehicleMotionWeaponAnimationDirector.MaxSuspensionTravel);
            float lateral = Mathf.Clamp(delta.x * 28f, -1f, 1f);
            float lean = -lateral * VehicleMotionWeaponAnimationDirector.MaxHullLeanDegrees * normalized * Mathf.Min(MotionWeight, 1.15f);

            _modelRoot.localPosition = Vector3.Lerp(
                _modelRoot.localPosition,
                _modelBaseLocalPosition + new Vector3(0f, 0f, bounce),
                1f - Mathf.Exp(-Time.deltaTime * 14f));
            _modelRoot.localRotation = Quaternion.Slerp(
                _modelRoot.localRotation,
                _modelBaseLocalRotation * Quaternion.Euler(0f, 0f, lean),
                1f - Mathf.Exp(-Time.deltaTime * 10f));

            for (int i = 0; i < _wheels.Count; i++)
                if (_wheels[i] != null) _wheels[i].Rotate(0f, 0f, delta.magnitude * 900f * MotionWeight, Space.Self);

            _recoil = Mathf.MoveTowards(_recoil, 0f, Time.deltaTime * (0.90f + 0.55f / Mathf.Max(0.45f, RecoilWeight)));
            if (_barrel != null)
                _barrel.localPosition = Vector3.Lerp(_barrel.localPosition, _barrelBaseLocalPosition + new Vector3(0f, -_recoil, 0f), 1f - Mathf.Exp(-Time.deltaTime * 28f));

            if (_muzzleFlash > 0f)
            {
                _muzzleFlash -= Time.deltaTime;
                if (_muzzleFlashObject != null)
                {
                    float pulse = Mathf.Clamp01(_muzzleFlash / 0.085f);
                    _muzzleFlashObject.transform.localScale *= 0.94f + pulse * 0.06f;
                    if (_muzzleFlash <= 0f) _muzzleFlashObject.SetActive(false);
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform nested = FindChildRecursive(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void CollectNamedChildren(Transform root, string name, List<Transform> result)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) result.Add(child);
                CollectNamedChildren(child, name, result);
            }
        }
    }
}
