using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(PrototypePlayerHealth))]
    public sealed class PrototypePlayerDamageFeedback : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float feedbackDuration = 0.12f;
        [SerializeField] private Color damageColor = new Color(1f, 0.42f, 0.42f, 1f);

        private PrototypePlayerHealth health;
        private SpriteRenderer spriteRenderer;
        private Color normalColor;
        private float feedbackUntil;
        private bool subscribed;

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            RestoreColor();
        }

        private void Update()
        {
            if (Time.time >= feedbackUntil) RestoreColor();
        }

        private void HandleDamaged(int _)
        {
            if (spriteRenderer == null) return;
            normalColor = spriteRenderer.color;
            spriteRenderer.color = damageColor;
            feedbackUntil = Time.time + feedbackDuration;
        }

        private void CacheComponents()
        {
            if (health == null) health = GetComponent<PrototypePlayerHealth>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null) normalColor = spriteRenderer.color;
            }
        }

        private void Subscribe()
        {
            if (health == null || subscribed) return;
            health.Damaged += HandleDamaged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (health != null && subscribed) health.Damaged -= HandleDamaged;
            subscribed = false;
        }

        private void RestoreColor()
        {
            if (spriteRenderer != null) spriteRenderer.color = normalColor;
        }
    }
}
