using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v5.2 production presentation pass. Adds non-authoritative, collider-free class signatures
    /// to existing enemy tanks and scales optional detail with WarfarePerformanceGovernor.
    /// Combat hitboxes, Health, AI, movement and spawn authority are never modified.
    /// </summary>
    [DefaultExecutionOrder(9100)]
    public sealed class ProductionPresentationDirector : MonoBehaviour
    {
        public static ProductionPresentationDirector Instance { get; private set; }

        private readonly Dictionary<int, EnemyClassSignature> _signatures = new Dictionary<int, EnemyClassSignature>();
        private float _nextScan;
        private WarfarePerformanceGovernor.BudgetTier _tier;

        public int InstalledSignatureCount => _signatures.Count;
        public int RequiredAudioAssetCount => 1;
        public bool HeavyCannonAssetAvailable => Resources.Load<AudioClip>("TankRevivalProduction/Audio/HeavyCannon") != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ProductionPresentationDirector>() != null) return;
            GameObject go = new GameObject("ProductionPresentationDirector_v5_2");
            DontDestroyOnLoad(go);
            go.AddComponent<ProductionPresentationDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _tier = WarfarePerformanceGovernor.Tier;
        }

        private void OnEnable()
        {
            WarfarePerformanceGovernor.BudgetChanged -= OnBudgetChanged;
            WarfarePerformanceGovernor.BudgetChanged += OnBudgetChanged;
        }

        private void OnDisable()
        {
            WarfarePerformanceGovernor.BudgetChanged -= OnBudgetChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + (_tier == WarfarePerformanceGovernor.BudgetTier.Survival ? 1.4f : 0.65f);
            RefreshEnemySignatures();
        }

        private void OnBudgetChanged(WarfarePerformanceGovernor.BudgetTier tier)
        {
            _tier = tier;
            foreach (EnemyClassSignature signature in _signatures.Values)
                if (signature != null) signature.ApplyBudget(tier);
        }

        private void RefreshEnemySignatures()
        {
            foreach (EnemyTank enemy in CombatRoster.Enemies)
            {
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;

                int id = enemy.GetInstanceID();
                if (_signatures.ContainsKey(id) && _signatures[id] != null) continue;

                EnemyClassSignature signature = enemy.GetComponent<EnemyClassSignature>();
                if (signature == null) signature = enemy.gameObject.AddComponent<EnemyClassSignature>();
                signature.Configure(enemy.Kind, _tier);
                _signatures[id] = signature;
            }

            if (_signatures.Count < 48) return;
            List<int> stale = null;
            foreach (KeyValuePair<int, EnemyClassSignature> pair in _signatures)
            {
                if (pair.Value != null) continue;
                if (stale == null) stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) _signatures.Remove(stale[i]);
        }
    }

    public sealed class EnemyClassSignature : MonoBehaviour
    {
        private GameObject _root;
        private readonly List<GameObject> _optional = new List<GameObject>();
        private bool _configured;

        public EnemyKind Kind { get; private set; }
        public bool IsConfigured => _configured;

        public void Configure(EnemyKind kind, WarfarePerformanceGovernor.BudgetTier tier)
        {
            if (_configured && Kind == kind)
            {
                ApplyBudget(tier);
                return;
            }

            if (_root != null) Destroy(_root);
            _optional.Clear();
            Kind = kind;
            _configured = true;

            _root = new GameObject("v5.2_ClassSignature_" + kind);
            _root.transform.SetParent(transform, false);
            _root.transform.localPosition = new Vector3(0f, 0f, -0.08f);

            Color primary;
            Color accent;
            GetPalette(kind, out primary, out accent);

            switch (kind)
            {
                case EnemyKind.Fast:
                    AddBlock("SpeedFinL", new Vector3(-0.48f, 0.12f, -0.03f), new Vector3(0.13f, 0.62f, 0.10f), accent, false);
                    AddBlock("SpeedFinR", new Vector3(0.48f, 0.12f, -0.03f), new Vector3(0.13f, 0.62f, 0.10f), accent, false);
                    AddBlock("ScoutMast", new Vector3(0f, 0.28f, -0.06f), new Vector3(0.08f, 0.55f, 0.08f), primary, true);
                    break;
                case EnemyKind.Heavy:
                    AddBlock("HeavySkirtL", new Vector3(-0.52f, -0.04f, -0.04f), new Vector3(0.22f, 0.92f, 0.12f), primary, false);
                    AddBlock("HeavySkirtR", new Vector3(0.52f, -0.04f, -0.04f), new Vector3(0.22f, 0.92f, 0.12f), primary, false);
                    AddBlock("HeavyMantlet", new Vector3(0f, 0.25f, -0.07f), new Vector3(0.62f, 0.20f, 0.14f), accent, true);
                    break;
                case EnemyKind.Sniper:
                    AddBlock("Rangefinder", new Vector3(0f, 0.43f, -0.07f), new Vector3(0.76f, 0.10f, 0.10f), accent, false);
                    AddBlock("RangefinderEyeL", new Vector3(-0.34f, 0.43f, -0.10f), new Vector3(0.10f, 0.18f, 0.08f), primary, true);
                    AddBlock("RangefinderEyeR", new Vector3(0.34f, 0.43f, -0.10f), new Vector3(0.10f, 0.18f, 0.08f), primary, true);
                    break;
                case EnemyKind.Siege:
                    AddBlock("SiegePlateL", new Vector3(-0.50f, 0.05f, -0.05f), new Vector3(0.28f, 0.98f, 0.14f), primary, false);
                    AddBlock("SiegePlateR", new Vector3(0.50f, 0.05f, -0.05f), new Vector3(0.28f, 0.98f, 0.14f), primary, false);
                    AddBlock("SiegeAmmoRack", new Vector3(0f, -0.48f, -0.06f), new Vector3(0.64f, 0.28f, 0.13f), accent, true);
                    break;
                case EnemyKind.Elite:
                    AddBlock("EliteCommandDeck", new Vector3(0f, 0.22f, -0.07f), new Vector3(0.72f, 0.26f, 0.14f), primary, false);
                    AddBlock("EliteAntennaL", new Vector3(-0.29f, 0.54f, -0.07f), new Vector3(0.06f, 0.58f, 0.06f), accent, true);
                    AddBlock("EliteAntennaR", new Vector3(0.29f, 0.54f, -0.07f), new Vector3(0.06f, 0.58f, 0.06f), accent, true);
                    break;
                case EnemyKind.Supply:
                    AddBlock("SupplyBox", new Vector3(0f, -0.08f, -0.06f), new Vector3(0.72f, 0.62f, 0.13f), primary, false);
                    AddBlock("SupplyMarkV", new Vector3(0f, -0.08f, -0.09f), new Vector3(0.13f, 0.46f, 0.05f), accent, false);
                    AddBlock("SupplyMarkH", new Vector3(0f, -0.08f, -0.09f), new Vector3(0.46f, 0.13f, 0.05f), accent, false);
                    break;
                case EnemyKind.Boss:
                    AddBlock("BossPodL", new Vector3(-0.62f, 0.10f, -0.05f), new Vector3(0.30f, 0.92f, 0.16f), primary, false);
                    AddBlock("BossPodR", new Vector3(0.62f, 0.10f, -0.05f), new Vector3(0.30f, 0.92f, 0.16f), primary, false);
                    AddBlock("BossCrown", new Vector3(0f, 0.50f, -0.08f), new Vector3(0.82f, 0.18f, 0.15f), accent, false);
                    AddBlock("BossCore", new Vector3(0f, 0.18f, -0.10f), new Vector3(0.28f, 0.28f, 0.08f), Color.white, true);
                    break;
                default:
                    AddBlock("BasicArmorBand", new Vector3(0f, 0.03f, -0.06f), new Vector3(0.78f, 0.13f, 0.12f), accent, true);
                    break;
            }

            ApplyBudget(tier);
        }

        public void ApplyBudget(WarfarePerformanceGovernor.BudgetTier tier)
        {
            bool allowOptional = tier != WarfarePerformanceGovernor.BudgetTier.Survival;
            for (int i = 0; i < _optional.Count; i++)
                if (_optional[i] != null) _optional[i].SetActive(allowOptional);
        }

        private void AddBlock(string name, Vector3 localPosition, Vector3 localScale, Color color, bool optional)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(_root.transform, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;

            Collider collider = block.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = block.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.color = color;
                    renderer.sharedMaterial = material;
                }
            }

            if (optional) _optional.Add(block);
        }

        private static void GetPalette(EnemyKind kind, out Color primary, out Color accent)
        {
            switch (kind)
            {
                case EnemyKind.Fast: primary = new Color(0.95f, 0.58f, 0.08f); accent = new Color(1f, 0.92f, 0.34f); break;
                case EnemyKind.Heavy: primary = new Color(0.34f, 0.12f, 0.50f); accent = new Color(0.82f, 0.42f, 1f); break;
                case EnemyKind.Sniper: primary = new Color(0.06f, 0.40f, 0.24f); accent = new Color(0.44f, 1f, 0.70f); break;
                case EnemyKind.Siege: primary = new Color(0.20f, 0.22f, 0.26f); accent = new Color(1f, 0.30f, 0.08f); break;
                case EnemyKind.Elite: primary = new Color(0.10f, 0.22f, 0.58f); accent = new Color(0.20f, 0.88f, 1f); break;
                case EnemyKind.Supply: primary = new Color(0.10f, 0.46f, 0.54f); accent = new Color(0.78f, 1f, 0.92f); break;
                case EnemyKind.Boss: primary = new Color(0.22f, 0.04f, 0.05f); accent = new Color(1f, 0.16f, 0.08f); break;
                default: primary = new Color(0.62f, 0.16f, 0.09f); accent = new Color(1f, 0.50f, 0.16f); break;
            }
        }
    }
}
