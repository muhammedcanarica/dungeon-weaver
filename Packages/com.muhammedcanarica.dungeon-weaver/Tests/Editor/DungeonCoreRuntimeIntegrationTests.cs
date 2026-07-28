using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonCoreRuntimeIntegrationTests
    {
        private GameObject dungeonObject;
        private GameObject gridObject;
        private GameObject actorObject;
        private GameObject cameraObject;
        private Tile floorTile;
        private Tile wallTile;
        private Grid grid;
        private DungeonRoomRoleAssigner roles;
        private DungeonDoorwayBuilder doorways;
        private DungeonAreaTracker areaTracker;
        private DungeonDoorRegistry doorRegistry;
        private DungeonRoomEncounterController encounters;
        private DungeonRuntimeController runtime;
        private Collider2D actorCollider;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Core Package Test Dungeon");
            dungeonObject.AddComponent<DungeonGenerator>();
            dungeonObject.AddComponent<DungeonGraphBuilder>();
            dungeonObject.AddComponent<DungeonCorridorBuilder>();
            doorways = dungeonObject.AddComponent<DungeonDoorwayBuilder>();
            DungeonTilemapRenderer renderer = dungeonObject.AddComponent<DungeonTilemapRenderer>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();

            gridObject = new GameObject("Core Package Test Grid");
            grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);
            Tilemap floorTilemap = CreateTilemap("Floor", gridObject.transform);
            Tilemap wallTilemap = CreateTilemap("Wall", gridObject.transform);
            floorTile = ScriptableObject.CreateInstance<Tile>();
            wallTile = ScriptableObject.CreateInstance<Tile>();
            SetField(renderer, "floorTilemap", floorTilemap);
            SetField(renderer, "wallTilemap", wallTilemap);
            SetField(renderer, "floorTile", floorTile);
            SetField(renderer, "wallTile", wallTile);

            actorObject = new GameObject("Generic Core Test Actor");
            actorObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            actorCollider = actorObject.AddComponent<CircleCollider2D>();

            DungeonPlayerSpawner spawner = dungeonObject.AddComponent<DungeonPlayerSpawner>();
            spawner.SetTrackedActor(actorObject.transform);
            SetField(spawner, "floorTilemap", floorTilemap);

            cameraObject = new GameObject("Core Package Test Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            TopDownCameraFollow cameraFollow = cameraObject.AddComponent<TopDownCameraFollow>();
            SetField(cameraFollow, "target", actorObject.transform);
            SetField(cameraFollow, "floorTilemap", floorTilemap);

            areaTracker = dungeonObject.AddComponent<DungeonAreaTracker>();
            SetField(areaTracker, "grid", grid);
            areaTracker.SetTrackedActor(actorObject.transform);
            dungeonObject.AddComponent<DungeonRoomStateController>();
            doorRegistry = dungeonObject.AddComponent<DungeonDoorRegistry>();
            SetField(doorRegistry, "grid", grid);
            encounters = dungeonObject.AddComponent<DungeonRoomEncounterController>();
            encounters.SetTrackedActor(actorObject.transform, actorCollider);

            runtime = dungeonObject.AddComponent<DungeonRuntimeController>();
            SetField(runtime, "cameraFollow", cameraFollow);
        }

        [TearDown]
        public void TearDown()
        {
            if (dungeonObject != null) Object.DestroyImmediate(dungeonObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (actorObject != null) Object.DestroyImmediate(actorObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (floorTile != null) Object.DestroyImmediate(floorTile);
            if (wallTile != null) Object.DestroyImmediate(wallTile);
        }

        [Test]
        public void GenericActor_CanGenerateLockClearOpenAndRegenerateWithoutSamples()
        {
            Assert.That(runtime.SetTrackedActor(actorObject.transform, actorCollider), Is.True);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            Assert.That(areaTracker.IsInsideRoom, Is.True);
            Assert.That(areaTracker.CurrentRoomId, Is.EqualTo(roles.StartRoomId));

            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveToCell(exit.EntranceCell);
            MoveToCell(exit.FirstCorridorCell);
            MoveToCell(entry.FirstCorridorCell);
            MoveToCell(entry.EntranceCell);
            Assert.That(encounters.HasPendingActivation, Is.True);

            MoveToCell(entry.EntranceCell
                - DungeonDoorwayBuilder.DirectionOffset(entry.OutwardDirection));
            Assert.That(encounters.ProcessPendingActivation(), Is.True);
            Assert.That(encounters.TryGetRoomState(entry.RoomId,
                out DungeonRoomEncounterRuntimeState locked), Is.True);
            Assert.That(locked.State, Is.EqualTo(RoomEncounterState.Locked));
            AssertRoomDoors(entry.RoomId, false);

            Assert.That(encounters.TryClearActiveRoom(), Is.True);
            AssertRoomDoors(entry.RoomId, true);
            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);
            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);
            Assert.That(areaTracker.TrackedActor, Is.SameAs(actorObject.transform));
            Assert.That(encounters.TrackedActorCollider, Is.SameAs(actorCollider));
        }

        [Test]
        public void MissingGenericActor_ReturnsExplicitValidationError()
        {
            encounters.SetTrackedActor(null, null);

            Assert.That(encounters.ValidateTrackedActor(out string error), Is.False);
            Assert.That(error, Does.Contain("Transform"));
            Assert.DoesNotThrow(() => encounters.ProcessPendingActivation());
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
                        || other.RoomId == candidate.RoomId) continue;
                    exit = candidate;
                    entry = other;
                    return;
                }
            }
            Assert.Fail("No passage from the Start room was found.");
        }

        private void MoveToCell(Vector2Int cell)
        {
            Vector3 world = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            actorObject.transform.position = new Vector3(world.x, world.y, actorObject.transform.position.z);
            Rigidbody2D body = actorObject.GetComponent<Rigidbody2D>();
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            areaTracker.RefreshTrackedActorArea();
        }

        private void AssertRoomDoors(int roomId, bool expectedOpen)
        {
            Assert.That(doorRegistry.TryGetDoorsForRoom(roomId,
                out IReadOnlyList<DungeonDoor> roomDoors), Is.True);
            Assert.That(roomDoors, Is.Not.Empty);
            for (int i = 0; i < roomDoors.Count; i++)
            {
                Assert.That(roomDoors[i].IsOpen, Is.EqualTo(expectedOpen));
                Assert.That(roomDoors[i].DoorCollider.enabled, Is.EqualTo(!expectedOpen));
            }
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
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
