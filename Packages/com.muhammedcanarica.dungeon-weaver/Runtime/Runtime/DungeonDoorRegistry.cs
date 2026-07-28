using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonDoorwayBuilder), typeof(DungeonTilemapRenderer))]
    public sealed class DungeonDoorRegistry : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private const float DoorThicknessRatio = 0.2f;

        [SerializeField] private Grid grid;

        private readonly List<DungeonDoor> doors = new List<DungeonDoor>();
        private readonly Dictionary<int, List<DungeonDoor>> doorsByRoom =
            new Dictionary<int, List<DungeonDoor>>();
        private readonly Dictionary<int, IReadOnlyList<DungeonDoor>> readOnlyDoorsByRoom =
            new Dictionary<int, IReadOnlyList<DungeonDoor>>();
        private IReadOnlyList<DungeonDoor> readOnlyDoors;
        private DungeonDoorwayBuilder doorwayBuilder;
        private DungeonTilemapRenderer tilemapRenderer;
        private Transform doorRoot;
        private Sprite runtimeSprite;
        private long layoutSignature;
        private bool hasBuiltDoors;
        private int builtCorridorWidth;

        public int DoorCount => doors.Count;
        public int OpenDoorCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < doors.Count; i++) if (doors[i] != null && doors[i].IsOpen) count++;
                return count;
            }
        }
        public int ClosedDoorCount => DoorCount - OpenDoorCount;
        public int DoorwayAssociationCount => doorwayBuilder != null ? doorwayBuilder.Doorways.Count : 0;
        public long LayoutSignature => layoutSignature;
        public bool HasBuiltDoors => hasBuiltDoors;
        public int CorridorWidth => builtCorridorWidth;
        public IReadOnlyList<DungeonDoor> Doors => readOnlyDoors ??= doors.AsReadOnly();

        public bool BuildDoors()
        {
            ClearDoors();
            CacheComponents();
            int corridorWidth = tilemapRenderer != null ? tilemapRenderer.CorridorWidth : 0;
            if (doorwayBuilder == null || !doorwayBuilder.IsDoorwayDataCurrent || grid == null
                || corridorWidth <= 0 || doorwayBuilder.Doorways.Count == 0)
            {
                Debug.LogError("[DungeonDoorRegistry] Door build rejected because doorway data or Grid is invalid.", this);
                return false;
            }

            var groupsByKey = new Dictionary<DoorKey, DoorBuildGroup>();
            for (int i = 0; i < doorwayBuilder.Doorways.Count; i++)
            {
                DoorwayData doorway = doorwayBuilder.Doorways[i];
                var key = new DoorKey(doorway.RoomId, doorway.EntranceCell, doorway.OutwardDirection);
                if (!groupsByKey.TryGetValue(key, out DoorBuildGroup group))
                {
                    group = new DoorBuildGroup(key, doorway.FirstCorridorCell);
                    groupsByKey.Add(key, group);
                }
                else if (group.FirstCorridorCell != doorway.FirstCorridorCell)
                {
                    Debug.LogError("[DungeonDoorRegistry] Coalesced doorway records disagree on their first corridor cell.", this);
                    return false;
                }
                group.ConnectionIndices.Add(doorway.ConnectionIndex);
            }

            var groups = new List<DoorBuildGroup>(groupsByKey.Values);
            groups.Sort(CompareGroups);
            var rootObject = new GameObject("DungeonDoors");
            rootObject.transform.SetParent(transform, false);
            doorRoot = rootObject.transform;
            runtimeSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            runtimeSprite.name = "Runtime Dungeon Door Sprite";

            for (int i = 0; i < groups.Count; i++) CreateDoor(groups[i], corridorWidth);
            builtCorridorWidth = corridorWidth;
            layoutSignature = ComputeLayoutSignature(doorwayBuilder.Doorways, corridorWidth);
            hasBuiltDoors = doors.Count == groups.Count && doors.Count > 0;
            if (!hasBuiltDoors)
            {
                ClearDoors();
                Debug.LogError("[DungeonDoorRegistry] Physical door creation was incomplete.", this);
                return false;
            }

            Debug.Log($"[DungeonDoorRegistry] Built {doors.Count} physical doors from {doorwayBuilder.Doorways.Count} doorway records.", this);
            return true;
        }

        public void OpenAllDoors()
        {
            for (int i = 0; i < doors.Count; i++) doors[i]?.Open();
        }

        public bool OpenRoomDoors(int roomId)
        {
            if (!doorsByRoom.TryGetValue(roomId, out List<DungeonDoor> roomDoors)) return false;
            for (int i = 0; i < roomDoors.Count; i++) roomDoors[i]?.Open();
            return true;
        }

        public bool CloseRoomDoors(int roomId)
        {
            if (!doorsByRoom.TryGetValue(roomId, out List<DungeonDoor> roomDoors)) return false;
            for (int i = 0; i < roomDoors.Count; i++) roomDoors[i]?.Close();
            return true;
        }

        public bool TryGetDoorsForRoom(int roomId, out IReadOnlyList<DungeonDoor> roomDoors)
        {
            if (readOnlyDoorsByRoom.TryGetValue(roomId, out roomDoors)) return true;
            roomDoors = null;
            return false;
        }

        public bool TryGetDoor(int roomId, int connectionIndex, out DungeonDoor door)
        {
            door = null;
            if (!doorsByRoom.TryGetValue(roomId, out List<DungeonDoor> roomDoors)) return false;
            for (int i = 0; i < roomDoors.Count; i++)
            {
                if (!roomDoors[i].RepresentsConnection(connectionIndex)) continue;
                door = roomDoors[i];
                return true;
            }
            return false;
        }

        public void ClearDoors()
        {
            doors.Clear();
            doorsByRoom.Clear();
            readOnlyDoorsByRoom.Clear();
            layoutSignature = 0;
            hasBuiltDoors = false;
            builtCorridorWidth = 0;
            if (doorRoot != null)
            {
                doorRoot.gameObject.SetActive(false);
                DestroyRuntimeObject(doorRoot.gameObject);
                doorRoot = null;
            }
            if (runtimeSprite != null)
            {
                DestroyRuntimeObject(runtimeSprite);
                runtimeSprite = null;
            }
        }

        private void OnDestroy()
        {
            if (runtimeSprite == null) return;
            DestroyRuntimeObject(runtimeSprite);
            runtimeSprite = null;
        }

        public static Vector2 CalculateDoorWorldSize(DoorwayDirection direction,
            Vector3 cellSize, int corridorWidth)
        {
            bool horizontal = direction == DoorwayDirection.Up
                || direction == DoorwayDirection.Down;
            float cellWidth = Mathf.Abs(cellSize.x);
            float cellHeight = Mathf.Abs(cellSize.y);
            int safeCorridorWidth = Mathf.Max(1, corridorWidth);
            return horizontal
                ? new Vector2(cellWidth * safeCorridorWidth,
                    cellHeight * DoorThicknessRatio)
                : new Vector2(cellWidth * DoorThicknessRatio,
                    cellHeight * safeCorridorWidth);
        }

        private void CreateDoor(DoorBuildGroup group, int corridorWidth)
        {
            group.ConnectionIndices.Sort();
            string connections = string.Empty;
            for (int i = 0; i < group.ConnectionIndices.Count; i++)
            {
                if (i > 0) connections += "_";
                connections += group.ConnectionIndices[i];
            }

            var doorObject = new GameObject($"Door_Room{group.Key.RoomId}_Connection{connections}");
            doorObject.transform.SetParent(doorRoot, false);
            Vector3 entranceWorld = grid.GetCellCenterWorld(
                new Vector3Int(group.Key.EntranceCell.x, group.Key.EntranceCell.y, 0));
            Vector3 corridorWorld = grid.GetCellCenterWorld(
                new Vector3Int(group.FirstCorridorCell.x, group.FirstCorridorCell.y, 0));
            doorObject.transform.position = (entranceWorld + corridorWorld) * 0.5f;
            doorObject.AddComponent<SpriteRenderer>();
            doorObject.AddComponent<BoxCollider2D>();
            DungeonDoor door = doorObject.AddComponent<DungeonDoor>();
            bool horizontal = group.Key.Direction == DoorwayDirection.Up
                || group.Key.Direction == DoorwayDirection.Down;
            DungeonDoorOrientation orientation = horizontal
                ? DungeonDoorOrientation.Horizontal : DungeonDoorOrientation.Vertical;
            Vector2 size = CalculateDoorWorldSize(group.Key.Direction,
                grid.cellSize, corridorWidth);
            door.Initialize(group.Key.RoomId, group.Key.EntranceCell, group.Key.Direction,
                orientation, size, group.ConnectionIndices, runtimeSprite);
            doors.Add(door);
            if (!doorsByRoom.TryGetValue(group.Key.RoomId, out List<DungeonDoor> roomDoors))
            {
                roomDoors = new List<DungeonDoor>();
                doorsByRoom.Add(group.Key.RoomId, roomDoors);
                readOnlyDoorsByRoom.Add(group.Key.RoomId, roomDoors.AsReadOnly());
            }
            roomDoors.Add(door);
        }

        private long ComputeLayoutSignature(IReadOnlyList<DoorwayData> sourceDoorways,
            int corridorWidth)
        {
            var sorted = new List<DoorwayData>(sourceDoorways);
            sorted.Sort(CompareDoorways);
            ulong hash = Offset;
            AddInt(ref hash, sorted.Count);
            AddInt(ref hash, corridorWidth);
            for (int i = 0; i < sorted.Count; i++)
            {
                DoorwayData doorway = sorted[i];
                Vector3 entranceWorld = grid.GetCellCenterWorld(
                    new Vector3Int(doorway.EntranceCell.x, doorway.EntranceCell.y, 0));
                Vector3 corridorWorld = grid.GetCellCenterWorld(new Vector3Int(
                    doorway.FirstCorridorCell.x, doorway.FirstCorridorCell.y, 0));
                Vector3 position = (entranceWorld + corridorWorld) * 0.5f;
                Vector2 size = CalculateDoorWorldSize(doorway.OutwardDirection,
                    grid.cellSize, corridorWidth);
                bool horizontal = doorway.OutwardDirection == DoorwayDirection.Up
                    || doorway.OutwardDirection == DoorwayDirection.Down;
                AddInt(ref hash, doorway.RoomId);
                AddInt(ref hash, doorway.ConnectionIndex);
                AddInt(ref hash, doorway.EntranceCell.x);
                AddInt(ref hash, doorway.EntranceCell.y);
                AddInt(ref hash, doorway.FirstCorridorCell.x);
                AddInt(ref hash, doorway.FirstCorridorCell.y);
                AddInt(ref hash, (int)doorway.OutwardDirection);
                AddInt(ref hash, Mathf.RoundToInt(position.x * 10000f));
                AddInt(ref hash, Mathf.RoundToInt(position.y * 10000f));
                AddInt(ref hash, Mathf.RoundToInt(size.x * 10000f));
                AddInt(ref hash, Mathf.RoundToInt(size.y * 10000f));
                AddInt(ref hash, horizontal ? (int)DungeonDoorOrientation.Horizontal
                    : (int)DungeonDoorOrientation.Vertical);
            }
            return unchecked((long)hash);
        }

        private void CacheComponents()
        {
            if (doorwayBuilder == null) doorwayBuilder = GetComponent<DungeonDoorwayBuilder>();
            if (tilemapRenderer == null) tilemapRenderer = GetComponent<DungeonTilemapRenderer>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
        }

        private static int CompareGroups(DoorBuildGroup left, DoorBuildGroup right)
        {
            int result = left.Key.RoomId.CompareTo(right.Key.RoomId);
            if (result != 0) return result;
            result = left.Key.EntranceCell.y.CompareTo(right.Key.EntranceCell.y);
            if (result != 0) return result;
            result = left.Key.EntranceCell.x.CompareTo(right.Key.EntranceCell.x);
            return result != 0 ? result : left.Key.Direction.CompareTo(right.Key.Direction);
        }

        private static int CompareDoorways(DoorwayData left, DoorwayData right)
        {
            int result = left.RoomId.CompareTo(right.RoomId);
            if (result != 0) return result;
            result = left.ConnectionIndex.CompareTo(right.ConnectionIndex);
            if (result != 0) return result;
            result = left.EntranceCell.y.CompareTo(right.EntranceCell.y);
            if (result != 0) return result;
            result = left.EntranceCell.x.CompareTo(right.EntranceCell.x);
            return result != 0 ? result : left.OutwardDirection.CompareTo(right.OutwardDirection);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
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

        private readonly struct DoorKey : IEquatable<DoorKey>
        {
            public int RoomId { get; }
            public Vector2Int EntranceCell { get; }
            public DoorwayDirection Direction { get; }

            public DoorKey(int roomId, Vector2Int entranceCell, DoorwayDirection direction)
            {
                RoomId = roomId;
                EntranceCell = entranceCell;
                Direction = direction;
            }

            public bool Equals(DoorKey other)
            {
                return RoomId == other.RoomId && EntranceCell == other.EntranceCell
                    && Direction == other.Direction;
            }

            public override bool Equals(object obj)
            {
                return obj is DoorKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = RoomId;
                    hash = (hash * 397) ^ EntranceCell.GetHashCode();
                    return (hash * 397) ^ (int)Direction;
                }
            }
        }

        private sealed class DoorBuildGroup
        {
            public DoorKey Key { get; }
            public Vector2Int FirstCorridorCell { get; }
            public List<int> ConnectionIndices { get; } = new List<int>();

            public DoorBuildGroup(DoorKey key, Vector2Int firstCorridorCell)
            {
                Key = key;
                FirstCorridorCell = firstCorridorCell;
            }
        }
    }
}
