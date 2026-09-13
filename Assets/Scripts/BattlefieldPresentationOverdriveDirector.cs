using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v6.5 presentation-only combat layer. It observes authoritative Health/EnemyTank state and
    /// translates it into persistent damage language, bounded high-value threat silhouettes and
    /// short non-text hit feedback. It never writes Health, ArmorSystem, AI or projectile state.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    public sealed class BattlefieldPresentationOverdriveDirector : MonoBehaviour
    {
        public const float DistressedHealthRatio = 0.58f;
        public const float CriticalHealthRatio = 0.30f;
        public const float ScanIntervalSeconds = 0.35f;
        public const int MaxThreatMarkers = 18;
        public const float PlayerHitVignetteSeconds = 0.32f;
        public const float EnemyHitConfirmSeconds = 0.16f;
        public const float DestructionEmphasisSeconds = 0.42f;

        private readonly HashSet<Health> _observed = new HashSet<Health>();
        private readonly List<ThreatSilhouette3D> _threats = new List<ThreatSilhouette3D>(MaxThreatMarkers);
        private float _nextScan;
        private float _playerHitUntil;
        private float _enemyHitUntil;
        private float _destructionUntil;
        private Team _lastDestroyedTeam = Team.Neutral;

        public static BattlefieldPresentationOverdriveDirector Instance { get; private set; }
        public static bool ConfigurationValid =>
            DistressedHealthRatio > CriticalHealthRatio && DistressedHealthRatio <= 0.70f &&
            CriticalHealthRatio >= 0.20f && CriticalHealthRatio <= 0.40f &&
            ScanIntervalSeconds >= 0.20f && ScanIntervalSeconds <= 0.60f &&
            MaxThreatMarkers >= 8 && MaxThreatMarkers <= 24 &&
            PlayerHitVignetteSeconds >= 0.20f && PlayerHitVignetteSeconds <= 0.60f &&
            EnemyHitConfirmSeconds >= 0.08f && EnemyHitConfirmSeconds <= 0.30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BattlefieldPresentationOverdriveDirector>() != null) return;
            var go = new GameObject("BattlefieldPresentationOverdriveDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<BattlefieldPresentationOverdriveDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            foreach (Health health in _observed)
            {
                if (health == null) continue;
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            _observed.Clear();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanIntervalSeconds;
            RefreshActors();
        }

        private void RefreshActors()
        {
            Health[] all = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Health health = all[i];
                if (health == null || health.IsDead) continue;

                if (_observed.Add(health))
                {
                    health.Damaged -= OnDamaged;
                    health.Damaged += OnDamaged;
                    health.Died -= OnDied;
                    health.Died += OnDied;
                }

                bool vehicle = health.GetComponent<PlayerTank>() != null || health.GetComponent<EnemyTank>() != null;
                if (vehicle && health.GetComponent<VehicleDamageStatePresentation3D>() == null)
                    health.gameObject.AddComponent<VehicleDamageStatePresentation3D>().Initialize(health);

                EnemyTank enemy = health.GetComponent<EnemyTank>();
                if (enemy != null && IsHighValue(enemy.Kind) && enemy.GetComponent<ThreatSilhouette3D>() == null && CountLiveThreats() < MaxThreatMarkers)
                {
                    ThreatSilhouette3D marker = enemy.gameObject.AddComponent<ThreatSilhouette3D>();
                    marker.Initialize(enemy.Kind, health);
                    _threats.Add(marker);
                }
            }

            _observed.RemoveWhere(h => h == null);
            for (int i = _threats.Count - 1; i >= 0; i--)
                if (_threats[i] == null) _threats.RemoveAt(i);
        }

        private int CountLiveThreats()
        {
            int count = 0;
            for (int i = 0; i < _threats.Count; i++)
                if (_threats[i] != null) count++;
            return count;
        }

        private static bool IsHighValue(EnemyKind kind)
        {
            return kind == EnemyKind.Boss || kind == EnemyKind.Elite || kind == EnemyKind.Siege;
        }

        private void OnDamaged(Health health, int amount)
        {
            if (health == null || amount <= 0) return;

            if (health.GetComponent<PlayerTank>() != null)
                _playerHitUntil = Mathf.Max(_playerHitUntil, Time.unscaledTime + PlayerHitVignetteSeconds);
            else if (health.Team == Team.Enemy)
                _enemyHitUntil = Mathf.Max(_enemyHitUntil, Time.unscaledTime + EnemyHitConfirmSeconds);

            PresentationHitPulse3D.Spawn(health.transform.position, health.Team, Mathf.Clamp(amount, 1, 6));
        }

        private void OnDied(Health health)
        {
            if (health == null) return;
            _lastDestroyedTeam = health.Team;
            _destructionUntil = Mathf.Max(_destructionUntil, Time.unscaledTime + DestructionEmphasisSeconds);
            PresentationDestructionEmphasis3D.Spawn(health.transform.position, health.Team);
        }

        private void OnGUI()
        {
            float now = Time.unscaledTime;
            if (now < _playerHitUntil)
            {
                float strength = Mathf.Clamp01((_playerHitUntil - now) / PlayerHitVignetteSeconds);
                DrawFrame(new Color(1f, 0.08f, 0.025f, 0.16f + 0.22f * strength), 12f + 10f * strength);
            }

            if (now < _enemyHitUntil)
            {
                float strength = Mathf.Clamp01((_enemyHitUntil - now) / EnemyHitConfirmSeconds);
                DrawHitConfirm(new Color(1f, 0.88f, 0.34f, 0.30f + 0.55f * strength), 14f + 9f * strength);
            }

            if (now < _destructionUntil)
            {
                float strength = Mathf.Clamp01((_destructionUntil - now) / DestructionEmphasisSeconds);
                Color color = _lastDestroyedTeam == Team.Enemy
                    ? new Color(1f, 0.48f, 0.10f, 0.10f * strength)
                    : new Color(0.20f, 0.72f, 1f, 0.10f * strength);
                DrawFrame(color, 5f);
            }
        }

        private static void DrawFrame(Color color, float thickness)
        {
            Color old = GUI.color;
            GUI.color = color;
            Texture2D tex = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), tex);
            GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness), tex);
            GUI.DrawTexture(new Rect(0f, 0f, thickness, Screen.height), tex);
            GUI.DrawTexture(new Rect(Screen.width - thickness, 0f, thickness, Screen.height), tex);
            GUI.color = old;
        }

        private static void DrawHitConfirm(Color color, float radius)
        {
            Color old = GUI.color;
            GUI.color = color;
            Texture2D tex = Texture2D.whiteTexture;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float len = 8f;
            float thick = 2f;
            GUI.DrawTexture(new Rect(cx - radius - len, cy - thick * 0.5f, len, thick), tex);
            GUI.DrawTexture(new Rect(cx + radius, cy - thick * 0.5f, len, thick), tex);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy - radius - len, thick, len), tex);
            GUI.DrawTexture(new Rect(cx - thick * 0.5f, cy + radius, thick, len), tex);
            GUI.color = old;
        }
    }

    public sealed class VehicleDamageStatePresentation3D : MonoBehaviour
    {
        private Health _health;
        private Transform _root;
        private MeshRenderer _ring;
        private MeshRenderer[] _criticalBars;
        private float _phase;

        public void Initialize(Health health)
        {
            _health = health;
            var root = new GameObject("DamageStateVisual3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.18f);
            _root = root.transform;

            GameObject ring = Runtime3DFactory.Cylinder("DamageStateRing3D", _root, Vector3.zero, 1.30f, 0.025f,
                new Color(1f, 0.44f, 0.08f, 0.55f), 0.01f, 0.78f);
            _ring = ring.GetComponent<MeshRenderer>();

            _criticalBars = new MeshRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                var holder = new GameObject("CriticalChevron3D_" + i);
                holder.transform.SetParent(_root, false);
                holder.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                GameObject bar = Runtime3DFactory.Box("CriticalBar3D", holder.transform, new Vector3(0f, 0.82f, 0f),
                    new Vector3(0.16f, 0.34f, 0.035f), new Color(1f, 0.08f, 0.025f, 0.75f), 0.01f, 0.82f);
                _criticalBars[i] = bar.GetComponent<MeshRenderer>();
            }
        }

        private void Update()
        {
            if (_health == null || _health.IsDead || _root == null) return;
            float ratio = _health.Maximum > 0 ? _health.Current / (float)_health.Maximum : 1f;
            bool distressed = ratio <= BattlefieldPresentationOverdriveDirector.DistressedHealthRatio;
            bool critical = ratio <= BattlefieldPresentationOverdriveDirector.CriticalHealthRatio;

            if (_ring != null)
            {
                _ring.enabled = distressed;
                if (distressed)
                    _ring.sharedMaterial = Runtime3DFactory.Material(critical
                        ? new Color(1f, 0.08f, 0.025f, 0.72f)
                        : new Color(1f, 0.50f, 0.06f, 0.50f), 0.01f, 0.78f);
            }

            for (int i = 0; i < _criticalBars.Length; i++)
                if (_criticalBars[i] != null) _criticalBars[i].enabled = critical;

            if (!distressed) return;
            _phase += Time.unscaledDeltaTime * (critical ? 7.5f : 4.0f);
            float pulse = 1f + Mathf.Sin(_phase) * (critical ? 0.10f : 0.045f);
            _root.localScale = Vector3.one * pulse;
            _root.Rotate(0f, 0f, (critical ? 28f : 12f) * Time.unscaledDeltaTime, Space.Self);
        }
    }

    public sealed class ThreatSilhouette3D : MonoBehaviour
    {
        private Health _health;
        private EnemyKind _kind;
        private Transform _root;
        private float _phase;

        public void Initialize(EnemyKind kind, Health health)
        {
            _kind = kind;
            _health = health;
            var root = new GameObject("HighValueThreatSilhouette3D");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 0.28f);
            _root = root.transform;

            Color color = ThreatColor(kind);
            float diameter = kind == EnemyKind.Boss ? 2.05f : kind == EnemyKind.Siege ? 1.72f : 1.56f;
            Runtime3DFactory.Cylinder("ThreatRing3D", _root, Vector3.zero, diameter, 0.03f, color, 0.01f, 0.86f);

            for (int i = 0; i < 3; i++)
            {
                var anchor = new GameObject("ThreatChevronAnchor3D_" + i);
                anchor.transform.SetParent(_root, false);
                anchor.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                Runtime3DFactory.Box("ThreatChevron3D", anchor.transform, new Vector3(0f, diameter * 0.53f, 0f),
                    new Vector3(0.15f, kind == EnemyKind.Boss ? 0.46f : 0.34f, 0.04f), color, 0.01f, 0.90f);
            }
        }

        private void Update()
        {
            if (_health == null || _health.IsDead || _root == null) return;
            _phase += Time.unscaledDeltaTime * (_kind == EnemyKind.Boss ? 3.8f : 2.8f);
            float pulse = 1f + Mathf.Sin(_phase) * (_kind == EnemyKind.Boss ? 0.10f : 0.06f);
            _root.localScale = Vector3.one * pulse;
            _root.Rotate(0f, 0f, (_kind == EnemyKind.Boss ? -24f : 18f) * Time.unscaledDeltaTime, Space.Self);
        }

        private static Color ThreatColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Boss: return new Color(1f, 0.12f, 0.025f, 0.82f);
                case EnemyKind.Siege: return new Color(1f, 0.44f, 0.08f, 0.72f);
                default: return new Color(0.78f, 0.26f, 1f, 0.68f);
            }
        }
    }

    public sealed class PresentationHitPulse3D : MonoBehaviour
    {
        private float _born;
        private float _life;
        private bool _ready;

        public static void Spawn(Vector3 position, Team team, int force)
        {
            var root = new GameObject("PresentationHitPulse3D");
            root.transform.position = new Vector3(position.x, position.y, -0.36f);
            Color color = team == Team.Enemy ? new Color(1f, 0.72f, 0.16f, 0.72f) : new Color(0.24f, 0.76f, 1f, 0.72f);
            Runtime3DFactory.Cylinder("HitPulseRing3D", root.transform, Vector3.zero,
                0.48f + force * 0.06f, 0.025f, color, 0.01f, 0.90f);
            var pulse = root.AddComponent<PresentationHitPulse3D>();
            pulse._born = Time.unscaledTime;
            pulse._life = 0.18f + Mathf.Min(force, 4) * 0.025f;
            pulse._ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / Mathf.Max(0.01f, _life));
            transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.75f, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    public sealed class PresentationDestructionEmphasis3D : MonoBehaviour
    {
        private float _born;
        private float _life;

        public static void Spawn(Vector3 position, Team team)
        {
            var root = new GameObject("PresentationDestructionEmphasis3D");
            root.transform.position = new Vector3(position.x, position.y, -0.30f);
            Color color = team == Team.Enemy ? new Color(1f, 0.20f, 0.03f, 0.64f) : new Color(0.16f, 0.66f, 1f, 0.60f);
            Runtime3DFactory.Cylinder("DestructionHalo3D", root.transform, Vector3.zero, 1.10f, 0.04f, color, 0.01f, 0.86f);
            for (int i = 0; i < 6; i++)
            {
                var ray = new GameObject("DestructionRayAnchor3D_" + i);
                ray.transform.SetParent(root.transform, false);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                Runtime3DFactory.Box("DestructionRay3D", ray.transform, new Vector3(0f, 0.72f, 0f),
                    new Vector3(0.07f, 0.72f, 0.035f), color, 0.01f, 0.90f);
            }
            var fx = root.AddComponent<PresentationDestructionEmphasis3D>();
            fx._born = Time.unscaledTime;
            fx._life = BattlefieldPresentationOverdriveDirector.DestructionEmphasisSeconds;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _born) / Mathf.Max(0.01f, _life));
            transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 2.10f, t);
            transform.Rotate(0f, 0f, 110f * Time.unscaledDeltaTime, Space.Self);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
