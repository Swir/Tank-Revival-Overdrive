using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum PlatoonCombatState
    {
        Formed,
        Suppressed,
        Regrouping
    }

    [DefaultExecutionOrder(22440)]
    public sealed class TacticalCombatReforgeDirector : MonoBehaviour
    {
        private sealed class MemberState
        {
            public EnemyTank Enemy;
            public int PlatoonIndex;
            public int Slot;
            public int LastHealth;
            public float Suppression;
            public float LastDamageAt;
            public SquadTacticalRole Role;
        }

        private sealed class PlatoonState
        {
            public readonly List<MemberState> Members = new List<MemberState>(MaxMembersPerPlatoon);
            public EnemyTank Leader;
            public int LeaderId;
            public float CohesionBrokenUntil;
            public int Losses;
        }

        public const int MaxPlatoons = 3;
        public const int MaxMembersPerPlatoon = 6;
        public const int MaxManagedCombatants = MaxPlatoons * MaxMembersPerPlatoon;
        public const float CommandCadence = 0.34f;
        public const float DamageScanCadence = 0.18f;
        public const float SuppressionPerDamage = 0.72f;
        public const float SuppressionThreshold = 0.70f;
        public const float SuppressionMaximum = 3.0f;
        public const float SuppressionDecayPerSecond = 0.36f;
        public const float RelocationSeconds = 2.8f;
        public const float LeaderLossRegroupSeconds = 2.4f;
        public const float PlatoonSpacing = 1.35f;

        private static TacticalCombatReforgeDirector _instance;
        private readonly List<PlatoonState> _platoons = new List<PlatoonState>(MaxPlatoons);
        private readonly Dictionary<EnemyTank, MemberState> _members = new Dictionary<EnemyTank, MemberState>(MaxManagedCombatants);
        private readonly int[] _previousLeaderIds = new int[MaxPlatoons];
        private TankGame _game;
        private int _lastRevision = -1;
        private int _lastRound = -1;
        private float _nextCommandAt;
        private float _nextDamageScanAt;
        private float _lastSuppressionTick;
        private int _suppressedCount;
        private int _cohesionBreakCount;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public static TacticalCombatReforgeDirector Instance => _instance;
        public int ActivePlatoons => _platoons.Count;
        public int ManagedCombatants => _members.Count;
        public int SuppressedCombatants => _suppressedCount;
        public int CohesionBreakCount => _cohesionBreakCount;

        public static bool ConfigurationValid =>
            MaxPlatoons >= 2 && MaxPlatoons <= 4 &&
            MaxMembersPerPlatoon >= 4 && MaxMembersPerPlatoon <= 8 &&
            MaxManagedCombatants <= 24 &&
            CommandCadence >= 0.25f && CommandCadence <= 0.50f &&
            DamageScanCadence >= 0.12f && DamageScanCadence <= 0.30f &&
            SuppressionThreshold > 0f && SuppressionThreshold < SuppressionMaximum &&
            SuppressionDecayPerSecond >= 0.20f && SuppressionDecayPerSecond <= 0.60f &&
            LeaderLossRegroupSeconds >= 1.5f && LeaderLossRegroupSeconds <= 3.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalCombatReforgeDirector>() != null) return;
            GameObject go = new GameObject("TacticalCombatReforgeDirector_v11_0");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalCombatReforgeDirector>();
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
            _nextCommandAt = Time.time + 0.6f;
            _nextDamageScanAt = Time.time + 0.35f;
            _lastSuppressionTick = Time.time;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_game == null) _game = FindAnyObjectByType<TankGame>();
            if (_game == null || !_game.IsPlaying) return;

            int round = Mathf.Clamp(_game.CurrentRound, 1, 100);
            if (_lastRevision != RuntimeBattleRegistry.Revision || _lastRound != round)
            {
                RebuildPlatoons(round);
                _lastRevision = RuntimeBattleRegistry.Revision;
                _lastRound = round;
            }

            if (Time.time >= _nextDamageScanAt)
            {
                float dt = Mathf.Max(0.01f, Time.time - _lastSuppressionTick);
                _lastSuppressionTick = Time.time;
                _nextDamageScanAt = Time.time + DamageScanCadence;
                ScanDamageAndSuppression(dt);
            }

            if (Time.time >= _nextCommandAt)
            {
                _nextCommandAt = Time.time + CommandCadence;
                IssuePlatoonOrders(round);
            }
        }

        private void RebuildPlatoons(int round)
        {
            for (int i = 0; i < MaxPlatoons; i++)
                _previousLeaderIds[i] = i < _platoons.Count ? _platoons[i].LeaderId : 0;

            Dictionary<EnemyTank, MemberState> previous = new Dictionary<EnemyTank, MemberState>(_members);
            _members.Clear();
            _platoons.Clear();

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null) return;

            int accepted = 0;
            for (int i = 0; i < enemies.Length && accepted < MaxManagedCombatants; i++)
            {
                EnemyTank enemy = enemies[i];
                if (!IsEligible(enemy)) continue;
                int platoonIndex = accepted / MaxMembersPerPlatoon;
                while (_platoons.Count <= platoonIndex) _platoons.Add(new PlatoonState());

                MemberState state;
                if (!previous.TryGetValue(enemy, out state) || state == null)
                {
                    state = new MemberState
                    {
                        Enemy = enemy,
                        LastHealth = enemy.Health.Current,
                        Suppression = 0f,
                        LastDamageAt = -100f
                    };
                }
                state.PlatoonIndex = platoonIndex;
                state.Slot = _platoons[platoonIndex].Members.Count;
                _platoons[platoonIndex].Members.Add(state);
                _members[enemy] = state;
                accepted++;
            }

            for (int p = 0; p < _platoons.Count; p++)
            {
                PlatoonState platoon = _platoons[p];
                platoon.Leader = SelectLeader(platoon.Members);
                platoon.LeaderId = platoon.Leader != null ? platoon.Leader.GetInstanceID() : 0;
                bool leaderLost = _previousLeaderIds[p] != 0 && _previousLeaderIds[p] != platoon.LeaderId && !ContainsInstanceId(enemies, _previousLeaderIds[p]);
                if (leaderLost)
                {
                    platoon.CohesionBrokenUntil = Time.time + LeaderLossRegroupSeconds;
                    platoon.Losses++;
                    _cohesionBreakCount++;
                    if (platoon.Leader != null)
                        VisualFactory.RingPulse(platoon.Leader.transform.position, new Color(1f, 0.55f, 0.08f), 0.92f);
                }

                for (int m = 0; m < platoon.Members.Count; m++)
                {
                    MemberState state = platoon.Members[m];
                    state.Role = RoleForKind(state.Enemy.Kind, m, state.Enemy == platoon.Leader, round);
                }
            }
        }

        private void ScanDamageAndSuppression(float dt)
        {
            int suppressed = 0;
            foreach (KeyValuePair<EnemyTank, MemberState> kv in _members)
            {
                MemberState state = kv.Value;
                EnemyTank enemy = state.Enemy;
                if (!IsEligible(enemy)) continue;

                int hp = enemy.Health.Current;
                int damage = Mathf.Max(0, state.LastHealth - hp);
                if (damage > 0)
                {
                    state.Suppression = SuppressionAfterDamage(state.Suppression, damage);
                    state.LastDamageAt = Time.time;
                    VisualFactory.RingPulse(enemy.transform.position, new Color(1f, 0.72f, 0.12f), 0.52f);
                }
                else
                {
                    state.Suppression = SuppressionAfterDecay(state.Suppression, dt);
                }
                state.LastHealth = hp;
                if (IsSuppressed(state.Suppression, Time.time - state.LastDamageAt)) suppressed++;
            }
            _suppressedCount = suppressed;
        }

        private void IssuePlatoonOrders(int round)
        {
            EnemyTank[] neighbors = RuntimeBattleRegistry.EnemySnapshot;
            if (neighbors == null || neighbors.Length == 0) return;
            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;
            float progress = (round - 1f) / 99f;

            for (int p = 0; p < _platoons.Count; p++)
            {
                PlatoonState platoon = _platoons[p];
                if (platoon.Leader == null || platoon.Leader.Health == null || platoon.Leader.Health.IsDead) continue;
                bool regrouping = Time.time < platoon.CohesionBrokenUntil;
                Vector2 leaderPos = platoon.Leader.transform.position;
                Vector2 centroid = PlatoonCentroid(platoon);

                for (int m = 0; m < platoon.Members.Count; m++)
                {
                    MemberState state = platoon.Members[m];
                    EnemyTank enemy = state.Enemy;
                    if (!IsEligible(enemy) || ShouldYieldToHighCommand(enemy)) continue;

                    TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                    if (agent == null)
                    {
                        agent = enemy.gameObject.AddComponent<TacticalNavigationAgent>();
                        agent.Initialize(enemy);
                    }

                    bool suppressed = IsSuppressed(state.Suppression, Time.time - state.LastDamageAt);
                    Vector2 objective;
                    float standoff;
                    float speedScale;
                    SquadTacticalRole role = state.Role;

                    if (regrouping)
                    {
                        role = SquadTacticalRole.Escort;
                        objective = leaderPos + FormationOffset(m, p) * 0.55f;
                        standoff = 0.95f;
                        speedScale = 1.06f;
                    }
                    else if (suppressed)
                    {
                        role = SquadTacticalRole.Escort;
                        Vector2 away = ((Vector2)enemy.transform.position - player).normalized;
                        if (away.sqrMagnitude < 0.2f) away = Vector2.up;
                        Vector2 side = new Vector2(-away.y, away.x) * ((((enemy.GetInstanceID() >> 1) & 1) == 0) ? 1f : -1f);
                        objective = (Vector2)enemy.transform.position + away * 2.0f + side * 1.5f;
                        standoff = 0.9f;
                        speedScale = 1.14f;
                    }
                    else
                    {
                        BuildRoleOrder(enemy, role, p, m, player, eagle, centroid, progress, out objective, out standoff, out speedScale);
                    }

                    TerrainIntelligenceDirector terrain = TerrainIntelligenceDirector.Instance;
                    if (terrain != null && terrain.TryRefineOrder(enemy, role, objective, standoff, neighbors, out Vector2 refined, out float refinedStandoff))
                    {
                        objective = refined;
                        standoff = refinedStandoff;
                    }

                    agent.SetRole(role);
                    agent.SetOrder(objective, standoff, speedScale, neighbors);
                }
            }
        }

        private static void BuildRoleOrder(EnemyTank enemy, SquadTacticalRole role, int platoonIndex, int slot, Vector2 player, Vector2 eagle, Vector2 centroid, float progress, out Vector2 objective, out float standoff, out float speedScale)
        {
            Vector2 offset = FormationOffset(slot, platoonIndex);
            objective = player + offset;
            standoff = 1.8f;
            speedScale = 1f;

            switch (role)
            {
                case SquadTacticalRole.Flanker:
                {
                    Vector2 toPlayer = player - (Vector2)enemy.transform.position;
                    Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized;
                    float sign = ((enemy.GetInstanceID() ^ platoonIndex) & 1) == 0 ? 1f : -1f;
                    objective = player + side * sign * Mathf.Lerp(3.4f, 4.8f, progress) + offset * 0.35f;
                    standoff = 1.35f;
                    speedScale = 1.14f;
                    break;
                }
                case SquadTacticalRole.Suppressor:
                    objective = player + offset * 0.30f;
                    standoff = enemy.Kind == EnemyKind.Sniper ? Mathf.Lerp(5.8f, 7.1f, progress) : 4.0f;
                    speedScale = 0.84f;
                    break;
                case SquadTacticalRole.Breaker:
                    objective = eagle + offset * 0.22f;
                    standoff = enemy.Kind == EnemyKind.Siege ? 2.1f : 1.2f;
                    speedScale = enemy.Kind == EnemyKind.Heavy ? 0.88f : 0.96f;
                    break;
                case SquadTacticalRole.Escort:
                    objective = centroid + offset * 0.45f;
                    standoff = 1.25f;
                    speedScale = 0.92f;
                    break;
                case SquadTacticalRole.Hunter:
                    objective = player + offset * 0.18f;
                    standoff = 1.35f;
                    speedScale = 1.16f;
                    break;
                default:
                    objective = Vector2.Lerp(player, eagle, 0.18f + platoonIndex * 0.06f) + offset * 0.35f;
                    standoff = enemy.Kind == EnemyKind.Heavy ? 2.0f : 1.55f;
                    speedScale = enemy.Kind == EnemyKind.Heavy ? 0.90f : 1.0f;
                    break;
            }
        }

        private static Vector2 FormationOffset(int slot, int platoonIndex)
        {
            int rank = slot / 2;
            float side = (slot & 1) == 0 ? -1f : 1f;
            return new Vector2(side * PlatoonSpacing * (0.65f + rank * 0.16f), (rank - 1) * PlatoonSpacing + (platoonIndex - 1) * 0.30f);
        }

        private static Vector2 PlatoonCentroid(PlatoonState platoon)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < platoon.Members.Count; i++)
            {
                EnemyTank enemy = platoon.Members[i].Enemy;
                if (!IsEligible(enemy)) continue;
                sum += (Vector2)enemy.transform.position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }

        private static EnemyTank SelectLeader(List<MemberState> members)
        {
            EnemyTank best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < members.Count; i++)
            {
                EnemyTank enemy = members[i].Enemy;
                if (!IsEligible(enemy)) continue;
                int score = LeaderPriority(enemy.Kind) * 100 - i;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        private static bool ContainsInstanceId(EnemyTank[] enemies, int instanceId)
        {
            if (instanceId == 0 || enemies == null) return false;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy != null && enemy.GetInstanceID() == instanceId && enemy.Health != null && !enemy.Health.IsDead) return true;
            }
            return false;
        }

        private static bool IsEligible(EnemyTank enemy)
        {
            return enemy != null && enemy.Health != null && !enemy.Health.IsDead && enemy.Kind != EnemyKind.Supply && enemy.Kind != EnemyKind.Boss;
        }

        private static bool ShouldYieldToHighCommand(EnemyTank enemy)
        {
            if (enemy == null || enemy.GetComponent<HighCommandNavigationDirective>() == null) return false;
            AdaptiveEnemyHighCommandDirector highCommand = AdaptiveEnemyHighCommandDirector.Instance;
            return highCommand != null && highCommand.ActiveCounterDoctrine != EnemyCounterDoctrine.None;
        }

        public static int LeaderPriority(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Elite: return 6;
                case EnemyKind.Heavy: return 5;
                case EnemyKind.Siege: return 4;
                case EnemyKind.Sniper: return 3;
                case EnemyKind.Fast: return 2;
                case EnemyKind.Basic: return 1;
                default: return 0;
            }
        }

        public static SquadTacticalRole RoleForKind(EnemyKind kind, int slot, bool leader, int round)
        {
            if (leader && (kind == EnemyKind.Elite || kind == EnemyKind.Heavy)) return SquadTacticalRole.Vanguard;
            switch (kind)
            {
                case EnemyKind.Siege: return SquadTacticalRole.Breaker;
                case EnemyKind.Sniper: return SquadTacticalRole.Suppressor;
                case EnemyKind.Fast: return (slot & 1) == 0 ? SquadTacticalRole.Flanker : SquadTacticalRole.Hunter;
                case EnemyKind.Heavy: return SquadTacticalRole.Vanguard;
                case EnemyKind.Elite: return round >= 55 ? SquadTacticalRole.Hunter : SquadTacticalRole.Suppressor;
                default: return (slot % 3) == 0 ? SquadTacticalRole.Flanker : SquadTacticalRole.Vanguard;
            }
        }

        public static float SuppressionAfterDamage(float current, int damage)
        {
            return Mathf.Clamp(current + Mathf.Max(0, damage) * SuppressionPerDamage, 0f, SuppressionMaximum);
        }

        public static float SuppressionAfterDecay(float current, float seconds)
        {
            return Mathf.Clamp(current - SuppressionDecayPerSecond * Mathf.Max(0f, seconds), 0f, SuppressionMaximum);
        }

        public static bool IsSuppressed(float suppression, float secondsSinceDamage)
        {
            return suppression >= SuppressionThreshold && secondsSinceDamage <= RelocationSeconds;
        }

        private void OnGUI()
        {
            if (Application.isBatchMode || _game == null || !_game.IsPlaying || _members.Count == 0) return;
            EnsureStyles();
            string headline = "TACTICAL PLATOONS  " + _platoons.Count + "  |  SUPPRESSED " + _suppressedCount;
            GUI.Label(new Rect(Screen.width - 330f, 118f, 310f, 24f), headline, _titleStyle);
            if (_cohesionBreakCount > 0)
                GUI.Label(new Rect(Screen.width - 330f, 140f, 310f, 22f), "Cohesion breaks: " + _cohesionBreakCount + "  •  leaders reform automatically", _bodyStyle);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _titleStyle.normal.textColor = new Color(1f, 0.78f, 0.22f);
            _bodyStyle = new GUIStyle(_titleStyle) { fontSize = 10, fontStyle = FontStyle.Normal };
            _bodyStyle.normal.textColor = new Color(0.88f, 0.90f, 0.94f);
        }
    }
}
