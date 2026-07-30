using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonRoomArchetypeTests
    {
        private GameObject dungeonObject;
        private DungeonGenerator generator;
        private DungeonGraphBuilder graph;
        private DungeonRoomRoleAssigner roles;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Room Archetype Test Dungeon");
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
        public void LegacyRoomConstructors_DefaultToStandardArchetype()
        {
            var bounds = new RectInt(2, 3, 8, 8);
            var legacy = new RoomData(0, bounds);
            var legacyShape = new RoomData(0, bounds,
                DungeonRoomShapeType.Rectangle, null);

            Assert.That(legacy.Archetype, Is.EqualTo(DungeonRoomArchetype.Standard));
            Assert.That(legacyShape.Archetype, Is.EqualTo(DungeonRoomArchetype.Standard));
        }

        [Test]
        public void GeometrySignature_IncludesArchetypeMetadata()
        {
            var bounds = new RectInt(0, 0, 12, 10);
            var standard = new RoomData(4, bounds, DungeonRoomArchetype.Standard,
                DungeonRoomShapeType.Rectangle, null);
            var large = new RoomData(4, bounds, DungeonRoomArchetype.Large,
                DungeonRoomShapeType.Rectangle, null);

            CollectionAssert.AreEqual(standard.OccupiedCells, large.OccupiedCells);
            Assert.That(standard.GeometrySignature, Is.Not.EqualTo(large.GeometrySignature));
        }

        [Test]
        public void SameSeed_ProducesSameArchetypesBoundsShapesAndGeometry()
        {
            generator.SetSeed(12345);
            generator.Generate();
            List<string> first = CaptureRoomLayout();
            long firstSignature = generator.RoomLayoutSignature;

            generator.Generate();

            CollectionAssert.AreEqual(first, CaptureRoomLayout());
            Assert.That(generator.RoomLayoutSignature, Is.EqualTo(firstSignature));
        }

        [TestCase(12345)]
        [TestCase(54321)]
        [TestCase(1)]
        [TestCase(42)]
        [TestCase(99999)]
        public void ReferenceSeeds_GenerateFifteenValidVariedRooms(int seed)
        {
            generator.SetSeed(seed);
            generator.Generate();

            Assert.That(generator.Rooms.Count, Is.EqualTo(15));
            Assert.That(generator.LastPlacementAttempts,
                Is.LessThan(generator.MaxPlacementAttempts));
            var archetypes = new HashSet<DungeonRoomArchetype>();
            var occupied = new HashSet<Vector2Int>();
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                RoomArchetypeSettings profile = generator.GetArchetypeSettings(room.Archetype);
                archetypes.Add(room.Archetype);
                Assert.That(profile.ContainsSize(room.Width, room.Height), Is.True);
                Assert.That(room.OccupiedCellCount,
                    Is.GreaterThanOrEqualTo(profile.MinimumUsableFloorArea));
                Assert.That(room.InteriorCellCount,
                    Is.GreaterThanOrEqualTo(profile.MinimumInteriorArea));
                Assert.That(DungeonRoomArchetypePolicy.IsShapeCompatible(
                    room.Archetype, room.ShapeType), Is.True);
                for (int cell = 0; cell < room.OccupiedCells.Count; cell++)
                    Assert.That(occupied.Add(room.OccupiedCells[cell]), Is.True,
                        $"Seed {seed} room {room.RoomId} overlaps at {room.OccupiedCells[cell]}.");
            }
            Assert.That(archetypes.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ProfileRanges_CreateMeaningfulSizeTiers()
        {
            RoomArchetypeSettings compact = generator.CompactProfile;
            RoomArchetypeSettings standard = generator.StandardProfile;
            RoomArchetypeSettings large = generator.LargeProfile;
            RoomArchetypeSettings arena = generator.ArenaProfile;

            Assert.That(compact.MaximumSize.x * compact.MaximumSize.y,
                Is.LessThan(standard.MaximumSize.x * standard.MaximumSize.y));
            Assert.That(large.MinimumSize.x * large.MinimumSize.y,
                Is.GreaterThan(standard.MinimumSize.x * standard.MinimumSize.y));
            Assert.That(arena.MinimumUsableFloorArea,
                Is.GreaterThan(large.MinimumUsableFloorArea));
            Assert.That(arena.MinimumInteriorArea,
                Is.GreaterThan(large.MinimumInteriorArea));
        }

        [Test]
        public void RolePolicy_UsesNonCompactStartAndArenaBoss()
        {
            generator.SetSeed(12345);
            generator.Generate();
            graph.BuildGraph();
            roles.AssignRoomRoles();

            RoomData start = FindRoom(roles.StartRoomId);
            RoomData boss = FindRoom(roles.BossRoomId);
            Assert.That(start.Archetype, Is.Not.EqualTo(DungeonRoomArchetype.Compact));
            Assert.That(boss.Archetype, Is.EqualTo(DungeonRoomArchetype.Arena));
            Assert.That(boss.ShapeType,
                Is.EqualTo(DungeonRoomShapeType.Rectangle)
                    .Or.EqualTo(DungeonRoomShapeType.Alcove));
            Assert.That(boss.InteriorCellCount,
                Is.GreaterThanOrEqualTo(generator.ArenaProfile.MinimumInteriorArea));
        }

        [Test]
        public void WeightedNormalSelection_CanProduceLargeRooms()
        {
            SetProfileWeight(generator.CompactProfile, 0);
            SetProfileWeight(generator.StandardProfile, 0);
            SetProfileWeight(generator.LargeProfile, 1);
            SetProfileWeight(generator.ArenaProfile, 0);
            SetField(generator, "targetRoomCount", 5);
            generator.SetSeed(12345);
            generator.Generate();

            Assert.That(generator.Rooms.Count, Is.EqualTo(5));
            for (int i = 2; i < generator.Rooms.Count; i++)
            {
                Assert.That(generator.Rooms[i].Archetype,
                    Is.EqualTo(DungeonRoomArchetype.Large));
                Assert.That(generator.Rooms[i].Width * generator.Rooms[i].Height,
                    Is.GreaterThanOrEqualTo(generator.LargeProfile.MinimumSize.x
                        * generator.LargeProfile.MinimumSize.y));
            }
        }

        private RoomData FindRoom(int roomId)
        {
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            Assert.Fail($"Room {roomId} was not found.");
            return null;
        }

        private List<string> CaptureRoomLayout()
        {
            var result = new List<string>(generator.Rooms.Count);
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                RoomData room = generator.Rooms[i];
                result.Add($"{room.Archetype}|{room.Bounds}|{room.ShapeType}|{room.GeometrySignature}");
            }
            return result;
        }

        private static void SetProfileWeight(RoomArchetypeSettings profile, int value)
        {
            SetField(profile, "selectionWeight", value);
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
