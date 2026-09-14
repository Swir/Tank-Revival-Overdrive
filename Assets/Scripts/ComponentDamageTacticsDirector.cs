using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// v11.1 bridge between authoritative ArmorSystem component states and v11.0 tactical navigation.
    /// It never moves or damages units directly: it only issues bounded orders to existing
    /// TacticalNavigationAgent instances after meaningful component damage is observed.
    /// </summary>
    [DefaultExecutionOrder(22520)]
    public sealed class ComponentDamageTacticsDirector : MonoBehaviour
    {
        public const int MaxManagedDamagedUnits = 8;
        public const int MaxProtectionEscorts = 4;
        public const int MaxRearExploitFlankers = 2;
        public const float DecisionCadence = 0.30f;
        public const float RearExposureWindow = 3.5f;
        public const float CrippledHoldRadius = 1.15f;
        public const float WeaponRetreatDistance = 3.2f;

        private static ComponentDamageTacticsDirector _instance;
        private TankGame _game;
        private float _nextDecision;
        private int _managedDamaged;
        private int _activeEscorts;
        private int _rearExploiters;

        public static ComponentDamageTacticsDirector Instance => _instance;
        public int ManagedDamagedUnits => _managedDamaged;
        public int ActiveProtectionEscorts => _activeEscorts;
        public int RearExploiters => _rearExploiters;

        public static bool ConfigurationValid =>
            MaxManagedDamagedUnits >= 4 && MaxManagedDamagedUnits <= 12 &&
            MaxProtectionEscorts >= 2 && MaxProtectionEscorts <= 6 &&
            MaxRearExploitFlankers >= 1 && MaxRearExploitFlankers <= 3 &&
            DecisionCadence >= 0.25f && DecisionCadence <= 0.50f &&
            RearExposureWindow >= 2.0f && RearExposureWindow <= 5.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ComponentDamageTacticsDirector>() != null) return;
            GameObject go = new GameObject("ComponentDamageTacticsDirector_v11_1");
            DontDestroyOnLoad(go);
            go.AddComponent<ComponentDamageTacticsDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _nextDecision = Time.time + 0.8f;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying || Time.time < _nextDecision) return;
            _nextDecision = Time.time + DecisionCadence;
            IssueDamageAwareOrders();
        }

        private void IssueDamageAwareOrders()
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            var damaged = new List<EnemyTank>(MaxManagedDamagedUnits);
            for (int i = 0; i < enemies.Length && damaged.Count < MaxManagedDamagedUnits; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!Eligible(enemy)) continue;
                ArmorSystem armor = enemy.GetComponent<ArmorSystem>();
                if (armor == null) continue;
                if (armor.IsMobilityCritical || armor.IsWeaponCritical)
                    damaged.Add(enemy);
            }

            _managedDamaged = damaged.Count;
            _activeEscorts = 0;
            var claimedEscorts = new HashSet<int>();

            for (int i = 0; i < damaged.Count; i++)
            {
                EnemyTank crippled = damaged[i];
                ArmorSystem armor = crippled.GetComponent<ArmorSystem>();
                TacticalNavigationAgent agent = EnsureAgent(crippled);
                if (agent == null) continue;

                Vector2 position = crippled.transform.position;
                if (armor.IsMobilityKilled)
                {
                    agent.SetRole(SquadTacticalRole.Vanguard);
                    agent.SetOrder(position, CrippledHoldRadius, 0.72f, enemies);
                    if ((crippled.Kind == EnemyKind.Heavy || crippled.Kind == EnemyKind.Siege || crippled.Kind == EnemyKind.Elite) && _activeEscorts < MaxProtectionEscorts)
                    {
                        EnemyTank escort = FindNearestHealthyEscort(position, enemies, claimedEscorts);
                        if (escort != null)
                        {
                            claimedEscorts.Add(escort.GetInstanceID());
                            TacticalNavigationAgent escortAgent = EnsureAgent(escort);
                            if (escortAgent != null)
                            {
                                escortAgent.SetRole(SquadTacticalRole.Escort);
                                escortAgent.SetOrder(position, 1.45f, 0.94f, enemies);
                                _activeEscorts++;
                            }
                        }
                    }
                }
                else if (armor.IsWeaponDisabled)
                {
                    Vector2 away = position - _game.PlayerPosition;
                    if (away.sqrMagnitude < 0.05f) away = Vector2.up;
                    Vector2 retreat = position + away.normalized * WeaponRetreatDistance;
                    agent.SetRole(SquadTacticalRole.Escort);
                    agent.SetOrder(retreat, 1.0f, 1.04f, enemies);
                }
                else if (armor.IsMobilityCritical || armor.IsWeaponCritical)
                {
                    agent.SetRole(SquadTacticalRole.Escort);
                    agent.SetOrder(position, 1.35f, 0.84f, enemies);
                }
            }

            IssueRearExposureExploit(enemies, claimedEscorts);
        }

        private void IssueRearExposureExploit(EnemyTank[] enemies, HashSet<int> claimedEscorts)
        {
            _rearExploiters = 0;
            PlayerTank player = FindAnyObjectByType<PlayerTank>();
            if (player == null) return;
            ArmorSystem playerArmor = player.GetComponent<ArmorSystem>();
            if (!ShouldExploitRear(playerArmor, Time.time)) return;

            Vector2 playerPos = player.transform.position;
            Vector2 playerForward = player.transform.up;
            if (playerForward.sqrMagnitude < 0.1f) playerForward = Vector2.up;
            Vector2 rearPoint = playerPos - playerForward.normalized * 3.3f;

            for (int i = 0; i < enemies.Length && _rearExploiters < MaxRearExploitFlankers; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!Eligible(enemy) || claimedEscorts.Contains(enemy.GetInstanceID())) continue;
                if (enemy.Kind != EnemyKind.Fast && enemy.Kind != EnemyKind.Elite && enemy.Kind != EnemyKind.Sniper) continue;
                ArmorSystem armor = enemy.GetComponent<ArmorSystem>();
                if (armor != null && (armor.IsMobilityCritical || armor.IsWeaponDisabled)) continue;

                TacticalNavigationAgent agent = EnsureAgent(enemy);
                if (agent == null) continue;
                Vector2 side = new Vector2(-playerForward.y, playerForward.x) * (_rearExploiters == 0 ? 1.8f : -1.8f);
                agent.SetRole(SquadTacticalRole.Flanker);
                agent.SetOrder(rearPoint + side, 1.35f, 1.16f, enemies);
                _rearExploiters++;
            }
        }

        public static bool ShouldExploitRear(ArmorSystem armor, float now)
        {
            if (armor == null) return false;
            if (now - armor.LastImpactAt > RearExposureWindow) return false;
            return armor.LastZone == ArmorZone.Side || armor.LastZone == ArmorZone.Rear;
        }

        public static bool IsPriorityProtectionTarget(EnemyKind kind, ArmorSystem armor)
        {
            if (armor == null || !armor.IsMobilityKilled) return false;
            return kind == EnemyKind.Heavy || kind == EnemyKind.Siege || kind == EnemyKind.Elite;
        }

        private static bool Eligible(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead &&
                   enemy.Kind != EnemyKind.Boss && enemy.Kind != EnemyKind.Supply;
        }

        private static TacticalNavigationAgent EnsureAgent(EnemyTank enemy)
        {
            if (enemy == null) return null;
            TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
            if (agent == null)
            {
                agent = enemy.gameObject.AddComponent<TacticalNavigationAgent>();
                agent.Initialize(enemy);
            }
            return agent;
        }

        private static EnemyTank FindNearestHealthyEscort(Vector2 from, EnemyTank[] enemies, HashSet<int> claimed)
        {
            EnemyTank best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!Eligible(enemy) || claimed.Contains(enemy.GetInstanceID())) continue;
                if (enemy.Kind == EnemyKind.Siege) continue;
                ArmorSystem armor = enemy.GetComponent<ArmorSystem>();
                if (armor != null && (armor.IsMobilityCritical || armor.IsWeaponCritical)) continue;
                float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (sqr < 0.16f || sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }
    }
}
