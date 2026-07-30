using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder))]
    public sealed class DungeonCorridorBuilder : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private const int MinimumDoorwaySpan = 3;
        private const int CandidateLimitPerRoom = 8;

        [SerializeField, HideInInspector] private List<CorridorData> corridors = new List<CorridorData>();
        [SerializeField, HideInInspector] private long builtCorridorSignature;
        [SerializeField, HideInInspector] private bool hasBuiltCorridors;
        [SerializeField, HideInInspector] private bool hasValidCorridorData;
        private IReadOnlyList<CorridorData> readOnlyCorridors;

        public IReadOnlyList<CorridorData> Corridors
        {
            get { EnsureList(); return readOnlyCorridors ??= corridors.AsReadOnly(); }
        }

        public bool IsCorridorDataCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
                DungeonTilemapRenderer renderer = GetComponent<DungeonTilemapRenderer>();
                int doorwaySpan = renderer != null
                    ? Mathf.Max(MinimumDoorwaySpan, renderer.CorridorWidth)
                    : MinimumDoorwaySpan;
                return generator != null && graph != null && graph.IsGraphCurrent && hasBuiltCorridors
                    && hasValidCorridorData
                    && builtCorridorSignature == Signature(generator.GenerationArea,
                        generator.Rooms, graph.Connections, doorwaySpan)
                    && Matches(graph.Connections);
            }
        }

        [ContextMenu("Build Corridors")]
        public void BuildCorridors()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
            DungeonTilemapRenderer renderer = GetComponent<DungeonTilemapRenderer>();
            int doorwaySpan = renderer != null
                ? Mathf.Max(MinimumDoorwaySpan, renderer.CorridorWidth)
                : MinimumDoorwaySpan;
            EnsureList();
            Invalidate();
            if (generator == null || graph == null)
            {
                Fail("Required component is missing.");
                return;
            }
            if (!graph.IsGraphCurrent)
            {
                Debug.LogWarning("[DungeonCorridorBuilder] Room graph is stale. Build Room Graph first.", this);
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

            int fallbackCount = 0;
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                RoomConnectionData connection = graph.Connections[i];
                if (connection == null || !roomsById.TryGetValue(connection.RoomAId, out RoomData roomA)
                    || !roomsById.TryGetValue(connection.RoomBId, out RoomData roomB))
                {
                    Fail($"Connection at index {i} is invalid.");
                    return;
                }
                if (!TryBuildPath(generator.GenerationArea, generator.Rooms, roomA, roomB,
                    doorwaySpan, out List<Vector2Int> path, out bool fallback,
                    out string error))
                {
                    Fail($"Could not build corridor {connection.RoomAId}-{connection.RoomBId}: {error}");
                    return;
                }
                corridors.Add(new CorridorData(connection.RoomAId, connection.RoomBId,
                    connection.IsPrimaryConnection, fallback, path));
                if (fallback) fallbackCount++;
            }

            if (!Validate(generator.GenerationArea, generator.Rooms, graph.Connections,
                roomsById, doorwaySpan, out string validationError))
            {
                Fail(validationError);
                return;
            }
            builtCorridorSignature = Signature(generator.GenerationArea,
                generator.Rooms, graph.Connections, doorwaySpan);
            hasBuiltCorridors = true;
            hasValidCorridorData = true;
            Debug.Log($"[DungeonCorridorBuilder] Built {corridors.Count} corridors: "
                + $"{corridors.Count - fallbackCount} direct L paths and {fallbackCount} BFS fallback paths.", this);
        }

        [ContextMenu("Clear Corridors")]
        public void ClearCorridors()
        {
            EnsureList();
            Invalidate();
        }

        private static bool TryBuildPath(RectInt area, IReadOnlyList<RoomData> rooms,
            RoomData roomA, RoomData roomB, int doorwaySpan,
            out List<Vector2Int> path,
            out bool fallback, out string error)
        {
            List<DoorCandidate> candidatesA = BuildDoorCandidates(area, roomA,
                roomB, doorwaySpan);
            List<DoorCandidate> candidatesB = BuildDoorCandidates(area, roomB,
                roomA, doorwaySpan);
            if (candidatesA.Count == 0 || candidatesB.Count == 0)
            {
                path = null;
                fallback = false;
                error = "A room has no valid flat outer-boundary doorway candidate.";
                return false;
            }

            int countA = Mathf.Min(CandidateLimitPerRoom, candidatesA.Count);
            int countB = Mathf.Min(CandidateLimitPerRoom, candidatesB.Count);
            for (int a = 0; a < countA; a++)
            {
                for (int b = 0; b < countB; b++)
                {
                    if (!TryDirectPath(candidatesA[a].OutsideCell, candidatesB[b].OutsideCell,
                        area, rooms, roomA.RoomId, roomB.RoomId, out List<Vector2Int> outsidePath)) continue;
                    path = Combine(candidatesA[a], outsidePath, candidatesB[b]);
                    fallback = false;
                    error = string.Empty;
                    return true;
                }
            }

            for (int a = 0; a < countA; a++)
            {
                for (int b = 0; b < countB; b++)
                {
                    if (!TryBfs(candidatesA[a].OutsideCell, candidatesB[b].OutsideCell,
                        area, rooms, roomA.RoomId, roomB.RoomId, out List<Vector2Int> outsidePath)) continue;
                    path = Combine(candidatesA[a], outsidePath, candidatesB[b]);
                    fallback = true;
                    error = string.Empty;
                    return true;
                }
            }

            path = null;
            fallback = false;
            error = "No path connects the valid doorway candidates.";
            return false;
        }

        private static List<DoorCandidate> BuildDoorCandidates(RectInt area,
            RoomData room, RoomData target, int doorwaySpan)
        {
            var result = new List<DoorCandidate>();
            for (int i = 0; i < room.BoundaryCells.Count; i++)
            {
                Vector2Int cell = room.BoundaryCells[i];
                if (!room.IsOuterBoundaryCell(cell)) continue;
                if (cell.x == room.Bounds.xMin) AddCandidate(area, room, target, cell, Vector2Int.left, doorwaySpan, result);
                if (cell.x == room.Bounds.xMax - 1) AddCandidate(area, room, target, cell, Vector2Int.right, doorwaySpan, result);
                if (cell.y == room.Bounds.yMin) AddCandidate(area, room, target, cell, Vector2Int.down, doorwaySpan, result);
                if (cell.y == room.Bounds.yMax - 1) AddCandidate(area, room, target, cell, Vector2Int.up, doorwaySpan, result);
            }
            result.Sort(DoorCandidate.Compare);
            return result;
        }

        private static void AddCandidate(RectInt area, RoomData room, RoomData target,
            Vector2Int entrance, Vector2Int outward, int doorwaySpan,
            List<DoorCandidate> candidates)
        {
            Vector2Int outside = entrance + outward;
            if (!area.Contains(outside)
                || !room.CanPlaceDoorway(entrance, outside, doorwaySpan)) return;
            Vector2Int targetCell = target.SpawnCell;
            Vector2Int towardTarget = targetCell - entrance;
            int facingDot = (towardTarget.x * outward.x) + (towardTarget.y * outward.y);
            int facingPenalty = facingDot > 0 ? 0 : 1;
            int distance = Manhattan(outside, targetCell);
            int clearance = room.GetCenteredDoorwaySpan(entrance, outward);
            candidates.Add(new DoorCandidate(entrance, outside, outward,
                facingPenalty, clearance, distance));
        }

        private static bool TryDirectPath(Vector2Int start, Vector2Int target,
            RectInt area, IReadOnlyList<RoomData> rooms, int roomAId, int roomBId,
            out List<Vector2Int> path)
        {
            List<Vector2Int> horizontal = LPath(start, target, true);
            List<Vector2Int> vertical = LPath(start, target, false);
            bool horizontalValid = IsOutsidePathClear(horizontal, area, rooms, roomAId, roomBId);
            bool verticalValid = IsOutsidePathClear(vertical, area, rooms, roomAId, roomBId);
            if (!horizontalValid && !verticalValid)
            {
                path = null;
                return false;
            }
            path = horizontalValid && verticalValid
                ? (((roomAId + roomBId) & 1) == 0 ? horizontal : vertical)
                : horizontalValid ? horizontal : vertical;
            return true;
        }

        private static List<Vector2Int> LPath(Vector2Int start, Vector2Int target,
            bool horizontalFirst)
        {
            var path = new List<Vector2Int> { start };
            Vector2Int current = start;
            if (horizontalFirst)
            {
                AddAxis(path, ref current, target.x, true);
                AddAxis(path, ref current, target.y, false);
            }
            else
            {
                AddAxis(path, ref current, target.y, false);
                AddAxis(path, ref current, target.x, true);
            }
            return path;
        }

        private static void AddAxis(List<Vector2Int> path, ref Vector2Int current,
            int target, bool xAxis)
        {
            int value = xAxis ? current.x : current.y;
            int step = Math.Sign(target - value);
            while (value != target)
            {
                value += step;
                current = xAxis ? new Vector2Int(value, current.y)
                    : new Vector2Int(current.x, value);
                path.Add(current);
            }
        }

        private static bool TryBfs(Vector2Int start, Vector2Int target, RectInt area,
            IReadOnlyList<RoomData> rooms, int roomAId, int roomBId,
            out List<Vector2Int> path)
        {
            path = null;
            long count;
            try { count = checked((long)area.width * area.height); }
            catch (OverflowException) { return false; }
            if (area.width <= 0 || area.height <= 0 || count > int.MaxValue
                || !area.Contains(start) || !area.Contains(target)) return false;

            int width = area.width;
            var visited = new bool[(int)count];
            var previous = new int[(int)count];
            Array.Fill(previous, -1);
            int startIndex = Index(start, area, width);
            int targetIndex = Index(target, area, width);
            var queue = new Queue<int>();
            visited[startIndex] = true;
            queue.Enqueue(startIndex);
            bool found = startIndex == targetIndex;
            Vector2Int[] directions =
            {
                Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
            };
            while (queue.Count > 0 && !found)
            {
                int currentIndex = queue.Dequeue();
                Vector2Int current = Cell(currentIndex, area, width);
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = current + directions[i];
                    if (!area.Contains(next) || IsOccupied(next, rooms)) continue;
                    int nextIndex = Index(next, area, width);
                    if (visited[nextIndex]) continue;
                    visited[nextIndex] = true;
                    previous[nextIndex] = currentIndex;
                    queue.Enqueue(nextIndex);
                    if (nextIndex == targetIndex) { found = true; break; }
                }
            }
            if (!found) return false;

            var reverse = new List<Vector2Int>();
            for (int i = targetIndex; i >= 0; i = previous[i])
            {
                reverse.Add(Cell(i, area, width));
                if (i == startIndex) break;
            }
            if (reverse.Count == 0 || reverse[reverse.Count - 1] != start) return false;
            reverse.Reverse();
            path = reverse;
            return IsOutsidePathClear(path, area, rooms, roomAId, roomBId);
        }

        private static List<Vector2Int> Combine(DoorCandidate first,
            IReadOnlyList<Vector2Int> outsidePath, DoorCandidate second)
        {
            var result = new List<Vector2Int>(outsidePath.Count + 2) { first.EntranceCell };
            for (int i = 0; i < outsidePath.Count; i++) result.Add(outsidePath[i]);
            result.Add(second.EntranceCell);
            return result;
        }

        private static bool IsOutsidePathClear(IReadOnlyList<Vector2Int> path, RectInt area,
            IReadOnlyList<RoomData> rooms, int roomAId, int roomBId)
        {
            if (path == null || path.Count == 0) return false;
            for (int i = 0; i < path.Count; i++)
            {
                if (!area.Contains(path[i]) || IsOccupied(path[i], rooms)
                    || (i > 0 && Manhattan(path[i - 1], path[i]) != 1)) return false;
            }
            return true;
        }

        private static bool IsOccupied(Vector2Int cell, IReadOnlyList<RoomData> rooms)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i] != null && rooms[i].ContainsCell(cell)) return true;
            return false;
        }

        private bool Validate(RectInt area, IReadOnlyList<RoomData> rooms,
            IReadOnlyList<RoomConnectionData> connections,
            Dictionary<int, RoomData> roomsById, int doorwaySpan, out string error)
        {
            if (corridors.Count != connections.Count)
            {
                error = "Corridor count mismatch.";
                return false;
            }
            var pairs = new HashSet<long>();
            for (int i = 0; i < connections.Count; i++)
            {
                CorridorData corridor = corridors[i];
                RoomConnectionData edge = connections[i];
                if (corridor == null || edge == null)
                {
                    error = $"Corridor metadata invalid at {i}.";
                    return false;
                }
                long key = ((long)corridor.RoomAId << 32) ^ (uint)corridor.RoomBId;
                if (corridor.RoomAId == corridor.RoomBId || corridor.RoomAId != edge.RoomAId
                    || corridor.RoomBId != edge.RoomBId
                    || corridor.IsPrimaryConnection != edge.IsPrimaryConnection || !pairs.Add(key)
                    || !roomsById.TryGetValue(corridor.RoomAId, out RoomData roomA)
                    || !roomsById.TryGetValue(corridor.RoomBId, out RoomData roomB))
                {
                    error = $"Corridor metadata invalid at {i}.";
                    return false;
                }

                IReadOnlyList<Vector2Int> path = corridor.PathCells;
                if (path.Count < 4
                    || !roomA.CanPlaceDoorway(path[0], path[1], doorwaySpan)
                    || !roomB.CanPlaceDoorway(path[path.Count - 1], path[path.Count - 2], doorwaySpan))
                {
                    error = "Invalid shape-aware corridor endpoint.";
                    return false;
                }
                for (int p = 0; p < path.Count; p++)
                {
                    if (!area.Contains(path[p]) || (p > 0 && Manhattan(path[p - 1], path[p]) != 1))
                    {
                        error = "Invalid corridor path cell.";
                        return false;
                    }
                    if (p > 0 && p < path.Count - 1 && IsOccupied(path[p], rooms))
                    {
                        error = "Corridor crosses an occupied room cell.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        private bool Matches(IReadOnlyList<RoomConnectionData> connections)
        {
            EnsureList();
            if (connections == null || corridors.Count != connections.Count) return false;
            for (int i = 0; i < connections.Count; i++)
                if (connections[i] == null || corridors[i] == null || corridors[i].CellCount < 4
                    || corridors[i].RoomAId != connections[i].RoomAId
                    || corridors[i].RoomBId != connections[i].RoomBId
                    || corridors[i].IsPrimaryConnection != connections[i].IsPrimaryConnection) return false;
            return true;
        }

        private static long Signature(RectInt area, IReadOnlyList<RoomData> rooms,
            IReadOnlyList<RoomConnectionData> connections, int doorwaySpan)
        {
            ulong hash = Offset;
            AddInt(ref hash, area.x);
            AddInt(ref hash, area.y);
            AddInt(ref hash, area.width);
            AddInt(ref hash, area.height);
            AddInt(ref hash, doorwaySpan);
            if (rooms != null)
                for (int i = 0; i < rooms.Count; i++)
                    AddLong(ref hash, rooms[i] != null ? rooms[i].GeometrySignature : long.MinValue);
            if (connections != null)
                for (int i = 0; i < connections.Count; i++)
                {
                    RoomConnectionData connection = connections[i];
                    if (connection == null) { AddInt(ref hash, int.MinValue); continue; }
                    AddInt(ref hash, connection.RoomAId);
                    AddInt(ref hash, connection.RoomBId);
                    AddLong(ref hash, connection.WeightSquared);
                    AddByte(ref hash, connection.IsPrimaryConnection ? (byte)1 : (byte)0);
                }
            return unchecked((long)hash);
        }

        private static int Index(Vector2Int cell, RectInt area, int width)
        {
            return (cell.y - area.yMin) * width + cell.x - area.xMin;
        }

        private static Vector2Int Cell(int index, RectInt area, int width)
        {
            return new Vector2Int(area.xMin + index % width, area.yMin + index / width);
        }

        private static int Manhattan(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
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
            corridors.Clear();
            builtCorridorSignature = 0;
            hasBuiltCorridors = false;
            hasValidCorridorData = false;
        }

        private void Fail(string message)
        {
            Invalidate();
            Debug.LogError($"[DungeonCorridorBuilder] {message}", this);
        }

        private void EnsureList()
        {
            if (corridors == null)
            {
                corridors = new List<CorridorData>();
                readOnlyCorridors = null;
            }
        }

        private readonly struct DoorCandidate
        {
            public Vector2Int EntranceCell { get; }
            public Vector2Int OutsideCell { get; }
            private Vector2Int Outward { get; }
            private int FacingPenalty { get; }
            private int Clearance { get; }
            private int Distance { get; }

            public DoorCandidate(Vector2Int entranceCell, Vector2Int outsideCell,
                Vector2Int outward, int facingPenalty, int clearance, int distance)
            {
                EntranceCell = entranceCell;
                OutsideCell = outsideCell;
                Outward = outward;
                FacingPenalty = facingPenalty;
                Clearance = clearance;
                Distance = distance;
            }

            public static int Compare(DoorCandidate left, DoorCandidate right)
            {
                int result = left.FacingPenalty.CompareTo(right.FacingPenalty);
                if (result != 0) return result;
                result = right.Clearance.CompareTo(left.Clearance);
                if (result != 0) return result;
                result = left.Distance.CompareTo(right.Distance);
                if (result != 0) return result;
                result = left.EntranceCell.y.CompareTo(right.EntranceCell.y);
                if (result != 0) return result;
                result = left.EntranceCell.x.CompareTo(right.EntranceCell.x);
                if (result != 0) return result;
                result = left.Outward.y.CompareTo(right.Outward.y);
                return result != 0 ? result : left.Outward.x.CompareTo(right.Outward.x);
            }
        }
    }
}
