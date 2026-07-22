using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonDoorwayBuilderTests
    {
        private GameObject dungeonObject;
        private GameObject gridObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private Tile floorTile;
        private Tile wallTile;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private DungeonGenerator generator;
        private DungeonGraphBuilder graph;
        private DungeonCorridorBuilder corridors;
        private DungeonDoorwayBuilder doorways;
        private DungeonTilemapRenderer renderer;
        private DungeonRoomRoleAssigner roles;
        private DungeonRuntimeController runtime;
        private TopDownPlayerController playerController;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Doorway Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            graph = dungeonObject.AddComponent<DungeonGraphBuilder>();
            corridors = dungeonObject.AddComponent<DungeonCorridorBuilder>();
            doorways = dungeonObject.AddComponent<DungeonDoorwayBuilder>();
            renderer = dungeonObject.AddComponent<DungeonTilemapRenderer>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();

            gridObject = new GameObject("Doorway Test Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            floorTilemap = CreateTilemap("Floor", gridObject.transform);
            wallTilemap = CreateTilemap("Wall", gridObject.transform);
            floorTile = ScriptableObject.CreateInstance<Tile>();
            floorTile.name = "Doorway Test Floor";
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.name = "Doorway Test Wall";
            SetField(renderer, "floorTilemap", floorTilemap);
            SetField(renderer, "wallTilemap", wallTilemap);
            SetField(renderer, "floorTile", floorTile);
            SetField(renderer, "wallTile", wallTile);

            playerObject = new GameObject("Doorway Test Player");
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerController = playerObject.AddComponent<TopDownPlayerController>();

            DungeonPlayerSpawner spawner = dungeonObject.AddComponent<DungeonPlayerSpawner>();
            SetField(spawner, "prototypePlayer", playerObject.transform);
            SetField(spawner, "floorTilemap", floorTilemap);

            cameraObject = new GameObject("Doorway Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            TopDownCameraFollow cameraFollow = cameraObject.AddComponent<TopDownCameraFollow>();
            SetField(cameraFollow, "target", playerObject.transform);
            SetField(cameraFollow, "floorTilemap", floorTilemap);

            DungeonAreaTracker areaTracker = dungeonObject.AddComponent<DungeonAreaTracker>();
            SetField(areaTracker, "grid", grid);
            SetField(areaTracker, "player", playerObject.transform);

            runtime = dungeonObject.AddComponent<DungeonRuntimeController>();
            SetField(runtime, "playerController", playerController);
            SetField(runtime, "cameraFollow", cameraFollow);
        }

        [TearDown]
        public void TearDown()
        {
            if (dungeonObject != null) UnityEngine.Object.DestroyImmediate(dungeonObject);
            if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
            if (playerObject != null) UnityEngine.Object.DestroyImmediate(playerObject);
            if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            if (floorTile != null) UnityEngine.Object.DestroyImmediate(floorTile);
            if (wallTile != null) UnityEngine.Object.DestroyImmediate(wallTile);
        }

        [Test]
        public void BuildDoorways_CreatesTwoValidRecordsPerConnection()
        {
            Build(12345, false);

            Assert.That(doorways.IsDoorwayDataCurrent, Is.True);
            Assert.That(doorways.Doorways.Count, Is.EqualTo(graph.Connections.Count * 2));
            var roomsById = RoomsById();
            var unique = new HashSet<long>();
            var perConnection = new int[graph.Connections.Count];

            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                DoorwayData doorway = doorways.Doorways[i];
                Assert.That(roomsById.ContainsKey(doorway.RoomId), Is.True);
                Assert.That(doorway.ConnectionIndex, Is.InRange(0, graph.Connections.Count - 1));
                RoomConnectionData connection = graph.Connections[doorway.ConnectionIndex];
                Assert.That(doorway.RoomId == connection.RoomAId || doorway.RoomId == connection.RoomBId, Is.True);
                Assert.That(IsBoundary(roomsById[doorway.RoomId].Bounds, doorway.EntranceCell), Is.True);
                Assert.That(Enum.IsDefined(typeof(DoorwayDirection), doorway.OutwardDirection), Is.True);
                Assert.That(doorway.FirstCorridorCell,
                    Is.EqualTo(doorway.EntranceCell + DungeonDoorwayBuilder.DirectionOffset(doorway.OutwardDirection)));
                Assert.That(roomsById[doorway.RoomId].Bounds.Contains(doorway.FirstCorridorCell), Is.False);
                Assert.That(CorridorContainsStep(connection, doorway.EntranceCell, doorway.FirstCorridorCell), Is.True);
                Assert.That(unique.Add(((long)doorway.ConnectionIndex << 32) ^ (uint)doorway.RoomId), Is.True);
                perConnection[doorway.ConnectionIndex]++;
            }

            for (int i = 0; i < perConnection.Length; i++) Assert.That(perConnection[i], Is.EqualTo(2));
        }

        [Test]
        public void RenderedDoorways_AreFloorAndNotWallCells()
        {
            Build(12345, true);

            Assert.That(renderer.IsTilemapCurrent, Is.True);
            for (int i = 0; i < doorways.Doorways.Count; i++)
            {
                Vector2Int cell = doorways.Doorways[i].EntranceCell;
                var position = new Vector3Int(cell.x, cell.y, 0);
                Assert.That(floorTilemap.HasTile(position), Is.True, $"Doorway {i} is missing from Floor Tilemap.");
                Assert.That(wallTilemap.HasTile(position), Is.False, $"Doorway {i} is blocked by Wall Tilemap.");
            }
        }

        [Test]
        public void DoorwaySignature_IsStableAcrossRepeatedAndRoundTripSeeds()
        {
            Build(12345, false);
            long first12345 = doorways.DoorwaySignature;
            Build(12345, false);
            long repeated12345 = doorways.DoorwaySignature;
            Build(54321, false);
            long seed54321 = doorways.DoorwaySignature;
            Build(12345, false);
            long second12345 = doorways.DoorwaySignature;

            Assert.That(repeated12345, Is.EqualTo(first12345));
            Assert.That(second12345, Is.EqualTo(first12345));
            Assert.That(seed54321, Is.Not.EqualTo(first12345));
        }

        [Test]
        public void RuntimeRegenerate_RebuildsWithoutDuplicateDoorways()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int count = doorways.Doorways.Count;
            long signature = doorways.DoorwaySignature;

            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);
            Assert.That(doorways.IsDoorwayDataCurrent, Is.True);
            Assert.That(doorways.Doorways.Count, Is.EqualTo(count));
            Assert.That(doorways.DoorwaySignature, Is.EqualTo(signature));
            Assert.That(count, Is.EqualTo(graph.Connections.Count * 2));
        }

        [Test]
        public void RuntimeClear_RemovesAllDoorwayData()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            Assert.That(doorways.Doorways, Is.Not.Empty);

            runtime.ClearRuntimeDungeon();

            Assert.That(doorways.Doorways, Is.Empty);
            Assert.That(doorways.IsDoorwayDataCurrent, Is.False);
            Assert.That(doorways.DoorwaySignature, Is.Zero);
        }

        [Test]
        public void RuntimeFailureCleanup_RemovesDoorwaysAndKeepsPlayerDisabled()
        {
            SetField(renderer, "wallTile", null);
            LogAssert.Expect(LogType.Warning, "[DungeonTilemapRenderer] Render rejected: Tilemap or Tile references are invalid.");
            LogAssert.Expect(LogType.Error, "[DungeonRuntimeController] Dungeon generation failed during Tilemap rendering.");

            Assert.That(runtime.GenerateDungeon(12345), Is.False);
            Assert.That(runtime.LastStatusMessage, Is.EqualTo("Dungeon generation failed during Tilemap rendering."));
            Assert.That(doorways.Doorways, Is.Empty);
            Assert.That(doorways.IsDoorwayDataCurrent, Is.False);
            Assert.That(playerController.enabled, Is.False);
        }

        [TestCase(-54321)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void SupportedIntegerSeeds_BuildValidDoorways(int seed)
        {
            Build(seed, false);
            Assert.That(doorways.IsDoorwayDataCurrent, Is.True);
            Assert.That(doorways.Doorways.Count, Is.EqualTo(graph.Connections.Count * 2));
        }

        private void Build(int seed, bool render)
        {
            generator.SetSeed(seed);
            generator.Generate();
            graph.BuildGraph();
            corridors.BuildCorridors();
            doorways.BuildDoorways();
            if (render) renderer.RenderDungeonTilemap();
        }

        private Dictionary<int, RoomData> RoomsById()
        {
            var result = new Dictionary<int, RoomData>();
            for (int i = 0; i < generator.Rooms.Count; i++) result.Add(generator.Rooms[i].RoomId, generator.Rooms[i]);
            return result;
        }

        private bool CorridorContainsStep(RoomConnectionData connection, Vector2Int a, Vector2Int b)
        {
            for (int i = 0; i < corridors.Corridors.Count; i++)
            {
                CorridorData corridor = corridors.Corridors[i];
                if (corridor.RoomAId != connection.RoomAId || corridor.RoomBId != connection.RoomBId) continue;
                for (int p = 1; p < corridor.PathCells.Count; p++)
                    if ((corridor.PathCells[p - 1] == a && corridor.PathCells[p] == b)
                        || (corridor.PathCells[p - 1] == b && corridor.PathCells[p] == a)) return true;
            }
            return false;
        }

        private static bool IsBoundary(RectInt bounds, Vector2Int cell)
        {
            return bounds.Contains(cell) && (cell.x == bounds.xMin || cell.x == bounds.xMax - 1
                || cell.y == bounds.yMin || cell.y == bounds.yMax - 1);
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
