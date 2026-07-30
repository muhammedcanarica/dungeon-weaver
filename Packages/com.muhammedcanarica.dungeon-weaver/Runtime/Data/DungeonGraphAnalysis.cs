using System.Collections.Generic;

namespace ProceduralDungeon
{
    public sealed class DungeonGraphAnalysis
    {
        private readonly Dictionary<int, List<int>> adjacency;
        private readonly Dictionary<int, Dictionary<int, int>> distances;
        private readonly HashSet<int> criticalPathRooms;
        private readonly List<int> deadEndRooms;
        private readonly List<int> junctionRooms;
        private readonly List<int> criticalPath;
        private readonly List<int> roomsOutsideCriticalPath;

        public int StartRoomId { get; }
        public int BossRoomId { get; }
        public int RoomCount { get; }
        public int EdgeCount { get; }
        public int ConnectedComponentCount { get; }
        public int LoopCount { get; }
        public int DiameterLength { get; }
        public bool IsConnected => RoomCount == 0 || ConnectedComponentCount == 1;
        public int CriticalPathLength => criticalPath.Count > 0 ? criticalPath.Count - 1 : 0;
        public IReadOnlyList<int> DeadEndRooms => deadEndRooms;
        public IReadOnlyList<int> JunctionRooms => junctionRooms;
        public IReadOnlyList<int> CriticalPath => criticalPath;
        public IReadOnlyList<int> RoomsOutsideCriticalPath => roomsOutsideCriticalPath;

        private DungeonGraphAnalysis(List<int> roomIds,
            IReadOnlyList<RoomConnectionData> edges, int startRoomId, int bossRoomId,
            Dictionary<int, List<int>> graph, int components)
        {
            StartRoomId = startRoomId;
            BossRoomId = bossRoomId;
            RoomCount = roomIds.Count;
            EdgeCount = edges.Count;
            ConnectedComponentCount = components;
            LoopCount = edges.Count - roomIds.Count + components;
            adjacency = graph;
            distances = new Dictionary<int, Dictionary<int, int>>(roomIds.Count);
            deadEndRooms = new List<int>();
            junctionRooms = new List<int>();
            criticalPath = BuildPath(startRoomId, bossRoomId);
            criticalPathRooms = new HashSet<int>(criticalPath);
            roomsOutsideCriticalPath = new List<int>();

            int diameter = 0;
            for (int i = 0; i < roomIds.Count; i++)
            {
                int roomId = roomIds[i];
                Dictionary<int, int> fromRoom = CalculateDistances(roomId);
                distances.Add(roomId, fromRoom);
                foreach (KeyValuePair<int, int> pair in fromRoom)
                    if (pair.Value > diameter) diameter = pair.Value;

                int degree = adjacency[roomId].Count;
                if (degree == 1 && roomId != startRoomId) deadEndRooms.Add(roomId);
                if (degree >= 3) junctionRooms.Add(roomId);
                if (!criticalPathRooms.Contains(roomId)) roomsOutsideCriticalPath.Add(roomId);
            }
            DiameterLength = diameter;
        }

        public int GetDegree(int roomId)
        {
            return adjacency.TryGetValue(roomId, out List<int> neighbors)
                ? neighbors.Count : -1;
        }

        public bool IsDeadEnd(int roomId)
        {
            return roomId != StartRoomId && GetDegree(roomId) == 1;
        }

        public bool IsJunction(int roomId)
        {
            return GetDegree(roomId) >= 3;
        }

        public bool IsOnCriticalPath(int roomId)
        {
            return criticalPathRooms.Contains(roomId);
        }

        public int GetShortestDistance(int fromRoomId, int toRoomId)
        {
            return distances.TryGetValue(fromRoomId, out Dictionary<int, int> fromRoom)
                && fromRoom.TryGetValue(toRoomId, out int distance) ? distance : -1;
        }

