using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Procedural 3D shell for the existing tank simulation. It deliberately does not add
    /// Rigidbody/Collider components, so all proven 2D combat, armor and AI logic stays authoritative.
    /// </summary>
    public sealed class Tank3DPresentation : MonoBehaviour
    {
        private Transform _modelRoot;
        private Transform _turretRoot;
        private TankTurretRig _aimRig;
        private Health _health;
        private GameObject _damageBeacon;
        private Vector3 _lastPosition;
        private bool _ready;
        private float _trackPhase;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;

            PlayerTank player = GetComponent<PlayerTank>();
            EnemyTank enemy = GetComponent<EnemyTank>();
            if (player == null && enemy == null) return;

            _aimRig = GetComponent<TankTurretRig>();
            _health = GetComponent<Health>();
            _lastPosition = transform.position;

            Runtime3DFactory.HideLegacySprites(transform);

            Color body;
            Color accent;
            float width = 1f;
            float length = 1f;
            float turretScale = 1f;

            if (player != null)
            {
                int chassis = Mathf.Clamp(PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0), 0, 3);
                switch (chassis)
                {
                    case 1:
                        body = new Color(0.12f, 0.38f, 0.54f);
                        accent = new Color(0.55f, 0.91f, 1f);
                        width = 1.12f;
                        length = 1.10f;
                        turretScale = 1.10f;
                        break;
                    case 2:
                        body = new Color(0.08f, 0.55f, 0.52f);
                        accent = new Color(0.55f, 1f, 0.90f);
                        width = 0.90f;
                        length = 1.08f;
                        turretScale = 0.90f;
                        break;
                    case 3:
                        body = new Color(0.16f, 0.42f, 0.72f);
                        accent = new Color(0.76f, 0.90f, 1f);
                        width = 1.02f;
                        length = 1.18f;
                        turretScale = 0.88f;
                        break;
                    default:
                        body = new Color(0.10f, 0.58f, 0.86f);
                        accent = new Color(0.78f, 0.96f, 1f);
                        break;
                }
            }
            else
            {
                ResolveEnemyPalette(enemy.Kind, out body, out accent, out width, out length, out turretScale);
            }

            BuildModel(body, accent, width, length, turretScale, enemy != null ? enemy.Kind : EnemyKind.Basic, player != null);
        }

        private void BuildModel(Color body, Color accent, float width, float length, float turretScale, EnemyKind kind, bool isPlayer)
        {
            Color dark = Color.Lerp(body, Color.black, 0.58f);
            Color track = new Color(0.035f, 0.042f, 0.050f, 1f);
            Color steel = new Color(0.28f, 0.31f, 0.34f, 1f);

            var root = new GameObject("Tank3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            _modelRoot = root.transform;

            Runtime3DFactory.Box("TrackL3D", _modelRoot, new Vector3(-0.39f * width, 0f, -0.05f), new Vector3(0.24f * width, 0.98f * length, 0.26f), track, 0.20f, 0.22f);
            Runtime3DFactory.Box("TrackR3D", _modelRoot, new Vector3(0.39f * width, 0f, -0.05f), new Vector3(0.24f * width, 0.98f * length, 0.26f), track, 0.20f, 0.22f);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = -0.32f * length + i * 0.215f * length;
                    Runtime3DFactory.Cylinder("RoadWheel", _modelRoot, new Vector3(side * 0.39f * width, y, -0.205f), 0.16f, 0.055f, steel, 0.55f, 0.48f);
                }
            }

            Runtime3DFactory.Box("LowerHull3D", _modelRoot, new Vector3(0f, -0.02f, -0.13f), new Vector3(0.68f * width, 0.79f * length, 0.31f), dark, 0.52f, 0.34f);
            Runtime3DFactory.Box("UpperHull3D", _modelRoot, new Vector3(0f, 0.045f, -0.315f), new Vector3(0.57f * width, 0.66f * length, 0.16f), body, 0.42f, 0.46f);
            Runtime3DFactory.Box("Glacis3D", _modelRoot, new Vector3(0f, 0.31f * length, -0.34f), new Vector3(0.51f * width, 0.18f * length, 0.13f), Color.Lerp(body, accent, 0.24f), 0.45f, 0.52f);
            Runtime3DFactory.Box("EngineDeck3D", _modelRoot, new Vector3(0f, -0.27f * length, -0.405f), new Vector3(0.42f * width, 0.15f * length, 0.07f), new Color(0.12f, 0.14f, 0.16f), 0.65f, 0.30f);

            for (int i = -1; i <= 1; i++)
                Runtime3DFactory.Box("EngineVent3D", _modelRoot, new Vector3(i * 0.105f * width, -0.27f * length, -0.455f), new Vector3(0.055f, 0.11f, 0.025f), steel, 0.65f, 0.25f);

            var turret = new GameObject("Turret3DRoot");
            turret.transform.SetParent(_modelRoot, false);
            turret.transform.localPosition = new Vector3(0f, 0.02f, -0.48f);
            _turretRoot = turret.transform;

            Runtime3DFactory.Cylinder("TurretRing3D", _turretRoot, Vector3.zero, 0.51f * turretScale, 0.14f, dark, 0.55f, 0.42f);
            Runtime3DFactory.Cylinder("Turret3D", _turretRoot, new Vector3(0f, 0.025f, -0.11f), 0.42f * turretScale, 0.18f, accent, 0.44f, 0.50f);
            Runtime3DFactory.Cylinder("Hatch3D", _turretRoot, new Vector3(-0.10f * turretScale, -0.035f, -0.225f), 0.17f * turretScale, 0.06f, Color.Lerp(accent, Color.white, 0.15f), 0.60f, 0.52f);

            float barrelLength = kind == EnemyKind.Sniper || kind == EnemyKind.Siege || kind == EnemyKind.Boss ? 0.78f : isPlayer && PlayerPrefs.GetInt("TankRevival.Garage.Chassis", 0) == 3 ? 0.82f : 0.63f;
            Runtime3DFactory.Box("GunMantlet3D", _turretRoot, new Vector3(0f, 0.205f, -0.10f), new Vector3(0.29f * turretScale, 0.16f, 0.17f), dark, 0.58f, 0.38f);
            Runtime3DFactory.Box("Barrel3D", _turretRoot, new Vector3(0f, 0.20f + barrelLength * 0.5f, -0.11f), new Vector3(kind == EnemyKind.Siege ? 0.16f : 0.12f, barrelLength, 0.12f), Color.Lerp(accent, Color.black, 0.20f), 0.68f, 0.38f);
            Runtime3DFactory.Box("MuzzleBrake3D", _turretRoot, new Vector3(0f, 0.21f + barrelLength, -0.11f), new Vector3(0.22f, 0.13f, 0.16f), dark, 0.70f, 0.34f);

            if (kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Boss)
            {
                Runtime3DFactory.Box("SideArmorL3D", _modelRoot, new Vector3(-0.48f * width, 0f, -0.19f), new Vector3(0.10f, 0.70f * length, 0.25f), dark, 0.68f, 0.30f);
                Runtime3DFactory.Box("SideArmorR3D", _modelRoot, new Vector3(0.48f * width, 0f, -0.19f), new Vector3(0.10f, 0.70f * length, 0.25f), dark, 0.68f, 0.30f);
            }

            if (kind == EnemyKind.Boss)
            {
                Runtime3DFactory.Box("BossCrown3D", _turretRoot, new Vector3(0f, -0.08f, -0.27f), new Vector3(0.34f, 0.13f, 0.09f), new Color(0.92f, 0.62f, 0.08f), 0.72f, 0.60f);
            }

            _damageBeacon = Runtime3DFactory.Cylinder("DamageBeacon3D", _modelRoot, new Vector3(0.22f * width, -0.25f * length, -0.50f), 0.10f, 0.05f, new Color(1f, 0.10f, 0.035f), 0.05f, 0.80f);
            _damageBeacon.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_ready || _modelRoot == null) return;

            if (_aimRig == null) _aimRig = GetComponent<TankTurretRig>();
            if (_aimRig != null && _turretRoot != null)
            {
                Vector2 aim = _aimRig.AimDirection;
                if (aim.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f;
                    _turretRoot.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            float moved = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;
            _trackPhase += moved * 7.5f;
            float suspension = moved > 0.0005f ? Mathf.Sin(_trackPhase) * 0.012f : Mathf.MoveTowards(_modelRoot.localPosition.z, 0f, Time.deltaTime * 0.10f);
            _modelRoot.localPosition = new Vector3(0f, 0f, suspension);

            if (_health != null && _damageBeacon != null)
            {
                float ratio = _health.Maximum > 0 ? _health.Current / (float)_health.Maximum : 1f;
                _damageBeacon.SetActive(ratio <= 0.50f);
                if (_damageBeacon.activeSelf)
                {
                    float pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 8f) * 0.22f;
                    _damageBeacon.transform.localScale = new Vector3(0.10f * pulse, 0.025f, 0.10f * pulse);
                }
            }
        }

        private static void ResolveEnemyPalette(EnemyKind kind, out Color body, out Color accent, out float width, out float length, out float turret)
        {
            width = 1f;
            length = 1f;
            turret = 1f;
            switch (kind)
            {
                case EnemyKind.Fast:
                    body = new Color(0.88f, 0.52f, 0.08f); accent = new Color(1f, 0.94f, 0.48f); width = 0.88f; length = 1.08f; break;
                case EnemyKind.Heavy:
                    body = new Color(0.38f, 0.12f, 0.54f); accent = new Color(0.88f, 0.50f, 1f); width = 1.12f; length = 1.08f; turret = 1.12f; break;
                case EnemyKind.Sniper:
                    body = new Color(0.08f, 0.48f, 0.28f); accent = new Color(0.58f, 1f, 0.74f); width = 0.92f; length = 1.08f; turret = 0.90f; break;
                case EnemyKind.Siege:
                    body = new Color(0.22f, 0.25f, 0.31f); accent = new Color(1f, 0.36f, 0.12f); width = 1.16f; length = 1.16f; turret = 1.12f; break;
                case EnemyKind.Elite:
                    body = new Color(0.12f, 0.22f, 0.62f); accent = new Color(0.30f, 0.90f, 1f); width = 1.02f; length = 1.08f; break;
                case EnemyKind.Supply:
                    body = new Color(0.32f, 0.36f, 0.20f); accent = new Color(0.80f, 0.96f, 0.32f); width = 1.02f; length = 1.04f; break;
                case EnemyKind.Boss:
                    body = new Color(0.58f, 0.035f, 0.055f); accent = new Color(1f, 0.70f, 0.08f); width = 1.18f; length = 1.18f; turret = 1.18f; break;
                default:
                    body = new Color(0.72f, 0.18f, 0.10f); accent = new Color(1f, 0.56f, 0.20f); break;
            }
        }
    }

    public sealed class Projectile3DPresentation : MonoBehaviour
    {
        private bool _ready;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;
            Projectile projectile = GetComponent<Projectile>();
            if (projectile == null) return;

            Runtime3DFactory.HideLegacySprites(transform);
            Color color = projectile.Ammo == AmmoType.Basic
                ? (projectile.OwnerTeam == Team.Player ? new Color(0.55f, 0.94f, 1f) : new Color(1f, 0.28f, 0.12f))
                : AmmoDatabase.Color(projectile.Ammo);

            float scale = projectile.Ammo == AmmoType.Plasma ? 1.55f : projectile.Ammo == AmmoType.Explosive ? 1.25f : 1f;
            Runtime3DFactory.Box("Shell3D", transform, new Vector3(0f, 0f, -0.40f), new Vector3(0.105f * scale, 0.26f * scale, 0.10f * scale), color, 0.38f, 0.68f);
            Runtime3DFactory.Cylinder("ShellHalo3D", transform, new Vector3(0f, -0.03f, -0.43f), 0.16f * scale, 0.035f, Color.Lerp(color, Color.white, 0.25f), 0.08f, 0.82f);
        }
    }
}
