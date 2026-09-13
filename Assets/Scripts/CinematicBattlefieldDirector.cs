using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v6.6 presentation-only battlefield reforge. It observes authoritative combat state and adds
    /// atmosphere, bounded camera framing and recyclable aftermath decals without changing gameplay.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CinematicBattlefieldDirector : MonoBehaviour
    {
        public const int SectorCount = 10;
        public const int MaxTrackMarks = 48;
        public const int MaxImpactScars = 32;
        public const int MaxDebris = 24;
        public const float MaxCameraOffset = 0.42f;
        public const float MaxCameraZoomDelta = 0.28f;
        public static bool ConfigurationValid => SectorCount == 10 && MaxTrackMarks <= 64 && MaxImpactScars <= 48 && MaxDebris <= 32 && MaxCameraOffset <= 0.5f && MaxCameraZoomDelta <= 0.35f;

        private sealed class FxSlot
        {
            public GameObject Go;
        }

        private static Sprite _whiteSprite;
        private readonly List<FxSlot> _tracks = new List<FxSlot>(MaxTrackMarks);
        private readonly List<FxSlot> _scars = new List<FxSlot>(MaxImpactScars);
        private readonly List<FxSlot> _debris = new List<FxSlot>(MaxDebris);
        private readonly HashSet<Health> _subscribed = new HashSet<Health>();

        private TankGame _game;
        private Camera _camera;
        private Transform _atmosphereRoot;
        private Transform _aftermathRoot;
        private int _sector = -1;
        private int _trackCursor;
        private int _scarCursor;
        private int _debrisCursor;
        private float _nextTrackAt;
        private Vector2 _lastPlayerPosition;
        private float _impulseUntil;
        private float _impulseStrength;
        private float _baseOrthographicSize;
        private Color _baseBackground;

        public int CurrentSector => _sector;
        public int ActiveTrackMarks => CountVisible(_tracks);
        public int ActiveImpactScars => CountVisible(_scars);
        public int ActiveDebris => CountVisible(_debris);
        public float CameraOffsetMagnitude { get; private set; }
        public float CameraZoomDelta { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstalled()
        {
            if (FindAnyObjectByType<CinematicBattlefieldDirector>() != null) return;
            var go = new GameObject("CinematicBattlefieldDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<CinematicBattlefieldDirector>();
        }

        private void Awake()
        {
            EnsureSprite();
            BuildRoots();
            WarmPool(_tracks, MaxTrackMarks, "Track", new Color(0.02f, 0.025f, 0.028f, 0.24f), new Vector2(0.10f, 0.34f), -49);
            WarmPool(_scars, MaxImpactScars, "ImpactScar", new Color(0.03f, 0.025f, 0.02f, 0.40f), new Vector2(0.34f, 0.34f), -48);
            WarmPool(_debris, MaxDebris, "Debris", new Color(0.22f, 0.18f, 0.14f, 0.72f), new Vector2(0.12f, 0.06f), -47);
            RuntimeBattleRegistry.Changed += ReconcileHealthSubscriptions;
        }

        private void Start()
        {
            ResolveAuthority();
            ReconcileHealthSubscriptions();
        }

        private void OnDestroy()
        {
            RuntimeBattleRegistry.Changed -= ReconcileHealthSubscriptions;
            foreach (Health h in _subscribed)
            {
                if (h == null) continue;
                h.Damaged -= OnHealthDamaged;
                h.Died -= OnHealthDied;
            }
            _subscribed.Clear();
        }

        private void Update()
        {
            ResolveAuthority();
            if (_game == null || !_game.IsPlaying) return;

            int sector = Mathf.Clamp((_game.CurrentRound - 1) / 10, 0, SectorCount - 1);
            if (sector != _sector)
            {
                _sector = sector;
                RebuildAtmosphere(sector);
            }

            PlayerTank player = RuntimeBattleRegistry.Player;
            if (player != null)
            {
                Vector2 now = player.transform.position;
                float moved = Vector2.Distance(now, _lastPlayerPosition);
                if (Time.time >= _nextTrackAt && moved > 0.09f)
                {
                    SpawnTrack(now, player.transform.eulerAngles.z);
                    _nextTrackAt = Time.time + 0.085f;
                    _lastPlayerPosition = now;
                }
                else if (_lastPlayerPosition == Vector2.zero)
                {
                    _lastPlayerPosition = now;
                }
            }
        }

        private void LateUpdate()
        {
            if (_camera == null || _game == null || !_game.IsPlaying) return;

            Vector3 basePos = new Vector3(0f, 0f, -10f);
            Vector2 desired = Vector2.zero;
            PlayerTank player = RuntimeBattleRegistry.Player;
            if (player != null)
            {
                Vector2 p = player.transform.position;
                desired += Vector2.ClampMagnitude(p * 0.022f, 0.22f);
            }

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies.Length > 0 && player != null)
            {
                Vector2 centroid = Vector2.zero;
                int count = 0;
                for (int i = 0; i < enemies.Length && count < 8; i++)
                {
                    if (enemies[i] == null) continue;
                    centroid += (Vector2)enemies[i].transform.position;
                    count++;
                }
                if (count > 0)
                {
                    centroid /= count;
                    Vector2 threatLead = (centroid - (Vector2)player.transform.position) * 0.022f;
                    desired += Vector2.ClampMagnitude(threatLead, 0.18f);
                }
            }

            if (Time.unscaledTime < _impulseUntil)
            {
                float remaining = Mathf.Clamp01((_impulseUntil - Time.unscaledTime) / 0.22f);
                desired += UnityEngine.Random.insideUnitCircle * (_impulseStrength * remaining);
            }

            desired = Vector2.ClampMagnitude(desired, MaxCameraOffset);
            CameraOffsetMagnitude = desired.magnitude;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, basePos + (Vector3)desired, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

            float pressure = Mathf.Clamp01(enemies.Length / 14f);
            float zoomDelta = Mathf.Lerp(0f, MaxCameraZoomDelta, pressure);
            CameraZoomDelta = zoomDelta;
            float targetSize = _baseOrthographicSize + zoomDelta;
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetSize, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
        }

        private void ResolveAuthority()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null)
                {
                    _baseOrthographicSize = Mathf.Max(1f, _camera.orthographicSize);
                    _baseBackground = _camera.backgroundColor;
                }
            }
        }

        private void BuildRoots()
        {
            var atmosphere = new GameObject("Cinematic_Atmosphere");
            atmosphere.transform.SetParent(transform, false);
            _atmosphereRoot = atmosphere.transform;

            var aftermath = new GameObject("Cinematic_Aftermath");
            aftermath.transform.SetParent(transform, false);
            _aftermathRoot = aftermath.transform;
        }

        private void RebuildAtmosphere(int sector)
        {
            for (int i = _atmosphereRoot.childCount - 1; i >= 0; i--)
                Destroy(_atmosphereRoot.GetChild(i).gameObject);

            Color[] sky =
            {
                new Color(0.025f,0.040f,0.052f), new Color(0.035f,0.045f,0.040f),
                new Color(0.052f,0.040f,0.032f), new Color(0.028f,0.045f,0.052f),
                new Color(0.052f,0.032f,0.036f), new Color(0.025f,0.036f,0.058f),
                new Color(0.042f,0.030f,0.055f), new Color(0.045f,0.048f,0.028f),
                new Color(0.027f,0.030f,0.036f), new Color(0.055f,0.022f,0.025f)
            };
            Color[] haze =
            {
                new Color(0.18f,0.34f,0.42f,0.08f), new Color(0.20f,0.34f,0.26f,0.07f),
                new Color(0.46f,0.30f,0.18f,0.08f), new Color(0.18f,0.36f,0.42f,0.07f),
                new Color(0.45f,0.18f,0.20f,0.08f), new Color(0.20f,0.28f,0.48f,0.08f),
                new Color(0.34f,0.20f,0.48f,0.08f), new Color(0.38f,0.40f,0.18f,0.07f),
                new Color(0.24f,0.25f,0.28f,0.08f), new Color(0.50f,0.12f,0.10f,0.09f)
            };

            if (_camera != null) _camera.backgroundColor = Color.Lerp(_baseBackground, sky[sector], 0.72f);

            var rng = new System.Random(701 + sector * 97);
            int bands = 6 + sector / 2;
            for (int i = 0; i < bands; i++)
            {
                float x = (float)(rng.NextDouble() * 22.0 - 11.0);
                float y = (float)(rng.NextDouble() * 11.0 - 5.5);
                float w = (float)(2.2 + rng.NextDouble() * 5.0);
                float h = (float)(0.18 + rng.NextDouble() * 0.48);
                CreateQuad("AtmosphereBand", _atmosphereRoot, new Vector2(x, y), new Vector2(w, h), haze[sector], -55 - (i % 2));
            }

            for (int i = 0; i < 18; i++)
            {
                float x = (float)(rng.NextDouble() * 23.0 - 11.5);
                float y = (float)(rng.NextDouble() * 12.0 - 6.0);
                float s = (float)(0.035 + rng.NextDouble() * 0.10);
                Color c = Color.Lerp(haze[sector], Color.white, 0.25f);
                c.a = 0.18f;
                CreateQuad("AtmosphereSpark", _atmosphereRoot, new Vector2(x, y), new Vector2(s, s), c, -45);
            }
        }

        private void ReconcileHealthSubscriptions()
        {
            Health[] health = RuntimeBattleRegistry.HealthSnapshot;
            for (int i = 0; i < health.Length; i++)
            {
                Health h = health[i];
                if (h == null || !_subscribed.Add(h)) continue;
                h.Damaged += OnHealthDamaged;
                h.Died += OnHealthDied;
            }
            _subscribed.RemoveWhere(h => h == null);
        }

        private void OnHealthDamaged(Health health, int amount)
        {
            if (health == null) return;
            SpawnScar(health.transform.position);
            if (health.Team == Team.Player)
                AddCameraImpulse(0.10f + Mathf.Min(0.07f, amount * 0.015f));
        }

        private void OnHealthDied(Health health)
        {
            if (health == null) return;
            Vector2 p = health.transform.position;
            SpawnScar(p);
            for (int i = 0; i < 4; i++) SpawnDebris(p + UnityEngine.Random.insideUnitCircle * 0.28f);
            AddCameraImpulse(health.Team == Team.Player ? 0.18f : 0.12f);
        }

        private void AddCameraImpulse(float strength)
        {
            _impulseStrength = Mathf.Clamp(strength, 0f, 0.20f);
            _impulseUntil = Time.unscaledTime + 0.22f;
        }

        private void SpawnTrack(Vector2 position, float angle)
        {
            FxSlot slot = Next(_tracks, ref _trackCursor);
            Activate(slot, position, angle, new Vector2(0.09f, 0.32f));
        }

        private void SpawnScar(Vector2 position)
        {
            FxSlot slot = Next(_scars, ref _scarCursor);
            Activate(slot, position + UnityEngine.Random.insideUnitCircle * 0.08f, UnityEngine.Random.Range(0f, 360f), Vector2.one * UnityEngine.Random.Range(0.28f, 0.44f));
        }

        private void SpawnDebris(Vector2 position)
        {
            FxSlot slot = Next(_debris, ref _debrisCursor);
            Activate(slot, position, UnityEngine.Random.Range(0f, 360f), new Vector2(UnityEngine.Random.Range(0.07f, 0.16f), UnityEngine.Random.Range(0.04f, 0.09f)));
        }

        private static FxSlot Next(List<FxSlot> list, ref int cursor)
        {
            FxSlot slot = list[cursor];
            cursor = (cursor + 1) % list.Count;
            return slot;
        }

        private static void Activate(FxSlot slot, Vector2 position, float angle, Vector2 scale)
        {
            slot.Go.transform.position = new Vector3(position.x, position.y, 0f);
            slot.Go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            slot.Go.transform.localScale = scale;
            slot.Go.SetActive(true);
        }

        private void WarmPool(List<FxSlot> pool, int count, string prefix, Color color, Vector2 scale, int order)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject go = CreateQuad(prefix + "_" + i, _aftermathRoot, Vector2.zero, scale, color, order);
                go.SetActive(false);
                pool.Add(new FxSlot { Go = go });
            }
        }

        private static int CountVisible(List<FxSlot> list)
        {
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i].Go.activeSelf) n++;
            return n;
        }

        private static GameObject CreateQuad(string name, Transform parent, Vector2 position, Vector2 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _whiteSprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private static void EnsureSprite()
        {
            if (_whiteSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.name = "CinematicBattlefieldWhite";
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _whiteSprite.name = "CinematicBattlefieldWhiteSprite";
        }
    }
}