        internal static bool TryCreate(IReadOnlyList<RoomData> rooms,
            IReadOnlyList<RoomConnectionData> edges, int startRoomId, int bossRoomId,
            out DungeonGraphAnalysis analysis, out string error)
        {
            analysis = null;
            if (rooms == null || edges == null)
            {
                error = "Graph analysis source is missing.";
                return false;
            }

            var roomIds = new List<int>(rooms.Count);
            var graph = new Dictionary<int, List<int>>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData room = rooms[i];
                if (room == null || graph.ContainsKey(room.RoomId))
                {
                    error = "Graph analysis found a null room or duplicate room ID.";
                    return false;
                }
                roomIds.Add(room.RoomId);
                graph.Add(room.RoomId, new List<int>());
            }
            roomIds.Sort();

            var pairs = new HashSet<long>();
            for (int i = 0; i < edges.Count; i++)
            {
                RoomConnectionData edge = edges[i];
                if (edge == null || edge.RoomAId == edge.RoomBId
                    || !graph.ContainsKey(edge.RoomAId) || !graph.ContainsKey(edge.RoomBId)
                    || !pairs.Add(EdgeKey(edge.RoomAId, edge.RoomBId)))
                {
                    error = $"Graph analysis found an invalid edge at index {i}.";
                    return false;
                }
                graph[edge.RoomAId].Add(edge.RoomBId);
                graph[edge.RoomBId].Add(edge.RoomAId);
            }
            for (int i = 0; i < roomIds.Count; i++) graph[roomIds[i]].Sort();

            int components = CountComponents(roomIds, graph);
            if (roomIds.Count > 0 && (!graph.ContainsKey(startRoomId)
                || (roomIds.Count > 1 && !graph.ContainsKey(bossRoomId))))
            {
                error = "Graph analysis start or boss room is invalid.";
                return false;
            }

            analysis = new DungeonGraphAnalysis(roomIds, edges, startRoomId,
                roomIds.Count > 1 ? bossRoomId : -1, graph, components);
            error = string.Empty;
            return true;
        }

        private List<int> BuildPath(int startRoomId, int bossRoomId)
        {
            var result = new List<int>();
            if (!adjacency.ContainsKey(startRoomId) || !adjacency.ContainsKey(bossRoomId))
                return result;
            if (startRoomId == bossRoomId)
            {
                result.Add(startRoomId);
                return result;
            }

            var parent = new Dictionary<int, int>();
            var visited = new HashSet<int> { startRoomId };
            var queue = new Queue<int>();
            queue.Enqueue(startRoomId);
            while (queue.Count > 0 && !visited.Contains(bossRoomId))
            {
                int roomId = queue.Dequeue();
                List<int> neighbors = adjacency[roomId];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (!visited.Add(neighbor)) continue;
                    parent.Add(neighbor, roomId);
                    queue.Enqueue(neighbor);
                }
            }
            if (!visited.Contains(bossRoomId)) return result;

            int current = bossRoomId;
            result.Add(current);
            while (current != startRoomId)
            {
                current = parent[current];
                result.Add(current);
            }
            result.Reverse();
            return result;
        }

        private Dictionary<int, int> CalculateDistances(int source)
        {
            var result = new Dictionary<int, int> { { source, 0 } };
            var queue = new Queue<int>();
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                int roomId = queue.Dequeue();
                List<int> neighbors = adjacency[roomId];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (result.ContainsKey(neighbor)) continue;
                    result.Add(neighbor, result[roomId] + 1);
                    queue.Enqueue(neighbor);
                }
            }
            return result;
        }

        private static int CountComponents(List<int> roomIds,
            Dictionary<int, List<int>> graph)
        {
            int count = 0;
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            for (int i = 0; i < roomIds.Count; i++)
            {
                if (!visited.Add(roomIds[i])) continue;
                count++;
                queue.Enqueue(roomIds[i]);
                while (queue.Count > 0)
                {
                    List<int> neighbors = graph[queue.Dequeue()];
                    for (int n = 0; n < neighbors.Count; n++)
                        if (visited.Add(neighbors[n])) queue.Enqueue(neighbors[n]);
                }
            }
            return count;
        }

        private static long EdgeKey(int a, int b)
        {
            int minimum = a < b ? a : b;
            int maximum = a < b ? b : a;
            return ((long)minimum << 32) ^ (uint)maximum;
        }
    }
}
