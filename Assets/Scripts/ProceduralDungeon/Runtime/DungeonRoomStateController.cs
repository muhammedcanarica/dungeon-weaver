using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonRoomRoleAssigner), typeof(DungeonAreaTracker))]
    public sealed class DungeonRoomStateController : MonoBehaviour
    {
        public const int InvalidId = -1;
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private readonly Dictionary<int, DungeonRoomRuntimeState> states =
            new Dictionary<int, DungeonRoomRuntimeState>();
        private DungeonGenerator generator;
        private DungeonRoomRoleAssigner roleAssigner;
        private DungeonAreaTracker areaTracker;
        private bool hasRuntimeStates;
        private int discoveredRoomCount;
        private int currentActiveRoomId = InvalidId;
        private int nextVisitOrder = 1;
        private long initialStateSignature;
        private bool hasInitialStateSignature;
        private bool subscribed;

        public int TotalRoomCount => states.Count;
        public int DiscoveredRoomCount => discoveredRoomCount;
        public int CurrentActiveRoomId => currentActiveRoomId;
        public bool HasRuntimeStates => hasRuntimeStates;
        public long InitialStateSignature => initialStateSignature;
        public bool HasInitialStateSignature => hasInitialStateSignature;

        public event Action<DungeonRoomStateChange> RoomDiscovered;
        public event Action<DungeonRoomStateChange> RoomStateChanged;
        public event Action<DungeonActiveRoomChange> ActiveRoomChanged;

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public bool InitializeStates()
        {
            ClearStates();
            CacheComponents();
            Subscribe();
            if (generator == null || roleAssigner == null || areaTracker == null
                || !roleAssigner.IsRoleDataCurrent || !areaTracker.IsLookupCurrent
                || generator.Rooms == null || generator.Rooms.Count == 0)
            {
                Debug.LogError("[DungeonRoomStateController] Initialization rejected because source data is invalid.", this);
                return false;
            }

            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room == null || states.ContainsKey(room.RoomId))
                {
                    ClearStates();
                    Debug.LogError("[DungeonRoomStateController] Room collection contains null or duplicate IDs.", this);
                    return false;
                }

                DungeonRoomRole role = room.RoomId == roleAssigner.StartRoomId
                    ? DungeonRoomRole.Start
                    : room.RoomId == roleAssigner.BossRoomId ? DungeonRoomRole.Boss : DungeonRoomRole.Normal;
                states.Add(room.RoomId, new DungeonRoomRuntimeState(room.RoomId, role, InvalidId));
            }

            hasRuntimeStates = true;
            Debug.Log($"[DungeonRoomStateController] Initialized runtime state for {states.Count} rooms.", this);
            return true;
        }

        public void ClearStates()
        {
            states.Clear();
            hasRuntimeStates = false;
            discoveredRoomCount = 0;
            currentActiveRoomId = InvalidId;
            nextVisitOrder = 1;
            initialStateSignature = 0;
            hasInitialStateSignature = false;
        }

        public bool CaptureInitialStateSignature()
        {
            if (!hasRuntimeStates || currentActiveRoomId == InvalidId || discoveredRoomCount != 1)
                return false;
            initialStateSignature = ComputeSignature();
            hasInitialStateSignature = true;
            return true;
        }

        public bool TryGetRoomState(int roomId, out DungeonRoomRuntimeState state)
        {
            return states.TryGetValue(roomId, out state);
        }

        public bool IsRoomVisited(int roomId)
        {
            return states.TryGetValue(roomId, out DungeonRoomRuntimeState state)
                && state.ExplorationState != RoomExplorationState.Unvisited;
        }

        public bool IsRoomActive(int roomId)
        {
            return states.TryGetValue(roomId, out DungeonRoomRuntimeState state)
                && state.ExplorationState == RoomExplorationState.Active;
        }

        private void HandleAreaChanged(DungeonAreaTransition transition)
        {
            if (!hasRuntimeStates) return;
            int previousActive = currentActiveRoomId;
            bool leavesRoom = transition.PreviousAreaType == DungeonAreaType.Room
                && transition.PreviousRoomId != transition.CurrentRoomId;
            bool entersRoom = transition.CurrentAreaType == DungeonAreaType.Room
                && transition.CurrentRoomId != transition.PreviousRoomId;

            if (leavesRoom) ExitRoom(transition.PreviousRoomId, transition.ConnectionIndex, transition.Cell);
            if (entersRoom) EnterRoom(transition.CurrentRoomId, transition.ConnectionIndex, transition.Cell);

            if (previousActive != currentActiveRoomId)
                ActiveRoomChanged?.Invoke(new DungeonActiveRoomChange(previousActive,
                    currentActiveRoomId, transition.ConnectionIndex, transition.Cell));
        }

        private void ExitRoom(int roomId, int connectionIndex, Vector2Int cell)
        {
            if (!states.TryGetValue(roomId, out DungeonRoomRuntimeState state)
                || state.ExplorationState != RoomExplorationState.Active) return;
            RoomExplorationState previous = state.ExplorationState;
            state.Exit(connectionIndex);
            if (currentActiveRoomId == roomId) currentActiveRoomId = InvalidId;
            RoomStateChanged?.Invoke(new DungeonRoomStateChange(roomId, previous,
                state.ExplorationState, state.VisitCount, connectionIndex, cell, false));
        }

        private void EnterRoom(int roomId, int connectionIndex, Vector2Int cell)
        {
            if (!states.TryGetValue(roomId, out DungeonRoomRuntimeState state)) return;
            RoomExplorationState previous = state.ExplorationState;
            bool firstDiscovery = previous == RoomExplorationState.Unvisited;
            int visitOrder = firstDiscovery ? nextVisitOrder++ : state.FirstVisitOrder;
            state.Enter(connectionIndex, visitOrder);
            currentActiveRoomId = roomId;
            if (firstDiscovery) discoveredRoomCount++;

            var change = new DungeonRoomStateChange(roomId, previous, state.ExplorationState,
                state.VisitCount, connectionIndex, cell, firstDiscovery);
            if (firstDiscovery) RoomDiscovered?.Invoke(change);
            RoomStateChanged?.Invoke(change);
        }

        private long ComputeSignature()
        {
            var roomIds = new List<int>(states.Keys);
            roomIds.Sort();
            ulong hash = Offset;
            AddInt(ref hash, roomIds.Count);
            for (int i = 0; i < roomIds.Count; i++)
            {
                DungeonRoomRuntimeState state = states[roomIds[i]];
                AddInt(ref hash, state.RoomId);
                AddInt(ref hash, (int)state.RoomRole);
                AddInt(ref hash, (int)state.ExplorationState);
                AddInt(ref hash, state.VisitCount);
                AddInt(ref hash, state.FirstVisitOrder);
                AddInt(ref hash, state.LastEnteredConnectionIndex);
                AddInt(ref hash, state.LastExitedConnectionIndex);
            }

            return unchecked((long)hash);
        }

        private void Subscribe()
        {
            if (areaTracker == null || subscribed) return;
            areaTracker.AreaChanged += HandleAreaChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (areaTracker != null && subscribed) areaTracker.AreaChanged -= HandleAreaChanged;
            subscribed = false;
        }

        private void CacheComponents()
        {
            if (generator == null) generator = GetComponent<DungeonGenerator>();
            if (roleAssigner == null) roleAssigner = GetComponent<DungeonRoomRoleAssigner>();
            if (areaTracker == null) areaTracker = GetComponent<DungeonAreaTracker>();
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
    }
}
