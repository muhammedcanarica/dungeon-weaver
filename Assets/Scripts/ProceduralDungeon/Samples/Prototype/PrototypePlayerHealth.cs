using System;
using UnityEngine;

namespace ProceduralDungeon
{
    public sealed class PrototypePlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.75f;

        private float invulnerabilityRemaining;

        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => invulnerabilityRemaining > 0f;
        public float InvulnerabilityDuration => invulnerabilityDuration;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;

        private void Awake()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
            CurrentHealth = maxHealth;
        }

        private void Update()
        {
            AdvanceInvulnerability(Time.deltaTime);
        }

        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvulnerable) return false;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            invulnerabilityRemaining = invulnerabilityDuration;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Damaged?.Invoke(amount);

            if (CurrentHealth != 0) return true;

            IsDead = true;
            Died?.Invoke();
            return true;
        }

        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            IsDead = false;
            invulnerabilityRemaining = 0f;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void AdvanceInvulnerability(float deltaTime)
        {
            if (deltaTime <= 0f || invulnerabilityRemaining <= 0f) return;
            invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - deltaTime);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
        }
    }
}
