using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ProceduralDungeon.Tests
{
    public sealed class DungeonGraphTopologyTests
    {
        private GameObject dungeonObject;
        private DungeonGenerator generator;
        private DungeonGraphBuilder graph;
        private DungeonRoomRoleAssigner roles;

        [SetUp]
        public void SetUp()
        {
            dungeonObject = new GameObject("Topology Test Dungeon");
            generator = dungeonObject.AddComponent<DungeonGenerator>();
            graph = dungeonObject.AddComponent<DungeonGraphBuilder>();
            roles = dungeonObject.AddComponent<DungeonRoomRoleAssigner>();
        }

        [TearDown]
        public void TearDown()
        {
            if (dungeonObject != null) Object.DestroyImmediate(dungeonObject);
        }

        [TestCase(12345)]
        [TestCase(54321)]
        [TestCase(1)]
        [TestCase(42)]
        [TestCase(99999)]
        [TestCase(-54321)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void ReferenceSeeds_ProduceConnectedControlledTopology(int seed)
        {
            Build(seed);

            DungeonGraphAnalysis analysis = graph.Analysis;
            Assert.That(generator.Rooms.Count, Is.EqualTo(15));
            Assert.That(graph.IsGraphCurrent, Is.True);
            Assert.That(roles.IsRoleDataCurrent, Is.True);
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.IsConnected, Is.True);
            Assert.That(analysis.EdgeCount, Is.EqualTo(graph.Connections.Count));
            Assert.That(analysis.LoopCount, Is.EqualTo(2));
            Assert.That(analysis.DeadEndRooms.Count,
                Is.EqualTo(graph.TargetDeadEndCount));
            Assert.That(analysis.JunctionRooms.Count, Is.InRange(2, 6));
            Assert.That(analysis.CriticalPathLength,
                Is.GreaterThanOrEqualTo(graph.MinimumCriticalPathHops(15)));
            Assert.That(analysis.DiameterLength,
                Is.GreaterThanOrEqualTo(analysis.CriticalPathLength));
            Assert.That(analysis.CriticalPath[0], Is.EqualTo(roles.StartRoomId));
            Assert.That(analysis.CriticalPath[analysis.CriticalPath.Count - 1],
                Is.EqualTo(roles.BossRoomId));
            Assert.That(FindRoom(roles.StartRoomId).Archetype,
                Is.Not.EqualTo(DungeonRoomArchetype.Compact));
            Assert.That(FindRoom(roles.BossRoomId).Archetype,
                Is.EqualTo(DungeonRoomArchetype.Arena));
            AssertGraphInvariants();
            AssertCriticalPathEdges(analysis.CriticalPath);
        }

        [Test]
        public void SameSeed_ReproducesEdgesAndDerivedTopology()
        {
            Build(12345);
            List<string> firstEdges = CaptureEdges();
            string firstAnalysis = CaptureAnalysis();
            long firstSignature = graph.GraphSignature;

            Build(12345);

            CollectionAssert.AreEqual(firstEdges, CaptureEdges());
            Assert.That(CaptureAnalysis(), Is.EqualTo(firstAnalysis));
            Assert.That(graph.GraphSignature, Is.EqualTo(firstSignature));
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentTopology()
        {
            Build(12345);
            List<string> first = CaptureEdges();
            Build(54321);

            CollectionAssert.AreNotEqual(first, CaptureEdges());
        }

        [Test]
        public void AnalysisQueries_MatchConnectionDegreesAndSets()
        {
            Build(12345);
            DungeonGraphAnalysis analysis = graph.Analysis;
            var degree = new Dictionary<int, int>();
            for (int i = 0; i < generator.Rooms.Count; i++)
                degree.Add(generator.Rooms[i].RoomId, 0);
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                degree[graph.Connections[i].RoomAId]++;
                degree[graph.Connections[i].RoomBId]++;
            }

            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                int roomId = generator.Rooms[i].RoomId;
                Assert.That(analysis.GetDegree(roomId), Is.EqualTo(degree[roomId]));
                Assert.That(analysis.IsDeadEnd(roomId),
                    Is.EqualTo(roomId != roles.StartRoomId && degree[roomId] == 1));
                Assert.That(analysis.IsJunction(roomId), Is.EqualTo(degree[roomId] >= 3));
                Assert.That(analysis.GetShortestDistance(roles.StartRoomId, roomId),
                    Is.EqualTo(analysis.GetShortestDistance(roomId, roles.StartRoomId)));
            }
            Assert.That(analysis.RoomsOutsideCriticalPath.Count
                + analysis.CriticalPath.Count, Is.EqualTo(generator.Rooms.Count));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SmallRoomCounts_ProduceValidScaledTopology(int roomCount)
        {
            SetField(generator, "targetRoomCount", roomCount);
            Build(12345);

            DungeonGraphAnalysis analysis = graph.Analysis;
            Assert.That(generator.Rooms.Count, Is.EqualTo(roomCount));
            Assert.That(analysis.IsConnected, Is.True);
            Assert.That(graph.Connections.Count, Is.EqualTo(roomCount - 1));
            Assert.That(analysis.LoopCount, Is.Zero);
            Assert.That(analysis.JunctionRooms, Is.Empty);
            Assert.That(analysis.CriticalPathLength,
                Is.EqualTo(roomCount > 0 ? roomCount - 1 : 0));
            Assert.That(analysis.DeadEndRooms.Count,
                Is.EqualTo(roomCount > 1 ? 1 : 0));
            AssertGraphInvariants();
        }

        [Test]
        public void Configuration_CanDisableLoopsAndReduceDeadEnds()
        {
            Build(12345);
            long defaultSignature = graph.GraphSignature;
            SetField(graph, "extraConnectionCount", 0);
            SetField(graph, "targetDeadEndCount", 2);
            SetField(graph, "minimumCriticalPathRatio", 0.6f);
            Assert.That(graph.IsGraphCurrent, Is.False);
            graph.BuildGraph();
            roles.AssignRoomRoles();

            Assert.That(graph.GraphSignature, Is.Not.EqualTo(defaultSignature));
            Assert.That(graph.Analysis.LoopCount, Is.Zero);
            Assert.That(graph.Analysis.DeadEndRooms.Count, Is.EqualTo(2));
            Assert.That(graph.Analysis.CriticalPathLength,
                Is.GreaterThanOrEqualTo(graph.MinimumCriticalPathHops(15)));
            AssertGraphInvariants();
        }

        private void Build(int seed)
        {
            generator.SetSeed(seed);
            generator.Generate();
            graph.BuildGraph();
            roles.AssignRoomRoles();
        }

        private void AssertGraphInvariants()
        {
            var roomIds = new HashSet<int>();
            var pairs = new HashSet<long>();
            for (int i = 0; i < generator.Rooms.Count; i++)
                roomIds.Add(generator.Rooms[i].RoomId);

            int primaryCount = 0;
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                RoomConnectionData edge = graph.Connections[i];
                Assert.That(edge.RoomAId, Is.Not.EqualTo(edge.RoomBId));
                Assert.That(roomIds.Contains(edge.RoomAId), Is.True);
                Assert.That(roomIds.Contains(edge.RoomBId), Is.True);
                Assert.That(pairs.Add(EdgeKey(edge.RoomAId, edge.RoomBId)), Is.True);
                if (edge.IsPrimaryConnection) primaryCount++;
            }
            Assert.That(primaryCount, Is.EqualTo(generator.Rooms.Count - 1));
            for (int i = 0; i < generator.Rooms.Count; i++)
                Assert.That(graph.Analysis.GetDegree(generator.Rooms[i].RoomId),
                    Is.LessThanOrEqualTo(graph.MaxRoomDegree));
            Assert.That(graph.Analysis.LoopCount,
                Is.EqualTo(graph.Connections.Count - generator.Rooms.Count + 1));
        }

        private void AssertCriticalPathEdges(IReadOnlyList<int> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                bool found = false;
                long key = EdgeKey(path[i - 1], path[i]);
                for (int edge = 0; edge < graph.Connections.Count; edge++)
                    if (EdgeKey(graph.Connections[edge].RoomAId,
                        graph.Connections[edge].RoomBId) == key) found = true;
                Assert.That(found, Is.True,
                    $"Critical path step {path[i - 1]}-{path[i]} is not an edge.");
            }
        }

        private List<string> CaptureEdges()
        {
            var result = new List<string>(graph.Connections.Count);
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                RoomConnectionData edge = graph.Connections[i];
                result.Add($"{edge.RoomAId}:{edge.RoomBId}:{edge.WeightSquared}:"
                    + edge.IsPrimaryConnection);
            }
            return result;
        }

        private string CaptureAnalysis()
        {
            DungeonGraphAnalysis value = graph.Analysis;
            return $"{value.StartRoomId}:{value.BossRoomId}:{value.LoopCount}:"
                + $"{value.DiameterLength}:{string.Join(",", value.DeadEndRooms)}:"
                + $"{string.Join(",", value.JunctionRooms)}:"
                + string.Join(",", value.CriticalPath);
        }

        private RoomData FindRoom(int roomId)
        {
            for (int i = 0; i < generator.Rooms.Count; i++)
                if (generator.Rooms[i].RoomId == roomId) return generator.Rooms[i];
            return null;
        }

        private static long EdgeKey(int a, int b)
        {
            int minimum = a < b ? a : b;
            int maximum = a < b ? b : a;
            return ((long)minimum << 32) ^ (uint)maximum;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }
    }
}
