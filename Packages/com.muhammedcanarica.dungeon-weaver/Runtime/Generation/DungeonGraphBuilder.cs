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

        [Header("Topology")]
        [SerializeField, Min(1)] private int targetDeadEndCount = 3;
        [SerializeField, Range(3, 8)] private int maxRoomDegree = 4;
        [SerializeField, Min(0)] private int extraConnectionCount = 2;
        [SerializeField, Range(0f, 1f)] private float minimumCriticalPathRatio = 0.45f;

        [SerializeField, HideInInspector]
        private List<RoomConnectionData> connections = new List<RoomConnectionData>();
        [SerializeField, HideInInspector] private long builtSourceSignature;
        [SerializeField, HideInInspector] private long builtGraphSignature;
        [SerializeField, HideInInspector] private int suggestedStartRoomId = -1;
        [SerializeField, HideInInspector] private int suggestedBossRoomId = -1;
        [SerializeField, HideInInspector] private bool hasBuiltGraph;

        private IReadOnlyList<RoomConnectionData> readOnlyConnections;
        private DungeonGraphAnalysis analysis;

        public int TargetDeadEndCount => Mathf.Max(1, targetDeadEndCount);
        public int MaxRoomDegree => Mathf.Clamp(maxRoomDegree, 3, 8);
        public int ExtraConnectionCount => Mathf.Max(0, extraConnectionCount);
        public float MinimumCriticalPathRatio => Mathf.Clamp01(minimumCriticalPathRatio);
        public int SuggestedStartRoomId => suggestedStartRoomId;
        public int SuggestedBossRoomId => suggestedBossRoomId;
        public long GraphSignature
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                return CalculateGraphSignature(generator != null ? generator.Rooms : null,
                    Connections);
            }
        }

        public IReadOnlyList<RoomConnectionData> Connections
        {
            get
            {
                EnsureList();
                return readOnlyConnections ??= connections.AsReadOnly();
            }
        }

        public DungeonGraphAnalysis Analysis
        {
            get
            {
                EnsureAnalysis();
                return analysis;
            }
        }

        public bool IsGraphCurrent
        {
            get
            {
                DungeonGenerator generator = GetComponent<DungeonGenerator>();
                return generator != null && hasBuiltGraph
                    && builtSourceSignature == CalculateSourceSignature(generator.Rooms)
                    && builtGraphSignature == CalculateGraphSignature(generator.Rooms,
                        Connections);
            }
        }

        public int MinimumCriticalPathHops(int roomCount)
        {
            if (roomCount <= 1) return 0;
            int scaled = Mathf.CeilToInt((roomCount - 1) * MinimumCriticalPathRatio);
            if (roomCount >= 3) scaled = Mathf.Max(2, scaled);
            return Mathf.Clamp(scaled, 1, roomCount - 1);
        }

        [ContextMenu("Build Room Graph")]
        public void BuildGraph()
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            EnsureList();
            Invalidate();
            if (generator == null || generator.Rooms == null
                || generator.Rooms.Count == 0)
            {
                Debug.LogWarning("[DungeonGraphBuilder] No generated rooms are available.", this);
                return;
            }

            IReadOnlyList<RoomData> rooms = generator.Rooms;
            if (!TryValidateRooms(rooms, out string roomError))
            {
                Debug.LogError($"[DungeonGraphBuilder] {roomError}", this);
                return;
            }

            RoomData start = DungeonRoomRoleAssigner.SelectStartCandidate(rooms);
            int minimumBossInterior = 24;
            DungeonRoomRoleAssigner roleAssigner = GetComponent<DungeonRoomRoleAssigner>();
            if (roleAssigner != null)
                minimumBossInterior = roleAssigner.MinimumBossInteriorCells;
            RoomData boss = rooms.Count > 1
                ? DungeonRoomRoleAssigner.SelectBossAnchor(rooms, start,
                    minimumBossInterior) : null;
            if (start == null || (rooms.Count > 1 && (boss == null
                || boss.RoomId == start.RoomId)))
            {
                Debug.LogError("[DungeonGraphBuilder] Could not select topology endpoints.", this);
                return;
            }

            int minimumHops = MinimumCriticalPathHops(rooms.Count);
            if (!DungeonTopologyPlanner.TryBuild(rooms, start.RoomId,
                boss != null ? boss.RoomId : -1, minimumHops, TargetDeadEndCount,
                MaxRoomDegree, ExtraConnectionCount, out List<RoomConnectionData> built,
                out string planningError))
            {
                Debug.LogError($"[DungeonGraphBuilder] {planningError}", this);
                return;
            }
            connections.AddRange(built);

            suggestedStartRoomId = start.RoomId;
            suggestedBossRoomId = boss != null ? boss.RoomId : -1;
            bool analysisValid = TryAnalyze(rooms, suggestedStartRoomId,
                suggestedBossRoomId, out DungeonGraphAnalysis builtAnalysis,
                out string analysisError);
            string topologyError = string.Empty;
            bool topologyValid = analysisValid && ValidateTopology(rooms,
                builtAnalysis, minimumHops, out topologyError);
            if (!analysisValid || !topologyValid)
            {
                Invalidate();
                string error = string.IsNullOrEmpty(analysisError)
                    ? topologyError : analysisError;
                Debug.LogError($"[DungeonGraphBuilder] Graph validation failed: {error}", this);
                return;
            }

            analysis = builtAnalysis;
            builtSourceSignature = CalculateSourceSignature(rooms);
            builtGraphSignature = CalculateGraphSignature(rooms, Connections);
            hasBuiltGraph = true;
            int primaryCount = rooms.Count - 1;
            Debug.Log($"[DungeonGraphBuilder] Built {rooms.Count}-room topology: "
                + $"{primaryCount} primary, {analysis.LoopCount} loops, "
                + $"{analysis.DeadEndRooms.Count} dead ends, "
                + $"{analysis.JunctionRooms.Count} junctions, "
                + $"critical path {analysis.CriticalPathLength} hops.", this);
        }

        [ContextMenu("Clear Room Graph")]
        public void ClearGraph()
        {
            EnsureList();
            Invalidate();
        }

        internal bool SetRoleEndpoints(int startRoomId, int bossRoomId)
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            if (generator == null || !IsGraphCurrent
                || !TryAnalyze(generator.Rooms, startRoomId, bossRoomId,
                    out DungeonGraphAnalysis roleAnalysis, out _)) return false;
            suggestedStartRoomId = startRoomId;
            suggestedBossRoomId = bossRoomId;
            analysis = roleAnalysis;
            return true;
        }

        private bool ValidateTopology(IReadOnlyList<RoomData> rooms,
            DungeonGraphAnalysis graphAnalysis, int minimumHops, out string error)
        {
            if (!graphAnalysis.IsConnected)
            {
                error = "Graph is disconnected.";
                return false;
            }
            int primaryCount = 0;
            for (int i = 0; i < Connections.Count; i++)
                if (Connections[i].IsPrimaryConnection) primaryCount++;
            if (primaryCount != rooms.Count - 1)
            {
                error = "Primary connections do not form a spanning tree.";
                return false;
            }
            for (int i = 0; i < rooms.Count; i++)
                if (graphAnalysis.GetDegree(rooms[i].RoomId) > MaxRoomDegree)
                {
                    error = $"Room {rooms[i].RoomId} exceeds maximum degree {MaxRoomDegree}.";
                    return false;
                }
            if (rooms.Count > 1 && graphAnalysis.CriticalPathLength < minimumHops)
            {
                error = $"Critical path {graphAnalysis.CriticalPathLength} is below minimum {minimumHops}.";
                return false;
            }
            if (graphAnalysis.LoopCount > ExtraConnectionCount)
            {
                error = "Loop count exceeds the configured target.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryAnalyze(IReadOnlyList<RoomData> rooms, int startRoomId,
            int bossRoomId, out DungeonGraphAnalysis result, out string error)
        {
            return DungeonGraphAnalysis.TryCreate(rooms, Connections,
                startRoomId, bossRoomId, out result, out error);
        }

        private void EnsureAnalysis()
        {
            if (analysis != null || !IsGraphCurrent) return;
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            if (generator != null) TryAnalyze(generator.Rooms, suggestedStartRoomId,
                suggestedBossRoomId, out analysis, out _);
        }

        private static bool TryValidateRooms(IReadOnlyList<RoomData> rooms,
            out string error)
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i] == null || !ids.Add(rooms[i].RoomId))
                {
                    error = "Null room or duplicate room ID.";
                    return false;
                }
            error = string.Empty;
            return true;
        }

        private long CalculateSourceSignature(IReadOnlyList<RoomData> rooms)
        {
            ulong hash = Offset;
            AddInt(ref hash, TargetDeadEndCount);
            AddInt(ref hash, MaxRoomDegree);
            AddInt(ref hash, ExtraConnectionCount);
            AddInt(ref hash, BitConverter.SingleToInt32Bits(MinimumCriticalPathRatio));
            AddInt(ref hash, rooms?.Count ?? 0);
            if (rooms != null)
                for (int i = 0; i < rooms.Count; i++)
                {
                    RoomData room = rooms[i];
                    if (room == null) AddInt(ref hash, int.MinValue);
                    else AddLong(ref hash, room.GeometrySignature);
                }
            return unchecked((long)hash);
        }

        private long CalculateGraphSignature(IReadOnlyList<RoomData> rooms,
            IReadOnlyList<RoomConnectionData> edges)
        {
            ulong hash = Offset;
            AddLong(ref hash, CalculateSourceSignature(rooms));
            AddInt(ref hash, edges?.Count ?? 0);
            if (edges != null)
                for (int i = 0; i < edges.Count; i++)
                {
                    RoomConnectionData edge = edges[i];
                    if (edge == null)
                    {
                        AddInt(ref hash, int.MinValue);
                        continue;
                    }
                    AddInt(ref hash, edge.RoomAId);
                    AddInt(ref hash, edge.RoomBId);
                    AddLong(ref hash, edge.WeightSquared);
                    AddByte(ref hash, edge.IsPrimaryConnection ? (byte)1 : (byte)0);
                }
            return unchecked((long)hash);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
                AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddLong(ref ulong hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(bits >> shift));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash = unchecked(hash * Prime);
        }

        private void Invalidate()
        {
            connections.Clear();
            builtSourceSignature = 0;
            builtGraphSignature = 0;
            suggestedStartRoomId = -1;
            suggestedBossRoomId = -1;
            hasBuiltGraph = false;
            analysis = null;
        }

        private void EnsureList()
        {
            if (connections != null) return;
            connections = new List<RoomConnectionData>();
            readOnlyConnections = null;
        }
    }
}
