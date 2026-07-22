using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder), typeof(DungeonCorridorBuilder))]
    public sealed class DungeonDoorwayBuilder : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        [SerializeField, HideInInspector] private List<DoorwayData> doorways = new List<DoorwayData>();
        [SerializeField, HideInInspector] private long builtSourceSignature;
        [SerializeField, HideInInspector] private long doorwaySignature;
        [SerializeField, HideInInspector] private bool hasBuiltDoorways;
        private IReadOnlyList<DoorwayData> readOnlyDoorways;

        public IReadOnlyList<DoorwayData> Doorways
        {
            get
            {
                EnsureList();
                return readOnlyDoorways ??= doorways.AsReadOnly();
            }
        }

        public long DoorwaySignature => doorwaySignature;

        public bool IsDoorwayDataCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
                DungeonCorridorBuilder corridors = GetComponent<DungeonCorridorBuilder>();
                return generator != null && graph != null && corridors != null
                    && corridors.IsCorridorDataCurrent && hasBuiltDoorways
                    && builtSourceSignature == SourceSignature(generator.Rooms, graph.Connections, corridors.Corridors)
                    && doorwaySignature == Signature(Doorways)
                    && Validate(generator.Rooms, graph.Connections, corridors.Corridors, Doorways, out _);
            }
        }

        [ContextMenu("Build Doorways")]
        public void BuildDoorways()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
            DungeonCorridorBuilder corridors = GetComponent<DungeonCorridorBuilder>();
            EnsureList();
            Invalidate();

            if (generator == null || graph == null || corridors == null)
            {
                Fail("Required component is missing.");
                return;
            }

            if (!corridors.IsCorridorDataCurrent)
            {
                Debug.LogWarning("[DungeonDoorwayBuilder] Corridor data is stale. Build Corridors first.", this);
                return;
            }

            var roomsById = new Dictionary<int, RoomData>();
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room == null || roomsById.ContainsKey(room.RoomId))
                {
                    Fail("Null room or duplicate room ID.");
                    return;
                }

                roomsById.Add(room.RoomId, room);
            }

            var usedCorridors = new HashSet<int>();
            for (int connectionIndex = 0; connectionIndex < graph.Connections.Count; connectionIndex++)
            {
                RoomConnectionData connection = graph.Connections[connectionIndex];
                if (connection == null
                    || !roomsById.TryGetValue(connection.RoomAId, out RoomData roomA)
                    || !roomsById.TryGetValue(connection.RoomBId, out RoomData roomB)
                    || !TryFindCorridor(corridors.Corridors, connection, usedCorridors, out CorridorData corridor))
                {
                    Fail($"Connection at index {connectionIndex} has no valid room/corridor mapping.");
                    return;
                }

                if (!TryCreateDoorway(connectionIndex, roomA, corridor.PathCells, out DoorwayData doorwayA, out string errorA))
                {
                    Fail($"Could not derive room {roomA.RoomId} doorway for connection {connectionIndex}: {errorA}");
                    return;
                }

                if (!TryCreateDoorway(connectionIndex, roomB, corridor.PathCells, out DoorwayData doorwayB, out string errorB))
                {
                    Fail($"Could not derive room {roomB.RoomId} doorway for connection {connectionIndex}: {errorB}");
                    return;
                }

                doorways.Add(doorwayA);
                doorways.Add(doorwayB);
            }

            if (!Validate(generator.Rooms, graph.Connections, corridors.Corridors, Doorways, out string validationError))
            {
                Fail(validationError);
                return;
            }

            builtSourceSignature = SourceSignature(generator.Rooms, graph.Connections, corridors.Corridors);
            doorwaySignature = Signature(Doorways);
            hasBuiltDoorways = true;
            Debug.Log($"[DungeonDoorwayBuilder] Built {doorways.Count} doorway records for {graph.Connections.Count} connections.", this);
        }

        [ContextMenu("Clear Doorways")]
        public void ClearDoorways()
        {
            EnsureList();
            Invalidate();
        }

        public static Vector2Int DirectionOffset(DoorwayDirection direction)
        {
            switch (direction)
            {
                case DoorwayDirection.Up: return Vector2Int.up;
                case DoorwayDirection.Down: return Vector2Int.down;
                case DoorwayDirection.Left: return Vector2Int.left;
                case DoorwayDirection.Right: return Vector2Int.right;
                default: return Vector2Int.zero;
            }
        }

        private static bool TryCreateDoorway(int connectionIndex, RoomData room, IReadOnlyList<Vector2Int> path,
            out DoorwayData doorway, out string error)
        {
            doorway = null;
            error = null;
            if (room == null || path == null || path.Count < 2)
            {
                error = "Room or corridor path is invalid.";
                return false;
            }

            bool firstBelongsToRoom = IsBoundary(room.Bounds, path[0]);
            bool lastBelongsToRoom = IsBoundary(room.Bounds, path[path.Count - 1]);
            if (firstBelongsToRoom == lastBelongsToRoom)
            {
                error = $"Path endpoints do not identify exactly one boundary cell for room {room.RoomId}.";
                return false;
            }

            Vector2Int entrance = firstBelongsToRoom ? path[0] : path[path.Count - 1];
            Vector2Int firstCorridor = firstBelongsToRoom ? path[1] : path[path.Count - 2];
            Vector2Int offset = firstCorridor - entrance;
            if (room.Bounds.Contains(firstCorridor) || !TryDirection(offset, out DoorwayDirection direction))
            {
                error = $"Room {room.RoomId} doorway does not continue to a cardinal outside corridor cell.";
                return false;
            }

            doorway = new DoorwayData(room.RoomId, connectionIndex, entrance, direction, firstCorridor);
            return true;
        }

        private static bool TryFindCorridor(IReadOnlyList<CorridorData> corridors, RoomConnectionData connection,
            HashSet<int> usedIndices, out CorridorData match)
        {
            for (int i = 0; i < corridors.Count; i++)
            {
                CorridorData corridor = corridors[i];
                if (usedIndices.Contains(i) || corridor == null
                    || corridor.RoomAId != connection.RoomAId || corridor.RoomBId != connection.RoomBId)
                    continue;

                usedIndices.Add(i);
                match = corridor;
                return true;
            }

            match = null;
            return false;
        }

        private static bool Validate(IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> connections,
            IReadOnlyList<CorridorData> corridors, IReadOnlyList<DoorwayData> records, out string error)
        {
            if (rooms == null || connections == null || corridors == null || records == null
                || corridors.Count != connections.Count || records.Count != connections.Count * 2)
            {
                error = "Doorway source/count mismatch.";
                return false;
            }

            var roomsById = new Dictionary<int, RoomData>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] == null || roomsById.ContainsKey(rooms[i].RoomId))
                {
                    error = "Doorway validation found an invalid room collection.";
                    return false;
                }

                roomsById.Add(rooms[i].RoomId, rooms[i]);
            }

            var seen = new HashSet<long>();
            var doorwayCounts = new int[connections.Count];
            for (int i = 0; i < records.Count; i++)
            {
                DoorwayData doorway = records[i];
                if (doorway == null || doorway.ConnectionIndex < 0 || doorway.ConnectionIndex >= connections.Count
                    || !roomsById.TryGetValue(doorway.RoomId, out RoomData room))
                {
                    error = $"Doorway metadata is invalid at index {i}.";
                    return false;
                }

                RoomConnectionData connection = connections[doorway.ConnectionIndex];
                if (connection == null || (doorway.RoomId != connection.RoomAId && doorway.RoomId != connection.RoomBId))
                {
                    error = $"Doorway {i} references an unrelated connection.";
                    return false;
                }

                long key = ((long)doorway.ConnectionIndex << 32) ^ (uint)doorway.RoomId;
                Vector2Int offset = DirectionOffset(doorway.OutwardDirection);
                CorridorData corridor = FindCorridor(corridors, connection);
                if (!seen.Add(key) || !IsBoundary(room.Bounds, doorway.EntranceCell) || offset == Vector2Int.zero
                    || doorway.FirstCorridorCell != doorway.EntranceCell + offset
                    || room.Bounds.Contains(doorway.FirstCorridorCell) || corridor == null
                    || !ContainsStep(corridor.PathCells, doorway.EntranceCell, doorway.FirstCorridorCell))
                {
                    error = $"Doorway geometry is invalid at index {i}.";
                    return false;
                }

                doorwayCounts[doorway.ConnectionIndex]++;
            }

            for (int i = 0; i < doorwayCounts.Length; i++)
            {
                if (doorwayCounts[i] != 2)
                {
                    error = $"Connection {i} does not have exactly two doorways.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static CorridorData FindCorridor(IReadOnlyList<CorridorData> corridors, RoomConnectionData connection)
        {
            for (int i = 0; i < corridors.Count; i++)
            {
                CorridorData corridor = corridors[i];
                if (corridor != null && corridor.RoomAId == connection.RoomAId && corridor.RoomBId == connection.RoomBId)
                    return corridor;
            }

            return null;
        }

        private static bool ContainsStep(IReadOnlyList<Vector2Int> path, Vector2Int entrance, Vector2Int firstCorridor)
        {
            if (path == null) return false;
            for (int i = 1; i < path.Count; i++)
                if ((path[i - 1] == entrance && path[i] == firstCorridor)
                    || (path[i - 1] == firstCorridor && path[i] == entrance)) return true;
            return false;
        }

        private static bool TryDirection(Vector2Int offset, out DoorwayDirection direction)
        {
            if (offset == Vector2Int.up) direction = DoorwayDirection.Up;
            else if (offset == Vector2Int.down) direction = DoorwayDirection.Down;
            else if (offset == Vector2Int.left) direction = DoorwayDirection.Left;
            else if (offset == Vector2Int.right) direction = DoorwayDirection.Right;
            else { direction = default; return false; }
            return true;
        }

        private static bool IsBoundary(RectInt bounds, Vector2Int cell)
        {
            return bounds.Contains(cell) && (cell.x == bounds.xMin || cell.x == bounds.xMax - 1
                || cell.y == bounds.yMin || cell.y == bounds.yMax - 1);
        }

        private static long SourceSignature(IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> connections,
            IReadOnlyList<CorridorData> corridors)
        {
            ulong hash = Offset;
            if (rooms != null) for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room == null) { AddInt(ref hash, int.MinValue); continue; }
                AddInt(ref hash, room.RoomId); AddInt(ref hash, room.Bounds.x); AddInt(ref hash, room.Bounds.y);
                AddInt(ref hash, room.Bounds.width); AddInt(ref hash, room.Bounds.height);
            }

            if (connections != null) for (int i = 0; i < connections.Count; i++)
            {
                RoomConnectionData connection = connections[i];
                if (connection == null) { AddInt(ref hash, int.MinValue); continue; }
                AddInt(ref hash, connection.RoomAId); AddInt(ref hash, connection.RoomBId);
                AddLong(ref hash, connection.WeightSquared); AddByte(ref hash, connection.IsPrimaryConnection ? (byte)1 : (byte)0);
            }

            if (corridors != null) for (int i = 0; i < corridors.Count; i++)
            {
                CorridorData corridor = corridors[i];
                if (corridor == null) { AddInt(ref hash, int.MinValue); continue; }
                AddInt(ref hash, corridor.RoomAId); AddInt(ref hash, corridor.RoomBId);
                AddByte(ref hash, corridor.IsPrimaryConnection ? (byte)1 : (byte)0);
                AddByte(ref hash, corridor.UsedFallbackPathfinding ? (byte)1 : (byte)0);
                AddInt(ref hash, corridor.CellCount);
                for (int p = 0; p < corridor.PathCells.Count; p++)
                {
                    AddInt(ref hash, corridor.PathCells[p].x);
                    AddInt(ref hash, corridor.PathCells[p].y);
                }
            }

            return unchecked((long)hash);
        }

        private static long Signature(IReadOnlyList<DoorwayData> records)
        {
            ulong hash = Offset;
            if (records != null) for (int i = 0; i < records.Count; i++)
            {
                DoorwayData doorway = records[i];
                if (doorway == null) { AddInt(ref hash, int.MinValue); continue; }
                AddInt(ref hash, doorway.ConnectionIndex); AddInt(ref hash, doorway.RoomId);
                AddInt(ref hash, doorway.EntranceCell.x); AddInt(ref hash, doorway.EntranceCell.y);
                AddInt(ref hash, (int)doorway.OutwardDirection);
                AddInt(ref hash, doorway.FirstCorridorCell.x); AddInt(ref hash, doorway.FirstCorridorCell.y);
            }

            return unchecked((long)hash);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8) AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddLong(ref ulong hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8) AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash = unchecked(hash * Prime);
        }

        private void Invalidate()
        {
            doorways.Clear();
            builtSourceSignature = 0;
            doorwaySignature = 0;
            hasBuiltDoorways = false;
        }

        private void Fail(string message)
        {
            Invalidate();
            Debug.LogError($"[DungeonDoorwayBuilder] {message}", this);
        }

        private void EnsureList()
        {
            if (doorways == null)
            {
                doorways = new List<DoorwayData>();
                readOnlyDoorways = null;
            }
        }
    }
}
