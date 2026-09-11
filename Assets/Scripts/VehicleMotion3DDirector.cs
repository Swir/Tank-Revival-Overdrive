using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.5 3D COMBAT & VEHICLE MOTION.
    /// Adds a visual suspension layer on top of the authoritative Rigidbody2D simulation.
    /// No 3D colliders or rigidbodies are introduced: this component only animates the
    /// procedural meshes created by Tank3DPresentation.
    /// </summary>
    public sealed class VehicleMotion3DDirector : MonoBehaviour
    {
        private static readonly HashSet<TankMotion3DAnimator> Animators = new HashSet<TankMotion3DAnimator>();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<VehicleMotion3DDirector>() != null) return;
            var go = new GameObject("VehicleMotion3DDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<VehicleMotion3DDirector>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.22f;

            Tank3DPresentation[] presentations = FindObjectsByType<Tank3DPresentation>(FindObjectsSortMode.None);
            for (int i = 0; i < presentations.Length; i++)
            {
                Tank3DPresentation presentation = presentations[i];
                if (presentation == null || presentation.GetComponent<TankMotion3DAnimator>() != null) continue;
                var animator = presentation.gameObject.AddComponent<TankMotion3DAnimator>();
                animator.Initialize();
            }

            Animators.RemoveWhere(a => a == null);
        }

        internal static void Register(TankMotion3DAnimator animator)
        {
            if (animator != null) Animators.Add(animator);
        }

        internal static void Unregister(TankMotion3DAnimator animator)
        {
            if (animator != null) Animators.Remove(animator);
        }

        /// <summary>
        /// Resolves the firing vehicle from the projectile muzzle position and applies visual recoil.
        /// Muzzle positions are intentionally used so this remains compatible with player, enemy,
        /// boss, sentry and future weapon systems that all spawn normal Projectile instances.
        /// </summary>
        public static void NotifyShot(Vector3 muzzlePosition, Team team, float force)
        {
            TankMotion3DAnimator best = null;
            float bestDistance = 1.65f;

            foreach (TankMotion3DAnimator animator in Animators)
            {
                if (animator == null || animator.OwnerTeam != team) continue;
                float distance = Vector2.Distance(animator.transform.position, muzzlePosition);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = animator;
            }

            best?.KickRecoil(force);
        }
    }

    [DefaultExecutionOrder(12020)]
    public sealed class TankMotion3DAnimator : MonoBehaviour
    {
        private readonly List<Transform> _roadWheels = new List<Transform>(10);
        private readonly List<Quaternion> _wheelBaseRotations = new List<Quaternion>(10);
        private readonly List<Transform> _trackPads = new List<Transform>(20);
        private readonly List<float> _trackPadOffsets = new List<float>(20);

        private Transform _modelRoot;
        private Transform _barrel;
        private Transform _muzzleBrake;
        private Transform _mantlet;
        private Transform _trackLeft;
        private Transform _trackRight;
        private Vector3 _barrelBasePosition;
        private Vector3 _muzzleBasePosition;
        private Vector3 _mantletBasePosition;
        private Vector3 _lastWorldPosition;
        private Vector2 _smoothedLocalVelocity;
        private Vector2 _previousLocalVelocity;
        private Health _health;
        private Team _team;
        private float _travel;
        private float _dustTravel;
        private float _recoil;
        private float _damageJolt;
        private bool _ready;

        public Team OwnerTeam => _team;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;

            PlayerTank player = GetComponent<PlayerTank>();
            EnemyTank enemy = GetComponent<EnemyTank>();
            if (player == null && enemy == null)
            {
                enabled = false;
                return;
            }

            _team = player != null ? Team.Player : Team.Enemy;
            _health = GetComponent<Health>();
            _modelRoot = FindDescendant(transform, "Tank3D");
            if (_modelRoot == null)
            {
                enabled = false;
                return;
            }

            _barrel = FindDescendant(_modelRoot, "Barrel3D");
            _muzzleBrake = FindDescendant(_modelRoot, "MuzzleBrake3D");
            _mantlet = FindDescendant(_modelRoot, "GunMantlet3D");
            _trackLeft = FindDescendant(_modelRoot, "TrackL3D");
            _trackRight = FindDescendant(_modelRoot, "TrackR3D");

            if (_barrel != null) _barrelBasePosition = _barrel.localPosition;
            if (_muzzleBrake != null) _muzzleBasePosition = _muzzleBrake.localPosition;
            if (_mantlet != null) _mantletBasePosition = _mantlet.localPosition;

            Transform[] children = _modelRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child.name != "RoadWheel") continue;
                _roadWheels.Add(child);
                _wheelBaseRotations.Add(child.localRotation);
                AddWheelSpoke(child);
            }

            BuildTrackPads(_trackLeft, 0f);
            BuildTrackPads(_trackRight, 0.5f);

            _lastWorldPosition = transform.position;
            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Damaged += OnDamaged;
            }
            VehicleMotion3DDirector.Register(this);
        }

        public void KickRecoil(float force)
        {
            _recoil = Mathf.Max(_recoil, Mathf.Clamp(force, 0.65f, 2.25f));
        }

        private void OnDamaged(Health health, int amount)
        {
            if (amount <= 0) return;
            _damageJolt = Mathf.Min(1.8f, _damageJolt + 0.35f + amount * 0.14f);
        }

        private void LateUpdate()
        {
            if (!_ready || _modelRoot == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 current = transform.position;
            Vector3 delta3 = current - _lastWorldPosition;
            _lastWorldPosition = current;

            float distance = new Vector2(delta3.x, delta3.y).magnitude;
            if (distance > 2.2f)
            {
                // Respawns/teleports must not produce a giant suspension impulse or dust wall.
                distance = 0f;
                delta3 = Vector3.zero;
            }

            Vector2 velocity = new Vector2(delta3.x, delta3.y) / Mathf.Max(0.001f, dt);
            Vector3 local3 = Quaternion.Inverse(transform.rotation) * new Vector3(velocity.x, velocity.y, 0f);
            Vector2 localVelocity = new Vector2(local3.x, local3.y);
            float blend = 1f - Mathf.Exp(-9f * dt);
            _smoothedLocalVelocity = Vector2.Lerp(_smoothedLocalVelocity, localVelocity, blend);

            Vector2 acceleration = (_smoothedLocalVelocity - _previousLocalVelocity) / Mathf.Max(0.001f, dt);
            _previousLocalVelocity = _smoothedLocalVelocity;
            acceleration = Vector2.ClampMagnitude(acceleration, 28f);

            float speed = _smoothedLocalVelocity.magnitude;
            _travel += distance;
            _dustTravel += distance;

            float engineBob = speed > 0.15f ? Mathf.Sin(_travel * 18f) * Mathf.Min(0.030f, 0.008f + speed * 0.0025f) : 0f;
            float damageRatio = _health != null && _health.Maximum > 0 ? 1f - _health.Current / (float)_health.Maximum : 0f;
            float damageShake = damageRatio > 0.50f ? Mathf.Sin(Time.unscaledTime * 13.5f) * damageRatio * 0.012f : 0f;

            float pitch = Mathf.Clamp(-_smoothedLocalVelocity.y * 0.55f - acceleration.y * 0.10f, -4.8f, 4.8f);
            float roll = Mathf.Clamp(_smoothedLocalVelocity.x * 0.52f + acceleration.x * 0.11f, -4.2f, 4.2f);
            float impactRoll = Mathf.Sin(Time.unscaledTime * 25f) * _damageJolt * 1.75f;

            _modelRoot.localPosition = new Vector3(0f, 0f, engineBob + damageShake);
            _modelRoot.localRotation = Quaternion.Euler(pitch, roll, impactRoll);

            AnimateWheels(speed);
            AnimateTrackPads(speed);
            AnimateGunRecoil(dt);

            _damageJolt = Mathf.MoveTowards(_damageJolt, 0f, dt * 3.6f);

            if (_dustTravel >= 0.42f && speed >= 1.2f)
            {
                _dustTravel = 0f;
                Vector3 rear = transform.position - transform.up * 0.34f;
                TrackDust3D.Spawn(rear, Mathf.Clamp01(speed / 7.5f));
            }
        }

        private void AnimateWheels(float speed)
        {
            if (_roadWheels.Count == 0 || speed < 0.02f) return;
            float spin = _travel * 680f;
            for (int i = 0; i < _roadWheels.Count; i++)
            {
                Transform wheel = _roadWheels[i];
                if (wheel == null) continue;
                wheel.localRotation = _wheelBaseRotations[i] * Quaternion.AngleAxis(spin, Vector3.forward);
            }
        }

        private void AnimateTrackPads(float speed)
        {
            if (_trackPads.Count == 0) return;
            float phase = _travel * 1.85f;
            for (int i = 0; i < _trackPads.Count; i++)
            {
                Transform pad = _trackPads[i];
                if (pad == null) continue;
                float p = Mathf.Repeat(_trackPadOffsets[i] + phase, 1f);
                float y = Mathf.Lerp(-0.43f, 0.43f, p);
                pad.localPosition = new Vector3(0f, y, -0.57f);
                float pulse = speed > 0.2f ? 0.92f + Mathf.Sin((phase + p) * Mathf.PI * 2f) * 0.08f : 1f;
                pad.localScale = new Vector3(0.86f, 0.065f * pulse, 0.10f);
            }
        }

        private void AnimateGunRecoil(float dt)
        {
            float displacement = Mathf.Clamp01(_recoil) * 0.115f;
            if (_barrel != null) _barrel.localPosition = _barrelBasePosition + Vector3.down * displacement;
            if (_muzzleBrake != null) _muzzleBrake.localPosition = _muzzleBasePosition + Vector3.down * displacement;
            if (_mantlet != null) _mantlet.localPosition = _mantletBasePosition + Vector3.down * displacement * 0.22f;
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 7.8f);
        }

        private void BuildTrackPads(Transform track, float offset)
        {
            if (track == null || track.Find("TrackPads3D") != null) return;
            var root = new GameObject("TrackPads3D");
            root.transform.SetParent(track, false);

            Color padColor = new Color(0.18f, 0.20f, 0.22f);
            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                float p = i / (float)count;
                GameObject pad = Runtime3DFactory.Box("MovingTrackPad3D", root.transform,
                    new Vector3(0f, Mathf.Lerp(-0.43f, 0.43f, p), -0.57f),
                    new Vector3(0.86f, 0.065f, 0.10f), padColor, 0.55f, 0.24f);
                _trackPads.Add(pad.transform);
                _trackPadOffsets.Add(Mathf.Repeat(p + offset, 1f));
            }
        }

        private static void AddWheelSpoke(Transform wheel)
        {
            if (wheel == null || wheel.Find("WheelSpoke3D") != null) return;
            Runtime3DFactory.Box("WheelSpoke3D", wheel, new Vector3(0.20f, 0f, -0.57f),
                new Vector3(0.52f, 0.10f, 0.08f), new Color(0.72f, 0.76f, 0.78f), 0.72f, 0.42f);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            VehicleMotion3DDirector.Unregister(this);
        }
    }

    public sealed class TrackDust3D : MonoBehaviour
    {
        private float _born;
        private float _life;
        private Vector3 _baseScale;
        private Vector3 _drift;

        public static void Spawn(Vector3 position, float intensity)
        {
            var root = new GameObject("TrackDust3D");
            root.transform.position = new Vector3(position.x, position.y, -0.15f);
            float size = Mathf.Lerp(0.16f, 0.30f, intensity);
            Color dust = Color.Lerp(new Color(0.22f, 0.20f, 0.18f), new Color(0.36f, 0.32f, 0.27f), intensity);
            Runtime3DFactory.Cylinder("DustPuffA", root.transform, new Vector3(-0.09f, 0f, -0.12f), size, 0.045f, dust, 0.01f, 0.08f);
            Runtime3DFactory.Cylinder("DustPuffB", root.transform, new Vector3(0.10f, -0.03f, -0.09f), size * 0.72f, 0.035f, Color.Lerp(dust, Color.white, 0.08f), 0.01f, 0.06f);
            var fx = root.AddComponent<TrackDust3D>();
            fx._born = Time.unscaledTime;
            fx._life = 0.52f;
            fx._baseScale = Vector3.one;
            fx._drift = new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(-0.12f, 0.04f), -0.10f);
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / Mathf.Max(0.01f, _life));
            transform.position += _drift * Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(0.65f, 1.75f, t) * (1f - t * 0.42f);
            transform.localScale = _baseScale * scale;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
