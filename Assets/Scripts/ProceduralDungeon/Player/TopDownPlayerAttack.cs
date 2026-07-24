using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TopDownPlayerAttack : MonoBehaviour
    {
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0.1f)] private float attackRadius = 1.5f;
        [SerializeField, Min(0f)] private float cooldown = 0.4f;
        [SerializeField, Min(0.01f)] private float feedbackDuration = 0.12f;

        private readonly Collider2D[] hitBuffer = new Collider2D[32];
        private readonly HashSet<DungeonEnemy> uniqueEnemies = new HashSet<DungeonEnemy>();
        private TopDownPlayerController movement;
        private DungeonRuntimeController runtimeController;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomEncounterController encounterController;
        private GameObject feedbackObject;
        private Sprite feedbackSprite;
        private float nextAttackTime;
        private float feedbackUntil;

        public int Damage => damage;
        public float AttackRadius => attackRadius;
        public float Cooldown => cooldown;
        public int LastHitCount { get; private set; }

        private void Awake()
        {
            CacheComponents();
            EnsureFeedback();
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureFeedback();
        }

        private void OnDisable()
        {
            LastHitCount = 0;
            if (feedbackObject != null) feedbackObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (feedbackSprite == null) return;
            if (Application.isPlaying) Destroy(feedbackSprite);
            else DestroyImmediate(feedbackSprite);
            feedbackSprite = null;
        }

        private void Update()
        {
            if (feedbackObject != null && feedbackObject.activeSelf && Time.time >= feedbackUntil)
                feedbackObject.SetActive(false);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TryAttack();
        }

        public bool TryAttack()
        {
            return TryAttackAtTime(Time.time);
        }

        public bool TryAttackAtTime(float currentTime)
        {
            return TryAttackAtTime(currentTime, DungeonRuntimeInputFocus.IsInputFieldFocused());
        }

        public bool TryAttackAtTimeForSelection(float currentTime, GameObject selectedObject)
        {
            return TryAttackAtTime(currentTime,
                DungeonRuntimeInputFocus.IsInputField(selectedObject));
        }

        private bool TryAttackAtTime(float currentTime, bool inputBlocked)
        {
            CacheComponents();
            if (!CanAttack(inputBlocked) || currentTime < nextAttackTime) return false;
            nextAttackTime = currentTime + cooldown;
            LastHitCount = 0;
            uniqueEnemies.Clear();

            var filter = new ContactFilter2D { useTriggers = true };
            int hitCount = Physics2D.OverlapCircle(transform.position, attackRadius,
                filter, hitBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = hitBuffer[i];
                if (hit == null) continue;
                DungeonEnemy enemy = hit.GetComponentInParent<DungeonEnemy>();
                if (enemy == null || enemy.RoomId != areaTracker.CurrentRoomId
                    || !uniqueEnemies.Add(enemy)) continue;
                if (enemy.TakeDamage(damage)) LastHitCount++;
            }

            ShowFeedback(currentTime);
            return true;
        }

        private bool CanAttack(bool inputBlocked)
        {
            return enabled && movement != null && movement.enabled
                && !inputBlocked
                && runtimeController != null && runtimeController.HasSuccessfulDungeon
                && !runtimeController.IsGenerating && areaTracker != null
                && areaTracker.CurrentAreaType == DungeonAreaType.Room
                && encounterController != null
                && encounterController.ActiveLockedRoomId == areaTracker.CurrentRoomId;
        }

        private void CacheComponents()
        {
            if (movement == null) movement = GetComponent<TopDownPlayerController>();
            if (runtimeController == null)
                runtimeController = FindFirstObjectByType<DungeonRuntimeController>();
            if (areaTracker == null) areaTracker = FindFirstObjectByType<DungeonAreaTracker>();
            if (encounterController == null)
                encounterController = FindFirstObjectByType<DungeonRoomEncounterController>();
        }

        private void EnsureFeedback()
        {
            if (feedbackObject != null) return;
            feedbackObject = new GameObject("AttackFeedback");
            feedbackObject.transform.SetParent(transform, false);
            feedbackObject.transform.localPosition = Vector3.zero;
            feedbackObject.transform.localScale = new Vector3(attackRadius * 2f,
                attackRadius * 2f, 1f);
            SpriteRenderer renderer = feedbackObject.AddComponent<SpriteRenderer>();
            feedbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            feedbackSprite.name = "Runtime Player Attack Feedback Sprite";
            renderer.sprite = feedbackSprite;
            renderer.color = new Color(0.15f, 0.9f, 1f, 0.28f);
            renderer.sortingOrder = 30;
            feedbackObject.SetActive(false);
        }

        private void ShowFeedback(float currentTime)
        {
            EnsureFeedback();
            feedbackObject.transform.localScale = new Vector3(attackRadius * 2f,
                attackRadius * 2f, 1f);
            feedbackUntil = currentTime + feedbackDuration;
            feedbackObject.SetActive(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRadius);
        }
    }
}
