using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonRoomRoleAssigner))]
    [RequireComponent(typeof(DungeonRoomStateController), typeof(DungeonDoorRegistry))]
    public sealed class DungeonRoomEncounterController : MonoBehaviour
    {
        [SerializeField] private Collider2D playerCollider;

        private readonly Dictionary<int, DungeonRoomEncounterRuntimeState> states =
            new Dictionary<int, DungeonRoomEncounterRuntimeState>();
        private readonly List<DungeonDoor> pendingDoors = new List<DungeonDoor>();
        private DungeonGenerator generator;
        private DungeonRoomRoleAssigner roleAssigner;
        private DungeonRoomStateController roomStateController;
        private DungeonDoorRegistry doorRegistry;
        private bool hasStates;
        private bool subscribed;

        public int TotalRoomCount => states.Count;
        public int PendingDoorCount => pendingDoors.Count;
        public bool HasEncounterStates => hasStates;
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) TryDebugClearActiveRoom();
            ProcessPendingClosures();
        }

        public bool InitializeStates()
        {
            ClearStates();
            CacheComponents();
            Subscribe();
            if (generator == null || roleAssigner == null || roomStateController == null
                || doorRegistry == null || !roleAssigner.IsRoleDataCurrent
                || !roomStateController.HasRuntimeStates || !doorRegistry.HasBuiltDoors
                || generator.Rooms == null || generator.Rooms.Count == 0)
            {
                Debug.LogError("[DungeonRoomEncounterController] Initialization rejected because source data is invalid.", this);
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
            hasStates = false;
        }

        public bool TryGetRoomState(int roomId, out DungeonRoomEncounterRuntimeState state)
        {
            return states.TryGetValue(roomId, out state);
        }

        public bool TryDebugClearActiveRoom()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            return TryDebugClearActiveRoomForSelection(selected);
        }

        public bool TryDebugClearActiveRoomForSelection(GameObject selectedObject)
        {
            if (selectedObject != null && selectedObject.GetComponentInParent<InputField>() != null) return false;
            int roomId = roomStateController != null
                ? roomStateController.CurrentActiveRoomId : DungeonRoomStateController.InvalidId;
            if (!states.TryGetValue(roomId, out DungeonRoomEncounterRuntimeState state)
                || state.State != RoomEncounterState.Locked) return false;
            SetEncounterState(state, RoomEncounterState.Cleared);
            RemovePendingDoors(roomId);
            doorRegistry.OpenRoomDoors(roomId);
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
                if (roomStateController.CurrentActiveRoomId != door.RoomId) continue;
                if (WouldOverlapPlayer(door)) continue;
                door.Close();
                pendingDoors.RemoveAt(i);
            }
        }

        private void HandleRoomStateChanged(DungeonRoomStateChange change)
        {
            if (!hasStates || change.CurrentState != RoomExplorationState.Active
                || !states.TryGetValue(change.RoomId, out DungeonRoomEncounterRuntimeState encounter)
                || encounter.State != RoomEncounterState.Inactive) return;
            if (change.RoomId == roleAssigner.StartRoomId)
            {
                SetEncounterState(encounter, RoomEncounterState.Cleared);
                return;
            }

            SetEncounterState(encounter, RoomEncounterState.Locked);
            CloseRoomDoorsSafely(change.RoomId);
        }

        private void CloseRoomDoorsSafely(int roomId)
        {
            if (!doorRegistry.TryGetDoorsForRoom(roomId, out IReadOnlyList<DungeonDoor> roomDoors)) return;
            for (int i = 0; i < roomDoors.Count; i++)
            {
                DungeonDoor door = roomDoors[i];
                if (WouldOverlapPlayer(door))
                {
                    if (!pendingDoors.Contains(door)) pendingDoors.Add(door);
                }
                else door.Close();
            }
        }

        private bool WouldOverlapPlayer(DungeonDoor door)
        {
            if (playerCollider == null || door == null) return false;
            Bounds playerBounds = playerCollider.bounds;
            var doorBounds = new Bounds(door.transform.position,
                new Vector3(door.WorldSize.x, door.WorldSize.y, 0.1f));
            return playerBounds.Intersects(doorBounds);
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
            if (playerCollider == null)
            {
                TopDownPlayerController player = FindFirstObjectByType<TopDownPlayerController>();
                if (player != null) playerCollider = player.GetComponent<Collider2D>();
            }
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
