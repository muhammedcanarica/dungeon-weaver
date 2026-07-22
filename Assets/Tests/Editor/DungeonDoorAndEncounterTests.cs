using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonDoorAndEncounterTests
    {
        private GameObject dungeonObject;
        private GameObject gridObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private GameObject eventSystemObject;
        private GameObject canvasObject;
        private GameObject inputObject;
        private Tile floorTile;
        private Tile wallTile;
        private Grid grid;
        private DungeonGenerator generator;
        private DungeonDoorwayBuilder doorways;
        private DungeonTilemapRenderer renderer;
        private DungeonRoomRoleAssigner roles;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomStateController roomStates;
        private DungeonDoorRegistry doorRegistry;
        private DungeonRoomEncounterController encounters;
        private DungeonRuntimeController runtime;
        private TopDownPlayerController playerController;
        private Collider2D playerCollider;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Door Encounter Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            dungeonObject.AddComponent<DungeonGraphBuilder>();
            dungeonObject.AddComponent<DungeonCorridorBuilder>();
            doorways = dungeonObject.AddComponent<DungeonDoorwayBuilder>();
            renderer = dungeonObject.AddComponent<DungeonTilemapRenderer>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();

            gridObject = new GameObject("Door Encounter Test Grid");
            grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            Tilemap floorTilemap = CreateTilemap("Floor", gridObject.transform);
            Tilemap wallTilemap = CreateTilemap("Wall", gridObject.transform);
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.name = "Door Encounter Test Floor";
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.name = "Door Encounter Test Wall";
            SetField(renderer, "floorTilemap", floorTilemap);
            SetField(renderer, "wallTilemap", wallTilemap);
            SetField(renderer, "floorTile", floorTile);
            SetField(renderer, "wallTile", wallTile);

            playerObject = new GameObject("Door Encounter Test Player");
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerCollider = playerObject.AddComponent<CircleCollider2D>();
            ((CircleCollider2D)playerCollider).radius = 0.38f;
            playerController = playerObject.AddComponent<TopDownPlayerController>();

            DungeonPlayerSpawner spawner = dungeonObject.AddComponent<DungeonPlayerSpawner>();
            SetField(spawner, "prototypePlayer", playerObject.transform);
            SetField(spawner, "floorTilemap", floorTilemap);

            cameraObject = new GameObject("Door Encounter Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            TopDownCameraFollow cameraFollow = cameraObject.AddComponent<TopDownCameraFollow>();
            SetField(cameraFollow, "target", playerObject.transform);
            SetField(cameraFollow, "floorTilemap", floorTilemap);

            areaTracker = dungeonObject.AddComponent<DungeonAreaTracker>();
            SetField(areaTracker, "grid", grid);
            SetField(areaTracker, "player", playerObject.transform);
            roomStates = dungeonObject.AddComponent<DungeonRoomStateController>();
            doorRegistry = dungeonObject.AddComponent<DungeonDoorRegistry>();
            SetField(doorRegistry, "grid", grid);
            encounters = dungeonObject.AddComponent<DungeonRoomEncounterController>();
            SetField(encounters, "playerCollider", playerCollider);

            runtime = dungeonObject.AddComponent<DungeonRuntimeController>();
            SetField(runtime, "playerController", playerController);
            SetField(runtime, "cameraFollow", cameraFollow);
        }

        [TearDown]
        public void TearDown()
        {
            if (eventSystemObject != null) Object.DestroyImmediate(eventSystemObject);
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            if (inputObject != null) Object.DestroyImmediate(inputObject);
            if (dungeonObject != null) Object.DestroyImmediate(dungeonObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (floorTile != null) Object.DestroyImmediate(floorTile);
            if (wallTile != null) Object.DestroyImmediate(wallTile);
        }

        [Test]
        public void Generation_CoalescesSharedEntrancesAndMapsEveryDoorwayRecord()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int expectedPhysicalCount = UniquePhysicalDoorCount();

            Assert.That(doorways.Doorways.Count, Is.EqualTo(32));
            Assert.That(expectedPhysicalCount, Is.EqualTo(29));
            Assert.That(doorRegistry.DoorCount, Is.EqualTo(expectedPhysicalCount));
            Assert.That(doorRegistry.DoorwayAssociationCount, Is.EqualTo(doorways.Doorways.Count));
            Assert.That(dungeonObject.GetComponents<DungeonDoorRegistry>().Length, Is.EqualTo(1));
            Assert.That(dungeonObject.GetComponents<DungeonRoomEncounterController>().Length, Is.EqualTo(1));

            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                DoorwayData doorway = doorways.Doorways[i];
                Assert.That(doorRegistry.TryGetDoor(doorway.RoomId, doorway.ConnectionIndex,
                    out DungeonDoor door), Is.True);
                Assert.That(door.RoomId, Is.EqualTo(doorway.RoomId));
                Assert.That(door.RepresentsConnection(doorway.ConnectionIndex), Is.True);
                Assert.That(door.EntranceCell, Is.EqualTo(doorway.EntranceCell));
                Assert.That(door.OutwardDirection, Is.EqualTo(doorway.OutwardDirection));
                Vector3 expected = grid.GetCellCenterWorld(new Vector3Int(
                    doorway.EntranceCell.x, doorway.EntranceCell.y, 0));
                Assert.That(door.transform.position, Is.EqualTo(expected));
            }
        }

        [Test]
        public void DoorOrientationColliderAndInitialVisualState_MatchDirectionAndGridSize()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(doorRegistry.OpenDoorCount, Is.EqualTo(doorRegistry.DoorCount));
            Assert.That(doorRegistry.ClosedDoorCount, Is.Zero);
            for (int i = 0; i < doorRegistry.Doors.Count; i++)
            {
                DungeonDoor door = doorRegistry.Doors[i];
                bool horizontal = door.OutwardDirection == DoorwayDirection.Up
                    || door.OutwardDirection == DoorwayDirection.Down;
                Assert.That(door.Orientation, Is.EqualTo(horizontal
                    ? DungeonDoorOrientation.Horizontal : DungeonDoorOrientation.Vertical));
                Assert.That(Mathf.DeltaAngle(door.transform.eulerAngles.z, horizontal ? 0f : 90f),
                    Is.EqualTo(0f).Within(0.01f));
                Assert.That(door.WorldSize.x, horizontal ? Is.GreaterThan(door.WorldSize.y)
                    : Is.LessThan(door.WorldSize.y));
                Assert.That(door.DoorCollider.isTrigger, Is.False);
                Assert.That(door.DoorCollider.enabled, Is.False);
                Assert.That(door.DoorRenderer.enabled, Is.True);
                Assert.That(door.DoorRenderer.color.a, Is.LessThan(1f));
            }
        }

        [Test]
        public void EncounterInitialization_ClearsStartAndLeavesOtherRoomsInactive()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(encounters.TotalRoomCount, Is.EqualTo(generator.Rooms.Count));
            Assert.That(encounters.ActiveLockedRoomId, Is.EqualTo(DungeonRoomStateController.InvalidId));
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                DungeonRoomEncounterRuntimeState state = Encounter(generator.Rooms[i].RoomId);
                Assert.That(state.State, Is.EqualTo(generator.Rooms[i].RoomId == roles.StartRoomId
                    ? RoomEncounterState.Cleared : RoomEncounterState.Inactive));
            }
        }

        [Test]
        public void EnteringNewRoom_LocksOnlyItsDoorsAndDefersOverlappingEntranceDoor()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);

            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            Assert.That(encounters.ActiveLockedRoomId, Is.EqualTo(entry.RoomId));
            Assert.That(encounters.PendingDoorCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor entranceDoor), Is.True);
            Assert.That(entranceDoor.IsOpen, Is.True);
            Assert.That(entranceDoor.DoorCollider.enabled, Is.False);

            Assert.That(doorRegistry.TryGetDoorsForRoom(entry.RoomId,
                out IReadOnlyList<DungeonDoor> lockedRoomDoors), Is.True);
            int alreadyClosed = 0;
            for (int i = 0; i < lockedRoomDoors.Count; i++) if (!lockedRoomDoors[i].IsOpen) alreadyClosed++;
            Assert.That(alreadyClosed, Is.EqualTo(lockedRoomDoors.Count - encounters.PendingDoorCount));
            Assert.That(doorRegistry.TryGetDoorsForRoom(roles.StartRoomId,
                out IReadOnlyList<DungeonDoor> startDoors), Is.True);
            for (int i = 0; i < startDoors.Count; i++) Assert.That(startDoors[i].IsOpen, Is.True);
        }

        [Test]
        public void PendingDoor_ClosesAfterPlayerMovesSafelyInside()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            Assert.That(encounters.PendingDoorCount, Is.GreaterThan(0));

            MoveToCell(RoomCenter(FindRoom(entry.RoomId)));
            encounters.ProcessPendingClosures();

            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(doorRegistry.TryGetDoorsForRoom(entry.RoomId,
                out IReadOnlyList<DungeonDoor> roomDoors), Is.True);
            for (int i = 0; i < roomDoors.Count; i++)
            {
                Assert.That(roomDoors[i].IsOpen, Is.False);
                Assert.That(roomDoors[i].DoorCollider.enabled, Is.True);
                Assert.That(roomDoors[i].DoorRenderer.color.a, Is.EqualTo(1f));
            }
        }

        [Test]
        public void PendingEntranceDoor_DoesNotCloseWithPlayerBackInCorridor()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor entranceDoor), Is.True);

            MoveToCell(entry.FirstCorridorCell);
            encounters.ProcessPendingClosures();
            Assert.That(entranceDoor.IsOpen, Is.True);
            Assert.That(encounters.PendingDoorCount, Is.GreaterThan(0));

            MoveToCell(entry.EntranceCell);
            MoveToCell(RoomCenter(FindRoom(entry.RoomId)));
            encounters.ProcessPendingClosures();
            Assert.That(entranceDoor.IsOpen, Is.False);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
        }

        [Test]
        public void ClosedDoorBlocksColliderCastAndClearedDoorDoesNot()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            MoveToCell(entry.EntranceCell - DungeonDoorwayBuilder.DirectionOffset(entry.OutwardDirection));
            encounters.ProcessPendingClosures();
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor door), Is.True);

            Assert.That(PlayerCastHitsDoor(door, entry.OutwardDirection), Is.True);
            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);
            Physics2D.SyncTransforms();
            Assert.That(PlayerCastHitsDoor(door, entry.OutwardDirection), Is.False);
            Assert.That(door.DoorCollider.enabled, Is.False);
        }

        [Test]
        public void DebugClear_OpensDoorsAndClearedRoomNeverRelocks()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData startDoorway, out DoorwayData otherDoorway);
            MoveThroughToEntrance(startDoorway, otherDoorway);
            MoveToCell(RoomCenter(FindRoom(otherDoorway.RoomId)));
            encounters.ProcessPendingClosures();
            int visitOrder = RoomState(otherDoorway.RoomId).FirstVisitOrder;

            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);
            Assert.That(Encounter(otherDoorway.RoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            AssertRoomDoorsOpen(otherDoorway.RoomId);
            MoveThroughToEntrance(otherDoorway, startDoorway);
            MoveThroughToEntrance(startDoorway, otherDoorway);

            Assert.That(Encounter(otherDoorway.RoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            Assert.That(RoomState(otherDoorway.RoomId).VisitCount, Is.EqualTo(2));
            Assert.That(RoomState(otherDoorway.RoomId).FirstVisitOrder, Is.EqualTo(visitOrder));
            AssertRoomDoorsOpen(otherDoorway.RoomId);
        }

        [Test]
        public void SeedInputFocus_BlocksDebugClear()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystemObject = new GameObject("Door Test EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            canvasObject = new GameObject("Door Test Canvas", typeof(Canvas));
            inputObject = new GameObject("Door Test Input", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            inputObject.SetActive(false);
            inputObject.transform.SetParent(canvasObject.transform, false);
            InputField inputField = inputObject.AddComponent<InputField>();
            var textObject = new GameObject("Text", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(inputObject.transform, false);
            inputField.textComponent = textObject.GetComponent<Text>();
            inputObject.SetActive(true);
            eventSystem.SetSelectedGameObject(inputObject);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(inputObject));

            Assert.That(encounters.TryDebugClearActiveRoomForSelection(inputObject), Is.False);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
        }

        [Test]
        public void TeleportIntoUnvisitedRoom_LocksImmediatelyWithoutPendingDoor()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            RoomData target = FirstRoomExcept(roles.StartRoomId);

            MoveToCell(RoomCenter(target));
            encounters.ProcessPendingClosures();

            Assert.That(Encounter(target.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            Assert.That(encounters.ActiveLockedRoomId, Is.EqualTo(target.RoomId));
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(RoomState(target.RoomId).ExplorationState, Is.EqualTo(RoomExplorationState.Active));
            Assert.That(RoomState(target.RoomId).VisitCount, Is.EqualTo(1));
            AssertRoomDoorsClosed(target.RoomId);
        }

        [Test]
        public void Regenerate_RemovesOldDoorsAndResetsEncounterHistory()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long signature = doorRegistry.LayoutSignature;
            int count = doorRegistry.DoorCount;
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);

            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);

            Assert.That(doorRegistry.DoorCount, Is.EqualTo(count));
            Assert.That(doorRegistry.LayoutSignature, Is.EqualTo(signature));
            Assert.That(doorRegistry.OpenDoorCount, Is.EqualTo(count));
            Assert.That(doorRegistry.ClosedDoorCount, Is.Zero);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(Encounter(roles.StartRoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId != roles.StartRoomId)
                    Assert.That(Encounter(generator.Rooms[i].RoomId).State,
                        Is.EqualTo(RoomEncounterState.Inactive));
        }

        [Test]
        public void ClearAndFailureCleanup_RemoveDoorsAndEncounterStateWithoutEvents()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int events = 0;
            encounters.EncounterStateChanged += _ => events++;
            runtime.ClearRuntimeDungeon();
            AssertCleared();
            Assert.That(events, Is.Zero);

            SetField(renderer, "wallTile", null);
            LogAssert.Expect(LogType.Warning,
                "[DungeonTilemapRenderer] Render rejected: Tilemap or Tile references are invalid.");
            LogAssert.Expect(LogType.Error,
                "[DungeonRuntimeController] Dungeon generation failed during Tilemap rendering.");
            Assert.That(runtime.GenerateDungeon(54321), Is.False);
            AssertCleared();
            Assert.That(events, Is.Zero);
            Assert.That(playerController.enabled, Is.False);
        }

        [Test]
        public void DoorLayoutSignature_IsStableAcrossRoundTrip()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long first12345 = doorRegistry.LayoutSignature;
            Assert.That(runtime.GenerateDungeon(54321), Is.True);
            long seed54321 = doorRegistry.LayoutSignature;
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long second12345 = doorRegistry.LayoutSignature;

            Assert.That(seed54321, Is.Not.EqualTo(first12345));
            Assert.That(second12345, Is.EqualTo(first12345));
        }

        [TestCase(-54321)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SupportedIntegerSeeds_BuildOpenPhysicalDoors(int seed)
        {
            Assert.That(runtime.GenerateDungeon(seed), Is.True);
            Assert.That(doorRegistry.DoorCount, Is.GreaterThan(0));
            Assert.That(doorRegistry.DoorwayAssociationCount, Is.EqualTo(doorways.Doorways.Count));
            Assert.That(doorRegistry.OpenDoorCount, Is.EqualTo(doorRegistry.DoorCount));
            Assert.That(encounters.TotalRoomCount, Is.EqualTo(generator.Rooms.Count));
        }

        [Test]
        public void EnableDisableCycle_DoesNotDuplicateEncounterCallbacks()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            int lockedEvents = 0;
            encounters.EncounterStateChanged += value =>
            {
                if (value.RoomId == entry.RoomId && value.CurrentState == RoomEncounterState.Locked)
                    lockedEvents++;
            };
            encounters.enabled = false;
            encounters.enabled = true;
            encounters.enabled = false;
            encounters.enabled = true;

            MoveThroughToEntrance(exit, entry);

            Assert.That(lockedEvents, Is.EqualTo(1));
        }

        private int UniquePhysicalDoorCount()
        {
            var keys = new HashSet<string>();
            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                DoorwayData doorway = doorways.Doorways[i];
                keys.Add($"{doorway.RoomId}:{doorway.EntranceCell.x}:{doorway.EntranceCell.y}:{doorway.OutwardDirection}");
            }
            return keys.Count;
        }

        private void AssertCleared()
        {
            Assert.That(doorRegistry.DoorCount, Is.Zero);
            Assert.That(doorRegistry.OpenDoorCount, Is.Zero);
            Assert.That(doorRegistry.ClosedDoorCount, Is.Zero);
            Assert.That(doorRegistry.LayoutSignature, Is.Zero);
            Assert.That(doorRegistry.HasBuiltDoors, Is.False);
            Assert.That(encounters.TotalRoomCount, Is.Zero);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(encounters.HasEncounterStates, Is.False);
            Assert.That(encounters.ActiveLockedRoomId,
                Is.EqualTo(DungeonRoomStateController.InvalidId));
        }

        private void AssertRoomDoorsOpen(int roomId)
        {
            Assert.That(doorRegistry.TryGetDoorsForRoom(roomId,
                out IReadOnlyList<DungeonDoor> roomDoors), Is.True);
            for (int i = 0; i < roomDoors.Count; i++)
            {
                Assert.That(roomDoors[i].IsOpen, Is.True);
                Assert.That(roomDoors[i].DoorCollider.enabled, Is.False);
            }
        }

        private void AssertRoomDoorsClosed(int roomId)
        {
            Assert.That(doorRegistry.TryGetDoorsForRoom(roomId,
                out IReadOnlyList<DungeonDoor> roomDoors), Is.True);
            for (int i = 0; i < roomDoors.Count; i++)
            {
                Assert.That(roomDoors[i].IsOpen, Is.False);
                Assert.That(roomDoors[i].DoorCollider.enabled, Is.True);
            }
        }

        private bool PlayerCastHitsDoor(DungeonDoor door, DoorwayDirection direction)
        {
            Physics2D.SyncTransforms();
            var hits = new RaycastHit2D[16];
            Vector2 castDirection = DungeonDoorwayBuilder.DirectionOffset(direction);
            int count = playerCollider.Cast(castDirection, hits, 2f);
            for (int i = 0; i < count; i++) if (hits[i].collider == door.DoorCollider) return true;
            return false;
        }

        private DungeonRoomRuntimeState RoomState(int roomId)
        {
            Assert.That(roomStates.TryGetRoomState(roomId, out DungeonRoomRuntimeState state), Is.True);
            return state;
        }

        private DungeonRoomEncounterRuntimeState Encounter(int roomId)
        {
            Assert.That(encounters.TryGetRoomState(roomId,
                out DungeonRoomEncounterRuntimeState state), Is.True);
            return state;
        }

        private void FindPassageFromStart(out DoorwayData exit, out DoorwayData entry)
        {
            exit = null;
            entry = null;
            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                DoorwayData candidate = doorways.Doorways[i];
                if (candidate.RoomId != roles.StartRoomId) continue;
                for (int j = 0; j < doorways.Doorways.Count; j++)
                {
                    DoorwayData other = doorways.Doorways[j];
                    if (other.ConnectionIndex != candidate.ConnectionIndex
                        || other.RoomId == candidate.RoomId || other.RoomId == roles.BossRoomId) continue;
                    exit = candidate;
                    entry = other;
                    return;
                }
            }
            Assert.Fail("No Start-to-Normal room passage was found.");
        }

        private void MoveThroughToEntrance(DoorwayData from, DoorwayData to)
        {
            MoveToCell(from.EntranceCell);
            MoveToCell(from.FirstCorridorCell);
            MoveToCell(to.FirstCorridorCell);
            MoveToCell(to.EntranceCell);
        }

        private void MoveToCell(Vector2Int cell)
        {
            Vector3 world = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            playerObject.transform.position = new Vector3(world.x, world.y, playerObject.transform.position.z);
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            areaTracker.RefreshPlayerArea();
        }

        private RoomData FindRoom(int roomId)
        {
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            Assert.Fail($"Room {roomId} was not found.");
            return null;
        }

        private RoomData FirstRoomExcept(int roomId)
        {
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId != roomId) return generator.Rooms[i];
            Assert.Fail("No second room was generated.");
            return null;
        }

        private static Vector2Int RoomCenter(RoomData room)
        {
            return new Vector2Int(room.Bounds.xMin + (room.Bounds.width - 1) / 2,
                room.Bounds.yMin + (room.Bounds.height - 1) / 2);
        }

        private static Tilemap CreateTilemap(string name, Transform parent)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
            return tilemap;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
