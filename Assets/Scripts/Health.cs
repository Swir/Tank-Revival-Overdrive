using System;
using UnityEngine;

namespace TankRevival
{
    public enum Team
    {
        Neutral,
        Player,
        Enemy
    }

    public sealed class Health : MonoBehaviour
    {
        public Team Team { get; private set; }
        public int Current { get; private set; }
        public int Maximum { get; private set; }
        public int Max => Maximum;
        public float InvulnerableUntil { get; set; }
        public bool IsDead { get; private set; }

        public Action<Health> Died;
        public Action<Health, int> Damaged;

        public void Initialize(Team team, int maxHealth, int currentHealth = -1)
        {
            Team = team;
            Maximum = Mathf.Max(1, maxHealth);
            Current = currentHealth < 0 ? Maximum : Mathf.Clamp(currentHealth, 1, Maximum);
            IsDead = false;

            if (GetComponent<DamageSmokeEmitter>() == null)
                gameObject.AddComponent<DamageSmokeEmitter>();

            RuntimeBattleRegistry.RegisterHealth(this);
        }

        private void OnDestroy()
        {
            RuntimeBattleRegistry.UnregisterHealth(this);
        }

        public void SetMaximum(int newMaximum)
        {
            SetMaximum(newMaximum, false);
        }

        public void SetMaximum(int newMaximum, bool grantAddedCapacity)
        {
            if (IsDead) return;

            int oldMaximum = Mathf.Max(1, Maximum);
            int target = Mathf.Max(1, newMaximum);
            int added = Mathf.Max(0, target - oldMaximum);
            Maximum = target;

            if (grantAddedCapacity && added > 0)
                Current += added;

            Current = Mathf.Clamp(Current, 1, Maximum);
        }

        public bool Damage(int amount, Team source)
        {
            if (IsDead || amount <= 0) return false;
            if (source == Team && Team != Team.Neutral) return false;
            if (Time.time < InvulnerableUntil) return false;

            Current = Mathf.Max(0, Current - amount);
            Damaged?.Invoke(this, amount);

            if (Current <= 0)
            {
                IsDead = true;
                Died?.Invoke(this);
                Destroy(gameObject);
            }

            return true;
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(Maximum, Current + amount);
        }
    }
}
