using System;
using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    public enum SquadTacticalRole
    {
        Vanguard,
        Flanker,
        Suppressor,
        Breaker,
        Escort,
        Hunter
    }

    [DefaultExecutionOrder(315)]
    public sealed class EnemySquadTacticsDirector : MonoBehaviour
    {
        private sealed class Assignment
        {
            public SquadTacticalRole Role;
            public float NextActionAt;
            public int SalvoIndex;
        }

        public const int RoleCount = 6;
        public const int MaxCoordinatedShotsPerBeat = 6;
        public const float MinimumBeatCadence = 2.8f;
        public const float MaximumBeatCadence = 6.5f;

        private static EnemySquadTacticsDirector _instance;
        private readonly Dictionary<EnemyTank, Assignment> _assignments = new Dictionary<EnemyTank, Assignment>(32);
        private readonly List<EnemyTank> _eligible = new List<EnemyTank>(32);

        private TankGame _game;
        private int _lastRegistryRevision = -1;
        private int _lastRound = -1;
        private float _nextBeat;
        private int _beatIndex;
        private int _lastActiveSquadSize;
        private int _lastRoleCoverage;

        public static EnemySquadTacticsDirector Instance => _instance;
        public static bool ConfigurationValid =>
            RoleCount == 6 &&
            MaxCoordinatedShotsPerBeat >= 4 && MaxCoordinatedShotsPerBeat <= 8 &&
            MinimumBeatCadence >= 2.5f && MaximumBeatCadence <= 7f &&
            MinimumBeatCadence < MaximumBeatCadence;

        public int ActiveSquadSize => _lastActiveSquadSize;
        public int ActiveRoleCoverage => _lastRoleCoverage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<EnemySquadTacticsDirector>() != null) return;
            var go = new GameObject("EnemySquadTacticsDirector_v7_2");
            DontDestroyOnLoad(go);
            go.AddComponent<EnemySquadTacticsDirector>();
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
            _nextBeat = Time.time + 4.0f;
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
            if (_lastRegistryRevision != RuntimeBattleRegistry.Revision || _lastRound != round)
            {
                RebuildAssignments(round);
                _lastRegistryRevision = RuntimeBattleRegistry.Revision;
                _lastRound = round;
            }

            if (round < 8 || Time.time < _nextBeat) return;

            float progress = (round - 1f) / 99f;
            _nextBeat = Time.time + Mathf.Lerp(MaximumBeatCadence, MinimumBeatCadence, progress);
            ExecuteTacticalBeat(round, progress);
        }

        private void RebuildAssignments(int round)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            _eligible.Clear();

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                _eligible.Add(enemy);
            }

            var stale = new List<EnemyTank>();
            foreach (KeyValuePair<EnemyTank, Assignment> kv in _assignments)
                if (kv.Key == null || !_eligible.Contains(kv.Key)) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++) _assignments.Remove(stale[i]);

            int roleMask = 0;
            for (int i = 0; i < _eligible.Count; i++)
            {
                EnemyTank enemy = _eligible[i];
                if (!_assignments.TryGetValue(enemy, out Assignment assignment))
                {
                    assignment = new Assignment
                    {
                        Role = ChooseRole(enemy, round, i),
                        NextActionAt = Time.time + 1.0f + (i % 5) * 0.23f,
                        SalvoIndex = 0
                    };
                    _assignments.Add(enemy, assignment);
                }
                roleMask |= 1 << (int)assignment.Role;
            }

            _lastActiveSquadSize = _eligible.Count;
            _lastRoleCoverage = CountBits(roleMask);
        }

        private static SquadTacticalRole ChooseRole(EnemyTank enemy, int round, int index)
        {
            switch (enemy.Kind)
            {
                case EnemyKind.Siege:
                    return SquadTacticalRole.Breaker;
                case EnemyKind.Sniper:
                    return index % 2 == 0 ? SquadTacticalRole.Suppressor : SquadTacticalRole.Hunter;
                case EnemyKind.Fast:
                    return index % 3 == 0 ? SquadTacticalRole.Hunter : SquadTacticalRole.Flanker;
                case EnemyKind.Heavy:
                    return index % 2 == 0 ? SquadTacticalRole.Vanguard : SquadTacticalRole.Escort;
                case EnemyKind.Elite:
                    return round >= 50 ? SquadTacticalRole.Hunter : SquadTacticalRole.Suppressor;
                case EnemyKind.Boss:
                    return SquadTacticalRole.Vanguard;
                default:
                    return (SquadTacticalRole)((index + round / 10) % RoleCount);
            }
        }

        private void ExecuteTacticalBeat(int round, float progress)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            _beatIndex++;
            int shotBudget = Mathf.Clamp(2 + round / 24, 2, MaxCoordinatedShotsPerBeat);
            int fired = 0;

            for (int pass = 0; pass < RoleCount && fired < shotBudget; pass++)
            {
                SquadTacticalRole desiredRole = (SquadTacticalRole)((_beatIndex + pass) % RoleCount);
                EnemyTank actor = SelectActor(enemies, desiredRole);
                if (actor == null || !_assignments.TryGetValue(actor, out Assignment assignment)) continue;
                if (Time.time < assignment.NextActionAt) continue;

                ExecuteRoleAction(actor, assignment, round, progress);
                assignment.SalvoIndex++;
                assignment.NextActionAt = Time.time + Mathf.Lerp(6.8f, 3.4f, progress) + (int)assignment.Role * 0.12f;
                fired++;
            }

            if (fired >= 3 && round >= 30)
            {
                Vector2 signal = Vector2.Lerp(_game.PlayerPosition, _game.BasePosition, 0.35f);
                VisualFactory.RingPulse(signal, new Color(1f, 0.24f, 0.08f), 0.75f);
            }
        }

        private EnemyTank SelectActor(EnemyTank[] enemies, SquadTacticalRole role)
        {
            EnemyTank best = null;
            float bestScore = float.MinValue;
            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                if (!_assignments.TryGetValue(enemy, out Assignment a) || a.Role != role) continue;

                float playerDist = Vector2.Distance(enemy.transform.position, player);
                float eagleDist = Vector2.Distance(enemy.transform.position, eagle);
                float score;
                switch (role)
                {
                    case SquadTacticalRole.Breaker: score = 14f - eagleDist; break;
                    case SquadTacticalRole.Flanker: score = Mathf.Abs(enemy.transform.position.x - player.x) + 0.15f * playerDist; break;
                    case SquadTacticalRole.Suppressor: score = 12f - playerDist; break;
                    case SquadTacticalRole.Escort: score = enemy.Kind == EnemyKind.Heavy || enemy.Kind == EnemyKind.Elite ? 5f - playerDist * 0.1f : -playerDist; break;
                    case SquadTacticalRole.Hunter: score = 10f - playerDist; break;
                    default: score = 8f - Mathf.Min(playerDist, eagleDist); break;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }
            return best;
        }

        private void ExecuteRoleAction(EnemyTank actor, Assignment assignment, int round, float progress)
        {
            Vector2 origin = actor.transform.position;
            Vector2 player = _game.PlayerPosition;
            Vector2 eagle = _game.BasePosition;
            Vector2 target = player;
            AmmoType ammo = AmmoType.Basic;
            int damage = 1 + (round >= 70 ? 1 : 0);
            float speed = Mathf.Lerp(8.5f, 11.8f, progress);
            Color color = new Color(1f, 0.30f, 0.08f);
            int localShots = 1;
            float spread = 0f;

            switch (assignment.Role)
            {
                case SquadTacticalRole.Flanker:
                    target = player + new Vector2(Mathf.Sign(origin.x - player.x) * 0.55f, 0f);
                    speed *= 1.08f;
                    color = new Color(1f, 0.62f, 0.12f);
                    spread = 5f;
                    break;
                case SquadTacticalRole.Suppressor:
                    target = player;
                    localShots = round >= 55 ? 2 : 1;
                    spread = 7f;
                    color = new Color(0.95f, 0.34f, 0.18f);
                    break;
                case SquadTacticalRole.Breaker:
                    target = eagle;
                    damage = Mathf.Max(damage, round >= 60 ? 2 : 1);
                    speed *= 0.92f;
                    color = new Color(1f, 0.16f, 0.06f);
                    break;
                case SquadTacticalRole.Escort:
                    target = player;
                    spread = 3f;
                    color = new Color(0.72f, 0.42f, 1f);
                    break;
                case SquadTacticalRole.Hunter:
                    target = player;
                    speed *= 1.16f;
                    color = new Color(0.24f, 0.88f, 1f);
                    break;
                default:
                    target = Vector2.Lerp(player, eagle, round >= 45 ? 0.20f : 0.08f);
                    color = new Color(1f, 0.44f, 0.10f);
                    break;
            }

            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.08f) return;
            direction.Normalize();
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 muzzle = origin + direction * (actor.Kind == EnemyKind.Boss ? 1.08f : 0.82f);

            for (int i = 0; i < localShots; i++)
            {
                float signed = localShots == 1 ? ((assignment.SalvoIndex & 1) == 0 ? -spread : spread) : (i == 0 ? -spread : spread);
                Vector2 shotDir = Rotate(direction, signed);
                _game.SpawnProjectile(muzzle + side * (i == 0 ? -0.08f : 0.08f), shotDir, Team.Enemy, damage, speed, color, ammo);
            }

            VisualFactory.MuzzleFlash(muzzle, color, actor.Kind == EnemyKind.Heavy || actor.Kind == EnemyKind.Siege || actor.Kind == EnemyKind.Boss ? 0.95f : 0.62f);
            BattleAudio.PlayGlobal(actor.Kind == EnemyKind.Heavy || actor.Kind == EnemyKind.Siege || actor.Kind == EnemyKind.Boss ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.10f, 0.08f);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c).normalized;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }
}
