using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonRoomStateControllerTests
    {
        private GameObject dungeonObject;
        private GameObject gridObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private Tile floorTile;
        private Tile wallTile;
        private Grid grid;
        private DungeonGenerator generator;
        private DungeonCorridorBuilder corridors;
        private DungeonDoorwayBuilder doorways;
        private DungeonTilemapRenderer renderer;
        private DungeonRoomRoleAssigner roles;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomStateController roomStates;
        private DungeonRuntimeController runtime;
        private TopDownPlayerController playerController;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Room State Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            dungeonObject.AddComponent<DungeonGraphBuilder>();
            corridors = dungeonObject.AddComponent<DungeonCorridorBuilder>();
            doorways = dungeonObject.AddComponent<DungeonDoorwayBuilder>();
            renderer = dungeonObject.AddComponent<DungeonTilemapRenderer>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();

            gridObject = new GameObject("Room State Test Grid");
            grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            Tilemap floorTilemap = CreateTilemap("Floor", gridObject.transform);
            Tilemap wallTilemap = CreateTilemap("Wall", gridObject.transform);
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.name = "Room State Test Floor";
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.name = "Room State Test Wall";
            SetField(renderer, "floorTilemap", floorTilemap);
            SetField(renderer, "wallTilemap", wallTilemap);
            SetField(renderer, "floorTile", floorTile);
            SetField(renderer, "wallTile", wallTile);

            playerObject = new GameObject("Room State Test Player");
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerController = playerObject.AddComponent<TopDownPlayerController>();

            DungeonPlayerSpawner spawner = dungeonObject.AddComponent<DungeonPlayerSpawner>();
            SetField(spawner, "prototypePlayer", playerObject.transform);
            SetField(spawner, "floorTilemap", floorTilemap);

            cameraObject = new GameObject("Room State Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            TopDownCameraFollow cameraFollow = cameraObject.AddComponent<TopDownCameraFollow>();
            SetField(cameraFollow, "target", playerObject.transform);
            SetField(cameraFollow, "floorTilemap", floorTilemap);

            areaTracker = dungeonObject.AddComponent<DungeonAreaTracker>();
            SetField(areaTracker, "grid", grid);
            SetField(areaTracker, "player", playerObject.transform);
            roomStates = dungeonObject.AddComponent<DungeonRoomStateController>();

            runtime = dungeonObject.AddComponent<DungeonRuntimeController>();
            SetField(runtime, "playerController", playerController);
            SetField(runtime, "cameraFollow", cameraFollow);
        }

        [TearDown]
        public void TearDown()
        {
            if (dungeonObject != null) Object.DestroyImmediate(dungeonObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (floorTile != null) Object.DestroyImmediate(floorTile);
            if (wallTile != null) Object.DestroyImmediate(wallTile);
        }

        [Test]
        public void Generation_CreatesOneStatePerRoomAndActivatesOnlyStartRoom()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(roomStates.HasRuntimeStates, Is.True);
            Assert.That(roomStates.TotalRoomCount, Is.EqualTo(generator.Rooms.Count));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(1));
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(roles.StartRoomId));
            Assert.That(roomStates.HasInitialStateSignature, Is.True);
            Assert.That(dungeonObject.GetComponents<DungeonRoomStateController>().Length, Is.EqualTo(1));

            var roomIds = new HashSet<int>();
            int active = 0;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                int roomId = generator.Rooms[i].RoomId;
                Assert.That(roomIds.Add(roomId), Is.True);
                Assert.That(roomStates.TryGetRoomState(roomId, out DungeonRoomRuntimeState state), Is.True);
                Assert.That(state.RoomId, Is.EqualTo(roomId));
                if (roomId == roles.StartRoomId)
                {
                    Assert.That(state.RoomRole, Is.EqualTo(DungeonRoomRole.Start));
                    Assert.That(state.ExplorationState, Is.EqualTo(RoomExplorationState.Active));
                    Assert.That(state.VisitCount, Is.EqualTo(1));
                    Assert.That(state.FirstVisitOrder, Is.EqualTo(1));
                    Assert.That(state.LastEnteredConnectionIndex, Is.EqualTo(DungeonRoomStateController.InvalidId));
                    active++;
                }
                else
                {
                    Assert.That(state.ExplorationState, Is.EqualTo(RoomExplorationState.Unvisited));
                    Assert.That(state.VisitCount, Is.Zero);
                    Assert.That(state.FirstVisitOrder, Is.EqualTo(DungeonRoomStateController.InvalidId));
                }
            }

            Assert.That(active, Is.EqualTo(1));
            Assert.That(roomStates.TryGetRoomState(roles.BossRoomId, out DungeonRoomRuntimeState boss), Is.True);
            Assert.That(boss.RoomRole, Is.EqualTo(DungeonRoomRole.Boss));
        }

        [Test]
        public void InitialAreaRefresh_RaisesOneDiscoveryStateAndActiveEvent()
        {
            int discovered = 0;
            int changed = 0;
            int active = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.RoomStateChanged += _ => changed++;
            roomStates.ActiveRoomChanged += _ => active++;

            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(discovered, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(active, Is.EqualTo(1));
        }

        [Test]
        public void MovingInsideSameRoom_DoesNotChangeStateOrRaiseEvents()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DungeonRoomRuntimeState start = State(roles.StartRoomId);
            int order = start.FirstVisitOrder;
            int discovered = 0;
            int changed = 0;
            int active = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.RoomStateChanged += _ => changed++;
            roomStates.ActiveRoomChanged += _ => active++;

            MoveToCell(RoomCenter(FindRoom(roles.StartRoomId)));

            Assert.That(start.ExplorationState, Is.EqualTo(RoomExplorationState.Active));
            Assert.That(start.VisitCount, Is.EqualTo(1));
            Assert.That(start.FirstVisitOrder, Is.EqualTo(order));
            Assert.That(discovered, Is.Zero);
            Assert.That(changed, Is.Zero);
            Assert.That(active, Is.Zero);
        }

        [Test]
        public void RoomToCorridor_MarksVisitedAndRecordsExactExitConnection()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DungeonRoomRuntimeState start = State(roles.StartRoomId);
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            MoveToCell(exit.EntranceCell);
            int discovered = 0;
            DungeonActiveRoomChange activeChange = default;
            int activeEvents = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.ActiveRoomChanged += value => { activeEvents++; activeChange = value; };

            MoveToCell(exit.FirstCorridorCell);

            Assert.That(start.ExplorationState, Is.EqualTo(RoomExplorationState.Visited));
            Assert.That(start.VisitCount, Is.EqualTo(1));
            Assert.That(start.LastExitedConnectionIndex, Is.EqualTo(exit.ConnectionIndex));
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(1));
            Assert.That(discovered, Is.Zero);
            Assert.That(activeEvents, Is.EqualTo(1));
            Assert.That(activeChange.CurrentRoomId, Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(activeChange.ConnectionIndex, Is.EqualTo(exit.ConnectionIndex));
        }

        [Test]
        public void CorridorToUnvisitedRoom_DiscoversAndRecordsEntryConnection()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            DoorwayData entry = FindOtherDoorway(exit);
            MoveToCell(exit.EntranceCell);
            MoveToCell(exit.FirstCorridorCell);
            MoveToCell(entry.FirstCorridorCell);
            int discovered = 0;
            DungeonRoomStateChange discovery = default;
            roomStates.RoomDiscovered += value => { discovered++; discovery = value; };

            MoveToCell(entry.EntranceCell);

            DungeonRoomRuntimeState entered = State(entry.RoomId);
            Assert.That(entered.ExplorationState, Is.EqualTo(RoomExplorationState.Active));
            Assert.That(entered.VisitCount, Is.EqualTo(1));
            Assert.That(entered.FirstVisitOrder, Is.EqualTo(2));
            Assert.That(entered.LastEnteredConnectionIndex, Is.EqualTo(entry.ConnectionIndex));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(2));
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(entry.RoomId));
            Assert.That(discovered, Is.EqualTo(1));
            Assert.That(discovery.IsFirstDiscovery, Is.True);
            Assert.That(discovery.ConnectionIndex, Is.EqualTo(entry.ConnectionIndex));
            AssertOnlyActive(entry.RoomId);
        }

        [Test]
        public void Revisit_IncrementsVisitCountWithoutChangingOrderOrRediscovering()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData startDoorway = FindDoorwayForRoom(roles.StartRoomId);
            DoorwayData otherDoorway = FindOtherDoorway(startDoorway);
            MoveThroughConnection(startDoorway, otherDoorway);
            DungeonRoomRuntimeState other = State(otherDoorway.RoomId);
            int firstOrder = other.FirstVisitOrder;
            MoveThroughConnection(otherDoorway, startDoorway);
            int discovered = 0;
            int stateChanges = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.RoomStateChanged += value =>
            {
                if (value.RoomId == otherDoorway.RoomId) stateChanges++;
            };

            MoveThroughConnection(startDoorway, otherDoorway);

            Assert.That(other.ExplorationState, Is.EqualTo(RoomExplorationState.Active));
            Assert.That(other.VisitCount, Is.EqualTo(2));
            Assert.That(other.FirstVisitOrder, Is.EqualTo(firstOrder));
            Assert.That(other.LastEnteredConnectionIndex, Is.EqualTo(otherDoorway.ConnectionIndex));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(2));
            Assert.That(discovered, Is.Zero);
            Assert.That(stateChanges, Is.EqualTo(1));
        }

        [Test]
        public void RoomToRoomTeleport_UsesDeterministicEventOrderWithoutFakeConnection()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int oldRoomId = roles.StartRoomId;
            RoomData destination = FirstRoomExcept(oldRoomId);
            var order = new List<string>();
            roomStates.RoomDiscovered += value => order.Add($"discover:{value.RoomId}");
            roomStates.RoomStateChanged += value => order.Add($"state:{value.RoomId}:{value.CurrentState}");
            roomStates.ActiveRoomChanged += value => order.Add($"active:{value.PreviousRoomId}>{value.CurrentRoomId}");

            MoveToCell(RoomCenter(destination));

            Assert.That(order, Is.EqualTo(new[]
            {
                $"state:{oldRoomId}:Visited",
                $"discover:{destination.RoomId}",
                $"state:{destination.RoomId}:Active",
                $"active:{oldRoomId}>{destination.RoomId}"
            }));
            Assert.That(State(oldRoomId).LastExitedConnectionIndex,
                Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(State(destination.RoomId).LastEnteredConnectionIndex,
                Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(State(destination.RoomId).VisitCount, Is.EqualTo(1));
            AssertOnlyActive(destination.RoomId);
        }

        [Test]
        public void OutsideTransitions_ChangeOnlyAnActuallyActiveRoom()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int startId = roles.StartRoomId;
            Vector2Int outside = OutsideCell();

            MoveToCell(outside);
            Assert.That(State(startId).ExplorationState, Is.EqualTo(RoomExplorationState.Visited));
            Assert.That(State(startId).LastExitedConnectionIndex,
                Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(DungeonRoomStateController.InvalidId));

            runtime.GenerateDungeon(12345);
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            MoveToCell(exit.EntranceCell);
            MoveToCell(exit.FirstCorridorCell);
            DungeonRoomRuntimeState start = State(roles.StartRoomId);
            int visitCount = start.VisitCount;
            int exitConnection = start.LastExitedConnectionIndex;
            int stateEvents = 0;
            roomStates.RoomStateChanged += _ => stateEvents++;

            MoveToCell(outside);

            Assert.That(start.ExplorationState, Is.EqualTo(RoomExplorationState.Visited));
            Assert.That(start.VisitCount, Is.EqualTo(visitCount));
            Assert.That(start.LastExitedConnectionIndex, Is.EqualTo(exitConnection));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(1));
            Assert.That(stateEvents, Is.Zero);
        }

        [Test]
        public void EnableDisableCycle_DoesNotDuplicateCallbacks()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            MoveToCell(exit.EntranceCell);
            int stateEvents = 0;
            int activeEvents = 0;
            roomStates.RoomStateChanged += _ => stateEvents++;
            roomStates.ActiveRoomChanged += _ => activeEvents++;

            roomStates.enabled = false;
            roomStates.enabled = true;
            roomStates.enabled = false;
            roomStates.enabled = true;
            MoveToCell(exit.FirstCorridorCell);

            Assert.That(stateEvents, Is.EqualTo(1));
            Assert.That(activeEvents, Is.EqualTo(1));
        }

        [Test]
        public void InitialSignature_IsStableAcrossRoundTripAndRegenerateResetsHistory()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long first12345 = roomStates.InitialStateSignature;
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            DoorwayData entry = FindOtherDoorway(exit);
            MoveThroughConnection(exit, entry);
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(2));

            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);
            Assert.That(roomStates.InitialStateSignature, Is.EqualTo(first12345));
            AssertFreshInitialState();
            Assert.That(runtime.GenerateDungeon(54321), Is.True);
            long seed54321 = roomStates.InitialStateSignature;
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long second12345 = roomStates.InitialStateSignature;

            Assert.That(seed54321, Is.Not.EqualTo(first12345));
            Assert.That(second12345, Is.EqualTo(first12345));
            AssertFreshInitialState();
        }

        [Test]
        public void Clear_ResetsAllStateWithoutEvents()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int discovered = 0;
            int changed = 0;
            int active = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.RoomStateChanged += _ => changed++;
            roomStates.ActiveRoomChanged += _ => active++;

            runtime.ClearRuntimeDungeon();

            AssertCleared();
            Assert.That(discovered, Is.Zero);
            Assert.That(changed, Is.Zero);
            Assert.That(active, Is.Zero);
        }

        [Test]
        public void FailureCleanup_RemovesOldExplorationWithoutFakeEventsAndDisablesPlayer()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            MoveToCell(RoomCenter(FirstRoomExcept(roles.StartRoomId)));
            int discovered = 0;
            int changed = 0;
            int active = 0;
            roomStates.RoomDiscovered += _ => discovered++;
            roomStates.RoomStateChanged += _ => changed++;
            roomStates.ActiveRoomChanged += _ => active++;
            SetField(renderer, "wallTile", null);
            LogAssert.Expect(LogType.Warning,
                "[DungeonTilemapRenderer] Render rejected: Tilemap or Tile references are invalid.");
            LogAssert.Expect(LogType.Error,
                "[DungeonRuntimeController] Dungeon generation failed during Tilemap rendering.");

            Assert.That(runtime.GenerateDungeon(54321), Is.False);

            AssertCleared();
            Assert.That(discovered, Is.Zero);
            Assert.That(changed, Is.Zero);
            Assert.That(active, Is.Zero);
            Assert.That(playerController.enabled, Is.False);
        }

        [TestCase(-54321)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SupportedIntegerSeeds_InitializeFreshStartState(int seed)
        {
            Assert.That(runtime.GenerateDungeon(seed), Is.True);
            AssertFreshInitialState();
        }

        private void AssertFreshInitialState()
        {
            Assert.That(roomStates.TotalRoomCount, Is.EqualTo(generator.Rooms.Count));
            Assert.That(roomStates.DiscoveredRoomCount, Is.EqualTo(1));
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(roles.StartRoomId));
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                DungeonRoomRuntimeState state = State(generator.Rooms[i].RoomId);
                if (state.RoomId == roles.StartRoomId)
                {
                    Assert.That(state.ExplorationState, Is.EqualTo(RoomExplorationState.Active));
                    Assert.That(state.VisitCount, Is.EqualTo(1));
                    Assert.That(state.FirstVisitOrder, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(state.ExplorationState, Is.EqualTo(RoomExplorationState.Unvisited));
                    Assert.That(state.VisitCount, Is.Zero);
                    Assert.That(state.FirstVisitOrder, Is.EqualTo(DungeonRoomStateController.InvalidId));
                }
            }
        }

        private void AssertCleared()
        {
            Assert.That(roomStates.HasRuntimeStates, Is.False);
            Assert.That(roomStates.TotalRoomCount, Is.Zero);
            Assert.That(roomStates.DiscoveredRoomCount, Is.Zero);
            Assert.That(roomStates.CurrentActiveRoomId, Is.EqualTo(DungeonRoomStateController.InvalidId));
            Assert.That(roomStates.HasInitialStateSignature, Is.False);
            Assert.That(roomStates.InitialStateSignature, Is.Zero);
        }

        private void AssertOnlyActive(int roomId)
        {
            int active = 0;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                DungeonRoomRuntimeState state = State(generator.Rooms[i].RoomId);
                if (state.ExplorationState == RoomExplorationState.Active)
                {
                    active++;
                    Assert.That(state.RoomId, Is.EqualTo(roomId));
                }
            }
            Assert.That(active, Is.EqualTo(1));
        }

        private DungeonRoomRuntimeState State(int roomId)
        {
            Assert.That(roomStates.TryGetRoomState(roomId, out DungeonRoomRuntimeState state), Is.True);
            return state;
        }

        private void MoveThroughConnection(DoorwayData from, DoorwayData to)
        {
            MoveToCell(from.EntranceCell);
            MoveToCell(from.FirstCorridorCell);
            MoveToCell(to.FirstCorridorCell);
            MoveToCell(to.EntranceCell);
        }

        private DoorwayData FindDoorwayForRoom(int roomId)
        {
            for (int i = 0; i < doorways.Doorways.Count; i++)
                if (doorways.Doorways[i].RoomId == roomId) return doorways.Doorways[i];
            Assert.Fail($"Room {roomId} has no doorway.");
            return null;
        }

        private DoorwayData FindOtherDoorway(DoorwayData doorway)
        {
            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                DoorwayData candidate = doorways.Doorways[i];
                if (candidate.ConnectionIndex == doorway.ConnectionIndex && candidate.RoomId != doorway.RoomId)
                    return candidate;
            }
            Assert.Fail($"Connection {doorway.ConnectionIndex} has no second doorway.");
            return null;
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

        private Vector2Int OutsideCell()
        {
            return new Vector2Int(generator.GenerationArea.xMin - 10, generator.GenerationArea.yMin - 10);
        }

        private void MoveToCell(Vector2Int cell)
        {
            Vector3 world = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            playerObject.transform.position = new Vector3(world.x, world.y, playerObject.transform.position.z);
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
            areaTracker.RefreshPlayerArea();
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
