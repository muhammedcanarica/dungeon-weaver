using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator))]
    public sealed class DungeonGraphBuilder : MonoBehaviour
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        [SerializeField, Min(0)] private int extraConnectionCount = 2;
        [SerializeField, HideInInspector] private List<RoomConnectionData> connections = new List<RoomConnectionData>();
        [SerializeField, HideInInspector] private long builtLayoutSignature;
        [SerializeField, HideInInspector] private bool hasBuiltGraph;
        private IReadOnlyList<RoomConnectionData> readOnlyConnections;

        public IReadOnlyList<RoomConnectionData> Connections
        {
            get
            {
                EnsureList();
                return readOnlyConnections ??= connections.AsReadOnly();
            }
        }

        public bool IsGraphCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                return generator != null && hasBuiltGraph && builtLayoutSignature == Signature(generator.Rooms);
            }
        }

        [ContextMenu("Build Room Graph")]
        public void BuildGraph()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            EnsureList();
            connections.Clear();
            hasBuiltGraph = false;
            builtLayoutSignature = 0;
            if (generator == null || generator.Rooms == null || generator.Rooms.Count == 0)
            {
                Debug.LogWarning("[DungeonGraphBuilder] No generated rooms are available.", this);
                return;
            }

            IReadOnlyList<RoomData> rooms = generator.Rooms;
            var index = new Dictionary<int, int>();
            var ids = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] == null || index.ContainsKey(rooms[i].RoomId))
                {
                    Debug.LogError("[DungeonGraphBuilder] Null room or duplicate room ID.", this);
                    return;
                }
                index.Add(rooms[i].RoomId, -1);
                ids.Add(rooms[i].RoomId);
            }
            ids.Sort();
            for (int i = 0; i < ids.Count; i++) index[ids[i]] = i;

            var candidates = new List<Candidate>();
            try
            {
                for (int i = 0; i < rooms.Count - 1; i++)
                    for (int j = i + 1; j < rooms.Count; j++)
                    {
                        long ax = checked((long)rooms[i].Bounds.xMin + rooms[i].Bounds.xMax);
                        long ay = checked((long)rooms[i].Bounds.yMin + rooms[i].Bounds.yMax);
                        long bx = checked((long)rooms[j].Bounds.xMin + rooms[j].Bounds.xMax);
                        long by = checked((long)rooms[j].Bounds.yMin + rooms[j].Bounds.yMax);
                        long dx = checked(ax - bx);
                        long dy = checked(ay - by);
                        candidates.Add(new Candidate(rooms[i].RoomId, rooms[j].RoomId, checked(dx * dx + dy * dy)));
                    }
            }
            catch (OverflowException)
            {
                Debug.LogError("[DungeonGraphBuilder] Room distance overflowed.", this);
                return;
            }

            candidates.Sort(Candidate.Compare);
            var union = new UnionFind(rooms.Count);
            var primary = new HashSet<Candidate>();
            for (int i = 0; i < candidates.Count && primary.Count < rooms.Count - 1; i++)
            {
                Candidate c = candidates[i];
                if (!union.Union(index[c.A], index[c.B])) continue;
                primary.Add(c);
                connections.Add(c.Data(true));
            }
            int extras = Math.Min(Math.Max(0, extraConnectionCount), candidates.Count - primary.Count);
            int added = 0;
            for (int i = 0; i < candidates.Count && added < extras; i++)
            {
                if (primary.Contains(candidates[i])) continue;
                connections.Add(candidates[i].Data(false));
                added++;
            }
            if (!Validate(rooms))
            {
                connections.Clear();
                Debug.LogError("[DungeonGraphBuilder] Graph validation failed.", this);
                return;
            }
            builtLayoutSignature = Signature(rooms);
            hasBuiltGraph = true;
            Debug.Log($"[DungeonGraphBuilder] Built connected graph for {rooms.Count} rooms: {primary.Count} primary and {added} extra connections.", this);
        }

        [ContextMenu("Clear Room Graph")]
        public void ClearGraph()
        {
            EnsureList();
            connections.Clear();
            builtLayoutSignature = 0;
            hasBuiltGraph = false;
        }

        private bool Validate(IReadOnlyList<RoomData> rooms)
        {
            var adjacency = new Dictionary<int, List<int>>();
            var pairs = new HashSet<long>();
            for (int i = 0; i < rooms.Count; i++) adjacency.Add(rooms[i].RoomId, new List<int>());
            int primary = 0;
            for (int i = 0; i < connections.Count; i++)
            {
                RoomConnectionData c = connections[i];
                long key = ((long)c.RoomAId << 32) ^ (uint)c.RoomBId;
                if (c.RoomAId == c.RoomBId || !adjacency.ContainsKey(c.RoomAId) || !adjacency.ContainsKey(c.RoomBId) || !pairs.Add(key)) return false;
                if (!c.IsPrimaryConnection) continue;
                primary++;
                adjacency[c.RoomAId].Add(c.RoomBId);
                adjacency[c.RoomBId].Add(c.RoomAId);
            }
            if (primary != rooms.Count - 1) return false;
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            visited.Add(rooms[0].RoomId);
            queue.Enqueue(rooms[0].RoomId);
            while (queue.Count > 0)
            {
                List<int> neighbors = adjacency[queue.Dequeue()];
                for (int i = 0; i < neighbors.Count; i++) if (visited.Add(neighbors[i])) queue.Enqueue(neighbors[i]);
            }
            return visited.Count == rooms.Count;
        }

        private static long Signature(IReadOnlyList<RoomData> rooms)
        {
            ulong hash = Offset;
            if (rooms != null) for (int i = 0; i < rooms.Count; i++)
            {
                RoomData r = rooms[i];
                if (r == null) { Add(ref hash, int.MinValue); continue; }
                Add(ref hash, r.RoomId); Add(ref hash, r.Bounds.x); Add(ref hash, r.Bounds.y); Add(ref hash, r.Bounds.width); Add(ref hash, r.Bounds.height);
            }
            return unchecked((long)hash);
        }

        private static void Add(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8) { hash ^= (byte)(bits >> shift); hash = unchecked(hash * Prime); }
        }

        private void EnsureList()
        {
            if (connections == null) { connections = new List<RoomConnectionData>(); readOnlyConnections = null; }
        }

        private sealed class Candidate
        {
            public readonly int A; public readonly int B; public readonly long Weight;
            public Candidate(int a, int b, long weight) { A = Math.Min(a, b); B = Math.Max(a, b); Weight = weight; }
            public RoomConnectionData Data(bool primary) => new RoomConnectionData(A, B, Weight, primary);
            public static int Compare(Candidate l, Candidate r)
            {
                int c = l.Weight.CompareTo(r.Weight); if (c != 0) return c;
                c = l.A.CompareTo(r.A); return c != 0 ? c : l.B.CompareTo(r.B);
            }
        }

        private sealed class UnionFind
        {
            private readonly int[] parent; private readonly byte[] rank;
            public UnionFind(int count) { parent = new int[count]; rank = new byte[count]; for (int i = 0; i < count; i++) parent[i] = i; }
            public bool Union(int a, int b)
            {
                a = Find(a); b = Find(b); if (a == b) return false;
                if (rank[a] < rank[b]) parent[a] = b; else if (rank[a] > rank[b]) parent[b] = a; else { parent[b] = a; rank[a]++; }
                return true;
            }
            private int Find(int value) { while (parent[value] != value) { parent[value] = parent[parent[value]]; value = parent[value]; } return value; }
        }
    }
}
