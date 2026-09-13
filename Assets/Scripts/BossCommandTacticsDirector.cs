using UnityEngine;

namespace TankRevival
{
    [DefaultExecutionOrder(320)]
    public sealed class BossCommandTacticsDirector : MonoBehaviour
    {
        public const int MaxEscortShots = 5;
        public const float PhaseCommandDelay = 0.9f;
        public const float ReinforcementCadenceFloor = 3.8f;

        private static BossCommandTacticsDirector _instance;
        private TankGame _game;
        private BossLegendDirector _bossLegend;
        private EnemyTank _boss;
        private int _observedPhase;
        private float _commandAt;
        private float _nextSupportVolley;
        private int _lastRound;

        public static BossCommandTacticsDirector Instance => _instance;
        public static bool ConfigurationValid => MaxEscortShots >= 3 && MaxEscortShots <= 6 && PhaseCommandDelay >= 0.5f && ReinforcementCadenceFloor >= 3f;
        public int ObservedPhase => _observedPhase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<BossCommandTacticsDirector>() != null) return;
            var go = new GameObject("BossCommandTacticsDirector_v7_2");
            DontDestroyOnLoad(go);
            go.AddComponent<BossCommandTacticsDirector>();
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
            if (_game == null || !_game.IsPlaying) return;

            if (_lastRound != _game.CurrentRound)
            {
                _lastRound = _game.CurrentRound;
                _bossLegend = null;
                _boss = null;
                _observedPhase = 0;
                _commandAt = 0f;
                _nextSupportVolley = 0f;
            }

            if (_game.CurrentRound % 10 != 0) return;
            if (_bossLegend == null) AcquireBoss();
            if (_bossLegend == null || _boss == null || _boss.Health == null || _boss.Health.IsDead) return;

            int phase = Mathf.Clamp(_bossLegend.Phase, 1, 4);
            if (phase != _observedPhase)
            {
                _observedPhase = phase;
                _commandAt = Time.time + PhaseCommandDelay;
                _nextSupportVolley = _commandAt + 0.8f;
            }

            if (_commandAt > 0f && Time.time >= _commandAt)
            {
                _commandAt = 0f;
                ExecutePhaseCommand(phase);
            }

            float cadence = Mathf.Max(ReinforcementCadenceFloor, 7.0f - phase * 0.72f - _game.CurrentRound * 0.012f);
            if (phase >= 2 && Time.time >= _nextSupportVolley)
            {
                _nextSupportVolley = Time.time + cadence;
                ExecuteEscortVolley(phase);
            }
        }

        private void AcquireBoss()
        {
            BossLegendDirector[] legends = FindObjectsByType<BossLegendDirector>(FindObjectsSortMode.None);
            for (int i = 0; i < legends.Length; i++)
            {
                if (legends[i] == null) continue;
                EnemyTank enemy = legends[i].GetComponent<EnemyTank>();
                if (enemy == null || enemy.Kind != EnemyKind.Boss || enemy.Health == null || enemy.Health.IsDead) continue;
                _bossLegend = legends[i];
                _boss = enemy;
                _observedPhase = Mathf.Clamp(_bossLegend.Phase, 1, 4);
                _nextSupportVolley = Time.time + 4.8f;
                return;
            }
        }

        private void ExecutePhaseCommand(int phase)
        {
            if (_boss == null) return;
            Color color = PhaseColor(phase);
            float radius = 1.05f + phase * 0.25f;
            VisualFactory.RingPulse(_boss.transform.position, color, radius);
            VisualFactory.MicroBurst(_boss.transform.position, color, 0.72f + phase * 0.14f);
            BattleAudio.PlayGlobal(phase >= 3 ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.24f + phase * 0.03f, 0.04f);

            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int marked = 0;
            for (int i = 0; i < enemies.Length && marked < 4; i++)
            {
                EnemyTank escort = enemies[i];
                if (!IsEscortCandidate(escort)) continue;
                Color pulse = Color.Lerp(color, Color.white, 0.22f);
                VisualFactory.RingPulse(escort.transform.position, pulse, 0.48f + phase * 0.05f);
                marked++;
            }
        }

        private void ExecuteEscortVolley(int phase)
        {
            EnemyTank[] enemies = RuntimeBattleRegistry.EnemySnapshot;
            int budget = Mathf.Clamp(2 + phase, 2, MaxEscortShots);
            int fired = 0;
            Vector2 target = phase >= 3 ? _game.PlayerPosition : Vector2.Lerp(_game.PlayerPosition, _game.BasePosition, 0.45f);

            for (int i = 0; i < enemies.Length && fired < budget; i++)
            {
                EnemyTank escort = enemies[i];
                if (!IsEscortCandidate(escort)) continue;

                Vector2 origin = escort.transform.position;
                Vector2 direction = target - origin;
                if (direction.sqrMagnitude < 0.18f) continue;
                direction.Normalize();

                float offset = (fired - (budget - 1) * 0.5f) * (phase >= 4 ? 4.0f : 6.5f);
                direction = Rotate(direction, offset);
                Color color = PhaseColor(phase);
                float speed = 9.0f + _game.CurrentRound * 0.025f + phase * 0.35f;
                int damage = phase >= 4 && _game.CurrentRound >= 70 ? 2 : 1;
                _game.SpawnProjectile(origin + direction * 0.78f, direction, Team.Enemy, damage, speed, color, AmmoType.Basic);
                VisualFactory.MuzzleFlash(origin + direction * 0.72f, color, 0.58f);
                fired++;
            }

            if (fired > 0)
                BattleAudio.PlayGlobal(phase >= 4 ? SoundCue.HeavyShot : SoundCue.EnemyShot, 0.14f, 0.06f);
        }

        private bool IsEscortCandidate(EnemyTank enemy)
        {
            if (enemy == null || enemy == _boss || enemy.Health == null || enemy.Health.IsDead) return false;
            switch (enemy.Kind)
            {
                case EnemyKind.Heavy:
                case EnemyKind.Elite:
                case EnemyKind.Sniper:
                case EnemyKind.Fast:
                case EnemyKind.Siege:
                    return true;
                default:
                    return false;
            }
        }

        private static Color PhaseColor(int phase)
        {
            switch (phase)
            {
                case 1: return new Color(1f, 0.70f, 0.08f);
                case 2: return new Color(1f, 0.42f, 0.06f);
                case 3: return new Color(1f, 0.16f, 0.08f);
                default: return new Color(0.86f, 0.18f, 1f);
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c).normalized;
        }
    }
}
