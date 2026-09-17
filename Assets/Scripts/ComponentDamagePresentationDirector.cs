using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v12.9 module-state presentation bridge. Threshold and repair events reuse the fixed v12.7
    /// CinematicCombatFeedbackDirector pool and add only a six-slot module glyph pool.
    /// No Health, Projectile, Rigidbody2D, navigation or economy authority is owned here.
    /// </summary>
    [DefaultExecutionOrder(775)]
    public sealed class ComponentDamagePresentationDirector : MonoBehaviour
    {
        public const int GlyphPoolCapacity = 6;
        public const int MaxTrackedEmitters = 128;
        public const float CueLifetime = 0.82f;

        private sealed class GlyphSlot
        {
            public GameObject Root;
            public LineRenderer Line;
            public float StartedAt;
            public int Priority;
            public bool Active;
        }

        private static ComponentDamagePresentationDirector _instance;
        private readonly GlyphSlot[] _pool = new GlyphSlot[GlyphPoolCapacity];
        private Material _material;

        public static ComponentDamagePresentationDirector Instance => _instance;
        public int PublishedCues { get; private set; }
        public int DroppedCues { get; private set; }
        public TankModule LastModule { get; private set; } = TankModule.None;
        public ModuleCondition LastCondition { get; private set; } = ModuleCondition.Operational;
        public bool LastWasRepair { get; private set; }
        public int ActiveCueCount
        {
            get
            {
                int active = 0;
                for (int i = 0; i < _pool.Length; i++) if (_pool[i] != null && _pool[i].Active) active++;
                return active;
            }
        }

        public static bool ConfigurationValid =>
            GlyphPoolCapacity == 6 && MaxTrackedEmitters == 128 && CueLifetime >= 0.6f && CueLifetime <= 1.2f &&
            CinematicCombatFeedbackDirector.PoolCapacity == 16;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ComponentDamagePresentationDirector>() != null) return;
            GameObject go = new GameObject("ComponentDamagePresentationDirector_v12_9");
            DontDestroyOnLoad(go);
            go.AddComponent<ComponentDamagePresentationDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreatePool();
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _pool.Length; i++)
            {
                GlyphSlot slot = _pool[i];
                if (slot == null || !slot.Active) continue;
                float t = Mathf.Clamp01((now - slot.StartedAt) / CueLifetime);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                slot.Root.transform.localScale = Vector3.one * pulse;
                if (t >= 1f) Deactivate(slot);
            }
        }

        public static bool RequestModuleCue(ArmorSystem armor, TankModule module, int before, int after)
        {
            if (_instance == null || armor == null || module == TankModule.None || before == after) return false;
            return _instance.Emit(armor, module, before, after);
        }

        public static float PulseRatio(ModuleCondition condition, bool repair)
        {
            if (repair) return 0.65f;
            switch (condition)
            {
                case ModuleCondition.Disabled: return 0.19f;
                case ModuleCondition.Critical: return 0.39f;
                case ModuleCondition.Damaged: return 0.65f;
                default: return 0.65f;
            }
        }

        private bool Emit(ArmorSystem armor, TankModule module, int before, int after)
        {
            bool repair = after > before;
            ModuleCondition beforeCondition = ArmorSystem.ConditionFor(before);
            ModuleCondition afterCondition = ArmorSystem.ConditionFor(after);
            if (!repair && beforeCondition == afterCondition) return false;

            int priority = repair ? 4 : afterCondition == ModuleCondition.Disabled ? 6 : afterCondition == ModuleCondition.Critical ? 5 : 3;
            if (!TryAcquire(priority, out GlyphSlot slot)) { DroppedCues++; return false; }

            Vector3 point = ModuleWorldPoint(armor.transform, module);
            CinematicCombatFeedbackDirector.RequestDamagePulse(point, PulseRatio(afterCondition, repair));
            ConfigureGlyph(slot, point, module, afterCondition, repair, priority);
            PublishedCues++;
            LastModule = module;
            LastCondition = afterCondition;
            LastWasRepair = repair;
            return true;
        }

        private static Vector3 ModuleWorldPoint(Transform actor, TankModule module)
        {
            Vector3 local = module == TankModule.Engine ? new Vector3(0f, -0.34f, 0f) :
                module == TankModule.Tracks ? new Vector3(0.34f, -0.02f, 0f) :
                module == TankModule.Gun ? new Vector3(0f, 0.42f, 0f) : new Vector3(-0.20f, 0.04f, 0f);
            return actor.TransformPoint(local);
        }

        private void CreatePool()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _material = shader != null ? new Material(shader) : null;
            for (int i = 0; i < _pool.Length; i++)
            {
                GameObject root = new GameObject("ComponentGlyph_" + i);
                root.transform.SetParent(transform, false);
                root.SetActive(false);
                LineRenderer line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = 0.045f;
                line.numCapVertices = 1;
                line.sortingOrder = 82;
                if (_material != null) line.sharedMaterial = _material;
                _pool[i] = new GlyphSlot { Root = root, Line = line };
            }
        }

        private bool TryAcquire(int priority, out GlyphSlot slot)
        {
            slot = null;
            GlyphSlot replacement = null;
            for (int i = 0; i < _pool.Length; i++)
            {
                GlyphSlot candidate = _pool[i];
                if (!candidate.Active) { if (slot == null) slot = candidate; continue; }
                if (replacement == null || candidate.Priority < replacement.Priority ||
                    (candidate.Priority == replacement.Priority && candidate.StartedAt < replacement.StartedAt)) replacement = candidate;
            }
            if (slot != null) return true;
            if (replacement != null && priority > replacement.Priority)
            {
                Deactivate(replacement);
                slot = replacement;
                return true;
            }
            return false;
        }

        private void ConfigureGlyph(GlyphSlot slot, Vector3 point, TankModule module, ModuleCondition condition, bool repair, int priority)
        {
            slot.Root.transform.position = point;
            slot.Root.transform.localScale = Vector3.one;
            slot.Root.SetActive(true);
            slot.Active = true;
            slot.StartedAt = Time.unscaledTime;
            slot.Priority = priority;

            Color color = repair ? new Color(0.18f, 1f, 0.46f, 0.96f) :
                module == TankModule.Engine ? new Color(1f, 0.34f, 0.06f, 0.96f) :
                module == TankModule.Tracks ? new Color(1f, 0.72f, 0.10f, 0.96f) :
                module == TankModule.Gun ? new Color(1f, 0.12f, 0.05f, 0.96f) : new Color(1f, 0.06f, 0.34f, 0.96f);
            if (!repair && condition == ModuleCondition.Disabled) color = Color.Lerp(color, Color.white, 0.20f);
            slot.Line.startColor = color;
            slot.Line.endColor = color;
            slot.Line.widthMultiplier = condition == ModuleCondition.Disabled ? 0.060f : 0.045f;
            SetGlyphShape(slot.Line, module, repair);
        }

        private static void SetGlyphShape(LineRenderer line, TankModule module, bool repair)
        {
            if (repair)
            {
                line.loop = false; line.positionCount = 4;
                line.SetPosition(0, new Vector3(-.20f, 0f, 0f)); line.SetPosition(1, new Vector3(.20f, 0f, 0f));
                line.SetPosition(2, Vector3.zero); line.SetPosition(3, new Vector3(0f, .20f, 0f));
                return;
            }
            line.loop = true;
            if (module == TankModule.Engine)
            {
                line.positionCount = 3; line.SetPosition(0, new Vector3(0f, .22f, 0f)); line.SetPosition(1, new Vector3(.20f, -.18f, 0f)); line.SetPosition(2, new Vector3(-.20f, -.18f, 0f));
            }
            else if (module == TankModule.Tracks)
            {
                line.positionCount = 4; line.SetPosition(0, new Vector3(-.24f, .12f, 0f)); line.SetPosition(1, new Vector3(.24f, .12f, 0f)); line.SetPosition(2, new Vector3(.24f, -.12f, 0f)); line.SetPosition(3, new Vector3(-.24f, -.12f, 0f));
            }
            else if (module == TankModule.Gun)
            {
                line.loop = false; line.positionCount = 3; line.SetPosition(0, new Vector3(-.22f, -.15f, 0f)); line.SetPosition(1, new Vector3(0f, .22f, 0f)); line.SetPosition(2, new Vector3(.22f, -.15f, 0f));
            }
            else
            {
                line.positionCount = 4; line.SetPosition(0, new Vector3(0f, .23f, 0f)); line.SetPosition(1, new Vector3(.23f, 0f, 0f)); line.SetPosition(2, new Vector3(0f, -.23f, 0f)); line.SetPosition(3, new Vector3(-.23f, 0f, 0f));
            }
        }

        private static void Deactivate(GlyphSlot slot)
        {
            slot.Active = false;
            slot.Priority = 0;
            slot.Root.SetActive(false);
        }
    }

    /// <summary>Zero-authority event bridge attached only to already-authoritative ArmorSystem actors.</summary>
    public sealed class ComponentDamagePresentationEmitter : MonoBehaviour
    {
        private ArmorSystem _armor;

        private void Awake() { _armor = GetComponent<ArmorSystem>(); }
        private void OnEnable()
        {
            if (_armor == null) _armor = GetComponent<ArmorSystem>();
            if (_armor != null) _armor.ModuleIntegrityChanged += OnModuleIntegrityChanged;
        }
        private void OnDisable()
        {
            if (_armor != null) _armor.ModuleIntegrityChanged -= OnModuleIntegrityChanged;
        }
        private void OnModuleIntegrityChanged(TankModule module, int before, int after)
        {
            ComponentDamagePresentationDirector.RequestModuleCue(_armor, module, before, after);
        }
    }
}
