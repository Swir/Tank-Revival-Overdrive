using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(435)]
    public sealed class TacticalRegroupDirector : MonoBehaviour
    {
        public const float FallbackHealthRatio = 0.35f;
        public const float IsolationDistance = 6.25f;
        public const float DecisionCadence = 0.24f;
        public const int MaxRegroupActors = 12;

        private static TacticalRegroupDirector _instance;
        private TankGame _game;
        private float _nextDecision;
        private int _lastFallbackCount;

        public static TacticalRegroupDirector Instance => _instance;
        public static bool ConfigurationValid =>
            FallbackHealthRatio >= 0.25f && FallbackHealthRatio <= 0.45f &&
            IsolationDistance >= 4.5f && IsolationDistance <= 8f &&
            DecisionCadence >= 0.18f && DecisionCadence <= 0.4f &&
            MaxRegroupActors >= 6 && MaxRegroupActors <= 16;
        public int LastFallbackCount => _lastFallbackCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<TacticalRegroupDirector>() != null) return;
            var go = new GameObject("TacticalRegroupDirector_v7_3");
            DontDestroyOnLoad(go);
            go.AddComponent<TacticalRegroupDirector>();
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

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            if (enemies == null || enemies.Length == 0) return;

            Vector2 centroid = ComputeCombatCentroid(enemies, out int liveCount);
            if (liveCount <= 1) return;

            Vector2 eagle = _game.BasePosition;
            Vector2 player = _game.PlayerPosition;
            Vector2 rally = Vector2.Lerp(eagle, centroid, 0.68f);
            int fallbackCount = 0;

            for (int i = 0; i < enemies.Length && fallbackCount < MaxRegroupActors; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead) continue;
                if (enemy.Kind == EnemyKind.Supply || enemy.Kind == EnemyKind.Boss || enemy.Kind == EnemyKind.Siege) continue;

                TacticalNavigationAgent agent = enemy.GetComponent<TacticalNavigationAgent>();
                if (agent == null) continue;

                float healthRatio = enemy.Health.Maximum > 0 ? enemy.Health.Current / (float)enemy.Health.Maximum : 1f;
                float isolation = Vector2.Distance(enemy.transform.position, centroid);
                bool damagedFallback = healthRatio <= FallbackHealthRatio;
                bool isolatedRegroup = isolation >= IsolationDistance && Vector2.Distance(enemy.transform.position, player) < isolation;
                if (!damagedFallback && !isolatedRegroup) continue;

                float standoff = enemy.Kind == EnemyKind.Sniper ? 3.2f : 1.25f;
                float speedScale = enemy.Kind == EnemyKind.Heavy ? 0.86f : 1.08f;
                agent.SetOrder(rally, standoff, speedScale, enemies);
                fallbackCount++;
            }

            _lastFallbackCount = fallbackCount;
        }

        private static Vector2 ComputeCombatCentroid(EnemyTank[] enemies, out int count)
        {
            Vector2 sum = Vector2.zero;
            count = 0;
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyTank enemy = enemies[i];
                if (enemy == null || enemy.Health == null || enemy.Health.IsDead || enemy.Kind == EnemyKind.Supply) continue;
                sum += (Vector2)enemy.transform.position;
                count++;
            }
            return count > 0 ? sum / count : Vector2.zero;
        }
    }
}
