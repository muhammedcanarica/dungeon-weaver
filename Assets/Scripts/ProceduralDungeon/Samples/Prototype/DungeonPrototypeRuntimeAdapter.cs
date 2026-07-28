using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonRuntimeController), typeof(DungeonPlayerSpawner))]
    public sealed class DungeonPrototypeRuntimeAdapter : MonoBehaviour
    {
        [SerializeField] private DungeonRuntimeController runtimeController;
        [SerializeField] private DungeonEnemyEncounterRegistry enemyRegistry;
        [SerializeField] private TopDownPlayerController playerController;
        [SerializeField] private TopDownPlayerAttack playerAttack;
        [SerializeField] private DungeonPrototypeDeathRespawn deathRespawn;

        private DungeonPlayerSpawner actorSpawner;
        private bool subscribed;

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Initialize()
        {
            CacheComponents();
            Subscribe();
        }

        public void Shutdown()
        {
            Unsubscribe();
            SetPrototypeGameplay(false);
        }

        private void HandleRuntimeClearing()
        {
            SetPrototypeGameplay(false);
            enemyRegistry?.ClearAll();
        }

        private void HandleGenerationCompleted(int seed)
        {
            CacheComponents();
            if (enemyRegistry != null)
            {
                enemyRegistry.SetTrackedActor(actorSpawner != null
                    ? actorSpawner.TrackedActor : null);
                if (!enemyRegistry.BuildPlans(seed))
                    Debug.LogError("[DungeonPrototypeRuntimeAdapter] Enemy plan generation failed.", this);
            }
            SetPrototypeGameplay(true);
            deathRespawn?.Initialize();
        }

        private void SetPrototypeGameplay(bool enabledState)
        {
            if (playerController != null) playerController.enabled = enabledState;
            if (playerAttack != null) playerAttack.enabled = enabledState;
        }

        private void CacheComponents()
        {
            if (runtimeController == null) runtimeController = GetComponent<DungeonRuntimeController>();
            if (enemyRegistry == null) enemyRegistry = GetComponent<DungeonEnemyEncounterRegistry>();
            if (actorSpawner == null) actorSpawner = GetComponent<DungeonPlayerSpawner>();
            Transform actor = actorSpawner != null ? actorSpawner.TrackedActor : null;
            if (actor == null) return;
            if (playerController == null) playerController = actor.GetComponent<TopDownPlayerController>();
            if (playerAttack == null) playerAttack = actor.GetComponent<TopDownPlayerAttack>();
            if (deathRespawn == null) deathRespawn = actor.GetComponent<DungeonPrototypeDeathRespawn>();
        }

        private void Subscribe()
        {
            if (runtimeController == null || subscribed) return;
            runtimeController.RuntimeClearing += HandleRuntimeClearing;
            runtimeController.GenerationCompleted += HandleGenerationCompleted;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (runtimeController != null && subscribed)
            {
                runtimeController.RuntimeClearing -= HandleRuntimeClearing;
                runtimeController.GenerationCompleted -= HandleGenerationCompleted;
            }
            subscribed = false;
        }
    }
}
