using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonDoorwayBuilder))]
    [RequireComponent(typeof(DungeonRoomRoleAssigner), typeof(DungeonRoomEncounterController))]
    [RequireComponent(typeof(DungeonTilemapRenderer), typeof(DungeonAreaTracker))]
    public sealed class DungeonEnemyEncounterRegistry : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private const int EncounterSalt = unchecked((int)0x51A7E91D);

        [SerializeField, Min(1)] private int normalMinimumCount = 2;
        [SerializeField, Min(1)] private int normalMaximumCount = 4;
        [SerializeField, Min(1)] private int bossMinimumCount = 4;
        [SerializeField, Min(1)] private int bossMaximumCount = 6;
        [SerializeField, Min(0)] private int minimumSpawnDistance = 3;
        [SerializeField, Min(0)] private int doorwaySafeDistance = 3;
        [SerializeField, Min(1)] private int safeCellsPerEnemy = 10;
        [SerializeField, Min(1)] private int enemyMaxHealth = 3;
        [SerializeField, Min(0f)] private float enemyMoveSpeed = 2.25f;

        private readonly Dictionary<int, List<DungeonEnemySpawnPlan>> plansByRoom =
            new Dictionary<int, List<DungeonEnemySpawnPlan>>();
        private readonly Dictionary<int, List<DungeonEnemy>> liveEnemiesByRoom =
            new Dictionary<int, List<DungeonEnemy>>();
        private readonly HashSet<int> spawnedRoomIds = new HashSet<int>();
        private DungeonGenerator generator;
        private DungeonDoorwayBuilder doorwayBuilder;
        private DungeonRoomRoleAssigner roleAssigner;
        private DungeonRoomEncounterController encounterController;
        private DungeonTilemapRenderer tilemapRenderer;
        private DungeonAreaTracker areaTracker;
        [SerializeField] private Transform trackedActor;
        private Grid grid;
        private Transform enemyRoot;
        private Sprite runtimeSprite;
        private bool hasPlans;
        private bool subscribed;
        private long planSignature;
        private int totalPlanCount;

        public int PlannedRoomCount => plansByRoom.Count;
        public int TotalPlannedEnemyCount => totalPlanCount;
        public int LiveEnemyCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<int, List<DungeonEnemy>> entry in liveEnemiesByRoom)
                    for (int i = 0; i < entry.Value.Count; i++)
                        if (entry.Value[i] != null && entry.Value[i].IsAlive) count++;
                return count;
            }
        }
        public bool HasPlans => hasPlans;
        public long PlanSignature => planSignature;
        public int MinimumSpawnDistance => Mathf.Max(0, minimumSpawnDistance);
        public int DoorwaySafeDistance => Mathf.Max(0, doorwaySafeDistance);
        public int SafeCellsPerEnemy => Mathf.Max(1, safeCellsPerEnemy);

        public void SetTrackedActor(Transform actorTransform)
        {
            trackedActor = actorTransform;
        }

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        public bool BuildPlans(int dungeonSeed)
        {
            ClearAll();
            CacheComponents();
            Subscribe();
            if (!HasValidSources())
            {
                Debug.LogError("[DungeonEnemyEncounterRegistry] Plan build rejected because source data is invalid.", this);
                return false;
            }

            var rooms = new List<RoomData>(generator.Rooms);
            rooms.Sort((left, right) => left.RoomId.CompareTo(right.RoomId));

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room.RoomId == roleAssigner.StartRoomId) continue;
                BuildRoomPlan(dungeonSeed, room);
            }

            planSignature = ComputePlanSignature();
            hasPlans = true;
            Debug.Log($"[DungeonEnemyEncounterRegistry] Planned {totalPlanCount} enemies for {plansByRoom.Count} rooms with signature {planSignature}.", this);
            return true;
        }

        public bool TryGetPlansForRoom(int roomId,
            out IReadOnlyList<DungeonEnemySpawnPlan> roomPlans)
        {
            if (plansByRoom.TryGetValue(roomId, out List<DungeonEnemySpawnPlan> plans))
            {
                roomPlans = plans.AsReadOnly();
                return true;
            }
            roomPlans = null;
            return false;
        }

        public int GetPlannedEnemyCount(int roomId)
        {
            return plansByRoom.TryGetValue(roomId, out List<DungeonEnemySpawnPlan> plans)
                ? plans.Count : 0;
        }

        public int GetLiveEnemyCount(int roomId)
        {
            if (!liveEnemiesByRoom.TryGetValue(roomId, out List<DungeonEnemy> enemies)) return 0;
            RemoveDestroyedReferences(enemies);
            return enemies.Count;
        }

        public bool TryGetLiveEnemiesForRoom(int roomId,
            out IReadOnlyList<DungeonEnemy> roomEnemies)
        {
            if (liveEnemiesByRoom.TryGetValue(roomId, out List<DungeonEnemy> enemies))
            {
                RemoveDestroyedReferences(enemies);
                roomEnemies = enemies.AsReadOnly();
                return true;
            }
            roomEnemies = null;
            return false;
        }

        public bool ActivateRoom(int roomId)
        {
            if (!hasPlans || spawnedRoomIds.Contains(roomId)
                || encounterController == null
                || !encounterController.TryGetRoomState(roomId,
                    out DungeonRoomEncounterRuntimeState state)
                || state.State != RoomEncounterState.Locked) return false;

            spawnedRoomIds.Add(roomId);
            if (!plansByRoom.TryGetValue(roomId, out List<DungeonEnemySpawnPlan> plans)
                || plans.Count == 0)
            {
                encounterController.TryClearRoom(roomId);
                return true;
            }

            EnsureRuntimeVisuals();
            RoomData room = FindRoom(roomId);
            var enemies = new List<DungeonEnemy>(plans.Count);
            liveEnemiesByRoom.Add(roomId, enemies);
            for (int i = 0; i < plans.Count; i++)
            {
                DungeonEnemySpawnPlan plan = plans[i];
                var enemyObject = new GameObject($"Enemy_Room{roomId}_{plan.EnemyIndex}");
                enemyObject.transform.SetParent(enemyRoot, false);
                Vector3 world = grid.GetCellCenterWorld(
                    new Vector3Int(plan.SpawnCell.x, plan.SpawnCell.y, 0));
                enemyObject.transform.position = new Vector3(world.x, world.y, 0f);
                enemyObject.AddComponent<SpriteRenderer>();
                enemyObject.AddComponent<Rigidbody2D>();
                enemyObject.AddComponent<CircleCollider2D>();
                DungeonEnemy enemy = enemyObject.AddComponent<DungeonEnemy>();
                enemy.Initialize(plan, room.Bounds, trackedActor, areaTracker, encounterController,
                    runtimeSprite, enemyMoveSpeed);
                enemy.Died += HandleEnemyDied;
                enemies.Add(enemy);
            }
            return true;
        }

        public void ClearRoomEnemies(int roomId)
        {
            if (!liveEnemiesByRoom.TryGetValue(roomId, out List<DungeonEnemy> enemies)) return;
            liveEnemiesByRoom.Remove(roomId);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                DungeonEnemy enemy = enemies[i];
                if (enemy == null) continue;
                enemy.Died -= HandleEnemyDied;
                enemy.DespawnWithoutDeath();
            }
            enemies.Clear();
        }

        public void ClearAll()
        {
            var roomIds = new List<int>(liveEnemiesByRoom.Keys);
            for (int i = 0; i < roomIds.Count; i++) ClearRoomEnemies(roomIds[i]);
            liveEnemiesByRoom.Clear();
            spawnedRoomIds.Clear();
            plansByRoom.Clear();
            totalPlanCount = 0;
            planSignature = 0;
            hasPlans = false;
            if (enemyRoot != null)
            {
                enemyRoot.gameObject.SetActive(false);
                DestroyRuntimeObject(enemyRoot.gameObject);
                enemyRoot = null;
            }
            if (runtimeSprite != null)
            {
                DestroyRuntimeObject(runtimeSprite);
                runtimeSprite = null;
            }
        }

        private void BuildRoomPlan(int dungeonSeed, RoomData room)
        {
            int derivedSeed = DeriveRoomSeed(dungeonSeed, room.RoomId);
            var random = new System.Random(derivedSeed);
            bool boss = room.RoomId == roleAssigner.BossRoomId;
            int minimum = boss ? bossMinimumCount : normalMinimumCount;
            int maximum = boss ? bossMaximumCount : normalMaximumCount;
            maximum = Mathf.Max(0, maximum);
            minimum = Mathf.Clamp(minimum, 0, maximum);
            int targetCount = maximum == 0 ? 0 : random.Next(minimum, maximum + 1);

            List<Vector2Int> safeCells = CollectSafeCells(room);
            int densityCapacity = safeCells.Count / SafeCellsPerEnemy;
            int desiredCount = Mathf.Min(targetCount, densityCapacity);
            Shuffle(safeCells, random);

            var selectedCells = new List<Vector2Int>(desiredCount);
            for (int i = 0; i < safeCells.Count && selectedCells.Count < desiredCount; i++)
            {
                Vector2Int candidate = safeCells[i];
                if (IsFarEnoughFromSelected(candidate, selectedCells)) selectedCells.Add(candidate);
            }

            var plans = new List<DungeonEnemySpawnPlan>(selectedCells.Count);
            for (int i = 0; i < selectedCells.Count; i++)
                plans.Add(new DungeonEnemySpawnPlan(room.RoomId, selectedCells[i], i, enemyMaxHealth));
            plansByRoom.Add(room.RoomId, plans);
            totalPlanCount += plans.Count;
            if (plans.Count < targetCount)
                Debug.LogWarning($"[DungeonEnemyEncounterRegistry] Room {room.RoomId} enemy count reduced from {targetCount} to {plans.Count}; {safeCells.Count} safe cells provide density capacity {densityCapacity} with minimum spacing {MinimumSpawnDistance}.", this);
        }

        private List<Vector2Int> CollectSafeCells(RoomData room)
        {
            var cells = new List<Vector2Int>();
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsValidFloorCell(cell) && IsOutsideDoorwaySafeAreas(cell)) cells.Add(cell);
                }
            }

            cells.Sort(CompareCells);
            return cells;
        }

        private bool IsValidFloorCell(Vector2Int cell)
        {
            var tileCell = new Vector3Int(cell.x, cell.y, 0);
            return tilemapRenderer.FloorTilemap.HasTile(tileCell)
                && !tilemapRenderer.WallTilemap.HasTile(tileCell);
        }

        private bool IsOutsideDoorwaySafeAreas(Vector2Int cell)
        {
            for (int i = 0; i < doorwayBuilder.Doorways.Count; i++)
            {
                DoorwayData doorway = doorwayBuilder.Doorways[i];
                if (ManhattanDistance(cell, doorway.EntranceCell) < DoorwaySafeDistance
                    || ManhattanDistance(cell, doorway.FirstCorridorCell) < DoorwaySafeDistance)
                    return false;
            }

            return true;
        }

        private bool IsFarEnoughFromSelected(Vector2Int candidate, List<Vector2Int> selectedCells)
        {
            for (int i = 0; i < selectedCells.Count; i++)
                if (ManhattanDistance(candidate, selectedCells[i]) < MinimumSpawnDistance) return false;
            return true;
        }

        private static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
        }

        private void HandleEncounterStateChanged(DungeonRoomEncounterChange change)
        {
            if (change.CurrentState == RoomEncounterState.Locked) ActivateRoom(change.RoomId);
            else if (change.CurrentState == RoomEncounterState.Cleared) ClearRoomEnemies(change.RoomId);
            else if (change.CurrentState == RoomEncounterState.Inactive)
            {
                ClearRoomEnemies(change.RoomId);
                spawnedRoomIds.Remove(change.RoomId);
            }
        }

        private void HandleEnemyDied(DungeonEnemy enemy)
        {
            if (enemy == null || !liveEnemiesByRoom.TryGetValue(enemy.RoomId,
                    out List<DungeonEnemy> enemies)) return;
            enemy.Died -= HandleEnemyDied;
            enemies.Remove(enemy);
            RemoveDestroyedReferences(enemies);
            if (enemies.Count != 0) return;
            liveEnemiesByRoom.Remove(enemy.RoomId);
            encounterController.TryClearRoom(enemy.RoomId);
        }

        private void EnsureRuntimeVisuals()
        {
            if (enemyRoot == null)
            {
                var rootObject = new GameObject("DungeonEnemies");
                rootObject.transform.SetParent(transform, false);
                enemyRoot = rootObject.transform;
            }
            if (runtimeSprite == null)
            {
                runtimeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f), 1f);
                runtimeSprite.name = "Runtime Dungeon Enemy Sprite";
            }
        }

        private long ComputePlanSignature()
        {
            var roomIds = new List<int>(plansByRoom.Keys);
            roomIds.Sort();
            ulong hash = Offset;
            AddInt(ref hash, roomIds.Count);
            AddInt(ref hash, totalPlanCount);
            for (int roomIndex = 0; roomIndex < roomIds.Count; roomIndex++)
            {
                int roomId = roomIds[roomIndex];
                List<DungeonEnemySpawnPlan> plans = plansByRoom[roomId];
                AddInt(ref hash, roomId);
                AddInt(ref hash, plans.Count);
                for (int planIndex = 0; planIndex < plans.Count; planIndex++)
                {
                    DungeonEnemySpawnPlan plan = plans[planIndex];
                    AddInt(ref hash, plan.EnemyIndex);
                    AddInt(ref hash, plan.SpawnCell.x);
                    AddInt(ref hash, plan.SpawnCell.y);
                    AddInt(ref hash, plan.MaxHealth);
                    AddInt(ref hash, plan.TypeId);
                }
            }
            return unchecked((long)hash);
        }

        private bool HasValidSources()
        {
            return generator != null && generator.Rooms != null && generator.Rooms.Count > 0
                && doorwayBuilder != null && doorwayBuilder.IsDoorwayDataCurrent
                && roleAssigner != null && roleAssigner.IsRoleDataCurrent
                && encounterController != null && encounterController.HasEncounterStates
                && tilemapRenderer != null && tilemapRenderer.IsTilemapCurrent
                && tilemapRenderer.FloorTilemap != null && tilemapRenderer.WallTilemap != null
                && areaTracker != null && areaTracker.IsLookupCurrent
                && trackedActor != null && grid != null;
        }

        private void CacheComponents()
        {
            if (generator == null) generator = GetComponent<DungeonGenerator>();
            if (doorwayBuilder == null) doorwayBuilder = GetComponent<DungeonDoorwayBuilder>();
            if (roleAssigner == null) roleAssigner = GetComponent<DungeonRoomRoleAssigner>();
            if (encounterController == null)
                encounterController = GetComponent<DungeonRoomEncounterController>();
            if (tilemapRenderer == null) tilemapRenderer = GetComponent<DungeonTilemapRenderer>();
            if (areaTracker == null) areaTracker = GetComponent<DungeonAreaTracker>();
            if (tilemapRenderer != null && tilemapRenderer.FloorTilemap != null)
                grid = tilemapRenderer.FloorTilemap.GetComponentInParent<Grid>();
        }

        private void Subscribe()
        {
            if (encounterController == null || subscribed) return;
            encounterController.EncounterStateChanged += HandleEncounterStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (encounterController != null && subscribed)
                encounterController.EncounterStateChanged -= HandleEncounterStateChanged;
            subscribed = false;
        }

        private RoomData FindRoom(int roomId)
        {
            if (generator == null || generator.Rooms == null) return null;
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            return null;
        }

        private static int DeriveRoomSeed(int dungeonSeed, int roomId)
        {
            return unchecked(dungeonSeed * 486187739 + roomId * 16777619 + EncounterSalt);
        }

        private static void Shuffle(List<Vector2Int> cells, System.Random random)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (cells[i], cells[swapIndex]) = (cells[swapIndex], cells[i]);
            }
        }

        private static int CompareCells(Vector2Int left, Vector2Int right)
        {
            int value = left.x.CompareTo(right.x);
            return value != 0 ? value : left.y.CompareTo(right.y);
        }

        private static void RemoveDestroyedReferences(List<DungeonEnemy> enemies)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i] == null || !enemies[i].IsAlive) enemies.RemoveAt(i);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8) AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash = unchecked(hash * Prime);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
