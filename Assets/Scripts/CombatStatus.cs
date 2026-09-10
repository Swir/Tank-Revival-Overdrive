using UnityEngine;

namespace TankRevival
{
    public sealed class CombatStatus : MonoBehaviour
    {
        public bool IsEmpDisabled => Time.time < _empUntil;

        private Health _health;
        private float _empUntil;
        private float _burnUntil;
        private float _nextBurnTick;
        private float _burnInterval = 0.75f;
        private int _burnDamage = 1;
        private Team _burnSource = Team.Neutral;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        public void ApplyEmp(float seconds)
        {
            _empUntil = Mathf.Max(_empUntil, Time.time + Mathf.Max(0.1f, seconds));
            Color c = AmmoDatabase.Color(AmmoType.EMP);
            VisualFactory.RingPulse(transform.position, c, 0.78f);
            BattleAudio.PlayGlobal(SoundCue.Emp, 0.55f);
        }

        public void ApplyBurn(Team source, float seconds, int damage = 1, float interval = 0.75f)
        {
            _health ??= GetComponent<Health>();
            _burnSource = source;
            _burnDamage = Mathf.Max(1, damage);
            _burnInterval = Mathf.Max(0.25f, interval);
            _burnUntil = Mathf.Max(_burnUntil, Time.time + Mathf.Max(0.5f, seconds));
            _nextBurnTick = Mathf.Min(_nextBurnTick <= 0f ? Time.time + 0.25f : _nextBurnTick, Time.time + 0.25f);
        }

        private void Update()
        {
            if (_burnUntil <= Time.time || _health == null || _health.IsDead) return;
            if (Time.time < _nextBurnTick) return;

            _nextBurnTick = Time.time + _burnInterval;
            Vector3 pos = transform.position + (Vector3)Random.insideUnitCircle * 0.18f;
            VisualFactory.MicroBurst(pos, new Color(1f, 0.42f, 0.05f), 0.32f);
            _health.Damage(_burnDamage, _burnSource);
        }
    }
}
