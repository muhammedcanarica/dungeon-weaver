using System;
using UnityEngine;

namespace ProceduralDungeon
{
    public sealed class PrototypePlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 5;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.75f;

        private float invulnerabilityRemaining;
        private int currentHealth;
        private bool initialized;

        public int MaxHealth => Mathf.Max(1, maxHealth);
        public int CurrentHealth => initialized ? currentHealth : MaxHealth;
        public bool IsDead { get; private set; }
        public bool IsInvulnerable => invulnerabilityRemaining > 0f;
        public float InvulnerabilityDuration => invulnerabilityDuration;

        public event Action<int, int> HealthChanged;
        public event Action<int> Damaged;
        public event Action Died;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            AdvanceInvulnerability(Time.deltaTime);
        }

        public bool TakeDamage(int amount)
        {
            EnsureInitialized();
            if (amount <= 0 || IsDead || IsInvulnerable) return false;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            invulnerabilityRemaining = invulnerabilityDuration;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Damaged?.Invoke(amount);

            if (currentHealth != 0) return true;

            IsDead = true;
            Died?.Invoke();
            return true;
        }

        public void ResetHealth()
        {
            EnsureInitialized();
            currentHealth = MaxHealth;
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

        private void EnsureInitialized()
        {
            if (initialized) return;

            maxHealth = Mathf.Max(1, maxHealth);
            invulnerabilityDuration = Mathf.Max(0f, invulnerabilityDuration);
            currentHealth = maxHealth;
            invulnerabilityRemaining = 0f;
            IsDead = false;
            initialized = true;
        }
    }
}
