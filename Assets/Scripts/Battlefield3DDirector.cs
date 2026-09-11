using System;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.4 3D OVERDRIVE FOUNDATION.
    /// Converts the presentation to a perspective 3D battlefield while preserving the proven
    /// XY Rigidbody2D simulation. This makes the migration reversible and keeps all campaign,
    /// armor, AI, siege and Orzelek rules functional during the transition.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class Battlefield3DDirector : MonoBehaviour
    {
        private static readonly Vector3 CameraPosition = new Vector3(0f, -4.85f, -19.25f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 0.35f, 0f);

        private TankGame _game;
        private Camera _camera;
        private Transform _currentWorld;
        private float _nextRefresh;
        private Light _keyLight;
        private GUIStyle _badgeStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<Battlefield3DDirector>() != null) return;
            var go = new GameObject("Battlefield3DDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<Battlefield3DDirector>();
        }

        private void Awake()
        {
            RenderSettings.ambientLight = new Color(0.23f, 0.27f, 0.33f);
            EnsureLighting();
        }

        public static bool TryProjectToGameplayPlane(Camera camera, Vector3 screenPoint, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (camera == null) return false;
            Ray ray = camera.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return false;
            worldPoint = ray.GetPoint(distance);
            worldPoint.z = 0f;
            return true;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.14f;
            RefreshPresentations();
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // TankGame applies its proven XY camera shake first. Preserve that displacement,
            // then map the result onto the new oblique perspective camera.
            Vector3 simulationCamera = _camera.transform.position;
            Vector2 shake = new Vector2(simulationCamera.x, simulationCamera.y);

            _camera.orthographic = false;
            _camera.fieldOfView = 43.5f;
            _camera.nearClipPlane = 0.10f;
            _camera.farClipPlane = 80f;
            _camera.transform.position = CameraPosition + new Vector3(shake.x, shake.y, 0f);
            _camera.transform.LookAt(CameraTarget + new Vector3(shake.x * 0.20f, shake.y * 0.20f, 0f), Vector3.up);
        }

        private void EnsureLighting()
        {
            if (_keyLight != null) return;
            var lightGo = new GameObject("3D_KeyLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(18f, -24f, 0f);
            _keyLight = lightGo.AddComponent<Light>();
            _keyLight.type = LightType.Directional;
            _keyLight.intensity = 1.18f;
            _keyLight.color = new Color(0.92f, 0.95f, 1f);
            _keyLight.shadows = LightShadows.Soft;
        }

        private void RefreshPresentations()
        {
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player != null)
            {
                EnsureTankPresentation(player.gameObject);
                TrackWorld(player.transform.parent);
            }

            EnemyTank[] enemies = FindObjectsByType<EnemyTank>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) EnsureTankPresentation(enemies[i].gameObject);

            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                Projectile projectile = projectiles[i];
                if (projectile == null || projectile.GetComponent<Projectile3DPresentation>() != null) continue;
                var presentation = projectile.gameObject.AddComponent<Projectile3DPresentation>();
                presentation.Initialize();
            }

            Obstacle[] obstacles = FindObjectsByType<Obstacle>(FindObjectsSortMode.None);
            for (int i = 0; i < obstacles.Length; i++)
            {
                Obstacle obstacle = obstacles[i];
                if (obstacle == null || obstacle.GetComponent<Obstacle3DPresentation>() != null) continue;
                var presentation = obstacle.gameObject.AddComponent<Obstacle3DPresentation>();
                presentation.Initialize();
            }

            GameObject eagle = GameObject.Find("ORZELEK_DEFENSE_CORE");
            if (eagle != null)
            {
                if (eagle.GetComponent<Eagle3DPresentation>() == null)
                {
                    var presentation = eagle.AddComponent<Eagle3DPresentation>();
                    presentation.Initialize();
                }
                TrackWorld(eagle.transform.parent);
            }
        }

        private static void EnsureTankPresentation(GameObject tank)
        {
            if (tank == null || tank.GetComponent<Tank3DPresentation>() != null) return;
            var presentation = tank.AddComponent<Tank3DPresentation>();
            presentation.Initialize();
        }

        private void TrackWorld(Transform world)
        {
            if (world == null || world == _currentWorld) return;
            _currentWorld = world;
            BuildWorldStage(world, _game != null ? _game.CurrentRound : 1);
        }

        private static void BuildWorldStage(Transform world, int round)
        {
            if (world == null || world.Find("World3D_Stage") != null) return;

            HideLegacyGround(world);

            var stageObject = new GameObject("World3D_Stage");
            stageObject.transform.SetParent(world, false);
            Transform stage = stageObject.transform;

            int sector = Mathf.Clamp((round - 1) / 10, 0, 9);
            Color[] groundPalette =
            {
                new Color(0.075f, 0.095f, 0.10f), new Color(0.075f, 0.10f, 0.082f),
                new Color(0.105f, 0.080f, 0.060f), new Color(0.064f, 0.088f, 0.098f),
                new Color(0.105f, 0.066f, 0.065f), new Color(0.055f, 0.075f, 0.11f),
                new Color(0.085f, 0.062f, 0.105f), new Color(0.090f, 0.092f, 0.060f),
                new Color(0.060f, 0.064f, 0.072f), new Color(0.090f, 0.045f, 0.050f)
            };
            Color ground = groundPalette[sector];

            Runtime3DFactory.Box("BattlefieldSlab3D", stage, new Vector3(0f, 0f, 0.46f), new Vector3(25.2f, 14.8f, 0.32f), ground, 0.02f, 0.18f);
            Runtime3DFactory.Box("NorthRim3D", stage, new Vector3(0f, 7.36f, 0.20f), new Vector3(25.4f, 0.22f, 0.48f), Color.Lerp(ground, Color.black, 0.30f), 0.10f, 0.20f);
            Runtime3DFactory.Box("SouthRim3D", stage, new Vector3(0f, -7.36f, 0.20f), new Vector3(25.4f, 0.22f, 0.48f), Color.Lerp(ground, Color.black, 0.30f), 0.10f, 0.20f);

            var rng = new System.Random(round * 1949 + 404);
            int marks = 16 + sector * 2;
            for (int i = 0; i < marks; i++)
            {
                float x = (float)(rng.NextDouble() * 21.5 - 10.75);
                float y = (float)(rng.NextDouble() * 10.7 - 5.35);
                float size = (float)(0.20 + rng.NextDouble() * 0.40);
                Color crater = Color.Lerp(ground, Color.black, 0.45f);
                Runtime3DFactory.Cylinder("Crater3D", stage, new Vector3(x, y, 0.275f), size, 0.025f, crater, 0.01f, 0.08f);
            }

            int debris = 22 + sector * 3;
            for (int i = 0; i < debris; i++)
            {
                float x = (float)(rng.NextDouble() * 22.0 - 11.0);
                float y = (float)(rng.NextDouble() * 11.3 - 5.65);
                float sx = (float)(0.045 + rng.NextDouble() * 0.10);
                float sy = (float)(0.055 + rng.NextDouble() * 0.16);
                var bit = Runtime3DFactory.Box("Debris3D", stage, new Vector3(x, y, 0.24f), new Vector3(sx, sy, 0.07f), Color.Lerp(ground, new Color(0.30f, 0.32f, 0.30f), 0.42f), 0.28f, 0.18f);
                bit.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 180.0));
            }

            // Sector pylons add parallax and depth outside the playable collision bounds.
            for (int i = 0; i < 6; i++)
            {
                float x = -10.5f + i * 4.2f;
                Runtime3DFactory.Box("SectorPylon3D", stage, new Vector3(x, 7.15f, -0.10f), new Vector3(0.25f, 0.25f, 1.15f + sector * 0.035f), Color.Lerp(ground, Color.white, 0.22f), 0.48f, 0.28f);
            }
        }

        private static void HideLegacyGround(Transform world)
        {
            SpriteRenderer[] sprites = world.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer sprite = sprites[i];
                if (sprite == null) continue;
                string n = sprite.gameObject.name;
                if (n == "Ground" || n == "GridV" || n == "GridH" || n == "Crater" || n == "GroundDebris" || n == "Grass" || n == "GrassBlade")
                    sprite.enabled = false;
            }
        }

        private void OnGUI()
        {
            if (_game == null) return;
            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.48f, 0.88f, 1f, 0.72f) }
                };
            }
            GUI.Label(new Rect(Screen.width - 300f, Screen.height - 31f, 285f, 22f), "3D OVERDRIVE FOUNDATION // HYBRID PHYSICS", _badgeStyle);
        }
    }

    public sealed class Obstacle3DPresentation : MonoBehaviour
    {
        private bool _ready;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;
            Obstacle obstacle = GetComponent<Obstacle>();
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            if (obstacle == null || collider == null) return;

            Runtime3DFactory.HideLegacySprites(transform);
            Vector2 size = collider.size;
            Color dark;

            if (obstacle.Kind == ObstacleKind.Brick)
            {
                Color brick = new Color(0.56f, 0.12f, 0.038f);
                dark = Color.Lerp(brick, Color.black, 0.36f);
                Runtime3DFactory.Box("BrickBlock3D", transform, new Vector3(0f, 0f, -0.12f), new Vector3(size.x * 0.90f, size.y * 0.90f, 0.46f), brick, 0.05f, 0.22f);
                Runtime3DFactory.Box("BrickCap3D", transform, new Vector3(-0.035f, size.y * 0.20f, -0.37f), new Vector3(size.x * 0.72f, size.y * 0.15f, 0.055f), new Color(0.90f, 0.32f, 0.075f), 0.04f, 0.25f);
                Runtime3DFactory.Box("Mortar3D", transform, new Vector3(size.x * 0.10f, 0f, -0.365f), new Vector3(size.x * 0.09f, size.y * 0.72f, 0.045f), dark, 0.02f, 0.15f);
            }
            else if (obstacle.Kind == ObstacleKind.Steel)
            {
                Color steel = new Color(0.31f, 0.38f, 0.46f);
                Runtime3DFactory.Box("SteelBlock3D", transform, new Vector3(0f, 0f, -0.14f), new Vector3(size.x * 0.90f, size.y * 0.90f, 0.50f), steel, 0.78f, 0.45f);
                Runtime3DFactory.Box("SteelCap3D", transform, new Vector3(-0.035f, size.y * 0.20f, -0.415f), new Vector3(size.x * 0.70f, size.y * 0.16f, 0.055f), new Color(0.68f, 0.78f, 0.86f), 0.84f, 0.56f);
                Runtime3DFactory.Cylinder("SteelBoltL3D", transform, new Vector3(-size.x * 0.28f, -size.y * 0.25f, -0.445f), 0.09f, 0.035f, new Color(0.82f, 0.86f, 0.90f), 0.90f, 0.58f);
                Runtime3DFactory.Cylinder("SteelBoltR3D", transform, new Vector3(size.x * 0.28f, -size.y * 0.25f, -0.445f), 0.09f, 0.035f, new Color(0.82f, 0.86f, 0.90f), 0.90f, 0.58f);
            }
            else
            {
                Runtime3DFactory.Box("Water3D", transform, new Vector3(0f, 0f, 0.255f), new Vector3(size.x * 0.94f, size.y * 0.94f, 0.055f), new Color(0.02f, 0.24f, 0.46f), 0.08f, 0.82f);
                Runtime3DFactory.Box("WaterHighlight3D", transform, new Vector3(-0.10f, size.y * 0.16f, 0.218f), new Vector3(size.x * 0.60f, size.y * 0.08f, 0.018f), new Color(0.18f, 0.72f, 0.92f), 0.02f, 0.92f);
            }
        }
    }

    public sealed class Eagle3DPresentation : MonoBehaviour
    {
        private Health _health;
        private GameObject _criticalBeacon;
        private Transform _wings;
        private bool _ready;

        public void Initialize()
        {
            if (_ready) return;
            _ready = true;
            _health = GetComponent<Health>();
            Runtime3DFactory.HideLegacySprites(transform);

            Color armor = new Color(0.28f, 0.34f, 0.42f);
            Color core = new Color(0.12f, 0.47f, 0.70f);
            Color eagle = new Color(0.92f, 0.76f, 0.22f);

            Runtime3DFactory.Box("EaglePlinth3D", transform, new Vector3(0f, -0.03f, -0.10f), new Vector3(1.12f, 0.82f, 0.42f), armor, 0.70f, 0.38f);
            Runtime3DFactory.Box("EagleCore3D", transform, new Vector3(0f, 0.02f, -0.42f), new Vector3(0.46f, 0.48f, 0.26f), core, 0.50f, 0.58f);
            Runtime3DFactory.Cylinder("EagleCrown3D", transform, new Vector3(0f, 0.03f, -0.61f), 0.25f, 0.09f, eagle, 0.75f, 0.68f);

            var wingRoot = new GameObject("EagleWings3D");
            wingRoot.transform.SetParent(transform, false);
            wingRoot.transform.localPosition = new Vector3(0f, 0.03f, -0.58f);
            _wings = wingRoot.transform;

            GameObject left = Runtime3DFactory.Box("LeftWing3D", _wings, new Vector3(-0.28f, 0.02f, 0f), new Vector3(0.48f, 0.11f, 0.07f), eagle, 0.72f, 0.62f);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            GameObject right = Runtime3DFactory.Box("RightWing3D", _wings, new Vector3(0.28f, 0.02f, 0f), new Vector3(0.48f, 0.11f, 0.07f), eagle, 0.72f, 0.62f);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            Runtime3DFactory.Box("EagleBody3D", _wings, new Vector3(0f, -0.04f, -0.015f), new Vector3(0.14f, 0.30f, 0.09f), eagle, 0.75f, 0.66f);

            _criticalBeacon = Runtime3DFactory.Cylinder("EagleCritical3D", transform, new Vector3(0f, -0.24f, -0.66f), 0.16f, 0.055f, new Color(1f, 0.06f, 0.025f), 0.05f, 0.88f);
            _criticalBeacon.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            if (_health == null) _health = GetComponent<Health>();
            if (_health == null || _health.Maximum <= 0) return;

            float ratio = _health.Current / (float)_health.Maximum;
            if (_criticalBeacon != null)
            {
                bool critical = ratio <= 0.34f;
                _criticalBeacon.SetActive(critical);
                if (critical)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f);
                    _criticalBeacon.transform.localScale = new Vector3(0.16f * pulse, 0.0275f, 0.16f * pulse);
                }
            }

            if (_wings != null)
            {
                float stress = Mathf.Lerp(1.5f, 0.25f, ratio);
                _wings.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2.4f) * stress);
            }
        }
    }
}
