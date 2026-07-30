using System;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class DungeonEnemy : MonoBehaviour
    {
        private static readonly Color NormalColor = new Color(0.72f, 0.2f, 0.95f, 1f);
        private static readonly Color HitColor = new Color(1f, 0.92f, 0.25f, 1f);
        private const float HitFeedbackDuration = 0.1f;

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Transform player;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomEncounterController encounterController;
        private RectInt roomBounds;
        private RoomData roomData;
        private Vector3 normalScale;
        private float moveSpeed;
        private float hitFeedbackUntil;
        private bool initialized;
        private bool deathRaised;

        public int RoomId { get; private set; } = DungeonRoomStateController.InvalidId;
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsAlive { get; private set; }

        public event Action<DungeonEnemy> Died;

        internal void Initialize(DungeonEnemySpawnPlan plan, RoomData room, Transform target,
            DungeonAreaTracker tracker, DungeonRoomEncounterController encounters,
            Sprite sprite, float speed)
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            CircleCollider2D enemyCollider = GetComponent<CircleCollider2D>();

            RoomId = plan.RoomId;
            MaxHealth = Mathf.Max(1, plan.MaxHealth);
            CurrentHealth = MaxHealth;
            IsAlive = true;
            deathRaised = false;
            roomData = room;
            roomBounds = room != null ? room.Bounds : default;
            player = target;
            areaTracker = tracker;
            encounterController = encounters;
            moveSpeed = Mathf.Max(0f, speed);

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            enemyCollider.isTrigger = true;
            enemyCollider.radius = 0.42f;
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = NormalColor;
            spriteRenderer.sortingOrder = 20;
            transform.localScale = new Vector3(0.78f, 0.78f, 1f);
            normalScale = transform.localScale;
            initialized = true;
        }

        internal void Initialize(DungeonEnemySpawnPlan plan, RectInt bounds, Transform target,
            DungeonAreaTracker tracker, DungeonRoomEncounterController encounters,
            Sprite sprite, float speed)
        {
            Initialize(plan, new RoomData(plan.RoomId, bounds), target, tracker,
                encounters, sprite, speed);
        }

        public bool TakeDamage(int amount)
        {
            if (!initialized || !IsAlive || amount <= 0) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            hitFeedbackUntil = Time.time + HitFeedbackDuration;
            ApplyHitFeedback();
            if (CurrentHealth == 0) Die();
            return true;
        }

        internal void DespawnWithoutDeath()
        {
            if (!initialized) return;
            IsAlive = false;
            CurrentHealth = 0;
            deathRaised = true;
            Died = null;
            gameObject.SetActive(false);
            DestroyRuntimeObject(gameObject);
        }

        private void Update()
        {
            if (!initialized || !IsAlive || spriteRenderer == null) return;
            if (Time.time < hitFeedbackUntil) return;
            spriteRenderer.color = NormalColor;
            transform.localScale = normalScale;
        }

        private void FixedUpdate()
        {
            TryMoveTowardsPlayer(Time.fixedDeltaTime);
        }

        public bool TryMoveTowardsPlayer(float deltaTime)
        {
            if (!CanMove() || deltaTime <= 0f)
            {
                if (body != null) body.linearVelocity = Vector2.zero;
                return false;
            }

            Vector2 next = Vector2.MoveTowards(body.position, player.position,
                moveSpeed * deltaTime);
            next.x = Mathf.Clamp(next.x, roomBounds.xMin + 0.5f, roomBounds.xMax - 0.5f);
            next.y = Mathf.Clamp(next.y, roomBounds.yMin + 0.5f, roomBounds.yMax - 0.5f);
            if (roomData != null && !roomData.ContainsCell(Vector2Int.FloorToInt(next)))
            {
                body.linearVelocity = Vector2.zero;
                return false;
            }
            if (Application.isPlaying) body.MovePosition(next);
            else body.position = next;
            return true;
        }

        private bool CanMove()
        {
            return initialized && IsAlive && body != null && player != null
                && areaTracker != null && areaTracker.CurrentAreaType == DungeonAreaType.Room
                && areaTracker.CurrentRoomId == RoomId && encounterController != null
                && encounterController.TryGetRoomState(RoomId,
                    out DungeonRoomEncounterRuntimeState encounter)
                && encounter.State == RoomEncounterState.Locked;
        }

        private void ApplyHitFeedback()
        {
            if (spriteRenderer != null) spriteRenderer.color = HitColor;
            transform.localScale = normalScale * 1.16f;
        }

        private void Die()
        {
            if (deathRaised) return;
            deathRaised = true;
            IsAlive = false;
            Action<DungeonEnemy> callback = Died;
            Died = null;
            callback?.Invoke(this);
            gameObject.SetActive(false);
            DestroyRuntimeObject(gameObject);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
