using System;
using System.Collections.Generic;

namespace ProceduralDungeon
{
    internal static class DungeonTopologyPlanner
    {
        public static bool TryBuild(IReadOnlyList<RoomData> rooms,
            int startRoomId, int bossRoomId, int minimumCriticalPathHops,
            int targetDeadEnds, int maximumDegree, int loopTarget,
            out List<RoomConnectionData> connections, out string error)
        {
            connections = new List<RoomConnectionData>();
            error = string.Empty;
            if (rooms == null || rooms.Count == 0)
            {
                error = "No rooms are available for topology planning.";
                return false;
            }
            if (rooms.Count == 1) return true;

            try
            {
                List<Candidate> candidates = BuildCandidates(rooms);
                var byPair = new Dictionary<long, Candidate>(candidates.Count);
                var unused = new List<int>(rooms.Count);
                var degree = new Dictionary<int, int>(rooms.Count);
                for (int i = 0; i < rooms.Count; i++)
                {
                    unused.Add(rooms[i].RoomId);
                    degree.Add(rooms[i].RoomId, 0);
                }
                unused.Sort();
                for (int i = 0; i < candidates.Count; i++)
                    byPair.Add(EdgeKey(candidates[i].A, candidates[i].B), candidates[i]);

                var criticalPath = new List<int> { startRoomId, bossRoomId };
                unused.Remove(startRoomId);
                unused.Remove(bossRoomId);
                int targetPathRooms = Math.Min(rooms.Count, minimumCriticalPathHops + 1);
                if (targetDeadEnds <= 1) targetPathRooms = rooms.Count;
                while (criticalPath.Count < targetPathRooms)
                {
                    if (!TryFindBestInsertion(criticalPath, unused, byPair,
                        out int roomId, out int insertionIndex))
                    {
                        error = "Could not expand the critical path.";
                        return false;
                    }
                    criticalPath.Insert(insertionIndex, roomId);
                    unused.Remove(roomId);
                }

                var usedPairs = new HashSet<long>();
                var adjacency = CreateAdjacency(rooms);
                for (int i = 1; i < criticalPath.Count; i++)
                    AddConnection(criticalPath[i - 1], criticalPath[i], true,
                        byPair, connections, usedPairs, degree, adjacency);

                int branchCount = Math.Min(Math.Max(0, targetDeadEnds - 1), unused.Count);
                var branchTips = new List<int>(branchCount);
                var usedRoots = new HashSet<int>();
                for (int branch = 0; branch < branchCount; branch++)
                {
                    if (!TryFindBranchStart(criticalPath, unused, degree, usedRoots,
                        maximumDegree, byPair, out int root, out int roomId))
                    {
                        error = "Could not attach a requested topology branch.";
                        return false;
                    }
                    AddConnection(root, roomId, true, byPair, connections,
                        usedPairs, degree, adjacency);
                    usedRoots.Add(root);
                    branchTips.Add(roomId);
                    unused.Remove(roomId);
                }

                while (unused.Count > 0)
                {
                    if (!TryExtendBranch(branchTips, unused, degree, maximumDegree,
                        byPair, out int branchIndex, out int roomId))
                    {
                        error = "Could not place every room within the degree policy.";
                        return false;
                    }
                    int tip = branchTips[branchIndex];
                    AddConnection(tip, roomId, true, byPair, connections,
                        usedPairs, degree, adjacency);
                    branchTips[branchIndex] = roomId;
                    unused.Remove(roomId);
                }

                int addedLoops = 0;
                for (int i = 0; i < candidates.Count && addedLoops < loopTarget; i++)
                {
                    Candidate candidate = candidates[i];
                    if (usedPairs.Contains(EdgeKey(candidate.A, candidate.B))
                        || degree[candidate.A] <= 1 || degree[candidate.B] <= 1
                        || degree[candidate.A] >= maximumDegree
                        || degree[candidate.B] >= maximumDegree
                        || ShortestDistance(adjacency, candidate.A, candidate.B) < 3)
                        continue;

                    adjacency[candidate.A].Add(candidate.B);
                    adjacency[candidate.B].Add(candidate.A);
                    int criticalDistance = ShortestDistance(adjacency,
                        startRoomId, bossRoomId);
                    adjacency[candidate.A].Remove(candidate.B);
                    adjacency[candidate.B].Remove(candidate.A);
                    if (criticalDistance < minimumCriticalPathHops) continue;

                    AddConnection(candidate.A, candidate.B, false, byPair,
                        connections, usedPairs, degree, adjacency);
                    addedLoops++;
                }
                return true;
            }
            catch (OverflowException)
            {
                connections.Clear();
                error = "Room distance overflowed during topology planning.";
                return false;
            }
        }

        private static List<Candidate> BuildCandidates(IReadOnlyList<RoomData> rooms)
        {
            var result = new List<Candidate>();
            for (int i = 0; i < rooms.Count - 1; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    long ax = checked((long)rooms[i].Bounds.xMin + rooms[i].Bounds.xMax);
                    long ay = checked((long)rooms[i].Bounds.yMin + rooms[i].Bounds.yMax);
                    long bx = checked((long)rooms[j].Bounds.xMin + rooms[j].Bounds.xMax);
                    long by = checked((long)rooms[j].Bounds.yMin + rooms[j].Bounds.yMax);
                    long dx = checked(ax - bx);
                    long dy = checked(ay - by);
                    result.Add(new Candidate(rooms[i].RoomId, rooms[j].RoomId,
                        checked(dx * dx + dy * dy)));
                }
            result.Sort(Candidate.Compare);
            return result;
        }

