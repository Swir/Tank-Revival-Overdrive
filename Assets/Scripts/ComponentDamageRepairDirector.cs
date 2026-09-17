using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v12.9 integration layer: attaches finite repair capability to canonical ArmorSystem actors,
    /// exposes player repair on R, and lets component-casualty doctrine request bounded recovery.
    /// It never moves a Rigidbody2D and never modifies Health.
    /// </summary>
    public sealed class ComponentDamageRepairDirector : MonoBehaviour
    {
        public const float DiscoveryInterval = 1.25f;
        public const float EnemyRepairSafeDistance = ComponentCasualtyTactics.MinRecoveryDistance;
        public const int MaxTrackedRepairSystems = 128;

        public static ComponentDamageRepairDirector Instance { get; private set; }
        public int TrackedSystems { get; private set; }
        public int EnemyRepairStarts { get; private set; }
        public int DiscoveryPasses { get; private set; }
        public int TacticEvaluations { get; private set; }
        public int RecoveryDecisions { get; private set; }

        private readonly EmergencyRepairSystem[] _systems = new EmergencyRepairSystem[MaxTrackedRepairSystems];
        private float _nextDiscovery;
        private PlayerTank _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ComponentDamageRepairDirector>() != null) return;
            GameObject go = new GameObject("ComponentDamageRepairDirector_v12_9");
            DontDestroyOnLoad(go);
            go.AddComponent<ComponentDamageRepairDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = Time.unscaledTime + DiscoveryInterval;
                DiscoverArmorActors();
            }

            if (_player == null) _player = FindAnyObjectByType<PlayerTank>();
            if (_player != null && Input.GetKeyDown(KeyCode.R))
            {
                EmergencyRepairSystem playerRepair = _player.GetComponent<EmergencyRepairSystem>();
                playerRepair?.TryBeginRepair();
            }

            Vector2 playerPosition = _player != null ? (Vector2)_player.transform.position : new Vector2(9999f, 9999f);
            for (int i = 0; i < TrackedSystems; i++)
            {
                EmergencyRepairSystem repair = _systems[i];
                if (repair == null) continue;
                EnemyTank enemy = repair.GetComponent<EnemyTank>();
                ArmorSystem armor = repair.GetComponent<ArmorSystem>();
                if (enemy == null || armor == null || repair.IsRepairing || repair.ChargesRemaining <= 0) continue;

                float distance = Vector2.Distance(enemy.transform.position, playerPosition);
                TacticEvaluations++;
                ComponentCasualtyTactic tactic = ComponentCasualtyTactics.Resolve(enemy.Kind, armor, distance, true);
                if (tactic != ComponentCasualtyTactic.Recover) continue;
                RecoveryDecisions++;

                Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
                if (body != null && body.linearVelocity.sqrMagnitude > EmergencyRepairSystem.MaxRepairSpeed * EmergencyRepairSystem.MaxRepairSpeed) continue;
                if (repair.TryBeginRepair()) EnemyRepairStarts++;
            }
        }

        private void DiscoverArmorActors()
        {
            DiscoveryPasses++;
            ArmorSystem[] armors = FindObjectsByType<ArmorSystem>(FindObjectsSortMode.None);
            for (int i = 0; i < armors.Length; i++)
            {
                ArmorSystem armor = armors[i];
                if (armor == null) continue;
                EmergencyRepairSystem repair = armor.GetComponent<EmergencyRepairSystem>();
                if (repair == null) repair = armor.gameObject.AddComponent<EmergencyRepairSystem>();
                Track(repair);
            }
        }

        private void Track(EmergencyRepairSystem repair)
        {
            for (int i = 0; i < TrackedSystems; i++) if (_systems[i] == repair) return;
            if (TrackedSystems >= MaxTrackedRepairSystems) return;
            _systems[TrackedSystems++] = repair;
        }

        public static bool ConfigurationValid =>
            DiscoveryInterval >= 0.75f && DiscoveryInterval <= 2f &&
            EnemyRepairSafeDistance >= 4f && MaxTrackedRepairSystems >= 64 &&
            EmergencyRepairSystem.ConfigurationValid && ComponentCasualtyTactics.ConfigurationValid;
    }
}
