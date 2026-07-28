using System.Collections;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(PrototypePlayerHealth))]
    public sealed class DungeonPrototypeDeathRespawn : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 1f;

        private PrototypePlayerHealth health;
        private TopDownPlayerController playerController;
        private TopDownPlayerAttack playerAttack;
        private Rigidbody2D body;
        private DungeonRuntimeController runtimeController;
        private Coroutine restartRoutine;
        private bool subscribed;
        private bool restartPending;

        public float RespawnDelay => respawnDelay;
        public bool IsRestartPending => restartPending;

        private void OnEnable()
        {
            Initialize();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (restartRoutine != null) StopCoroutine(restartRoutine);
            restartRoutine = null;
        }

        public void Initialize()
        {
            CacheComponents();
            Subscribe();
        }

        private void HandleDied()
        {
            if (restartPending) return;

            restartPending = true;
            SetGameplayEnabled(false);
            if (!Application.isPlaying) return;
            restartRoutine = StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            if (respawnDelay > 0f) yield return new WaitForSeconds(respawnDelay);
            restartRoutine = null;
            TryRestartRun();
        }

        public bool TryRestartRun()
        {
            CacheComponents();
            if (runtimeController == null || !runtimeController.HasSuccessfulDungeon)
            {
                Debug.LogError("[DungeonPrototypeDeathRespawn] Restart rejected: no successful dungeon is available.", this);
                return false;
            }

            if (runtimeController.RegenerateCurrentSeed()) return true;

            Debug.LogError("[DungeonPrototypeDeathRespawn] Restart failed; player controls remain disabled.", this);
            return false;
        }

        private void HandleGenerationCompleted(int _)
        {
            if (health != null) health.ResetHealth();
            if (!restartPending) return;

            restartPending = false;
            SetGameplayEnabled(true);
        }

        private void SetGameplayEnabled(bool enabledState)
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            if (playerController != null) playerController.enabled = enabledState;
            if (playerAttack != null) playerAttack.enabled = enabledState;
        }

        private void CacheComponents()
        {
            if (health == null) health = GetComponent<PrototypePlayerHealth>();
            if (playerController == null) playerController = GetComponent<TopDownPlayerController>();
            if (playerAttack == null) playerAttack = GetComponent<TopDownPlayerAttack>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (runtimeController == null)
                runtimeController = FindFirstObjectByType<DungeonRuntimeController>();
        }

        private void Subscribe()
        {
            if (health != null && !subscribed)
            {
                health.Died += HandleDied;
                subscribed = true;
            }
            if (runtimeController != null) runtimeController.GenerationCompleted -= HandleGenerationCompleted;
            if (runtimeController != null) runtimeController.GenerationCompleted += HandleGenerationCompleted;
        }

        private void Unsubscribe()
        {
            if (health != null && subscribed) health.Died -= HandleDied;
            if (runtimeController != null) runtimeController.GenerationCompleted -= HandleGenerationCompleted;
            subscribed = false;
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0f, respawnDelay);
        }
    }
}
