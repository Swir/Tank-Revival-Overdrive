using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v1.9 DESTRUCTION & WARZONE REFORGED
    /// Observes combatants and leaves bounded, same-round battlefield scars: wrecks,
    /// craters, debris and visible Orzelek damage marks. The budget keeps late rounds stable.
    /// </summary>
    public sealed class WarzoneDestructionDirector : MonoBehaviour
    {
        public static WarzoneDestructionDirector Instance { get; private set; }

        private readonly Queue<GameObject> _wrecks = new Queue<GameObject>();
        private readonly Queue<GameObject> _craters = new Queue<GameObject>();
        private readonly Queue<GameObject> _marks = new Queue<GameObject>();

        private TankGame _game;
        private float _nextScan;

        private const int MaxWrecks = 26;
        private const int MaxCraters = 34;
        private const int MaxMarks = 52;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<WarzoneDestructionDirector>() != null) return;
            var go = new GameObject("WarzoneDestructionDirector");
            DontDestroyOnLoad(go);
            go.AddComponent<WarzoneDestructionDirector>();
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
            if (_game == null)
            {
                _game = FindAnyObjectByType<TankGame>();
                return;
            }

            if (!_game.IsPlaying || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.35f;

            var healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            foreach (var health in healths)
            {
                if (health == null || health.IsDead) continue;
                if (health.GetComponent<DestructionWitness>() != null) continue;
                health.gameObject.AddComponent<DestructionWitness>().Initialize(this, health);
            }

            CleanupDeadReferences(_wrecks);
            CleanupDeadReferences(_craters);
            CleanupDeadReferences(_marks);
        }

        public void ReportDamage(Health health, int amount)
        {
            if (health == null || amount <= 0) return;
            string name = health.gameObject.name.ToUpperInvariant();

            if (name.Contains("ORZELEK"))
            {
                SpawnCoreScar(health.transform.position, amount);
                return;
            }

            if (health.GetComponent<EnemyTank>() != null || health.GetComponent<PlayerTank>() != null)
                SpawnArmorScar(health.transform, amount);
        }

        public void ReportDeath(Health health)
        {
            if (health == null) return;

            Vector3 position = health.transform.position;
            Transform parent = health.transform.parent;
            Quaternion rotation = health.transform.rotation;
            string upperName = health.gameObject.name.ToUpperInvariant();

            if (upperName.Contains("ORZELEK"))
            {
                SpawnEagleRuin(parent, position);
                return;
            }

            EnemyTank enemy = health.GetComponent<EnemyTank>();
            PlayerTank player = health.GetComponent<PlayerTank>();
            if (enemy == null && player == null) return;

            EnemyKind kind = enemy != null ? enemy.Kind : EnemyKind.Basic;
            bool heavy = enemy != null && (kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Elite || kind == EnemyKind.Boss);
            Color accent = player != null ? new Color(0.12f, 0.62f, 0.88f) : new Color(0.72f, 0.18f, 0.08f);

            SpawnCrater(parent, position, heavy ? 1.20f : 0.80f);
            GameObject wreck = SpawnWreck(parent, position, rotation, accent, heavy);
            if (heavy && wreck != null)
            {
                var fire = wreck.AddComponent<BurningWreckHazard>();
                fire.Initialize(kind == EnemyKind.Boss ? 9f : 5.5f, kind == EnemyKind.Boss ? 1.05f : 0.78f);
            }
        }

        public static void ReportStaticImpact(Vector3 position, bool heavy)
        {
            if (Instance == null) return;
            Instance.SpawnImpactMark(position, heavy);
        }

        private GameObject SpawnWreck(Transform parent, Vector3 position, Quaternion rotation, Color accent, bool heavy)
        {
            if (parent == null) return null;
            var root = new GameObject(heavy ? "HeavyTankWreck" : "TankWreck");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = rotation * Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));

            float scale = heavy ? 1.22f : 0.96f;
            Color charred = new Color(0.075f, 0.070f, 0.065f, 0.98f);
            Color metal = Color.Lerp(charred, accent, heavy ? 0.18f : 0.12f);

            VisualFactory.Disc("WreckShadow", root.transform, new Vector2(1.08f, 0.88f) * scale, new Color(0f, 0f, 0f, 0.48f), new Vector3(0.05f, -0.05f, 0f), 0);
            VisualFactory.Rect("BurnedTrackL", root.transform, new Vector2(0.23f, 0.92f) * scale, new Color(0.045f, 0.045f, 0.045f), new Vector3(-0.38f * scale, 0f, 0f), 1);
            VisualFactory.Rect("BurnedTrackR", root.transform, new Vector2(0.23f, 0.92f) * scale, new Color(0.045f, 0.045f, 0.045f), new Vector3(0.38f * scale, 0f, 0f), 1);
            VisualFactory.RectRotated("CollapsedHull", root.transform, new Vector2(0.67f, 0.70f) * scale, metal, Vector3.zero, Random.Range(-8f, 8f), 2);
            VisualFactory.Disc("RuinedTurret", root.transform, new Vector2(0.43f, 0.40f) * scale, Color.Lerp(metal, Color.black, 0.28f), new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(-0.08f, 0.07f), 0f), 3);
            VisualFactory.RectRotated("BrokenBarrel", root.transform, new Vector2(0.10f, 0.48f) * scale, new Color(0.12f, 0.12f, 0.11f), new Vector3(0.08f, 0.36f * scale, 0f), Random.Range(-34f, 34f), 3);

            for (int i = 0; i < (heavy ? 7 : 4); i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(0.50f, heavy ? 1.05f : 0.82f);
                Vector3 local = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                VisualFactory.RectRotated("WreckDebris", root.transform, new Vector2(Random.Range(0.07f, 0.18f), Random.Range(0.04f, 0.12f)), Color.Lerp(metal, Color.black, Random.Range(0.15f, 0.55f)), local, Random.Range(0f, 180f), 2);
            }

            Track(_wrecks, root, MaxWrecks);
            return root;
        }

        private void SpawnCrater(Transform parent, Vector3 position, float scale)
        {
            if (parent == null) return;
            var root = new GameObject("CombatCrater");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            VisualFactory.Disc("CraterShadow", root.transform, new Vector2(1.34f, 0.82f) * scale, new Color(0f, 0f, 0f, 0.25f), Vector3.zero, -54);
            VisualFactory.RingObject("CraterLip", root.transform, new Vector2(1.28f, 0.78f) * scale, new Color(0.20f, 0.16f, 0.12f, 0.52f), Vector3.zero, -53);
            Track(_craters, root, MaxCraters);
        }

        private void SpawnArmorScar(Transform tank, int amount)
        {
            if (tank == null) return;
            var mark = VisualFactory.Disc("ArmorScorch", tank, new Vector2(0.12f, 0.09f) * Mathf.Clamp(amount, 1, 3), new Color(0.04f, 0.025f, 0.02f, 0.72f), new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.24f, 0.24f), 0f), 17);
            Track(_marks, mark, MaxMarks);
        }

        private void SpawnCoreScar(Vector3 position, int amount)
        {
            Transform parent = _game != null ? FindAnyObjectByType<TankGame>()?.transform : null;
            var root = new GameObject("OrzelekImpactScar");
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = position + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.20f, 0.20f), 0f);
            VisualFactory.RingObject("CoreDamageRing", root.transform, Vector2.one * (0.42f + amount * 0.08f), new Color(1f, 0.10f, 0.04f, 0.68f), Vector3.zero, 32);
            VisualFactory.Disc("CoreScorch", root.transform, Vector2.one * (0.18f + amount * 0.05f), new Color(0.08f, 0.01f, 0.01f, 0.78f), Vector3.zero, 31);
            Track(_marks, root, MaxMarks);
        }

        private void SpawnImpactMark(Vector3 position, bool heavy)
        {
            Transform parent = null;
            var game = FindAnyObjectByType<TankGame>();
            if (game != null) parent = game.transform;
            var root = new GameObject(heavy ? "HeavyImpactScar" : "ImpactScar");
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = position;
            VisualFactory.Disc("ImpactScorch", root.transform, Vector2.one * (heavy ? 0.48f : 0.25f), new Color(0.03f, 0.025f, 0.02f, heavy ? 0.42f : 0.28f), Vector3.zero, -50);
            Track(_marks, root, MaxMarks);
        }

        private void SpawnEagleRuin(Transform parent, Vector3 position)
        {
            if (parent == null) return;
            var root = new GameObject("ORZELEK_RUIN");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            VisualFactory.Disc("RuinScorch", root.transform, new Vector2(2.8f, 1.9f), new Color(0f, 0f, 0f, 0.48f), Vector3.zero, -40);
            for (int i = 0; i < 14; i++)
            {
                Vector3 local = new Vector3(Random.Range(-1.1f, 1.1f), Random.Range(-0.75f, 0.75f), 0f);
                VisualFactory.RectRotated("CoreDebris", root.transform, new Vector2(Random.Range(0.10f, 0.28f), Random.Range(0.06f, 0.19f)), new Color(0.18f, 0.14f, 0.12f), local, Random.Range(0f, 180f), 4);
            }
            Track(_wrecks, root, MaxWrecks);
        }

        private static void Track(Queue<GameObject> queue, GameObject go, int max)
        {
            if (go == null) return;
            queue.Enqueue(go);
            while (queue.Count > max)
            {
                GameObject oldest = queue.Dequeue();
                if (oldest != null) Destroy(oldest);
            }
        }

        private static void CleanupDeadReferences(Queue<GameObject> queue)
        {
            if (queue.Count == 0) return;
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                GameObject item = queue.Dequeue();
                if (item != null) queue.Enqueue(item);
            }
        }
    }

    public sealed class DestructionWitness : MonoBehaviour
    {
        private WarzoneDestructionDirector _director;
        private Health _health;

        public void Initialize(WarzoneDestructionDirector director, Health health)
        {
            _director = director;
            _health = health;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDamaged(Health health, int amount)
        {
            _director?.ReportDamage(health, amount);
        }

        private void OnDied(Health health)
        {
            _director?.ReportDeath(health);
        }

        private void OnDestroy()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }
    }
}
