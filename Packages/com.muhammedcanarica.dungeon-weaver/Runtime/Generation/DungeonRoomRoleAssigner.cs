using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder))]
    public sealed class DungeonRoomRoleAssigner : MonoBehaviour
    {
        private const int InvalidRoomId = -1;
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        [SerializeField, HideInInspector] private int startRoomId = InvalidRoomId;
        [SerializeField, HideInInspector] private int bossRoomId = InvalidRoomId;
        [SerializeField, HideInInspector] private long storedLayoutSignature;
        [SerializeField, HideInInspector] private bool hasAssignedRoles;

        public int StartRoomId => startRoomId;
        public int BossRoomId => bossRoomId;
        public bool HasAssignedRoles => hasAssignedRoles;

        public bool IsRoleDataCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
                if (generator == null || graph == null || !graph.IsGraphCurrent || !hasAssignedRoles
                    || storedLayoutSignature != CalculateSignature(generator.Rooms, graph.Connections)) return false;

                bool hasStart = false, hasBoss = false;
                for (int i = 0; i < generator.Rooms.Count; i++)
                {
                    if (generator.Rooms[i].RoomId == startRoomId) hasStart = true;
                    if (generator.Rooms[i].RoomId == bossRoomId) hasBoss = true;
                }
                return hasStart && (generator.Rooms.Count == 1 ? bossRoomId == InvalidRoomId : hasBoss && bossRoomId != startRoomId);
            }
        }

        [ContextMenu("Assign Room Roles")]
        public void AssignRoomRoles()
        {
            ClearValues();
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonGraphBuilder graph = GetComponent<DungeonGraphBuilder>();
            if (generator == null || graph == null) { Debug.LogError("[DungeonRoomRoleAssigner] Required component is missing.", this); return; }
            if (!graph.IsGraphCurrent) { Debug.LogWarning("[DungeonRoomRoleAssigner] Role assignment rejected because the graph is stale.", this); return; }
            IReadOnlyList<RoomData> rooms = generator.Rooms;
            if (rooms == null || rooms.Count == 0) { Debug.LogWarning("[DungeonRoomRoleAssigner] No rooms are available.", this); return; }

            RoomData start = rooms[0];
            for (int i = 1; i < rooms.Count; i++) if (CompareStart(rooms[i], start) < 0) start = rooms[i];
            startRoomId = start.RoomId;

            if (rooms.Count == 1)
            {
                bossRoomId = InvalidRoomId;
                storedLayoutSignature = CalculateSignature(rooms, graph.Connections);
                hasAssignedRoles = true;
                Debug.Log($"[DungeonRoomRoleAssigner] Assigned start room {startRoomId}; no boss room exists in a one-room layout.", this);
                return;
            }

            if (!TryCalculateHopDistances(rooms, graph.Connections, startRoomId, out Dictionary<int, int> distance, out string error))
            {
                ClearValues();
                Debug.LogError($"[DungeonRoomRoleAssigner] Role assignment failed: {error}", this);
                return;
            }

            RoomData boss = null;
            int bestHops = -1;
            long bestWeight = long.MinValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomData candidate = rooms[i];
                if (candidate.RoomId == startRoomId) continue;
                int hops = distance[candidate.RoomId];
                long weight;
                try { weight = CenterWeightSquared(start.Bounds, candidate.Bounds); }
                catch (OverflowException) { ClearValues(); Debug.LogError("[DungeonRoomRoleAssigner] Center distance overflowed.", this); return; }
                if (boss == null || hops > bestHops || (hops == bestHops && (weight > bestWeight || (weight == bestWeight && candidate.RoomId < boss.RoomId))))
                { boss = candidate; bestHops = hops; bestWeight = weight; }
            }

            if (boss == null || boss.RoomId == startRoomId) { ClearValues(); Debug.LogError("[DungeonRoomRoleAssigner] Could not select a distinct boss room.", this); return; }
            bossRoomId = boss.RoomId;
            storedLayoutSignature = CalculateSignature(rooms, graph.Connections);
            hasAssignedRoles = true;
            Debug.Log($"[DungeonRoomRoleAssigner] Assigned start room {startRoomId} and boss room {bossRoomId} ({bestHops} graph hops).", this);
        }

        [ContextMenu("Clear Room Roles")]
        public void ClearRoomRoles() => ClearValues();

        private static int CompareStart(RoomData left, RoomData right)
        {
            int c = left.Bounds.xMin.CompareTo(right.Bounds.xMin); if (c != 0) return c;
            c = left.Bounds.yMin.CompareTo(right.Bounds.yMin); return c != 0 ? c : left.RoomId.CompareTo(right.RoomId);
        }

        private static bool TryCalculateHopDistances(IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> edges, int start,
            out Dictionary<int, int> distance, out string error)
        {
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < rooms.Count; i++) adjacency.Add(rooms[i].RoomId, new List<int>());
            for (int i = 0; i < edges.Count; i++)
            {
                RoomConnectionData edge = edges[i];
                if (edge == null || !adjacency.ContainsKey(edge.RoomAId) || !adjacency.ContainsKey(edge.RoomBId)) { distance = null; error = "Graph references a missing room."; return false; }
                adjacency[edge.RoomAId].Add(edge.RoomBId); adjacency[edge.RoomBId].Add(edge.RoomAId);
            }
            distance = new Dictionary<int, int>(); var queue = new Queue<int>(); distance.Add(start, 0); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue(); List<int> neighbors = adjacency[room];
                for (int i = 0; i < neighbors.Count; i++) if (!distance.ContainsKey(neighbors[i])) { distance.Add(neighbors[i], distance[room] + 1); queue.Enqueue(neighbors[i]); }
            }
            if (distance.Count != rooms.Count) { error = $"Graph is disconnected: reached {distance.Count}/{rooms.Count} rooms."; return false; }
            error = string.Empty; return true;
        }

        private static long CenterWeightSquared(RectInt a, RectInt b)
        {
            long dx = checked(checked((long)a.xMin + a.xMax) - checked((long)b.xMin + b.xMax));
            long dy = checked(checked((long)a.yMin + a.yMax) - checked((long)b.yMin + b.yMax));
            return checked(dx * dx + dy * dy);
        }

        private static long CalculateSignature(IReadOnlyList<RoomData> rooms, IReadOnlyList<RoomConnectionData> edges)
        {
            ulong hash = Offset;
            if (rooms != null) for (int i=0;i<rooms.Count;i++){RoomData r=rooms[i];if(r==null){AddInt(ref hash,int.MinValue);continue;}AddInt(ref hash,r.RoomId);AddInt(ref hash,r.Bounds.x);AddInt(ref hash,r.Bounds.y);AddInt(ref hash,r.Bounds.width);AddInt(ref hash,r.Bounds.height);}
            if (edges != null) for(int i=0;i<edges.Count;i++){RoomConnectionData e=edges[i];if(e==null){AddInt(ref hash,int.MinValue);continue;}AddInt(ref hash,e.RoomAId);AddInt(ref hash,e.RoomBId);AddLong(ref hash,e.WeightSquared);AddByte(ref hash,e.IsPrimaryConnection?(byte)1:(byte)0);}
            return unchecked((long)hash);
        }
        private static void AddInt(ref ulong h,int v){uint b=unchecked((uint)v);for(int s=0;s<32;s+=8)AddByte(ref h,(byte)(b>>s));}
        private static void AddLong(ref ulong h,long v){ulong b=unchecked((ulong)v);for(int s=0;s<64;s+=8)AddByte(ref h,(byte)(b>>s));}
        private static void AddByte(ref ulong h,byte v){h^=v;h=unchecked(h*Prime);}
        private void ClearValues(){startRoomId=InvalidRoomId;bossRoomId=InvalidRoomId;storedLayoutSignature=0;hasAssignedRoles=false;}
    }
}
