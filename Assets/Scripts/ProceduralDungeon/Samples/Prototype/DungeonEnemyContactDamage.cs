using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonEnemy), typeof(Collider2D))]
    public sealed class DungeonEnemyContactDamage : MonoBehaviour
    {
        private DungeonEnemy enemy;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomEncounterController encounterController;

        public int ContactDamage { get; private set; } = 1;

        internal void Initialize(DungeonEnemy dungeonEnemy, DungeonAreaTracker tracker,
            DungeonRoomEncounterController encounters, int damage)
        {
            enemy = dungeonEnemy;
            areaTracker = tracker;
            encounterController = encounters;
            ContactDamage = Mathf.Max(1, damage);
        }

        private void Awake()
        {
            if (enemy == null) enemy = GetComponent<DungeonEnemy>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDealContactDamage(other != null ? other.GetComponentInParent<PrototypePlayerHealth>() : null);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealContactDamage(other != null ? other.GetComponentInParent<PrototypePlayerHealth>() : null);
        }

        public bool TryDealContactDamage(PrototypePlayerHealth playerHealth)
        {
            if (!CanDamage(playerHealth)) return false;
            return playerHealth.TakeDamage(ContactDamage);
        }

        private bool CanDamage(PrototypePlayerHealth playerHealth)
        {
            return enemy != null && enemy.IsAlive && playerHealth != null && !playerHealth.IsDead
                && areaTracker != null && areaTracker.CurrentAreaType == DungeonAreaType.Room
                && areaTracker.CurrentRoomId == enemy.RoomId && encounterController != null
                && encounterController.TryGetRoomState(enemy.RoomId,
                    out DungeonRoomEncounterRuntimeState encounter)
                && encounter.State == RoomEncounterState.Locked;
        }
    }
}
