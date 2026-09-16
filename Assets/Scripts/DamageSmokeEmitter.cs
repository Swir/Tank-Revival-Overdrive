using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Lightweight Health observer. v12.7 removes per-puff GameObject churn and forwards damaged,
    /// critical and burning pulses into the shared fixed CinematicCombatFeedbackDirector pool.
    /// </summary>
    public sealed class DamageSmokeEmitter : MonoBehaviour
    {
        private Health _health;
        private float _nextPulse;
        private float _phase;

        private void Start()
        {
            _health = GetComponent<Health>();
            _phase = Mathf.Abs(GetInstanceID() % 97) / 97f;
            _nextPulse = Time.unscaledTime + 0.08f + _phase * 0.18f;
        }

        private void Update()
        {
            if (_health == null || _health.IsDead || _health.Maximum <= 1) return;
            float ratio = Mathf.Clamp01(_health.Current / (float)_health.Maximum);
            DamageVisualState state = CinematicCombatFeedbackDirector.ResolveDamageState(ratio);
            if (state == DamageVisualState.Healthy) return;
            if (Time.unscaledTime < _nextPulse) return;

            TankGame game = TankGame.I;
            float pressure = game != null ? Mathf.Clamp01(game.LateBattleDensity01) : 0f;
            float cadence = CinematicCombatFeedbackDirector.DamagePulseCadence(ratio, pressure);
            float jitter = Mathf.Lerp(0.92f, 1.10f, Mathf.PingPong(Time.unscaledTime * 0.37f + _phase, 1f));
            _nextPulse = Time.unscaledTime + cadence * jitter;

            Vector3 offset = new Vector3((_phase - 0.5f) * 0.24f, 0.08f + Mathf.PingPong(_phase * 0.31f, 0.10f), 0f);
            CinematicCombatFeedbackDirector.RequestDamagePulse(transform.position + offset, ratio);
        }
    }
}
