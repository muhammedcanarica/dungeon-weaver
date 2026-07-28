using UnityEngine;

namespace ProceduralDungeon
{
    public sealed class DungeonEnemySpawnPlan
    {
        public const int EnemyTypeId = 1;

        public int RoomId { get; }
        public Vector2Int SpawnCell { get; }
        public int EnemyIndex { get; }
        public int MaxHealth { get; }
        public int TypeId => EnemyTypeId;

        public DungeonEnemySpawnPlan(int roomId, Vector2Int spawnCell, int enemyIndex,
            int maxHealth)
        {
            RoomId = roomId;
            SpawnCell = spawnCell;
            EnemyIndex = enemyIndex;
            MaxHealth = maxHealth;
        }
    }
}
