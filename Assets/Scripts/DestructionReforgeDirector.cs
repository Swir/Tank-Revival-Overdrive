using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public sealed class DestructionReforgeDirector : MonoBehaviour
    {
        public const int WreckProfileCount = 7;
        public const int FullWreckBudget = 26;
        public const int BalancedWreckBudget = 18;
        public const int SurvivalWreckBudget = 12;
        public const int FullDebrisBudget = 72;
        public const int BalancedDebrisBudget = 44;
        public const int SurvivalDebrisBudget = 24;
        public const float HotPhaseSeconds = 4.5f;
        public const float SmolderPhaseSeconds = 8.5f;
        public static bool SingleWreckAuthority => true;
        public static bool UsesHealthAuthority => true;
        public static bool UsesObstacleAuthority => true;
        public static bool ConfigurationValid => WreckProfileCount == 7 && FullWreckBudget <= 28 && BalancedWreckBudget < FullWreckBudget && SurvivalWreckBudget < BalancedWreckBudget && FullDebrisBudget <= 80 && HotPhaseSeconds >= 3f && SmolderPhaseSeconds >= 6f;

        private sealed class Witness
        {
            public Health Health;
            public EnemyTank Enemy;
            public PlayerTank Player;
            public int Id;
        }

        private readonly Dictionary<int, Witness> _witnesses = new Dictionary<int, Witness>();
        private readonly Queue<GameObject> _wrecks = new Queue<GameObject>();
        private readonly Queue<GameObject> _debris = new Queue<GameObject>();
        private float _nextScan;

        public static DestructionReforgeDirector Instance { get; private set; }
        public int ActiveWrecks => _wrecks.Count;
        public int ActiveDebris => _debris.Count;
        public int CurrentWreckBudget => ResolveWreckBudget();
        public int CurrentDebrisBudget => ResolveDebrisBudget();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<DestructionReforgeDirector>() != null) return;
            var go = new GameObject("DestructionReforgeDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<DestructionReforgeDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.30f;
            ScanCombatants();
            PruneQueue(_wrecks);
            PruneQueue(_debris);
            TrimQueue(_wrecks, ResolveWreckBudget());
            TrimQueue(_debris, ResolveDebrisBudget());
        }

        private void ScanCombatants()
        {
            Health[] healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < healths.Length; i++)
            {
                Health health = healths[i];
                if (health == null || health.IsDead) continue;
                EnemyTank enemy = health.GetComponent<EnemyTank>();
                PlayerTank player = health.GetComponent<PlayerTank>();
                if (enemy == null && player == null) continue;
                int id = health.GetInstanceID();
                if (_witnesses.ContainsKey(id)) continue;
                var witness = new Witness { Health = health, Enemy = enemy, Player = player, Id = id };
                _witnesses.Add(id, witness);
                health.Died += OnDeath;
            }
        }

        private void OnDeath(Health health)
        {
            if (health == null) return;
            int id = health.GetInstanceID();
            Witness witness;
            if (!_witnesses.TryGetValue(id, out witness)) return;
            _witnesses.Remove(id);
            health.Died -= OnDeath;

            EnemyKind kind = witness.Enemy != null ? witness.Enemy.Kind : EnemyKind.Basic;
            bool player = witness.Player != null;
            SpawnClassWreck(health.transform.position, health.transform.rotation, kind, player);
        }

        public void ReportObstacleImpact(Vector3 position, ObstacleKind kind, bool destroyed, bool heavy)
        {
            if (kind == ObstacleKind.Water) return;
            Color color = kind == ObstacleKind.Steel ? new Color(0.62f, 0.72f, 0.78f) : new Color(0.46f, 0.17f, 0.06f);
            int pieces = destroyed ? (heavy ? 8 : 5) : (heavy ? 3 : 1);
            SpawnDebrisBurst(position, color, pieces, destroyed ? 0.72f : 0.42f);
            if (destroyed)
            {
                VisualFactory.RingPulse(position, new Color(color.r, color.g, color.b, 0.42f), heavy ? 0.86f : 0.62f);
                VisualFactory.MicroBurst(position, Color.Lerp(color, Color.white, 0.25f), heavy ? 0.72f : 0.52f);
            }
        }

        private void SpawnClassWreck(Vector3 position, Quaternion rotation, EnemyKind kind, bool player)
        {
            WreckProfile profile = Profile(kind, player);
            var root = new GameObject("ReforgedWreck_" + (player ? "Player" : kind.ToString()));
            root.transform.position = new Vector3(position.x, position.y, 0f);
            root.transform.rotation = rotation * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-profile.RotationJitter, profile.RotationJitter));

            Color charred = new Color(0.055f, 0.050f, 0.046f, 0.98f);
            Color metal = Color.Lerp(charred, profile.Accent, profile.AccentMix);
            VisualFactory.Disc("WreckShadow", root.transform, new Vector2(1.08f, 0.82f) * profile.Scale, new Color(0f, 0f, 0f, 0.48f), new Vector3(0.04f, -0.05f, 0f), -36);
            VisualFactory.Rect("TrackL", root.transform, new Vector2(0.22f, 0.91f) * profile.Scale, new Color(0.035f, 0.035f, 0.035f), new Vector3(-0.37f * profile.Scale, 0f, 0f), -34);
            VisualFactory.Rect("TrackR", root.transform, new Vector2(0.22f, 0.91f) * profile.Scale, new Color(0.035f, 0.035f, 0.035f), new Vector3(0.37f * profile.Scale, 0f, 0f), -34);
            VisualFactory.RectRotated("CollapsedHull", root.transform, new Vector2(0.68f, 0.70f) * profile.Scale, metal, Vector3.zero, UnityEngine.Random.Range(-8f, 8f), -33);
            VisualFactory.Disc("RuinedTurret", root.transform, new Vector2(0.44f, 0.41f) * profile.Scale, Color.Lerp(metal, Color.black, 0.30f), new Vector3(UnityEngine.Random.Range(-0.10f, 0.10f), UnityEngine.Random.Range(-0.08f, 0.08f), 0f), -32);
            VisualFactory.RectRotated("BrokenBarrel", root.transform, new Vector2(0.10f, 0.50f) * profile.Scale, new Color(0.11f, 0.11f, 0.10f), new Vector3(0.06f, 0.36f * profile.Scale, 0f), UnityEngine.Random.Range(-profile.BarrelJitter, profile.BarrelJitter), -31);

            root.AddComponent<ReforgedWreckLifecycle>().Initialize(profile, this);
            _wrecks.Enqueue(root);
            TrimQueue(_wrecks, ResolveWreckBudget());

            int debrisCount = Mathf.Min(profile.DebrisPieces, ResolveDebrisBudget() / 3 + 2);
            SpawnDebrisBurst(position, metal, debrisCount, profile.Scale);
            VisualFactory.RingPulse(position, new Color(profile.Accent.r, profile.Accent.g, profile.Accent.b, 0.34f), 0.55f * profile.Scale);
        }

        private void SpawnDebrisBurst(Vector3 position, Color color, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                var piece = new GameObject("ReforgedDebris");
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float distance = UnityEngine.Random.Range(0.18f, radius);
                piece.transform.position = position + new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
                piece.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 180f));
                VisualFactory.Rect("Debris", piece.transform, new Vector2(UnityEngine.Random.Range(0.07f, 0.19f), UnityEngine.Random.Range(0.04f, 0.12f)), Color.Lerp(color, Color.black, UnityEngine.Random.Range(0.12f, 0.48f)), Vector3.zero, -29);
                piece.AddComponent<ReforgedDebrisDecay>().Initialize(UnityEngine.Random.Range(7f, 15f));
                _debris.Enqueue(piece);
            }
            TrimQueue(_debris, ResolveDebrisBudget());
        }

        internal void EmitLifecycleBurst(Vector3 position, Color color, float scale, bool hot)
        {
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival && UnityEngine.Random.value > 0.42f) return;
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced && UnityEngine.Random.value > 0.70f) return;
            Color c = hot ? Color.Lerp(color, new Color(1f, 0.28f, 0.04f), 0.55f) : Color.Lerp(color, new Color(0.13f, 0.13f, 0.13f), 0.72f);
            VisualFactory.MicroBurst(position + new Vector3(UnityEngine.Random.Range(-0.20f, 0.20f), UnityEngine.Random.Range(-0.10f, 0.22f), 0f), c, scale);
        }

        private static int ResolveWreckBudget()
        {
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival) return SurvivalWreckBudget;
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced) return BalancedWreckBudget;
            return FullWreckBudget;
        }

        private static int ResolveDebrisBudget()
        {
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival) return SurvivalDebrisBudget;
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced) return BalancedDebrisBudget;
            return FullDebrisBudget;
        }

        private static void TrimQueue(Queue<GameObject> queue, int max)
        {
            while (queue.Count > max)
            {
                GameObject oldest = queue.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
        }

        private static void PruneQueue(Queue<GameObject> queue)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                GameObject item = queue.Dequeue();
                if (item != null) queue.Enqueue(item);
            }
        }

        public static WreckProfile Profile(EnemyKind kind, bool player)
        {
            if (player) return new WreckProfile(1.02f, 6, 16f, 22f, new Color(0.10f, 0.55f, 0.92f), 0.22f, 18f, 38f);
            switch (kind)
            {
                case EnemyKind.Fast: return new WreckProfile(0.84f, 4, 11f, 16f, new Color(0.90f, 0.48f, 0.08f), 0.16f, 26f, 48f);
                case EnemyKind.Sniper: return new WreckProfile(0.94f, 5, 13f, 19f, new Color(0.74f, 0.24f, 0.18f), 0.17f, 22f, 44f);
                case EnemyKind.Heavy: return new WreckProfile(1.24f, 8, 18f, 27f, new Color(0.72f, 0.15f, 0.06f), 0.20f, 14f, 34f);
                case EnemyKind.Siege: return new WreckProfile(1.34f, 9, 20f, 30f, new Color(0.86f, 0.22f, 0.04f), 0.22f, 12f, 30f);
                case EnemyKind.Elite: return new WreckProfile(1.18f, 8, 19f, 28f, new Color(0.88f, 0.08f, 0.18f), 0.24f, 18f, 38f);
                case EnemyKind.Boss: return new WreckProfile(1.68f, 12, 28f, 42f, new Color(1f, 0.08f, 0.04f), 0.28f, 10f, 28f);
                default: return new WreckProfile(0.96f, 5, 13f, 19f, new Color(0.66f, 0.18f, 0.07f), 0.15f, 20f, 42f);
            }
        }
    }

    public readonly struct WreckProfile
    {
        public readonly float Scale;
        public readonly int DebrisPieces;
        public readonly float HotLifetime;
        public readonly float TotalLifetime;
        public readonly Color Accent;
        public readonly float AccentMix;
        public readonly float RotationJitter;
        public readonly float BarrelJitter;

        public WreckProfile(float scale, int debrisPieces, float hotLifetime, float totalLifetime, Color accent, float accentMix, float rotationJitter, float barrelJitter)
        {
            Scale = scale;
            DebrisPieces = debrisPieces;
            HotLifetime = hotLifetime;
            TotalLifetime = totalLifetime;
            Accent = accent;
            AccentMix = accentMix;
            RotationJitter = rotationJitter;
            BarrelJitter = barrelJitter;
        }
    }

    public sealed class ReforgedWreckLifecycle : MonoBehaviour
    {
        private WreckProfile _profile;
        private DestructionReforgeDirector _director;
        private float _born;
        private float _nextFx;

        public void Initialize(WreckProfile profile, DestructionReforgeDirector director)
        {
            _profile = profile;
            _director = director;
            _born = Time.time;
            _nextFx = Time.time + 0.15f;
        }

        private void Update()
        {
            float age = Time.time - _born;
            if (age >= _profile.TotalLifetime)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.time < _nextFx) return;

            bool hot = age < Mathf.Min(_profile.HotLifetime, DestructionReforgeDirector.HotPhaseSeconds + _profile.Scale * 1.4f);
            bool smolder = !hot && age < DestructionReforgeDirector.HotPhaseSeconds + DestructionReforgeDirector.SmolderPhaseSeconds + _profile.Scale * 2f;
            if (!hot && !smolder) return;

            float cadence = hot ? 0.34f : 0.72f;
            if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Balanced) cadence *= 1.35f;
            else if (WarfarePerformanceGovernor.Tier == WarfarePerformanceGovernor.BudgetTier.Survival) cadence *= 2.0f;
            _nextFx = Time.time + UnityEngine.Random.Range(cadence * 0.82f, cadence * 1.18f);
            _director?.EmitLifecycleBurst(transform.position, _profile.Accent, hot ? 0.46f * _profile.Scale : 0.34f * _profile.Scale, hot);
        }
    }

    public sealed class ReforgedDebrisDecay : MonoBehaviour
    {
        private float _dieAt;
        public void Initialize(float lifetime) { _dieAt = Time.time + Mathf.Max(3f, lifetime); }
        private void Update() { if (Time.time >= _dieAt) Destroy(gameObject); }
    }
}
