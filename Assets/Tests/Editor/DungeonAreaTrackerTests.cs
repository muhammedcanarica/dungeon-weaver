using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonAreaTrackerTests
    {
        private GameObject dungeonObject;
        private GameObject gridObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private Tile floorTile;
        private Tile wallTile;
        private Grid grid;
        private DungeonGenerator generator;
        private DungeonGraphBuilder graph;
        private DungeonCorridorBuilder corridors;
        private DungeonDoorwayBuilder doorways;
        private DungeonTilemapRenderer renderer;
        private DungeonRoomRoleAssigner roles;
        private DungeonAreaTracker tracker;
        private DungeonRuntimeController runtime;
        private TopDownPlayerController playerController;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Area Tracker Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            graph = dungeonObject.AddComponent<DungeonGraphBuilder>();
            corridors = dungeonObject.AddComponent<DungeonCorridorBuilder>();
            doorways = dungeonObject.AddComponent<DungeonDoorwayBuilder>();
            renderer = dungeonObject.AddComponent<DungeonTilemapRenderer>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();

            gridObject = new GameObject("Area Tracker Test Grid");
            grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            Tilemap floorTilemap = CreateTilemap("Floor", gridObject.transform);
            Tilemap wallTilemap = CreateTilemap("Wall", gridObject.transform);
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.name = "Area Tracker Test Floor";
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.name = "Area Tracker Test Wall";
            SetField(renderer, "floorTilemap", floorTilemap);
            SetField(renderer, "wallTilemap", wallTilemap);
            SetField(renderer, "floorTile", floorTile);
            SetField(renderer, "wallTile", wallTile);

            playerObject = new GameObject("Area Tracker Test Player");
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerController = playerObject.AddComponent<TopDownPlayerController>();

            DungeonPlayerSpawner spawner = dungeonObject.AddComponent<DungeonPlayerSpawner>();
            SetField(spawner, "prototypePlayer", playerObject.transform);
            SetField(spawner, "floorTilemap", floorTilemap);

            cameraObject = new GameObject("Area Tracker Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            TopDownCameraFollow cameraFollow = cameraObject.AddComponent<TopDownCameraFollow>();
            SetField(cameraFollow, "target", playerObject.transform);
            SetField(cameraFollow, "floorTilemap", floorTilemap);

            tracker = dungeonObject.AddComponent<DungeonAreaTracker>();
            SetField(tracker, "grid", grid);
            SetField(tracker, "player", playerObject.transform);

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
        public void RuntimeGeneration_BuildsLookupAndInitializesStartRoomWithOneEntryEvent()
        {
            int entered = 0;
            int exited = 0;
            int changed = 0;
            tracker.RoomEntered += _ => entered++;
            tracker.RoomExited += _ => exited++;
            tracker.AreaChanged += _ => changed++;

            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(tracker.HasLookup, Is.True);
            Assert.That(tracker.IsLookupCurrent, Is.True);
            Assert.That(tracker.RoomLookupCellCount, Is.GreaterThan(0));
            Assert.That(tracker.CorridorOnlyCellCount, Is.GreaterThan(0));
            Assert.That(tracker.TotalLookupCellCount,
                Is.EqualTo(tracker.RoomLookupCellCount + tracker.CorridorOnlyCellCount));
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(roles.StartRoomId));
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(exited, Is.Zero);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(dungeonObject.GetComponents<DungeonAreaTracker>().Length, Is.EqualTo(1));
        }

        [Test]
        public void MovingWithinRoomAndOntoEntranceCell_DoesNotRepeatEvents()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData doorway = FindDoorwayForRoom(roles.StartRoomId);
            int entered = 0;
            int exited = 0;
            int changed = 0;
            tracker.RoomEntered += _ => entered++;
            tracker.RoomExited += _ => exited++;
            tracker.AreaChanged += _ => changed++;

            RoomData start = FindRoom(roles.StartRoomId);
            MoveToCell(new Vector2Int(start.Bounds.xMin, start.Bounds.yMin));
            MoveToCell(doorway.EntranceCell);

            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(roles.StartRoomId));
            Assert.That(entered, Is.Zero);
            Assert.That(exited, Is.Zero);
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void RoomToCorridor_UsesTheExactExitDoorwayAndConnection()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData doorway = FindDoorwayForRoom(roles.StartRoomId);
            MoveToCell(doorway.EntranceCell);
            int entered = 0;
            int exited = 0;
            int changed = 0;
            DungeonAreaTransition last = default;
            tracker.RoomEntered += value => { entered++; last = value; };
            tracker.RoomExited += value => { exited++; last = value; };
            tracker.AreaChanged += value => { changed++; last = value; };

            MoveToCell(doorway.FirstCorridorCell);

            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Corridor));
            Assert.That(tracker.CurrentConnectionIndex, Is.EqualTo(doorway.ConnectionIndex));
            Assert.That(tracker.LastTransitionConnectionIndex, Is.EqualTo(doorway.ConnectionIndex));
            Assert.That(tracker.LastUsedDoorway, Is.SameAs(doorway));
            Assert.That(last.PreviousRoomId, Is.EqualTo(doorway.RoomId));
            Assert.That(last.Doorway, Is.SameAs(doorway));
            Assert.That(entered, Is.Zero);
            Assert.That(exited, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void CorridorToConnectedRoom_UsesTheExactEntryDoorwayAndConnection()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData exit = FindDoorwayForRoom(roles.StartRoomId);
            DoorwayData entry = FindOtherDoorway(exit);
            MoveToCell(exit.EntranceCell);
            MoveToCell(exit.FirstCorridorCell);
            MoveToCell(entry.FirstCorridorCell);
            int entered = 0;
            int exited = 0;
            int changed = 0;
            DungeonAreaTransition last = default;
            tracker.RoomEntered += value => { entered++; last = value; };
            tracker.RoomExited += _ => exited++;
            tracker.AreaChanged += value => { changed++; last = value; };

            MoveToCell(entry.EntranceCell);

            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(entry.RoomId));
            Assert.That(tracker.LastTransitionConnectionIndex, Is.EqualTo(entry.ConnectionIndex));
            Assert.That(tracker.LastUsedDoorway, Is.SameAs(entry));
            Assert.That(last.CurrentRoomId, Is.EqualTo(entry.RoomId));
            Assert.That(last.ConnectionIndex, Is.EqualTo(entry.ConnectionIndex));
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(exited, Is.Zero);
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void CorridorPathRoomCellsHavePriorityAndOutsideCellIsOutsideDungeon()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            CorridorData corridor = corridors.Corridors[0];

            MoveToCell(corridor.PathCells[0]);
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));
            MoveToCell(corridor.PathCells[corridor.PathCells.Count - 1]);
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));

            MoveToCell(new Vector2Int(generator.GenerationArea.xMin - 10, generator.GenerationArea.yMin - 10));
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.OutsideDungeon));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(DungeonAreaTracker.InvalidId));
            Assert.That(tracker.CurrentCorridorIndex, Is.EqualTo(DungeonAreaTracker.InvalidId));
            Assert.That(tracker.CurrentConnectionIndex, Is.EqualTo(DungeonAreaTracker.InvalidId));
        }

        [Test]
        public void TeleportBetweenRooms_UpdatesStateAndEventsWithoutInventingDoorway()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            RoomData first = generator.Rooms[0];
            RoomData second = generator.Rooms[1];
            MoveToCell(RoomCenter(first));
            int entered = 0;
            int exited = 0;
            int changed = 0;
            DungeonAreaTransition last = default;
            tracker.RoomEntered += value => { entered++; last = value; };
            tracker.RoomExited += _ => exited++;
            tracker.AreaChanged += value => { changed++; last = value; };

            MoveToCell(RoomCenter(second));

            Assert.That(tracker.CurrentRoomId, Is.EqualTo(second.RoomId));
            Assert.That(tracker.PreviousRoomId, Is.EqualTo(first.RoomId));
            Assert.That(tracker.LastUsedDoorway, Is.Null);
            Assert.That(last.Doorway, Is.Null);
            Assert.That(last.ConnectionIndex, Is.EqualTo(DungeonAreaTracker.InvalidId));
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(exited, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void MovingInsideSameCorridor_DoesNotRaiseAreaChanged()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindTwoCorridorOnlyCells(out Vector2Int first, out Vector2Int second);
            MoveToCell(first);
            int changed = 0;
            tracker.AreaChanged += _ => changed++;

            MoveToCell(second);

            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Corridor));
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void LookupSignature_IsStableAcrossRepeatedRoundTripAndRegenerate()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long first12345 = tracker.LookupSignature;
            int firstTotal = tracker.TotalLookupCellCount;
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long repeated12345 = tracker.LookupSignature;
            Assert.That(runtime.GenerateDungeon(54321), Is.True);
            long seed54321 = tracker.LookupSignature;
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long second12345 = tracker.LookupSignature;
            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);

            Assert.That(repeated12345, Is.EqualTo(first12345));
            Assert.That(second12345, Is.EqualTo(first12345));
            Assert.That(seed54321, Is.Not.EqualTo(first12345));
            Assert.That(tracker.LookupSignature, Is.EqualTo(first12345));
            Assert.That(tracker.TotalLookupCellCount, Is.EqualTo(firstTotal));
            Assert.That(tracker.IsLookupCurrent, Is.True);
        }

        [Test]
        public void ClearAndFailureCleanup_ResetAllTrackerState()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            runtime.ClearRuntimeDungeon();
            AssertCleared();

            SetField(renderer, "wallTile", null);
            LogAssert.Expect(LogType.Warning, "[DungeonTilemapRenderer] Render rejected: Tilemap or Tile references are invalid.");
            LogAssert.Expect(LogType.Error, "[DungeonRuntimeController] Dungeon generation failed during Tilemap rendering.");
            Assert.That(runtime.GenerateDungeon(12345), Is.False);
            AssertCleared();
            Assert.That(playerController.enabled, Is.False);
        }

        [TestCase(-54321)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SupportedIntegerSeeds_InitializeTrackerAtStartRoom(int seed)
        {
            Assert.That(runtime.GenerateDungeon(seed), Is.True);
            Assert.That(tracker.IsLookupCurrent, Is.True);
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.Room));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(roles.StartRoomId));
        }

        [Test]
        public void EnableDisableCycle_DoesNotDuplicateEventCallbacks()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            DoorwayData doorway = FindDoorwayForRoom(roles.StartRoomId);
            MoveToCell(doorway.EntranceCell);
            int exited = 0;
            int changed = 0;
            tracker.RoomExited += _ => exited++;
            tracker.AreaChanged += _ => changed++;

            tracker.enabled = false;
            tracker.enabled = true;
            MoveToCell(doorway.FirstCorridorCell);

            Assert.That(exited, Is.EqualTo(1));
            Assert.That(changed, Is.EqualTo(1));
        }

        private void AssertCleared()
        {
            Assert.That(tracker.HasLookup, Is.False);
            Assert.That(tracker.IsLookupCurrent, Is.False);
            Assert.That(tracker.LookupSignature, Is.Zero);
            Assert.That(tracker.RoomLookupCellCount, Is.Zero);
            Assert.That(tracker.CorridorLookupCellCount, Is.Zero);
            Assert.That(tracker.TotalLookupCellCount, Is.Zero);
            Assert.That(tracker.CurrentAreaType, Is.EqualTo(DungeonAreaType.OutsideDungeon));
            Assert.That(tracker.CurrentRoomId, Is.EqualTo(DungeonAreaTracker.InvalidId));
            Assert.That(tracker.CurrentCorridorIndex, Is.EqualTo(DungeonAreaTracker.InvalidId));
            Assert.That(tracker.CurrentConnectionIndex, Is.EqualTo(DungeonAreaTracker.InvalidId));
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

        private void FindTwoCorridorOnlyCells(out Vector2Int first, out Vector2Int second)
        {
            var roomCells = new HashSet<Vector2Int>();
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RectInt bounds = generator.Rooms[i].Bounds;
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    for (int x = bounds.xMin; x < bounds.xMax; x++) roomCells.Add(new Vector2Int(x, y));
            }

            for (int i = 0; i < corridors.Corridors.Count; i++)
            {
                first = default;
                second = default;
                bool foundFirst = false;
                for (int p = 0; p < corridors.Corridors[i].PathCells.Count; p++)
                {
                    Vector2Int cell = corridors.Corridors[i].PathCells[p];
                    if (roomCells.Contains(cell)) continue;
                    if (!foundFirst) { first = cell; foundFirst = true; }
                    else { second = cell; return; }
                }
            }

            Assert.Fail("No corridor has two outside-room cells.");
            first = default;
            second = default;
        }

        private void MoveToCell(Vector2Int cell)
        {
            Vector3 world = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            playerObject.transform.position = new Vector3(world.x, world.y, playerObject.transform.position.z);
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
            tracker.RefreshPlayerArea();
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
