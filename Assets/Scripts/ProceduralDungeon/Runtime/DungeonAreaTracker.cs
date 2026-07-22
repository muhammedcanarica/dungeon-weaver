using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum DungeonAreaType
    {
        OutsideDungeon,
        Room,
        Corridor
    }

    public readonly struct DungeonAreaTransition
    {
        public DungeonAreaType PreviousAreaType { get; }
        public DungeonAreaType CurrentAreaType { get; }
        public int PreviousRoomId { get; }
        public int CurrentRoomId { get; }
        public int CorridorIndex { get; }
        public int ConnectionIndex { get; }
        public DoorwayData Doorway { get; }
        public Vector2Int Cell { get; }

        public DungeonAreaTransition(DungeonAreaType previousAreaType, DungeonAreaType currentAreaType,
            int previousRoomId, int currentRoomId, int corridorIndex, int connectionIndex,
            DoorwayData doorway, Vector2Int cell)
        {
            PreviousAreaType = previousAreaType;
            CurrentAreaType = currentAreaType;
            PreviousRoomId = previousRoomId;
            CurrentRoomId = currentRoomId;
            CorridorIndex = corridorIndex;
            ConnectionIndex = connectionIndex;
            Doorway = doorway;
            Cell = cell;
        }
    }

    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder), typeof(DungeonCorridorBuilder))]
    [RequireComponent(typeof(DungeonDoorwayBuilder))]
    public sealed class DungeonAreaTracker : MonoBehaviour
    {
        public const int InvalidId = -1;
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        [SerializeField] private Grid grid;
        [SerializeField] private Transform player;

        private readonly Dictionary<Vector2Int, int> roomByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, List<CorridorCellInfo>> corridorsByCell =
            new Dictionary<Vector2Int, List<CorridorCellInfo>>();
        private DungeonGenerator generator;
        private DungeonGraphBuilder graphBuilder;
        private DungeonCorridorBuilder corridorBuilder;
        private DungeonDoorwayBuilder doorwayBuilder;
        private bool hasLookup;
        private long lookupSignature;
        private int overlapCellCount;
        private DungeonAreaType currentAreaType = DungeonAreaType.OutsideDungeon;
        private Vector2Int currentCell;
        private int currentRoomId = InvalidId;
        private int previousRoomId = InvalidId;
        private int currentCorridorIndex = InvalidId;
        private int currentConnectionIndex = InvalidId;
        private DoorwayData lastUsedDoorway;
        private int lastTransitionConnectionIndex = InvalidId;

        public DungeonAreaType CurrentAreaType => currentAreaType;
        public Vector2Int CurrentCell => currentCell;
        public int CurrentRoomId => currentRoomId;
        public int PreviousRoomId => previousRoomId;
        public int CurrentCorridorIndex => currentCorridorIndex;
        public int CurrentConnectionIndex => currentConnectionIndex;
        public int LastTransitionConnectionIndex => lastTransitionConnectionIndex;
        public DoorwayData LastUsedDoorway => lastUsedDoorway;
        public bool IsInsideRoom => currentAreaType == DungeonAreaType.Room;
        public bool IsInsideCorridor => currentAreaType == DungeonAreaType.Corridor;
        public bool HasLookup => hasLookup;
        public bool IsLookupCurrent => hasLookup && HasCurrentSources();
        public long LookupSignature => lookupSignature;
        public int RoomLookupCellCount => roomByCell.Count;
        public int CorridorLookupCellCount => corridorsByCell.Count;
        public int CorridorOnlyCellCount => corridorsByCell.Count - overlapCellCount;
        public int OverlapCellCount => overlapCellCount;
        public int TotalLookupCellCount => roomByCell.Count + corridorsByCell.Count - overlapCellCount;

        public event Action<DungeonAreaTransition> RoomEntered;
        public event Action<DungeonAreaTransition> RoomExited;
        public event Action<DungeonAreaTransition> AreaChanged;

        private void Awake()
        {
            CacheComponents();
        }

        private void LateUpdate()
        {
            if (hasLookup && player != null) RefreshPlayerArea();
        }

        public bool BuildLookup()
        {
            ClearTracking();
            CacheComponents();
            if (!HasCurrentSources() || grid == null || player == null)
            {
                Debug.LogError("[DungeonAreaTracker] Lookup build rejected because source data or scene references are invalid.", this);
                return false;
            }

            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room == null)
                {
                    FailBuild("A room record is null.");
                    return false;
                }

                for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                {
                    for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        if (roomByCell.ContainsKey(cell))
                        {
                            FailBuild($"Room lookup contains an overlapping cell at {cell}.");
                            return false;
                        }

                        roomByCell.Add(cell, room.RoomId);
                    }
                }
            }

            var overlapCells = new HashSet<Vector2Int>();
            for (int corridorIndex = 0; corridorIndex < corridorBuilder.Corridors.Count; corridorIndex++)
            {
                CorridorData corridor = corridorBuilder.Corridors[corridorIndex];
                if (corridor == null || corridorIndex >= graphBuilder.Connections.Count)
                {
                    FailBuild($"Corridor metadata is invalid at index {corridorIndex}.");
                    return false;
                }

                RoomConnectionData connection = graphBuilder.Connections[corridorIndex];
                if (connection == null || corridor.RoomAId != connection.RoomAId
                    || corridor.RoomBId != connection.RoomBId
                    || corridor.IsPrimaryConnection != connection.IsPrimaryConnection)
                {
                    FailBuild($"Corridor and connection data do not match at index {corridorIndex}.");
                    return false;
                }

                CorridorCellInfo candidate = new CorridorCellInfo(corridorIndex, corridorIndex);
                for (int pathIndex = 0; pathIndex < corridor.PathCells.Count; pathIndex++)
                {
                    Vector2Int cell = corridor.PathCells[pathIndex];
                    if (roomByCell.ContainsKey(cell)) overlapCells.Add(cell);
                    if (!corridorsByCell.TryGetValue(cell, out List<CorridorCellInfo> entries))
                    {
                        entries = new List<CorridorCellInfo>();
                        corridorsByCell.Add(cell, entries);
                    }

                    entries.Add(candidate);
                }
            }

            foreach (KeyValuePair<Vector2Int, List<CorridorCellInfo>> entry in corridorsByCell)
                entry.Value.Sort(CompareCorridors);
            overlapCellCount = overlapCells.Count;
            lookupSignature = ComputeStableLookupSignature();
            hasLookup = true;
            Debug.Log($"[DungeonAreaTracker] Built lookup with {RoomLookupCellCount} room cells, {CorridorOnlyCellCount} corridor-only cells and {OverlapCellCount} overlap cells.", this);
            return true;
        }

        public void ClearTracking()
        {
            roomByCell.Clear();
            corridorsByCell.Clear();
            hasLookup = false;
            lookupSignature = 0;
            overlapCellCount = 0;
            currentAreaType = DungeonAreaType.OutsideDungeon;
            currentCell = default;
            currentRoomId = InvalidId;
            previousRoomId = InvalidId;
            currentCorridorIndex = InvalidId;
            currentConnectionIndex = InvalidId;
            lastUsedDoorway = null;
            lastTransitionConnectionIndex = InvalidId;
        }

        public bool RefreshPlayerArea(bool notifyEvents = true)
        {
            if (!hasLookup || grid == null || player == null) return false;
            Vector3Int cell = grid.WorldToCell(player.position);
            return RefreshCell(new Vector2Int(cell.x, cell.y), notifyEvents);
        }

        private bool RefreshCell(Vector2Int cell, bool notifyEvents)
        {
            DungeonAreaType nextArea;
            int nextRoom = InvalidId;
            int nextCorridor = InvalidId;
            int nextConnection = InvalidId;
            DoorwayData transitionDoorway = null;
            if (roomByCell.TryGetValue(cell, out int roomId))
            {
                nextArea = DungeonAreaType.Room;
                nextRoom = roomId;
            }
            else if (corridorsByCell.TryGetValue(cell, out List<CorridorCellInfo> corridorEntries))
            {
                nextArea = DungeonAreaType.Corridor;
                transitionDoorway = FindTransitionDoorway(currentAreaType, currentRoomId, currentCell,
                    nextArea, nextRoom, cell, currentConnectionIndex);
                int preferredConnection = transitionDoorway != null
                    ? transitionDoorway.ConnectionIndex
                    : currentAreaType == DungeonAreaType.Corridor ? currentConnectionIndex : InvalidId;
                CorridorCellInfo corridor = ChooseCorridor(corridorEntries, preferredConnection);
                nextCorridor = corridor.CorridorIndex;
                nextConnection = corridor.ConnectionIndex;
            }
            else
            {
                nextArea = DungeonAreaType.OutsideDungeon;
            }

            bool changed = nextArea != currentAreaType
                || (nextArea == DungeonAreaType.Room && nextRoom != currentRoomId)
                || (nextArea == DungeonAreaType.Corridor
                    && (nextCorridor != currentCorridorIndex || nextConnection != currentConnectionIndex));
            Vector2Int oldCell = currentCell;
            currentCell = cell;
            if (!changed) return false;

            DungeonAreaType oldArea = currentAreaType;
            int oldRoom = currentRoomId;
            int oldCorridor = currentCorridorIndex;
            int oldConnection = currentConnectionIndex;
            if (transitionDoorway == null)
                transitionDoorway = FindTransitionDoorway(oldArea, oldRoom, oldCell, nextArea, nextRoom, cell, oldConnection);
            int transitionConnection = transitionDoorway != null
                ? transitionDoorway.ConnectionIndex
                : nextConnection != InvalidId ? nextConnection : oldConnection;
            int transitionCorridor = nextCorridor != InvalidId ? nextCorridor : oldCorridor;

            if (oldArea == DungeonAreaType.Room && oldRoom != InvalidId && oldRoom != nextRoom)
                previousRoomId = oldRoom;
            currentAreaType = nextArea;
            currentRoomId = nextRoom;
            currentCorridorIndex = nextCorridor;
            currentConnectionIndex = nextConnection;
            lastUsedDoorway = transitionDoorway;
            lastTransitionConnectionIndex = transitionConnection;

            var transition = new DungeonAreaTransition(oldArea, nextArea, oldRoom, nextRoom,
                transitionCorridor, transitionConnection, transitionDoorway, cell);
            if (!notifyEvents) return true;
            if (oldArea == DungeonAreaType.Room && oldRoom != nextRoom) RoomExited?.Invoke(transition);
            AreaChanged?.Invoke(transition);
            if (nextArea == DungeonAreaType.Room && nextRoom != oldRoom) RoomEntered?.Invoke(transition);
            return true;
        }

        private DoorwayData FindTransitionDoorway(DungeonAreaType oldArea, int oldRoom, Vector2Int oldCell,
            DungeonAreaType nextArea, int nextRoom, Vector2Int nextCell, int preferredConnection)
        {
            if (doorwayBuilder == null) return null;
            DoorwayData fallback = null;
            for (int i = 0; i < doorwayBuilder.Doorways.Count; i++)
            {
                DoorwayData doorway = doorwayBuilder.Doorways[i];
                bool exited = oldArea == DungeonAreaType.Room && nextArea == DungeonAreaType.Corridor
                    && doorway.RoomId == oldRoom && doorway.EntranceCell == oldCell
                    && doorway.FirstCorridorCell == nextCell;
                bool entered = oldArea == DungeonAreaType.Corridor && nextArea == DungeonAreaType.Room
                    && doorway.RoomId == nextRoom && doorway.FirstCorridorCell == oldCell
                    && doorway.EntranceCell == nextCell;
                if (!exited && !entered) continue;
                if (doorway.ConnectionIndex == preferredConnection) return doorway;
                if (fallback == null) fallback = doorway;
            }

            return fallback;
        }

        private static CorridorCellInfo ChooseCorridor(IReadOnlyList<CorridorCellInfo> entries, int preferredConnection)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].ConnectionIndex == preferredConnection) return entries[i];
            return entries[0];
        }

        private bool HasCurrentSources()
        {
            return generator != null && graphBuilder != null && corridorBuilder != null && doorwayBuilder != null
                && graphBuilder.IsGraphCurrent && corridorBuilder.IsCorridorDataCurrent
                && doorwayBuilder.IsDoorwayDataCurrent
                && corridorBuilder.Corridors.Count == graphBuilder.Connections.Count;
        }

        private long ComputeStableLookupSignature()
        {
            var roomCells = new List<Vector2Int>(roomByCell.Keys);
            var corridorCells = new List<Vector2Int>(corridorsByCell.Keys);
            roomCells.Sort(CompareCells);
            corridorCells.Sort(CompareCells);
            ulong hash = Offset;
            AddInt(ref hash, roomCells.Count);
            for (int i = 0; i < roomCells.Count; i++)
            {
                Vector2Int cell = roomCells[i];
                AddInt(ref hash, cell.x);
                AddInt(ref hash, cell.y);
                AddInt(ref hash, roomByCell[cell]);
            }

            AddInt(ref hash, corridorCells.Count);
            for (int i = 0; i < corridorCells.Count; i++)
            {
                Vector2Int cell = corridorCells[i];
                AddInt(ref hash, cell.x);
                AddInt(ref hash, cell.y);
                List<CorridorCellInfo> entries = corridorsByCell[cell];
                AddInt(ref hash, entries.Count);
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    AddInt(ref hash, entries[entryIndex].CorridorIndex);
                    AddInt(ref hash, entries[entryIndex].ConnectionIndex);
                }
            }

            return unchecked((long)hash);
        }

        private void FailBuild(string message)
        {
            ClearTracking();
            Debug.LogError($"[DungeonAreaTracker] {message}", this);
        }

        private void CacheComponents()
        {
            if (generator == null) generator = GetComponent<DungeonGenerator>();
            if (graphBuilder == null) graphBuilder = GetComponent<DungeonGraphBuilder>();
            if (corridorBuilder == null) corridorBuilder = GetComponent<DungeonCorridorBuilder>();
            if (doorwayBuilder == null) doorwayBuilder = GetComponent<DungeonDoorwayBuilder>();
        }

        private static int CompareCells(Vector2Int a, Vector2Int b)
        {
            int y = a.y.CompareTo(b.y);
            return y != 0 ? y : a.x.CompareTo(b.x);
        }

        private static int CompareCorridors(CorridorCellInfo a, CorridorCellInfo b)
        {
            int connection = a.ConnectionIndex.CompareTo(b.ConnectionIndex);
            return connection != 0 ? connection : a.CorridorIndex.CompareTo(b.CorridorIndex);
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

        private readonly struct CorridorCellInfo
        {
            public int CorridorIndex { get; }
            public int ConnectionIndex { get; }

            public CorridorCellInfo(int corridorIndex, int connectionIndex)
            {
                CorridorIndex = corridorIndex;
                ConnectionIndex = connectionIndex;
            }
        }
    }
}
