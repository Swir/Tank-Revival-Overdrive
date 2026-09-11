using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v2.6 presentation director for sector lighting, battlefield pulse and pressure-aware camera.
    /// Runs after Battlefield3DDirector so it can layer cinematic framing on top of the stable
    /// perspective camera without touching the authoritative XY simulation.
    /// </summary>
    [DefaultExecutionOrder(10040)]
    public sealed class TheaterAtmosphere3DDirector : MonoBehaviour
    {
        private static readonly Color[] Ambient =
        {
            new Color(0.24f, 0.28f, 0.31f), new Color(0.20f, 0.22f, 0.23f), new Color(0.27f, 0.20f, 0.15f),
            new Color(0.14f, 0.19f, 0.27f), new Color(0.10f, 0.18f, 0.22f), new Color(0.24f, 0.30f, 0.34f),
            new Color(0.22f, 0.21f, 0.18f), new Color(0.075f, 0.070f, 0.13f), new Color(0.30f, 0.12f, 0.07f),
            new Color(0.22f, 0.045f, 0.060f)
        };

        private static readonly Color[] KeyColors =
        {
            new Color(0.92f, 0.96f, 1f), new Color(0.82f, 0.88f, 0.92f), new Color(1f, 0.72f, 0.44f),
            new Color(0.60f, 0.78f, 1f), new Color(0.42f, 0.76f, 0.90f), new Color(0.78f, 0.92f, 1f),
            new Color(0.88f, 0.82f, 0.65f), new Color(0.48f, 0.42f, 0.88f), new Color(1f, 0.40f, 0.16f),
            new Color(1f, 0.16f, 0.20f)
        };

        private TankGame _game;
        private Camera _camera;
        private Light _sectorKey;
        private Light _combatFill;
        private PlayerTank _player;
        private int _sector = -1;
        private int _round = -1;
        private float _nextPlayerScan;
        private float _damagePressure;
        private float _flash;
        private Vector3 _smoothedLead;
        private Vector3 _lastPlayerPosition;
        private Health _observedPlayerHealth;
        private Health _observedEagleHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TheaterAtmosphere3DDirector>() != null) return;
            var go = new GameObject("TheaterAtmosphere3DDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<TheaterAtmosphere3DDirector>();
        }

        private void Awake()
        {
            CreateLights();
        }

        private void CreateLights()
        {
            var keyGo = new GameObject("TheaterSectorKey3D");
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.rotation = Quaternion.Euler(32f, -38f, -8f);
            _sectorKey = keyGo.AddComponent<Light>();
            _sectorKey.type = LightType.Directional;
            _sectorKey.intensity = 0.44f;
            _sectorKey.shadows = LightShadows.Soft;

            var fillGo = new GameObject("TheaterCombatFill3D");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.position = new Vector3(0f, -2.8f, -5.5f);
            _combatFill = fillGo.AddComponent<Light>();
            _combatFill.type = LightType.Point;
            _combatFill.range = 24f;
            _combatFill.intensity = 0.12f;
            _combatFill.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_camera == null) _camera = Camera.main;
            if (_game == null) return;

            if (!_game.IsPlaying)
            {
                _damagePressure = Mathf.MoveTowards(_damagePressure, 0f, Time.unscaledDeltaTime * 0.8f);
                return;
            }

            if (_game.CurrentRound != _round)
            {
                _round = _game.CurrentRound;
                int nextSector = Mathf.Clamp((_round - 1) / 10, 0, 9);
                if (nextSector != _sector)
                {
                    _sector = nextSector;
                    ApplySectorLighting();
                }
            }

            if (Time.unscaledTime >= _nextPlayerScan)
            {
                _nextPlayerScan = Time.unscaledTime + 0.45f;
                ResolveTrackedObjects();
            }

            UpdateLightingPulse();
            _damagePressure = Mathf.MoveTowards(_damagePressure, 0f, Time.unscaledDeltaTime * 0.085f);
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 1.9f);
        }

        private void ResolveTrackedObjects()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
            if (_player != null && _observedPlayerHealth != _player.Health)
            {
                if (_observedPlayerHealth != null) _observedPlayerHealth.Damaged -= OnImportantDamaged;
                _observedPlayerHealth = _player.Health;
                if (_observedPlayerHealth != null)
                {
                    _observedPlayerHealth.Damaged -= OnImportantDamaged;
                    _observedPlayerHealth.Damaged += OnImportantDamaged;
                }
                _lastPlayerPosition = _player.transform.position;
            }

            GameObject eagle = GameObject.Find("ORZELEK_DEFENSE_CORE");
            Health eagleHealth = eagle != null ? eagle.GetComponent<Health>() : null;
            if (_observedEagleHealth != eagleHealth)
            {
                if (_observedEagleHealth != null) _observedEagleHealth.Damaged -= OnImportantDamaged;
                _observedEagleHealth = eagleHealth;
                if (_observedEagleHealth != null)
                {
                    _observedEagleHealth.Damaged -= OnImportantDamaged;
                    _observedEagleHealth.Damaged += OnImportantDamaged;
                }
            }
        }

        private void OnImportantDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0) return;
            float weight = health == _observedEagleHealth ? 0.36f : 0.18f;
            _damagePressure = Mathf.Clamp01(_damagePressure + weight + amount * 0.035f);
            _flash = Mathf.Clamp01(_flash + (health == _observedEagleHealth ? 0.72f : 0.34f));
        }

        private void ApplySectorLighting()
        {
            if (_sector < 0) return;
            RenderSettings.ambientLight = Ambient[_sector];
            if (_sectorKey != null)
            {
                _sectorKey.color = KeyColors[_sector];
                _sectorKey.intensity = BaseKeyIntensity(_sector);
                _sectorKey.transform.rotation = Quaternion.Euler(24f + _sector * 1.8f, -42f + _sector * 6f, _sector % 2 == 0 ? -7f : 9f);
            }
            if (_combatFill != null)
            {
                _combatFill.color = Color.Lerp(KeyColors[_sector], new Color(1f, 0.18f, 0.08f), _sector >= 8 ? 0.45f : 0.10f);
                _combatFill.intensity = _sector == 7 ? 0.38f : 0.13f;
            }
        }

        private void UpdateLightingPulse()
        {
            if (_sector < 0 || _sectorKey == null || _combatFill == null) return;
            float time = Time.unscaledTime;
            float baseIntensity = BaseKeyIntensity(_sector);
            float weatherPulse = 0f;

            if (_sector == 3)
                weatherPulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 0.72f) * Mathf.Sin(time * 2.11f)), 8f) * 0.62f;
            else if (_sector == 7)
                weatherPulse = Mathf.Sin(time * 0.55f) * 0.035f;
            else if (_sector == 8)
                weatherPulse = Mathf.Sin(time * 3.2f) * 0.055f + Mathf.Sin(time * 7.3f) * 0.025f;
            else if (_sector == 9)
                weatherPulse = Mathf.Sin(time * 1.7f) * 0.08f;

            _sectorKey.intensity = Mathf.Max(0.10f, baseIntensity + weatherPulse + _flash * 0.34f);
            _combatFill.intensity = (_sector == 7 ? 0.38f : 0.13f) + _damagePressure * 0.42f + _flash * 0.22f;
            _combatFill.transform.position = new Vector3(Mathf.Sin(time * 0.16f) * 4f, -2.4f + Mathf.Cos(time * 0.11f) * 2.2f, -4.8f);
        }

        private static float BaseKeyIntensity(int sector)
        {
            switch (sector)
            {
                case 7: return 0.18f;
                case 8: return 0.62f;
                case 9: return 0.58f;
                case 5: return 0.52f;
                default: return 0.40f + sector * 0.012f;
            }
        }

        private void LateUpdate()
        {
            if (_game == null || !_game.IsPlaying || _camera == null || !_camera.gameObject.activeInHierarchy) return;

            Vector3 leadTarget = Vector3.zero;
            if (_player != null)
            {
                Vector3 current = _player.transform.position;
                Vector3 velocity = (current - _lastPlayerPosition) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                _lastPlayerPosition = current;
                velocity = Vector3.ClampMagnitude(velocity, 7f);
                leadTarget = new Vector3(current.x * 0.10f + velocity.x * 0.035f, current.y * 0.07f + velocity.y * 0.025f, 0f);
            }

            _smoothedLead = Vector3.Lerp(_smoothedLead, leadTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.6f));

            float endgame = Mathf.InverseLerp(55f, 100f, _round);
            float pressure = Mathf.Clamp01(_damagePressure + endgame * 0.22f);
            float targetFov = Mathf.Lerp(43.5f, 39.8f, pressure);
            if (_sector == 7) targetFov += 1.2f;
            if (_sector == 9) targetFov -= 0.8f;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.0f));

            Vector3 position = _camera.transform.position;
            Vector3 desired = position + new Vector3(_smoothedLead.x, _smoothedLead.y, 0f) * 0.22f;
            _camera.transform.position = Vector3.Lerp(position, desired, Time.unscaledDeltaTime * 0.35f);

            Vector3 look = new Vector3(_smoothedLead.x * 0.42f, 0.35f + _smoothedLead.y * 0.34f, 0f);
            Quaternion desiredRotation = Quaternion.LookRotation(look - _camera.transform.position, Vector3.up);
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, desiredRotation, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 1.8f));
        }

        private void OnDestroy()
        {
            if (_observedPlayerHealth != null) _observedPlayerHealth.Damaged -= OnImportantDamaged;
            if (_observedEagleHealth != null) _observedEagleHealth.Damaged -= OnImportantDamaged;
        }
    }
}
