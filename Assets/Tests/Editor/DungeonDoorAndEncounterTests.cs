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
        private DungeonEnemyEncounterRegistry enemyRegistry;
        private DungeonRuntimeController runtime;
        private TopDownPlayerController playerController;
        private TopDownPlayerAttack playerAttack;
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
            playerAttack = playerObject.AddComponent<TopDownPlayerAttack>();

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
            enemyRegistry = dungeonObject.AddComponent<DungeonEnemyEncounterRegistry>();

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
            Assert.That(expectedPhysicalCount, Is.EqualTo(31));
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
                Vector3 entranceWorld = grid.GetCellCenterWorld(new Vector3Int(
                    doorway.EntranceCell.x, doorway.EntranceCell.y, 0));
                Vector3 corridorWorld = grid.GetCellCenterWorld(new Vector3Int(
                    doorway.FirstCorridorCell.x, doorway.FirstCorridorCell.y, 0));
                Vector3 expected = (entranceWorld + corridorWorld) * 0.5f;
                Assert.That(door.transform.position, Is.EqualTo(expected));
            }
        }

        [Test]
        public void DoorOrientationColliderAndInitialVisualState_MatchDirectionAndGridSize()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(doorRegistry.OpenDoorCount, Is.EqualTo(doorRegistry.DoorCount));
            Assert.That(doorRegistry.ClosedDoorCount, Is.Zero);
            Assert.That(doorRegistry.CorridorWidth, Is.EqualTo(3));
            bool foundHorizontal = false;
            bool foundVertical = false;
            for (int i = 0; i < doorRegistry.Doors.Count; i++)
            {
                DungeonDoor door = doorRegistry.Doors[i];
                bool horizontal = door.OutwardDirection == DoorwayDirection.Up
                    || door.OutwardDirection == DoorwayDirection.Down;
                Assert.That(door.Orientation, Is.EqualTo(horizontal
                    ? DungeonDoorOrientation.Horizontal : DungeonDoorOrientation.Vertical));
                Assert.That(Mathf.DeltaAngle(door.transform.eulerAngles.z, 0f),
                    Is.EqualTo(0f).Within(0.01f));
                Assert.That(door.PassageSpan, Is.EqualTo(3f).Within(0.001f));
                Assert.That(horizontal ? door.WorldSize.x : door.WorldSize.y,
                    Is.EqualTo(3f).Within(0.001f));
                Assert.That(horizontal ? door.WorldSize.y : door.WorldSize.x,
                    Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(door.ColliderBounds.size.x,
                    Is.EqualTo(door.WorldSize.x).Within(0.001f));
                Assert.That(door.ColliderBounds.size.y,
                    Is.EqualTo(door.WorldSize.y).Within(0.001f));
                Assert.That(door.DoorCollider.isTrigger, Is.False);
                Assert.That(door.DoorCollider.enabled, Is.False);
                Assert.That(door.DoorRenderer.enabled, Is.True);
                Assert.That(door.DoorRenderer.color.a, Is.LessThan(1f));
                foundHorizontal |= horizontal;
                foundVertical |= !horizontal;
            }
            Assert.That(foundHorizontal, Is.True);
            Assert.That(foundVertical, Is.True);
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

            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            MoveThroughToEntrance(entry, exit);
            Assert.That(Encounter(roles.StartRoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            Assert.That(encounters.HasPendingActivation, Is.False);
            Assert.That(enemyRegistry.GetLiveEnemyCount(roles.StartRoomId), Is.Zero);
        }

        [Test]
        public void DoorwayEntry_RemainsPendingUntilPlayerIsSafelyInsideRoom()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);

            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Inactive));
            Assert.That(encounters.HasPendingActivation, Is.True);
            Assert.That(encounters.PendingActivationRoomId, Is.EqualTo(entry.RoomId));
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.Zero);
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor entranceDoor), Is.True);
            Assert.That(entranceDoor.IsOpen, Is.True);
            Assert.That(entranceDoor.DoorCollider.enabled, Is.False);

            MovePlayerToWorld(entranceDoor.transform.position);
            Assert.That(encounters.ProcessPendingActivation(), Is.False);
            Assert.That(encounters.HasPendingActivation, Is.True);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Inactive));

            MoveToCell(InteriorCell(entry));
            Assert.That(encounters.ProcessPendingActivation(), Is.True);

            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            Assert.That(encounters.HasPendingActivation, Is.False);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId),
                Is.EqualTo(enemyRegistry.GetPlannedEnemyCount(entry.RoomId)));
            AssertRoomDoorsClosed(entry.RoomId);
            AssertRoomDoorsOpen(roles.StartRoomId);
        }

        [Test]
        public void BackingOutBeforeInterior_CancelsPendingWithoutSpawningAndReentryLocksOnce()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            int lockedEvents = 0;
            encounters.EncounterStateChanged += change =>
            {
                if (change.RoomId == entry.RoomId
                    && change.CurrentState == RoomEncounterState.Locked) lockedEvents++;
            };
            MoveThroughToEntrance(exit, entry);

            MoveToCell(entry.FirstCorridorCell);
            Assert.That(encounters.HasPendingActivation, Is.False);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Inactive));
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.Zero);
            AssertRoomDoorsOpen(entry.RoomId);

            MoveToCell(entry.EntranceCell);
            Assert.That(encounters.HasPendingActivation, Is.True);
            MoveToCell(InteriorCell(entry));
            Assert.That(encounters.ProcessPendingActivation(), Is.True);
            Assert.That(encounters.ProcessPendingActivation(), Is.False);
            Assert.That(lockedEvents, Is.EqualTo(1));
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            AssertRoomDoorsClosed(entry.RoomId);
        }

        [Test]
        public void LeavingUnexpectedlyAfterLock_CancelsEncounterWithoutTeleportAndCanRelock()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Vector3 corridorWorld = grid.GetCellCenterWorld(new Vector3Int(
                entry.FirstCorridorCell.x, entry.FirstCorridorCell.y, 0));

            MoveToCell(entry.FirstCorridorCell);

            Assert.That(playerObject.transform.position.x, Is.EqualTo(corridorWorld.x).Within(0.001f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(corridorWorld.y).Within(0.001f));
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Inactive));
            Assert.That(encounters.HasPendingActivation, Is.False);
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.Zero);
            AssertRoomDoorsOpen(entry.RoomId);

            MoveToCell(entry.EntranceCell);
            Assert.That(encounters.HasPendingActivation, Is.True);
            MoveToCell(InteriorCell(entry));
            Assert.That(encounters.ProcessPendingActivation(), Is.True);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            AssertRoomDoorsClosed(entry.RoomId);
        }

        [Test]
        public void PendingDoorClosure_CompletesAfterOverlapEndsEvenIfActiveRoomIdChanges()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor door), Is.True);
            door.Open();
            MovePlayerToWorld(door.transform.position);

            Assert.That(InvokeCloseRoomDoorsSafely(entry.RoomId), Is.False);
            Assert.That(encounters.PendingDoorCount, Is.EqualTo(1));
            Assert.That(door.IsOpen, Is.True);

            SetField(roomStates, "currentActiveRoomId", DungeonRoomStateController.InvalidId);
            MovePlayerToWorld(grid.GetCellCenterWorld(new Vector3Int(
                InteriorCell(entry).x, InteriorCell(entry).y, 0)));
            encounters.ProcessPendingClosures();

            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.DoorCollider.enabled, Is.True);
        }

        [Test]
        public void ClosedDoorBlocksColliderCastAndClearedDoorDoesNot()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(doorRegistry.TryGetDoor(entry.RoomId, entry.ConnectionIndex,
                out DungeonDoor door), Is.True);

            AssertPassageBlockedAtCenterAndEdges(door, entry.OutwardDirection, true);
            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);
            Physics2D.SyncTransforms();
            AssertPassageBlockedAtCenterAndEdges(door, entry.OutwardDirection, false);
            Assert.That(door.DoorCollider.enabled, Is.False);
        }

        [Test]
        public void DebugClear_OpensDoorsAndClearedRoomNeverRelocks()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData startDoorway, out DoorwayData otherDoorway);
            MoveThroughToEntrance(startDoorway, otherDoorway);
            CompleteRoomEntry(otherDoorway);
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
            CompleteRoomEntry(entry);
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
        public void EnemyPlans_AreSafeDeterministicAndExcludeStartRoom()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(enemyRegistry.HasPlans, Is.True);
            Assert.That(enemyRegistry.PlannedRoomCount, Is.EqualTo(generator.Rooms.Count - 1));
            Assert.That(enemyRegistry.GetPlannedEnemyCount(roles.StartRoomId), Is.Zero);
            Assert.That(enemyRegistry.TryGetPlansForRoom(roles.StartRoomId, out _), Is.False);
            Assert.That(enemyRegistry.LiveEnemyCount, Is.Zero);
            Assert.That(enemyRegistry.PlanSignature, Is.Not.Zero);

            for (int roomIndex = 0; roomIndex < generator.Rooms.Count; roomIndex++)
            {
                RoomData room = generator.Rooms[roomIndex];
                if (room.RoomId == roles.StartRoomId) continue;
                Assert.That(enemyRegistry.TryGetPlansForRoom(room.RoomId,
                    out IReadOnlyList<DungeonEnemySpawnPlan> plans), Is.True);
                int maximumCount = room.RoomId == roles.BossRoomId ? 6 : 4;
                Assert.That(plans.Count, Is.InRange(0, maximumCount));
                Assert.That(plans.Count,
                    Is.LessThanOrEqualTo(CountSafeCells(room) / enemyRegistry.SafeCellsPerEnemy));
                var cells = new HashSet<Vector2Int>();
                for (int i = 0; i < plans.Count; i++)
                {
                    DungeonEnemySpawnPlan plan = plans[i];
                    Vector3Int cell = new Vector3Int(plan.SpawnCell.x, plan.SpawnCell.y, 0);
                    Assert.That(plan.RoomId, Is.EqualTo(room.RoomId));
                    Assert.That(plan.EnemyIndex, Is.EqualTo(i));
                    Assert.That(plan.MaxHealth, Is.EqualTo(3));
                    Assert.That(room.Bounds.Contains(plan.SpawnCell), Is.True);
                    Assert.That(renderer.FloorTilemap.HasTile(cell), Is.True);
                    Assert.That(renderer.WallTilemap.HasTile(cell), Is.False);
                    Assert.That(cells.Add(plan.SpawnCell), Is.True);
                    for (int doorwayIndex = 0; doorwayIndex < doorways.Doorways.Count; doorwayIndex++)
                    {
                        DoorwayData doorway = doorways.Doorways[doorwayIndex];
                        Assert.That(ManhattanDistance(plan.SpawnCell, doorway.EntranceCell),
                            Is.GreaterThanOrEqualTo(enemyRegistry.DoorwaySafeDistance));
                        Assert.That(ManhattanDistance(plan.SpawnCell, doorway.FirstCorridorCell),
                            Is.GreaterThanOrEqualTo(enemyRegistry.DoorwaySafeDistance));
                    }

                    for (int otherIndex = 0; otherIndex < i; otherIndex++)
                        Assert.That(ManhattanDistance(plan.SpawnCell, plans[otherIndex].SpawnCell),
                            Is.GreaterThanOrEqualTo(enemyRegistry.MinimumSpawnDistance));
                }
            }
        }

        [Test]
        public void RestrictedSafeAreaAndDensityCapacity_CanReduceBelowConfiguredMinimums()
        {
            SetField(enemyRegistry, "doorwaySafeDistance", 1000);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.Zero);
            Assert.That(enemyRegistry.PlannedRoomCount, Is.EqualTo(generator.Rooms.Count - 1));

            SetField(enemyRegistry, "doorwaySafeDistance", 3);
            SetField(enemyRegistry, "safeCellsPerEnemy", 10000);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.Zero);
        }

        [Test]
        public void Regenerate_RemovesLiveEnemiesAndDoesNotDuplicatePlans()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            int plannedCount = enemyRegistry.TotalPlannedEnemyCount;
            long signature = enemyRegistry.PlanSignature;
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(enemyRegistry.LiveEnemyCount, Is.GreaterThan(0));

            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);

            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.EqualTo(plannedCount));
            Assert.That(enemyRegistry.PlannedRoomCount, Is.EqualTo(generator.Rooms.Count - 1));
            Assert.That(enemyRegistry.PlanSignature, Is.EqualTo(signature));
            Assert.That(enemyRegistry.LiveEnemyCount, Is.Zero);
            Assert.That(dungeonObject.GetComponentsInChildren<DungeonEnemy>(true), Is.Empty);
            Assert.That(dungeonObject.GetComponents<DungeonEnemyEncounterRegistry>().Length, Is.EqualTo(1));
        }

        [Test]
        public void EnemyPlanSignature_IsStableAcrossRoundTripAndSupportedSeeds()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long first = enemyRegistry.PlanSignature;
            List<string> firstPlans = CaptureEnemyPlanLayout();
            Assert.That(runtime.GenerateDungeon(54321), Is.True);
            Assert.That(enemyRegistry.PlanSignature, Is.Not.EqualTo(first));
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            Assert.That(enemyRegistry.PlanSignature, Is.EqualTo(first));
            CollectionAssert.AreEqual(firstPlans, CaptureEnemyPlanLayout());

            Assert.That(runtime.GenerateDungeon(-54321), Is.True);
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.GreaterThan(0));
            Assert.That(runtime.GenerateDungeon(int.MinValue), Is.True);
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.GreaterThan(0));
            Assert.That(runtime.GenerateDungeon(int.MaxValue), Is.True);
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.GreaterThan(0));
        }

        [Test]
        public void LockedRoom_SpawnsOnceAndLeavingCancelsItsLiveEnemies()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);

            int planned = enemyRegistry.GetPlannedEnemyCount(entry.RoomId);
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.EqualTo(planned));
            Assert.That(enemyRegistry.ActivateRoom(entry.RoomId), Is.False);
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.EqualTo(planned));
            Assert.That(enemyRegistry.TryGetLiveEnemiesForRoom(entry.RoomId,
                out IReadOnlyList<DungeonEnemy> liveEnemies), Is.True);
            DungeonEnemy enemy = liveEnemies[0];
            Assert.That(enemy.GetComponent<Rigidbody2D>().bodyType,
                Is.EqualTo(RigidbodyType2D.Kinematic));
            Assert.That(enemy.GetComponent<CircleCollider2D>().isTrigger, Is.True);
            Assert.That(enemy.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(20));

            Vector2 before = enemy.GetComponent<Rigidbody2D>().position;
            MoveToCell(RoomCenter(FindRoom(entry.RoomId)));
            Assert.That(enemy.TryMoveTowardsPlayer(0.25f), Is.True);
            Vector2 after = enemy.GetComponent<Rigidbody2D>().position;
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(FindRoom(entry.RoomId).Bounds.Contains(Vector2Int.FloorToInt(after)), Is.True);

            MoveToCell(entry.FirstCorridorCell);
            Assert.That(enemy == null, Is.True);
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.Zero);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Inactive));
        }

        [Test]
        public void PlayerAttack_DamagesEachEnemyOnceAndHonorsCooldownAndInputFocus()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(enemyRegistry.TryGetLiveEnemiesForRoom(entry.RoomId,
                out IReadOnlyList<DungeonEnemy> liveEnemies), Is.True);
            DungeonEnemy enemy = liveEnemies[0];
            BoxCollider2D secondCollider = enemy.gameObject.AddComponent<BoxCollider2D>();
            secondCollider.isTrigger = true;
            playerObject.transform.position = enemy.transform.position;
            playerObject.GetComponent<Rigidbody2D>().position = enemy.transform.position;
            areaTracker.RefreshPlayerArea();
            Physics2D.SyncTransforms();

            int initialHealth = enemy.CurrentHealth;
            Assert.That(playerAttack.TryAttackAtTime(10f), Is.True);
            Assert.That(playerAttack.LastHitCount, Is.EqualTo(1));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(initialHealth - playerAttack.Damage));
            Assert.That(playerAttack.TryAttackAtTime(10.1f), Is.False);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(initialHealth - playerAttack.Damage));
            Assert.That(playerAttack.TryAttackAtTime(10f + playerAttack.Cooldown), Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(initialHealth - playerAttack.Damage * 2));

            EventSystem eventSystem = EnsureInputFieldSelected();
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(inputObject));
            Assert.That(DungeonRuntimeInputFocus.IsInputField(inputObject), Is.True);
            Assert.That(playerAttack.TryAttackAtTimeForSelection(20f, inputObject), Is.False);
        }

        [Test]
        public void LastEnemyDeath_ClearsRoomOpensDoorsAndLeavesNoLiveEnemy()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(enemyRegistry.TryGetLiveEnemiesForRoom(entry.RoomId,
                out IReadOnlyList<DungeonEnemy> liveEnemies), Is.True);
            var enemiesToKill = new List<DungeonEnemy>(liveEnemies);

            for (int i = 0; i < enemiesToKill.Count - 1; i++)
            {
                Assert.That(enemiesToKill[i].TakeDamage(enemiesToKill[i].MaxHealth), Is.True);
                Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
            }
            DungeonEnemy last = enemiesToKill[enemiesToKill.Count - 1];
            Assert.That(last.TakeDamage(last.MaxHealth), Is.True);

            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            Assert.That(enemyRegistry.GetLiveEnemyCount(entry.RoomId), Is.Zero);
            Assert.That(enemyRegistry.LiveEnemyCount, Is.Zero);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            AssertRoomDoorsOpen(entry.RoomId);
        }

        [Test]
        public void DebugClear_DespawnIsSilentAndClearedRoomDoesNotRespawnEnemies()
        {
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            FindPassageFromStart(out DoorwayData startDoorway, out DoorwayData otherDoorway);
            MoveThroughToEntrance(startDoorway, otherDoorway);
            CompleteRoomEntry(otherDoorway);
            Assert.That(enemyRegistry.GetLiveEnemyCount(otherDoorway.RoomId), Is.GreaterThan(0));
            int clearEvents = 0;
            encounters.EncounterStateChanged += change =>
            {
                if (change.RoomId == otherDoorway.RoomId
                    && change.CurrentState == RoomEncounterState.Cleared) clearEvents++;
            };

            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);
            Assert.That(clearEvents, Is.EqualTo(1));
            Assert.That(enemyRegistry.GetLiveEnemyCount(otherDoorway.RoomId), Is.Zero);
            Assert.That(encounters.TryClearRoom(otherDoorway.RoomId), Is.False);
            MoveThroughToEntrance(otherDoorway, startDoorway);
            MoveThroughToEntrance(startDoorway, otherDoorway);

            Assert.That(Encounter(otherDoorway.RoomId).State, Is.EqualTo(RoomEncounterState.Cleared));
            Assert.That(enemyRegistry.GetLiveEnemyCount(otherDoorway.RoomId), Is.Zero);
            Assert.That(clearEvents, Is.EqualTo(1));
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
            long enemySignature = enemyRegistry.PlanSignature;
            int count = doorRegistry.DoorCount;
            FindPassageFromStart(out DoorwayData exit, out DoorwayData entry);
            MoveThroughToEntrance(exit, entry);
            CompleteRoomEntry(entry);
            Assert.That(encounters.TryDebugClearActiveRoom(), Is.True);

            Assert.That(runtime.RegenerateCurrentSeed(), Is.True);

            Assert.That(doorRegistry.DoorCount, Is.EqualTo(count));
            Assert.That(doorRegistry.LayoutSignature, Is.EqualTo(signature));
            Assert.That(doorRegistry.OpenDoorCount, Is.EqualTo(count));
            Assert.That(doorRegistry.ClosedDoorCount, Is.Zero);
            Assert.That(enemyRegistry.PlanSignature, Is.EqualTo(enemySignature));
            Assert.That(enemyRegistry.LiveEnemyCount, Is.Zero);
            Assert.That(encounters.PendingDoorCount, Is.Zero);
            Assert.That(encounters.HasPendingActivation, Is.False);
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

        [Test]
        public void CorridorWidth_ControlsDoorSpanAndParticipatesInLayoutSignature()
        {
            SetField(renderer, "corridorWidth", 1);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long widthOneSignature = doorRegistry.LayoutSignature;
            AssertDoorSpans(1f);

            SetField(renderer, "corridorWidth", 5);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);
            long widthFiveSignature = doorRegistry.LayoutSignature;
            AssertDoorSpans(5f);

            SetField(renderer, "corridorWidth", 1);
            Assert.That(runtime.GenerateDungeon(12345), Is.True);

            Assert.That(widthFiveSignature, Is.Not.EqualTo(widthOneSignature));
            Assert.That(doorRegistry.LayoutSignature, Is.EqualTo(widthOneSignature));
            AssertDoorSpans(1f);
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
            CompleteRoomEntry(entry);

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
            Assert.That(enemyRegistry.TotalPlannedEnemyCount, Is.Zero);
            Assert.That(enemyRegistry.PlannedRoomCount, Is.Zero);
            Assert.That(enemyRegistry.LiveEnemyCount, Is.Zero);
            Assert.That(enemyRegistry.PlanSignature, Is.Zero);
            Assert.That(enemyRegistry.HasPlans, Is.False);
        }

        private EventSystem EnsureInputFieldSelected()
        {
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
            return eventSystem;
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

        private void AssertPassageBlockedAtCenterAndEdges(DungeonDoor door,
            DoorwayDirection direction, bool expectedBlocked)
        {
            Vector2 outward = DungeonDoorwayBuilder.DirectionOffset(direction);
            Vector2 across = new Vector2(-outward.y, outward.x);
            float edgeOffset = Mathf.Max(0f, door.PassageSpan * 0.5f - 0.05f);
            float[] offsets = { 0f, -edgeOffset, edgeOffset };
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 start = (Vector2)door.transform.position - outward * 0.75f
                    + across * offsets[i];
                MovePlayerToWorld(start);
                Assert.That(PlayerCastHitsDoor(door, direction), Is.EqualTo(expectedBlocked),
                    $"Door collision mismatch at passage offset {offsets[i]:0.00}.");
            }
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
                        || other.RoomId == candidate.RoomId || other.RoomId == roles.BossRoomId
                        || enemyRegistry.GetPlannedEnemyCount(other.RoomId) == 0) continue;
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

        private void CompleteRoomEntry(DoorwayData entry)
        {
            MoveToCell(InteriorCell(entry));
            Assert.That(encounters.ProcessPendingActivation(), Is.True);
            Assert.That(Encounter(entry.RoomId).State, Is.EqualTo(RoomEncounterState.Locked));
        }

        private void MovePlayerToWorld(Vector3 world)
        {
            playerObject.transform.position = new Vector3(
                world.x, world.y, playerObject.transform.position.z);
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.position = new Vector2(world.x, world.y);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
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

        private static Vector2Int InteriorCell(DoorwayData doorway)
        {
            return doorway.EntranceCell
                - DungeonDoorwayBuilder.DirectionOffset(doorway.OutwardDirection);
        }

        private bool InvokeCloseRoomDoorsSafely(int roomId)
        {
            MethodInfo method = typeof(DungeonRoomEncounterController).GetMethod(
                "CloseRoomDoorsSafely", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(encounters, new object[] { roomId });
        }

        private void AssertDoorSpans(float expectedSpan)
        {
            Assert.That(doorRegistry.CorridorWidth, Is.EqualTo(Mathf.RoundToInt(expectedSpan)));
            for (int i = 0; i < doorRegistry.Doors.Count; i++)
                Assert.That(doorRegistry.Doors[i].PassageSpan,
                    Is.EqualTo(expectedSpan).Within(0.001f));
        }

        private int CountSafeCells(RoomData room)
        {
            int count = 0;
            for (int y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                for (int x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var tileCell = new Vector3Int(x, y, 0);
                    if (!renderer.FloorTilemap.HasTile(tileCell)
                        || renderer.WallTilemap.HasTile(tileCell)) continue;

                    bool safe = true;
                    for (int i = 0; i < doorways.Doorways.Count; i++)
                    {
                        DoorwayData doorway = doorways.Doorways[i];
                        if (ManhattanDistance(cell, doorway.EntranceCell)
                                < enemyRegistry.DoorwaySafeDistance
                            || ManhattanDistance(cell, doorway.FirstCorridorCell)
                                < enemyRegistry.DoorwaySafeDistance)
                        {
                            safe = false;
                            break;
                        }
                    }

                    if (safe) count++;
                }
            }

            return count;
        }

        private List<string> CaptureEnemyPlanLayout()
        {
            var result = new List<string>();
            var rooms = new List<RoomData>(generator.Rooms);
            rooms.Sort((left, right) => left.RoomId.CompareTo(right.RoomId));
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                int roomId = rooms[roomIndex].RoomId;
                if (!enemyRegistry.TryGetPlansForRoom(roomId,
                    out IReadOnlyList<DungeonEnemySpawnPlan> plans)) continue;
                result.Add($"Room:{roomId}:Count:{plans.Count}");
                for (int planIndex = 0; planIndex < plans.Count; planIndex++)
                {
                    DungeonEnemySpawnPlan plan = plans[planIndex];
                    result.Add($"{plan.RoomId}:{plan.EnemyIndex}:{plan.SpawnCell.x}:{plan.SpawnCell.y}");
                }
            }

            return result;
        }

        private static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
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
