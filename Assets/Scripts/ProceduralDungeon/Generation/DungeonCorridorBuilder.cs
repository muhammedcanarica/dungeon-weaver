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
                return generator != null && graph != null && graph.IsGraphCurrent && hasBuiltCorridors && hasValidCorridorData
                    && builtCorridorSignature == Signature(generator.GenerationArea, generator.Rooms, graph.Connections)
                    && Matches(graph.Connections);
            }
        }

        [ContextMenu("Build Corridors")]
        public void BuildCorridors()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
            EnsureList(); Invalidate();
            if (generator == null || graph == null) { Fail("Required component is missing."); return; }
            if (!graph.IsGraphCurrent) { Debug.LogWarning("[DungeonCorridorBuilder] Room graph is stale. Build Room Graph first.", this); return; }

            var roomsById = new Dictionary<int, RoomData>();
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room == null || roomsById.ContainsKey(room.RoomId)) { Fail("Null room or duplicate room ID."); return; }
                roomsById.Add(room.RoomId, room);
            }

            int fallbackCount = 0;
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                RoomConnectionData connection = graph.Connections[i];
                if (connection == null || !roomsById.TryGetValue(connection.RoomAId, out RoomData roomA) || !roomsById.TryGetValue(connection.RoomBId, out RoomData roomB))
                { Fail($"Connection at index {i} is invalid."); return; }
                if (!TryPath(generator.GenerationArea, generator.Rooms, roomA, roomB, out List<Vector2Int> path, out bool fallback, out string error))
                { Fail($"Could not build corridor {connection.RoomAId}-{connection.RoomBId}: {error}"); return; }
                corridors.Add(new CorridorData(connection.RoomAId, connection.RoomBId, connection.IsPrimaryConnection, fallback, path));
                if (fallback) fallbackCount++;
            }
            if (!Validate(generator.GenerationArea, generator.Rooms, graph.Connections, roomsById, out string validationError))
            { Fail(validationError); return; }
            builtCorridorSignature = Signature(generator.GenerationArea, generator.Rooms, graph.Connections);
            hasBuiltCorridors = true; hasValidCorridorData = true;
            Debug.Log($"[DungeonCorridorBuilder] Built {corridors.Count} corridors: {corridors.Count - fallbackCount} direct L paths and {fallbackCount} BFS fallback paths.", this);
        }

        [ContextMenu("Clear Corridors")]
        public void ClearCorridors() { EnsureList(); Invalidate(); }

        private static bool TryPath(RectInt area, IReadOnlyList<RoomData> rooms, RoomData roomA, RoomData roomB,
            out List<Vector2Int> path, out bool fallback, out string error)
        {
            Vector2Int start = Center(roomA.Bounds), target = Center(roomB.Bounds);
            List<Vector2Int> horizontal = Trim(LPath(start, target, true), roomA.Bounds, roomB.Bounds);
            List<Vector2Int> vertical = Trim(LPath(start, target, false), roomA.Bounds, roomB.Bounds);
            bool horizontalValid = Structural(horizontal, area, rooms, roomA.RoomId, roomB.RoomId);
            bool verticalValid = Structural(vertical, area, rooms, roomA.RoomId, roomB.RoomId);
            if (horizontalValid || verticalValid)
            {
                path = horizontalValid && verticalValid ? (((roomA.RoomId + roomB.RoomId) & 1) == 0 ? horizontal : vertical) : (horizontalValid ? horizontal : vertical);
                fallback = false; error = string.Empty; return true;
            }
            if (!Bfs(start, target, area, rooms, roomA.RoomId, roomB.RoomId, out List<Vector2Int> bfs, out error))
            { path = null; fallback = false; return false; }
            path = Trim(bfs, roomA.Bounds, roomB.Bounds); fallback = true;
            if (!Structural(path, area, rooms, roomA.RoomId, roomB.RoomId)) { error = "Trimmed BFS path is invalid."; return false; }
            error = string.Empty; return true;
        }

        private static Vector2Int Center(RectInt bounds) => new Vector2Int(bounds.xMin + (bounds.width - 1) / 2, bounds.yMin + (bounds.height - 1) / 2);
        private static List<Vector2Int> LPath(Vector2Int start, Vector2Int target, bool horizontalFirst)
        {
            var path = new List<Vector2Int> { start }; Vector2Int current = start;
            if (horizontalFirst) { Axis(path, ref current, target.x, true); Axis(path, ref current, target.y, false); }
            else { Axis(path, ref current, target.y, false); Axis(path, ref current, target.x, true); }
            return path;
        }
        private static void Axis(List<Vector2Int> path, ref Vector2Int current, int target, bool xAxis)
        {
            int value = xAxis ? current.x : current.y, step = Math.Sign(target - value);
            while (value != target) { value += step; current = xAxis ? new Vector2Int(value, current.y) : new Vector2Int(current.x, value); path.Add(current); }
        }
        private static List<Vector2Int> Trim(IReadOnlyList<Vector2Int> path, RectInt roomA, RectInt roomB)
        {
            if (path == null || path.Count == 0) return new List<Vector2Int>();
            int first = 0; while (first + 1 < path.Count && roomA.Contains(path[first + 1])) first++;
            int last = path.Count - 1; while (last - 1 >= first && roomB.Contains(path[last - 1])) last--;
            var result = new List<Vector2Int>(); for (int i = first; i <= last; i++) result.Add(path[i]); return result;
        }
        private static bool Structural(IReadOnlyList<Vector2Int> path, RectInt area, IReadOnlyList<RoomData> rooms, int a, int b)
        {
            if (path == null || path.Count < 2) return false;
            for (int i = 0; i < path.Count; i++)
                if (!area.Contains(path[i]) || Blocked(path[i], rooms, a, b) || (i > 0 && Manhattan(path[i - 1], path[i]) != 1)) return false;
            return true;
        }

        private static bool Bfs(Vector2Int start, Vector2Int target, RectInt area, IReadOnlyList<RoomData> rooms, int a, int b,
            out List<Vector2Int> path, out string error)
        {
            path = null; long count;
            try { count = checked((long)area.width * area.height); }
            catch (OverflowException) { error = "Generation Area overflowed."; return false; }
            if (area.width <= 0 || area.height <= 0 || count > int.MaxValue || !area.Contains(start) || !area.Contains(target))
            { error = "Generation Area is invalid."; return false; }
            int width = area.width; var visited = new bool[(int)count]; var previous = new int[(int)count]; Array.Fill(previous, -1);
            int startIndex = Index(start, area, width), targetIndex = Index(target, area, width); var queue = new Queue<int>();
            visited[startIndex] = true; queue.Enqueue(startIndex); bool found = startIndex == targetIndex;
            Vector2Int[] directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
            while (queue.Count > 0 && !found)
            {
                int currentIndex = queue.Dequeue(); Vector2Int current = Cell(currentIndex, area, width);
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = current + directions[i]; if (!area.Contains(next) || Blocked(next, rooms, a, b)) continue;
                    int nextIndex = Index(next, area, width); if (visited[nextIndex]) continue;
                    visited[nextIndex] = true; previous[nextIndex] = currentIndex; queue.Enqueue(nextIndex);
                    if (nextIndex == targetIndex) { found = true; break; }
                }
            }
            if (!found) { error = "BFS found no route."; return false; }
            var reverse = new List<Vector2Int>();
            for (int i = targetIndex; i >= 0; i = previous[i]) { reverse.Add(Cell(i, area, width)); if (i == startIndex) break; }
            if (reverse.Count == 0 || reverse[reverse.Count - 1] != start) { error = "BFS predecessor chain failed."; return false; }
            reverse.Reverse(); path = reverse; error = string.Empty; return true;
        }
        private static int Index(Vector2Int cell, RectInt area, int width) => (cell.y - area.yMin) * width + cell.x - area.xMin;
        private static Vector2Int Cell(int index, RectInt area, int width) => new Vector2Int(area.xMin + index % width, area.yMin + index / width);
        private static bool Blocked(Vector2Int cell, IReadOnlyList<RoomData> rooms, int a, int b)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i] != null && rooms[i].RoomId != a && rooms[i].RoomId != b && rooms[i].Bounds.Contains(cell)) return true;
            return false;
        }
        private static long Manhattan(Vector2Int a, Vector2Int b) => Math.Abs((long)a.x - b.x) + Math.Abs((long)a.y - b.y);
        private static bool Boundary(RectInt bounds, Vector2Int cell) => bounds.Contains(cell) && (cell.x == bounds.xMin || cell.x == bounds.xMax - 1 || cell.y == bounds.yMin || cell.y == bounds.yMax - 1);

        private bool Validate(RectInt area, IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> connections,
            Dictionary<int, RoomData> lookup, out string error)
        {
            if (corridors.Count != connections.Count) { error = "Corridor count mismatch."; return false; }
            var pairs = new HashSet<long>();
            for (int i = 0; i < connections.Count; i++)
            {
                CorridorData c = corridors[i]; RoomConnectionData edge = connections[i];
                long key = ((long)c.RoomAId << 32) ^ (uint)c.RoomBId;
                if (c == null || edge == null || c.RoomAId == c.RoomBId || c.RoomAId != edge.RoomAId || c.RoomBId != edge.RoomBId
                    || c.IsPrimaryConnection != edge.IsPrimaryConnection || !pairs.Add(key)
                    || !lookup.TryGetValue(c.RoomAId, out RoomData roomA) || !lookup.TryGetValue(c.RoomBId, out RoomData roomB))
                { error = $"Corridor metadata invalid at {i}."; return false; }
                IReadOnlyList<Vector2Int> path = c.PathCells;
                if (path.Count < 2 || !Boundary(roomA.Bounds, path[0]) || !Boundary(roomB.Bounds, path[path.Count - 1])) { error = "Invalid corridor endpoint."; return false; }
                for (int p = 0; p < path.Count; p++)
                    if (!area.Contains(path[p]) || Blocked(path[p], rooms, c.RoomAId, c.RoomBId) || (p > 0 && roomA.Bounds.Contains(path[p]))
                        || (p < path.Count - 1 && roomB.Bounds.Contains(path[p])) || (p > 0 && Manhattan(path[p - 1], path[p]) != 1))
                    { error = "Invalid corridor path cell."; return false; }
            }
            error = string.Empty; return true;
        }

        private bool Matches(IReadOnlyList<RoomConnectionData> connections)
        {
            EnsureList(); if (connections == null || corridors.Count != connections.Count) return false;
            for (int i = 0; i < connections.Count; i++) if (connections[i] == null || corridors[i] == null || corridors[i].CellCount < 2
                || corridors[i].RoomAId != connections[i].RoomAId || corridors[i].RoomBId != connections[i].RoomBId
                || corridors[i].IsPrimaryConnection != connections[i].IsPrimaryConnection) return false;
            return true;
        }
        private static long Signature(RectInt area, IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> connections)
        {
            ulong hash = Offset; AddInt(ref hash, area.x); AddInt(ref hash, area.y); AddInt(ref hash, area.width); AddInt(ref hash, area.height);
            if (rooms != null) for (int i = 0; i < rooms.Count; i++) { RoomData r = rooms[i]; if (r == null) { AddInt(ref hash, int.MinValue); continue; } AddInt(ref hash, r.RoomId); AddInt(ref hash, r.Bounds.x); AddInt(ref hash, r.Bounds.y); AddInt(ref hash, r.Bounds.width); AddInt(ref hash, r.Bounds.height); }
            if (connections != null) for (int i = 0; i < connections.Count; i++) { RoomConnectionData c = connections[i]; if (c == null) { AddInt(ref hash, int.MinValue); continue; } AddInt(ref hash, c.RoomAId); AddInt(ref hash, c.RoomBId); AddLong(ref hash, c.WeightSquared); AddByte(ref hash, c.IsPrimaryConnection ? (byte)1 : (byte)0); }
            return unchecked((long)hash);
        }
        private static void AddInt(ref ulong hash, int value) { uint bits = unchecked((uint)value); for (int s = 0; s < 32; s += 8) AddByte(ref hash, (byte)(bits >> s)); }
        private static void AddLong(ref ulong hash, long value) { ulong bits = unchecked((ulong)value); for (int s = 0; s < 64; s += 8) AddByte(ref hash, (byte)(bits >> s)); }
        private static void AddByte(ref ulong hash, byte value) { hash ^= value; hash = unchecked(hash * Prime); }
        private void Invalidate() { corridors.Clear(); builtCorridorSignature = 0; hasBuiltCorridors = false; hasValidCorridorData = false; }
        private void Fail(string message) { Invalidate(); Debug.LogError($"[DungeonCorridorBuilder] {message}", this); }
        private void EnsureList() { if (corridors == null) { corridors = new List<CorridorData>(); readOnlyCorridors = null; } }
    }
}
