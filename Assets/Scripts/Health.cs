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
