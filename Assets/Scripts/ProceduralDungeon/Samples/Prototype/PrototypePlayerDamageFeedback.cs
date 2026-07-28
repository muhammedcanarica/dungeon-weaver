using UnityEngine;

namespace ProceduralDungeon
{
    public enum PrototypePlayerFeedbackState
    {
        Normal,
        Damage,
        Death
    }

    [RequireComponent(typeof(PrototypePlayerHealth))]
    public sealed class PrototypePlayerDamageFeedback : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float feedbackDuration = 0.12f;
        [SerializeField] private Color damageColor = new Color(1f, 0.42f, 0.42f, 1f);
        [SerializeField] private Color deathColor = new Color(0.28f, 0.28f, 0.34f, 0.55f);

        private PrototypePlayerHealth health;
        private SpriteRenderer spriteRenderer;
        private Color normalColor;
        private float feedbackRemaining;
        private bool subscribed;
        private bool hasNormalColor;

        public PrototypePlayerFeedbackState CurrentState { get; private set; }
        public float FeedbackDuration => feedbackDuration;

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
            SyncVisualState();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (CurrentState != PrototypePlayerFeedbackState.Death) SetState(PrototypePlayerFeedbackState.Normal);
        }

        private void Update()
        {
            AdvanceFeedback(Time.deltaTime);
        }

        public void AdvanceFeedback(float deltaTime)
        {
            if (CurrentState != PrototypePlayerFeedbackState.Damage) return;
            feedbackRemaining = Mathf.Max(0f, feedbackRemaining - Mathf.Max(0f, deltaTime));
            if (feedbackRemaining <= 0f) SetState(PrototypePlayerFeedbackState.Normal);
        }

        private void HandleDamaged(int _)
        {
            if (health == null || health.IsDead) return;
            feedbackRemaining = feedbackDuration;
            SetState(PrototypePlayerFeedbackState.Damage);
        }

        private void HandleDied()
        {
            feedbackRemaining = 0f;
            SetState(PrototypePlayerFeedbackState.Death);
        }

        private void HandleHealthChanged(int _, int __)
        {
            if (health != null && health.IsDead)
            {
                HandleDied();
                return;
            }

            if (health != null && health.CurrentHealth == health.MaxHealth)
            {
                feedbackRemaining = 0f;
                SetState(PrototypePlayerFeedbackState.Normal);
            }
        }

        private void CacheComponents()
        {
            if (health == null) health = GetComponent<PrototypePlayerHealth>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    normalColor = spriteRenderer.color;
                    hasNormalColor = true;
                }
            }
        }

        private void Subscribe()
        {
            if (health == null || subscribed) return;
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            health.HealthChanged += HandleHealthChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (health != null && subscribed)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
                health.HealthChanged -= HandleHealthChanged;
            }
            subscribed = false;
        }

        private void SyncVisualState()
        {
            if (health != null && health.IsDead) SetState(PrototypePlayerFeedbackState.Death);
            else SetState(PrototypePlayerFeedbackState.Normal);
        }

        private void SetState(PrototypePlayerFeedbackState state)
        {
            if (CurrentState == PrototypePlayerFeedbackState.Death
                && state == PrototypePlayerFeedbackState.Damage) return;
            CurrentState = state;
            if (spriteRenderer == null || !hasNormalColor) return;
            spriteRenderer.color = state == PrototypePlayerFeedbackState.Damage
                ? damageColor
                : state == PrototypePlayerFeedbackState.Death ? deathColor : normalColor;
        }
    }
}
