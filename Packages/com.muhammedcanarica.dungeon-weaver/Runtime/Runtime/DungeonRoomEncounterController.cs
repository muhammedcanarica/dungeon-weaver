using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonRoomRoleAssigner))]
    [RequireComponent(typeof(DungeonRoomStateController), typeof(DungeonDoorRegistry))]
    [RequireComponent(typeof(DungeonAreaTracker))]
    public sealed class DungeonRoomEncounterController : MonoBehaviour
    {
        [FormerlySerializedAs("playerCollider")]
        [SerializeField] private Collider2D trackedActorCollider;

        private readonly Dictionary<int, DungeonRoomEncounterRuntimeState> states =
            new Dictionary<int, DungeonRoomEncounterRuntimeState>();
        private readonly List<DungeonDoor> pendingDoors = new List<DungeonDoor>();
        private DungeonGenerator generator;
        private DungeonRoomRoleAssigner roleAssigner;
        private DungeonRoomStateController roomStateController;
        private DungeonDoorRegistry doorRegistry;
        private DungeonAreaTracker areaTracker;
        private bool hasStates;
        private bool subscribed;
        private int pendingActivationRoomId = DungeonRoomStateController.InvalidId;

        public int TotalRoomCount => states.Count;
        public int PendingDoorCount => pendingDoors.Count;
        public int PendingActivationRoomId => pendingActivationRoomId;
        public bool HasPendingActivation => pendingActivationRoomId
            != DungeonRoomStateController.InvalidId;
        public bool HasEncounterStates => hasStates;
        public Collider2D TrackedActorCollider => trackedActorCollider;
        public bool HasTrackedActor => areaTracker != null && areaTracker.HasTrackedActor
            && trackedActorCollider != null;
        public int ActiveLockedRoomId
        {
            get
            {
                int roomId = roomStateController != null
                    ? roomStateController.CurrentActiveRoomId : DungeonRoomStateController.InvalidId;
                return states.TryGetValue(roomId, out DungeonRoomEncounterRuntimeState state)
                    && state.State == RoomEncounterState.Locked
                    ? roomId : DungeonRoomStateController.InvalidId;
            }
        }

        public event Action<DungeonRoomEncounterChange> EncounterStateChanged;

        private void OnEnable()
        {
            CacheComponents();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            ProcessPendingActivation();
            ProcessPendingClosures();
        }

        public void SetTrackedActor(Transform actorTransform, Collider2D actorCollider)
        {
            CacheComponents();
            trackedActorCollider = actorCollider;
            areaTracker?.SetTrackedActor(actorTransform);
        }

        public bool ValidateTrackedActor(out string error)
        {
            CacheComponents();
            if (areaTracker == null || !areaTracker.HasTrackedActor)
            {
                error = "Tracked actor Transform is missing.";
                return false;
            }
            if (trackedActorCollider == null)
            {
                error = "Tracked actor Collider2D is missing.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public bool InitializeStates()
        {
            ClearStates();
            CacheComponents();
            Subscribe();
            bool actorValid = ValidateTrackedActor(out string actorError);
            if (generator == null || roleAssigner == null || roomStateController == null
                || doorRegistry == null || !roleAssigner.IsRoleDataCurrent
                || !roomStateController.HasRuntimeStates || !doorRegistry.HasBuiltDoors
                || generator.Rooms == null || generator.Rooms.Count == 0
                || !actorValid)
            {
                Debug.LogError($"[DungeonRoomEncounterController] Initialization rejected because source data or actor references are invalid. {actorError}", this);
                return false;
            }

            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room == null || states.ContainsKey(room.RoomId))
                {
                    ClearStates();
                    Debug.LogError("[DungeonRoomEncounterController] Room collection contains null or duplicate IDs.", this);
                    return false;
                }
                RoomEncounterState initial = room.RoomId == roleAssigner.StartRoomId
                    ? RoomEncounterState.Cleared : RoomEncounterState.Inactive;
                states.Add(room.RoomId, new DungeonRoomEncounterRuntimeState(room.RoomId, initial));
            }

            hasStates = true;
            Debug.Log($"[DungeonRoomEncounterController] Initialized encounter state for {states.Count} rooms.", this);
            return true;
        }

        public void ClearStates()
        {
            states.Clear();
            pendingDoors.Clear();
            pendingActivationRoomId = DungeonRoomStateController.InvalidId;
            hasStates = false;
        }

        public bool TryGetRoomState(int roomId, out DungeonRoomEncounterRuntimeState state)
        {
            return states.TryGetValue(roomId, out state);
        }

        public bool TryClearActiveRoom()
        {
            int roomId = roomStateController != null
                ? roomStateController.CurrentActiveRoomId : DungeonRoomStateController.InvalidId;
            return TryClearRoom(roomId);
        }

        public bool TryDebugClearActiveRoom()
        {
            return TryClearActiveRoom();
        }

        public bool TryDebugClearActiveRoomForSelection(GameObject selectedObject)
        {
            return TryClearActiveRoom();
        }

        public bool TryClearRoom(int roomId)
        {
            if (!states.TryGetValue(roomId, out DungeonRoomEncounterRuntimeState state)
                || state.State != RoomEncounterState.Locked || doorRegistry == null) return false;
            if (pendingActivationRoomId == roomId)
                pendingActivationRoomId = DungeonRoomStateController.InvalidId;
            SetEncounterState(state, RoomEncounterState.Cleared);
            RemovePendingDoors(roomId);
            doorRegistry.OpenRoomDoors(roomId);
            return true;
        }

        public bool ProcessPendingActivation()
        {
            int roomId = pendingActivationRoomId;
            if (roomId == DungeonRoomStateController.InvalidId) return false;
            if (!states.TryGetValue(roomId, out DungeonRoomEncounterRuntimeState state)
                || state.State != RoomEncounterState.Inactive)
            {
                pendingActivationRoomId = DungeonRoomStateController.InvalidId;
                return false;
            }

            if (roomStateController == null || !roomStateController.IsRoomActive(roomId)
                || roomStateController.CurrentActiveRoomId != roomId)
            {
                pendingActivationRoomId = DungeonRoomStateController.InvalidId;
                return false;
            }

            if (!IsTrackedActorSafelyInsideRoom(roomId) || !CloseRoomDoorsSafely(roomId)) return false;
            pendingActivationRoomId = DungeonRoomStateController.InvalidId;
            SetEncounterState(state, RoomEncounterState.Locked);
            return true;
        }

        public void ProcessPendingClosures()
        {
            for (int i = pendingDoors.Count - 1; i >= 0; i--)
            {
                DungeonDoor door = pendingDoors[i];
                if (door == null || !states.TryGetValue(door.RoomId,
                        out DungeonRoomEncounterRuntimeState state)
                    || state.State != RoomEncounterState.Locked)
                {
                    pendingDoors.RemoveAt(i);
                    continue;
                }
                if (WouldOverlapTrackedActor(door)) continue;
                door.Close();
                pendingDoors.RemoveAt(i);
            }
        }

        private void HandleRoomStateChanged(DungeonRoomStateChange change)
        {
            if (!hasStates || !states.TryGetValue(change.RoomId,
                out DungeonRoomEncounterRuntimeState encounter)) return;
            if (change.RoomId == roleAssigner.StartRoomId)
            {
                if (pendingActivationRoomId == change.RoomId)
                    pendingActivationRoomId = DungeonRoomStateController.InvalidId;
                if (encounter.State != RoomEncounterState.Cleared)
                    SetEncounterState(encounter, RoomEncounterState.Cleared);
                return;
            }

            if (change.CurrentState == RoomExplorationState.Active
                && encounter.State == RoomEncounterState.Inactive)
            {
                pendingActivationRoomId = change.RoomId;
                ProcessPendingActivation();
                return;
            }

            if (pendingActivationRoomId == change.RoomId)
                pendingActivationRoomId = DungeonRoomStateController.InvalidId;
            if (encounter.State == RoomEncounterState.Locked
                && change.CurrentState != RoomExplorationState.Active)
                CancelLockedEncounter(encounter);
        }

        private bool CloseRoomDoorsSafely(int roomId)
        {
            if (!doorRegistry.TryGetDoorsForRoom(roomId,
                out IReadOnlyList<DungeonDoor> roomDoors)) return false;
            bool allClosed = true;
            for (int i = 0; i < roomDoors.Count; i++)
            {
                DungeonDoor door = roomDoors[i];
                if (door == null) continue;
                if (WouldOverlapTrackedActor(door))
                {
                    if (!pendingDoors.Contains(door)) pendingDoors.Add(door);
                    allClosed = false;
                }
                else door.Close();
            }
            return allClosed;
        }

        private bool WouldOverlapTrackedActor(DungeonDoor door)
        {
            if (trackedActorCollider == null || door == null) return false;
            return trackedActorCollider.bounds.Intersects(door.ColliderBounds);
        }

        private bool IsTrackedActorSafelyInsideRoom(int roomId)
        {
            if (areaTracker == null || areaTracker.CurrentAreaType != DungeonAreaType.Room
                || areaTracker.CurrentRoomId != roomId || trackedActorCollider == null) return false;
            RoomData room = FindRoom(roomId);
            Vector2Int cell = areaTracker.CurrentCell;
            if (room == null || cell.x <= room.Bounds.xMin || cell.x >= room.Bounds.xMax - 1
                || cell.y <= room.Bounds.yMin || cell.y >= room.Bounds.yMax - 1) return false;
            if (!doorRegistry.TryGetDoorsForRoom(roomId,
                out IReadOnlyList<DungeonDoor> roomDoors)) return false;
            for (int i = 0; i < roomDoors.Count; i++)
                if (WouldOverlapTrackedActor(roomDoors[i])) return false;
            return true;
        }

        private void CancelLockedEncounter(DungeonRoomEncounterRuntimeState state)
        {
            RemovePendingDoors(state.RoomId);
            doorRegistry.OpenRoomDoors(state.RoomId);
            SetEncounterState(state, RoomEncounterState.Inactive);
        }

        private RoomData FindRoom(int roomId)
        {
            if (generator == null || generator.Rooms == null) return null;
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            return null;
        }

        private void RemovePendingDoors(int roomId)
        {
            for (int i = pendingDoors.Count - 1; i >= 0; i--)
                if (pendingDoors[i] == null || pendingDoors[i].RoomId == roomId) pendingDoors.RemoveAt(i);
        }

        private void SetEncounterState(DungeonRoomEncounterRuntimeState state, RoomEncounterState next)
        {
            if (state.State == next) return;
            RoomEncounterState previous = state.State;
            state.SetState(next);
            EncounterStateChanged?.Invoke(new DungeonRoomEncounterChange(state.RoomId, previous, next));
        }

        private void CacheComponents()
        {
            if (generator == null) generator = GetComponent<DungeonGenerator>();
            if (roleAssigner == null) roleAssigner = GetComponent<DungeonRoomRoleAssigner>();
            if (roomStateController == null) roomStateController = GetComponent<DungeonRoomStateController>();
            if (doorRegistry == null) doorRegistry = GetComponent<DungeonDoorRegistry>();
            if (areaTracker == null) areaTracker = GetComponent<DungeonAreaTracker>();
        }

        private void Subscribe()
        {
            if (roomStateController == null || subscribed) return;
            roomStateController.RoomStateChanged += HandleRoomStateChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (roomStateController != null && subscribed)
                roomStateController.RoomStateChanged -= HandleRoomStateChanged;
            subscribed = false;
        }

    }
}
