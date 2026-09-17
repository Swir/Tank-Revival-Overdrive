using UnityEngine;

namespace TankRevival
{
    /// <summary>Finite, interruptible module-only field repair. Never heals Health.</summary>
    public sealed class EmergencyRepairSystem : MonoBehaviour
    {
        public const int MaxCharges = 2;
        public const int RepairAmount = 38;
        public const float ChannelSeconds = 2.6f;
        public const float CooldownSeconds = 10f;
        public const float MaxRepairSpeed = 0.22f;

        public int ChargesRemaining { get; private set; } = MaxCharges;
        public bool IsRepairing { get; private set; }
        public TankModule TargetModule { get; private set; } = TankModule.None;
        public float Progress01 => IsRepairing ? Mathf.Clamp01((Time.time - _startedAt) / ChannelSeconds) : 0f;
        public float CooldownRemaining => Mathf.Max(0f, _cooldownUntil - Time.time);
        public int CompletedRepairs { get; private set; }
        public int Interruptions { get; private set; }

        private ArmorSystem _armor;
        private Rigidbody2D _body;
        private Health _health;
        private float _startedAt;
        private float _cooldownUntil;
        private float _impactStampAtStart;

        public static bool ConfigurationValid =>
            MaxCharges > 0 && MaxCharges <= 3 && RepairAmount > 0 && RepairAmount <= 45 &&
            ChannelSeconds >= 2f && ChannelSeconds <= 4f && CooldownSeconds >= 6f;

        private void Awake()
        {
            _armor = GetComponent<ArmorSystem>();
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<Health>();
            if (_health != null) _health.Damaged += OnHealthDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnHealthDamaged;
        }

        private void Update()
        {
            if (!IsRepairing) return;
            if (_armor == null || (_health != null && _health.IsDead)) { InterruptRepair(); return; }
            if (_body != null && _body.linearVelocity.sqrMagnitude > MaxRepairSpeed * MaxRepairSpeed) { InterruptRepair(); return; }
            if (_armor.LastImpactAt > _impactStampAtStart + 0.0001f) { InterruptRepair(); return; }
            if (Time.time - _startedAt < ChannelSeconds) return;

            int repaired = _armor.RepairModule(TargetModule, RepairAmount, true);
            if (repaired > 0)
            {
                ChargesRemaining = Mathf.Max(0, ChargesRemaining - 1);
                CompletedRepairs++;
                _cooldownUntil = Time.time + CooldownSeconds;
            }
            IsRepairing = false;
            TargetModule = TankModule.None;
        }

        public bool TryBeginRepair()
        {
            if (IsRepairing || ChargesRemaining <= 0 || Time.time < _cooldownUntil) return false;
            if (_armor == null) _armor = GetComponent<ArmorSystem>();
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            if (_armor == null || !_armor.HasRepairableDamage) return false;
            if (_body != null && _body.linearVelocity.sqrMagnitude > MaxRepairSpeed * MaxRepairSpeed) return false;

            TankModule target = _armor.MostDamagedModule();
            if (target == TankModule.None || _armor.GetModuleIntegrity(target) >= 100) return false;
            TargetModule = target;
            _startedAt = Time.time;
            _impactStampAtStart = _armor.LastImpactAt;
            IsRepairing = true;
            return true;
        }

        public void InterruptRepair()
        {
            if (!IsRepairing) return;
            IsRepairing = false;
            TargetModule = TankModule.None;
            Interruptions++;
            _cooldownUntil = Mathf.Max(_cooldownUntil, Time.time + 1.2f);
        }

        private void OnHealthDamaged(Health health, int amount)
        {
            if (amount > 0) InterruptRepair();
        }

        public void ConfigureForSmoke(int charges = MaxCharges)
        {
            ChargesRemaining = Mathf.Clamp(charges, 0, MaxCharges);
            _cooldownUntil = 0f;
            IsRepairing = false;
            TargetModule = TankModule.None;
            CompletedRepairs = 0;
            Interruptions = 0;
        }

        public bool CompleteImmediatelyForSmoke()
        {
            if (!TryBeginRepair()) return false;
            int repaired = _armor.RepairModule(TargetModule, RepairAmount, false);
            if (repaired <= 0) { IsRepairing = false; TargetModule = TankModule.None; return false; }
            ChargesRemaining = Mathf.Max(0, ChargesRemaining - 1);
            CompletedRepairs++;
            IsRepairing = false;
            TargetModule = TankModule.None;
            _cooldownUntil = 0f;
            return true;
        }
    }
}
