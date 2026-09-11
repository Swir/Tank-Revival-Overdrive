using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Bridges secondary gameplay objects into the v2.4 3D presentation so the first 3D
    /// milestone is visually coherent instead of mixing flat fortress/pickup sprites with tanks.
    /// </summary>
    public sealed class WorldObjects3DPresentation : MonoBehaviour
    {
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WorldObjects3DPresentation>() != null) return;
            var go = new GameObject("WorldObjects3DPresentation");
            DontDestroyOnLoad(go);
            go.AddComponent<WorldObjects3DPresentation>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.24f;

            Health[] healthObjects = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < healthObjects.Length; i++)
            {
                Health health = healthObjects[i];
                if (health == null || health.Team != Team.Player) continue;
                string n = health.gameObject.name;
                if (!IsFortressModule(n) || health.GetComponent<FortressModule3DPresentation>() != null) continue;
                var presentation = health.gameObject.AddComponent<FortressModule3DPresentation>();
                presentation.Initialize();
            }

            AmmoPickup[] ammo = FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None);
            for (int i = 0; i < ammo.Length; i++)
            {
                if (ammo[i] == null || ammo[i].GetComponent<Pickup3DPresentation>() != null) continue;
                var presentation = ammo[i].gameObject.AddComponent<Pickup3DPresentation>();
                presentation.InitializeAmmo(ammo[i].Kind);
            }

            PowerUp[] powers = FindObjectsByType<PowerUp>(FindObjectsSortMode.None);
            for (int i = 0; i < powers.Length; i++)
            {
                if (powers[i] == null || powers[i].GetComponent<Pickup3DPresentation>() != null) continue;
                var presentation = powers[i].gameObject.AddComponent<Pickup3DPresentation>();
                presentation.InitializePower(powers[i].Kind);
            }
        }

        private static bool IsFortressModule(string n)
        {
            return n.StartsWith("FORTRESS_") || n.StartsWith("EAGLE_SENTRY_") || n == "EAGLE_REPAIR_RELAY";
        }
    }

    public sealed class FortressModule3DPresentation : MonoBehaviour
    {
        private Health _health;
        private Transform _head;
        private GameObject _warning;
        private bool _ready;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;
            _health = GetComponent<Health>();
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (_health == null || collider == null) return;

            Runtime3DFactory.HideLegacySprites(transform);
            Vector2 size = collider.size;
            string n = gameObject.name;

            if (n.StartsWith("EAGLE_SENTRY_"))
                BuildSentry(size);
            else if (n == "EAGLE_REPAIR_RELAY")
                BuildRelay(size);
            else
                BuildArmor(size);

            _warning = Runtime3DFactory.Cylinder("ModuleWarning3D", transform, new Vector3(0f, -size.y * 0.27f, -0.46f), 0.10f, 0.04f, new Color(1f, 0.08f, 0.025f), 0.06f, 0.86f);
            _warning.SetActive(false);
        }

        private void BuildArmor(Vector2 size)
        {
            Color body = new Color(0.16f, 0.43f, 0.59f);
            Color edge = new Color(0.34f, 0.92f, 1f);
            Runtime3DFactory.Box("FortressArmor3D", transform, new Vector3(0f, 0f, -0.15f), new Vector3(size.x, size.y, 0.50f), body, 0.68f, 0.42f);
            Runtime3DFactory.Box("FortressStripe3D", transform, new Vector3(0f, size.y * 0.23f, -0.425f), new Vector3(size.x * 0.80f, 0.075f, 0.055f), edge, 0.56f, 0.64f);
            Runtime3DFactory.Box("FortressFoot3D", transform, new Vector3(0f, -size.y * 0.33f, -0.31f), new Vector3(size.x * 1.08f, 0.12f, 0.22f), new Color(0.08f, 0.17f, 0.22f), 0.78f, 0.30f);
        }

        private void BuildSentry(Vector2 size)
        {
            Color baseColor = new Color(0.08f, 0.28f, 0.35f);
            Color glow = new Color(0.28f, 1f, 0.70f);
            Runtime3DFactory.Box("SentryPedestal3D", transform, new Vector3(0f, 0f, -0.14f), new Vector3(size.x * 0.90f, size.y * 0.90f, 0.43f), baseColor, 0.65f, 0.40f);

            var headObject = new GameObject("SentryHead3DRoot");
            headObject.transform.SetParent(transform, false);
            headObject.transform.localPosition = new Vector3(0f, 0.02f, -0.43f);
            _head = headObject.transform;

            Runtime3DFactory.Cylinder("SentryHead3D", _head, Vector3.zero, 0.40f, 0.12f, new Color(0.20f, 0.66f, 0.73f), 0.62f, 0.55f);
            Runtime3DFactory.Box("SentryGun3D", _head, new Vector3(0f, 0.34f, -0.02f), new Vector3(0.10f, 0.55f, 0.10f), glow, 0.60f, 0.54f);
            Runtime3DFactory.Box("SentryMuzzle3D", _head, new Vector3(0f, 0.64f, -0.02f), new Vector3(0.17f, 0.10f, 0.13f), new Color(0.10f, 0.30f, 0.34f), 0.72f, 0.40f);
        }

        private void BuildRelay(Vector2 size)
        {
            Color body = new Color(0.09f, 0.34f, 0.19f);
            Color glow = new Color(0.36f, 1f, 0.52f);
            Runtime3DFactory.Box("RelayHousing3D", transform, new Vector3(0f, 0f, -0.13f), new Vector3(size.x, size.y, 0.45f), body, 0.42f, 0.46f);
            Runtime3DFactory.Cylinder("RelayCore3D", transform, new Vector3(0f, 0f, -0.43f), 0.23f, 0.08f, glow, 0.18f, 0.82f);
            Runtime3DFactory.Box("RelayAntennaL3D", transform, new Vector3(-size.x * 0.28f, 0.15f, -0.48f), new Vector3(0.055f, 0.36f, 0.055f), glow, 0.58f, 0.58f);
            Runtime3DFactory.Box("RelayAntennaR3D", transform, new Vector3(size.x * 0.28f, 0.15f, -0.48f), new Vector3(0.055f, 0.36f, 0.055f), glow, 0.58f, 0.58f);
        }

        private void LateUpdate()
        {
            if (!_ready || _health == null || _health.Maximum <= 0) return;
            float ratio = _health.Current / (float)_health.Maximum;
            if (_warning != null)
            {
                _warning.SetActive(ratio <= 0.45f);
                if (_warning.activeSelf)
                {
                    float p = 0.75f + Mathf.Sin(Time.unscaledTime * 9f) * 0.25f;
                    _warning.transform.localScale = new Vector3(0.10f * p, 0.020f, 0.10f * p);
                }
            }

            if (_head != null)
            {
                EnemyTank target = FindClosestEnemy();
                if (target != null)
                {
                    Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                    _head.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        private EnemyTank FindClosestEnemy()
        {
            EnemyTank[] enemies = CombatRoster.Enemies;
            if (enemies == null) return null;
            EnemyTank best = null;
            float bestDistance = 100f;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                float distance = ((Vector2)enemy.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }
    }

    public sealed class Pickup3DPresentation : MonoBehaviour
    {
        private Transform _visual;
        private bool _ready;

        public void InitializeAmmo(AmmoType kind)
        {
            if (_ready) return;
            _ready = true;
            Runtime3DFactory.HideLegacySprites(transform);
            Color c = AmmoDatabase.Color(kind);
            var root = new GameObject("AmmoPickup3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, -0.35f);
            _visual = root.transform;

            Runtime3DFactory.Box("AmmoCrate3D", _visual, Vector3.zero, new Vector3(0.62f, 0.54f, 0.34f), Color.Lerp(c, Color.black, 0.40f), 0.40f, 0.38f);
            Runtime3DFactory.Box("AmmoBand3D", _visual, new Vector3(0f, 0f, -0.19f), new Vector3(0.14f, 0.54f, 0.055f), c, 0.32f, 0.66f);
            Runtime3DFactory.Box("AmmoCap3D", _visual, new Vector3(0f, 0.17f, -0.19f), new Vector3(0.48f, 0.10f, 0.055f), Color.Lerp(c, Color.white, 0.30f), 0.38f, 0.70f);
        }

        public void InitializePower(PowerUpKind kind)
        {
            if (_ready) return;
            _ready = true;
            Runtime3DFactory.HideLegacySprites(transform);
            Color c = PowerColor(kind);
            var root = new GameObject("PowerUp3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, -0.38f);
            _visual = root.transform;

            Runtime3DFactory.Cylinder("PowerCore3D", _visual, Vector3.zero, 0.42f, 0.20f, c, 0.20f, 0.80f);
            Runtime3DFactory.Box("PowerCrossV3D", _visual, new Vector3(0f, 0f, -0.13f), new Vector3(0.11f, 0.46f, 0.065f), Color.white, 0.10f, 0.86f);
            Runtime3DFactory.Box("PowerCrossH3D", _visual, new Vector3(0f, 0f, -0.13f), new Vector3(0.46f, 0.11f, 0.065f), Color.white, 0.10f, 0.86f);
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            _visual.Rotate(0f, 0f, 52f * Time.unscaledDeltaTime);
            Vector3 p = _visual.localPosition;
            p.z = -0.38f - Mathf.Sin(Time.unscaledTime * 4.6f) * 0.06f;
            _visual.localPosition = p;
        }

        private static Color PowerColor(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Repair: return new Color(0.30f, 1f, 0.35f);
                case PowerUpKind.RapidFire: return new Color(1f, 0.75f, 0.12f);
                case PowerUpKind.PowerShot: return new Color(1f, 0.22f, 0.20f);
                case PowerUpKind.Speed: return new Color(0.25f, 0.90f, 1f);
                default: return new Color(0.72f, 0.42f, 1f);
            }
        }
    }
}
