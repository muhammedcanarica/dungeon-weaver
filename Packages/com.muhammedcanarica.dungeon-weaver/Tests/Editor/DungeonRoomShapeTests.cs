using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonRoomShapeTests
    {
        private GameObject dungeonObject;
        private DungeonGenerator generator;
        private DungeonGraphBuilder graph;
        private DungeonRoomRoleAssigner roles;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Room Shape Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            graph = dungeonObject.AddComponent<DungeonGraphBuilder>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();
        }

        [TearDown]
        public void TearDown()
        {
            if (dungeonObject != null) Object.DestroyImmediate(dungeonObject);
        }

        [Test]
        public void SameSeed_ProducesSameShapeTypesAndOccupiedCells()
        {
            generator.SetSeed(12345);
            generator.Generate();
            Assert.That(generator.Rooms, Is.Not.Empty);
            List<long> first = CaptureGeometrySignatures();
            long firstLayout = generator.RoomLayoutSignature;

            generator.Generate();

            CollectionAssert.AreEqual(first, CaptureGeometrySignatures());
            Assert.That(generator.RoomLayoutSignature, Is.EqualTo(firstLayout));
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentShapeSequences()
        {
            generator.SetSeed(12345);
            generator.Generate();
            List<DungeonRoomShapeType> first = CaptureShapeTypes();

            generator.SetSeed(54321);
            generator.Generate();

            CollectionAssert.AreNotEqual(first, CaptureShapeTypes());
        }

        [TestCase(DungeonRoomShapeType.Rectangle)]
        [TestCase(DungeonRoomShapeType.LShape)]
        [TestCase(DungeonRoomShapeType.Cross)]
        [TestCase(DungeonRoomShapeType.Alcove)]
        public void ForcedShape_ProducesConnectedUsableGeometry(DungeonRoomShapeType shape)
        {
            SetOnlyShapeWeight(shape);
            SetField(generator, "targetRoomCount", 1);
            generator.SetSeed(12345);
            generator.Generate();

            Assert.That(generator.Rooms, Is.Not.Empty);
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                Assert.That(room.ShapeType, Is.EqualTo(shape));
                Assert.That(room.OccupiedCellCount, Is.GreaterThanOrEqualTo(25));
                Assert.That(room.InteriorCellCount, Is.GreaterThanOrEqualTo(9));
                Assert.That(IsConnected(room.OccupiedCells), Is.True,
                    $"Room {room.RoomId} {shape} geometry is disconnected.");
                for (int cell = 0; cell < room.OccupiedCells.Count; cell++)
                    Assert.That(room.Bounds.Contains(room.OccupiedCells[cell]), Is.True);
            }
        }

        [Test]
        public void OccupiedCells_NeverOverlapAcrossRooms()
        {
            generator.SetSeed(12345);
            generator.Generate();
            var occupied = new HashSet<Vector2Int>();
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                for (int cell = 0; cell < room.OccupiedCells.Count; cell++)
                    Assert.That(occupied.Add(room.OccupiedCells[cell]), Is.True,
                        $"Room {room.RoomId} overlaps another room at {room.OccupiedCells[cell]}.");
            }
        }

        [Test]
        public void RolePolicy_PrefersSimpleStartAndSafeBossShapes()
        {
            generator.SetSeed(12345);
            generator.Generate();
            graph.BuildGraph();
            roles.AssignRoomRoles();

            RoomData start = FindRoom(roles.StartRoomId);
            RoomData boss = FindRoom(roles.BossRoomId);
            bool hasRectangle = false;
            bool hasPreferredBoss = false;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                if (room.ShapeType == DungeonRoomShapeType.Rectangle) hasRectangle = true;
                if (room.RoomId != start.RoomId && room.ShapeType != DungeonRoomShapeType.LShape
                    && room.InteriorCellCount >= roles.MinimumBossInteriorCells) hasPreferredBoss = true;
            }

            if (hasRectangle) Assert.That(start.ShapeType, Is.EqualTo(DungeonRoomShapeType.Rectangle));
            Assert.That(start.Archetype, Is.Not.EqualTo(DungeonRoomArchetype.Compact));
            Assert.That(boss.Archetype, Is.EqualTo(DungeonRoomArchetype.Arena));
            if (hasPreferredBoss)
            {
                Assert.That(boss.ShapeType, Is.Not.EqualTo(DungeonRoomShapeType.LShape));
                Assert.That(boss.InteriorCellCount,
                    Is.GreaterThanOrEqualTo(roles.MinimumBossInteriorCells));
            }
        }

        private RoomData FindRoom(int roomId)
        {
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            Assert.Fail($"Room {roomId} was not found.");
            return null;
        }

        private List<long> CaptureGeometrySignatures()
        {
            var result = new List<long>(generator.Rooms.Count);
            for (int i = 0; i < generator.Rooms.Count; i++)
                result.Add(generator.Rooms[i].GeometrySignature);
            return result;
        }

        private List<DungeonRoomShapeType> CaptureShapeTypes()
        {
            var result = new List<DungeonRoomShapeType>(generator.Rooms.Count);
            for (int i = 0; i < generator.Rooms.Count; i++)
                result.Add(generator.Rooms[i].ShapeType);
            return result;
        }

        private void SetOnlyShapeWeight(DungeonRoomShapeType shape)
        {
            SetField(generator, "rectangleWeight", shape == DungeonRoomShapeType.Rectangle ? 1 : 0);
            SetField(generator, "lShapeWeight", shape == DungeonRoomShapeType.LShape ? 1 : 0);
            SetField(generator, "crossShapeWeight", shape == DungeonRoomShapeType.Cross ? 1 : 0);
            SetField(generator, "alcoveWeight", shape == DungeonRoomShapeType.Alcove ? 1 : 0);
        }

        private static bool IsConnected(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return false;
            var all = new HashSet<Vector2Int>(cells);
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(cells[0]);
            queue.Enqueue(cells[0]);
            Vector2Int[] directions =
            {
                Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
            };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = current + directions[i];
                    if (all.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Count == all.Count;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