        private static bool TryFindBestInsertion(List<int> path, List<int> unused,
            Dictionary<long, Candidate> candidates, out int roomId,
            out int insertionIndex)
        {
            roomId = -1;
            insertionIndex = -1;
            long bestDelta = long.MaxValue;
            long bestCombined = long.MaxValue;
            for (int room = 0; room < unused.Count; room++)
                for (int edge = 1; edge < path.Count; edge++)
                {
                    Candidate left = candidates[EdgeKey(path[edge - 1], unused[room])];
                    Candidate right = candidates[EdgeKey(unused[room], path[edge])];
                    Candidate replaced = candidates[EdgeKey(path[edge - 1], path[edge])];
                    long combined = checked(left.Weight + right.Weight);
                    long delta = checked(combined - replaced.Weight);
                    if (roomId < 0 || delta < bestDelta
                        || (delta == bestDelta && (combined < bestCombined
                        || (combined == bestCombined && (unused[room] < roomId
                        || (unused[room] == roomId && edge < insertionIndex))))))
                    {
                        roomId = unused[room];
                        insertionIndex = edge;
                        bestDelta = delta;
                        bestCombined = combined;
                    }
                }
            return roomId >= 0;
        }

        private static bool TryFindBranchStart(List<int> path, List<int> unused,
            Dictionary<int, int> degree, HashSet<int> usedRoots, int maximumDegree,
            Dictionary<long, Candidate> candidates, out int root, out int roomId)
        {
            if (FindBranchStart(path, unused, degree, usedRoots, maximumDegree,
                candidates, true, out root, out roomId)) return true;
            return FindBranchStart(path, unused, degree, usedRoots, maximumDegree,
                candidates, false, out root, out roomId);
        }

        private static bool FindBranchStart(List<int> path, List<int> unused,
            Dictionary<int, int> degree, HashSet<int> usedRoots, int maximumDegree,
            Dictionary<long, Candidate> candidates, bool requireUnusedRoot,
            out int root, out int roomId)
        {
            root = -1;
            roomId = -1;
            long bestWeight = long.MaxValue;
            bool hasInternalRoom = path.Count > 2;
            for (int p = 0; p < path.Count; p++)
            {
                int candidateRoot = path[p];
                if (hasInternalRoom && (p == 0 || p == path.Count - 1)) continue;
                if (degree[candidateRoot] >= maximumDegree
                    || (requireUnusedRoot && usedRoots.Contains(candidateRoot))) continue;
                for (int room = 0; room < unused.Count; room++)
                {
                    long weight = candidates[EdgeKey(candidateRoot, unused[room])].Weight;
                    if (root < 0 || weight < bestWeight
                        || (weight == bestWeight && (candidateRoot < root
                        || (candidateRoot == root && unused[room] < roomId))))
                    {
                        root = candidateRoot;
                        roomId = unused[room];
                        bestWeight = weight;
                    }
                }
            }
            return root >= 0;
        }

        private static bool TryExtendBranch(List<int> tips, List<int> unused,
            Dictionary<int, int> degree, int maximumDegree,
            Dictionary<long, Candidate> candidates, out int branchIndex,
            out int roomId)
        {
            branchIndex = -1;
            roomId = -1;
            long bestWeight = long.MaxValue;
            for (int branch = 0; branch < tips.Count; branch++)
            {
                int tip = tips[branch];
                if (degree[tip] >= maximumDegree) continue;
                for (int room = 0; room < unused.Count; room++)
                {
                    long weight = candidates[EdgeKey(tip, unused[room])].Weight;
                    if (branchIndex < 0 || weight < bestWeight
                        || (weight == bestWeight && (tip < tips[branchIndex]
                        || (tip == tips[branchIndex] && unused[room] < roomId))))
                    {
                        branchIndex = branch;
                        roomId = unused[room];
                        bestWeight = weight;
                    }
                }
            }
            return branchIndex >= 0;
        }

        private static void AddConnection(int a, int b, bool primary,
            Dictionary<long, Candidate> candidates,
            List<RoomConnectionData> connections, HashSet<long> usedPairs,
            Dictionary<int, int> degree, Dictionary<int, List<int>> adjacency)
        {
            Candidate candidate = candidates[EdgeKey(a, b)];
            connections.Add(new RoomConnectionData(candidate.A, candidate.B,
                candidate.Weight, primary));
            usedPairs.Add(EdgeKey(a, b));
            degree[a]++;
            degree[b]++;
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }

        private static Dictionary<int, List<int>> CreateAdjacency(
            IReadOnlyList<RoomData> rooms)
        {
            var result = new Dictionary<int, List<int>>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
                result.Add(rooms[i].RoomId, new List<int>());
            return result;
        }

        private static int ShortestDistance(Dictionary<int, List<int>> graph,
            int start, int target)
        {
            if (start == target) return 0;
            var distance = new Dictionary<int, int> { { start, 0 } };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                List<int> neighbors = graph[room];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    if (distance.ContainsKey(neighbor)) continue;
                    int next = distance[room] + 1;
                    if (neighbor == target) return next;
                    distance.Add(neighbor, next);
                    queue.Enqueue(neighbor);
                }
            }
            return -1;
        }

        private static long EdgeKey(int a, int b)
        {
            int minimum = a < b ? a : b;
            int maximum = a < b ? b : a;
            return ((long)minimum << 32) ^ (uint)maximum;
        }

        private sealed class Candidate
        {
            public int A { get; }
            public int B { get; }
            public long Weight { get; }

            public Candidate(int a, int b, long weight)
            {
                A = Math.Min(a, b);
                B = Math.Max(a, b);
                Weight = weight;
            }

            public static int Compare(Candidate left, Candidate right)
            {
                int result = left.Weight.CompareTo(right.Weight);
                if (result != 0) return result;
                result = left.A.CompareTo(right.A);
                return result != 0 ? result : left.B.CompareTo(right.B);
            }
        }
    }
}
