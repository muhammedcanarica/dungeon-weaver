using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Editor.Tests
{
    public sealed class DungeonSetupMenuTests
    {
        private string originalScenePath;
        private Scene testScene;

        [SetUp]
        public void SetUp()
        {
            originalScenePath = SceneManager.GetActiveScene().path;
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Undo.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void EmptyScene_CreatesExpectedHierarchyAndReferences()
        {
            GameObject setup = CreateSetupExpectWarnings();

            Assert.That(setup.name, Is.EqualTo("DungeonWeaver"));
            Transform gridTransform = setup.transform.Find("Grid");
            Assert.That(gridTransform, Is.Not.Null);
            Grid grid = gridTransform.GetComponent<Grid>();
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.cellSize, Is.EqualTo(new Vector3(1f, 1f, 0f)));
            Assert.That(gridTransform.Find("Floor").GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(gridTransform.Find("Walls").GetComponent<Tilemap>(), Is.Not.Null);
            Assert.That(gridTransform.Find("Walls").GetComponent<TilemapCollider2D>(), Is.Not.Null);
            Assert.That(setup.transform.Find("Dungeon Camera").GetComponent<Camera>(), Is.Not.Null);

            DungeonTilemapRenderer renderer = setup.GetComponent<DungeonTilemapRenderer>();
            Assert.That(renderer.FloorTilemap,
                Is.SameAs(gridTransform.Find("Floor").GetComponent<Tilemap>()));
            Assert.That(renderer.WallTilemap,
                Is.SameAs(gridTransform.Find("Walls").GetComponent<Tilemap>()));
        }

        [Test]
        public void SecondRun_ReusesSetupWithoutDuplicates()
        {
            GameObject first = CreateSetupExpectWarnings();
            GameObject second = CreateSetupExpectWarnings();

            Assert.That(second, Is.SameAs(first));
            Assert.That(CountComponents<DungeonRuntimeController>(), Is.EqualTo(1));
            Assert.That(CountNamedObjects("Grid"), Is.EqualTo(1));
            Assert.That(CountNamedObjects("Floor"), Is.EqualTo(1));
            Assert.That(CountNamedObjects("Walls"), Is.EqualTo(1));
        }

        [Test]
        public void Setup_AddsEveryRequiredCoreComponentOnlyOnce()
        {
            GameObject setup = CreateSetupExpectWarnings();

            AssertSingle<DungeonGenerator>(setup);
            AssertSingle<DungeonGraphBuilder>(setup);
            AssertSingle<DungeonCorridorBuilder>(setup);
            AssertSingle<DungeonDoorwayBuilder>(setup);
            AssertSingle<DungeonTilemapRenderer>(setup);
            AssertSingle<DungeonRoomRoleAssigner>(setup);
            AssertSingle<DungeonPlayerSpawner>(setup);
            AssertSingle<DungeonAreaTracker>(setup);
            AssertSingle<DungeonRoomStateController>(setup);
            AssertSingle<DungeonDoorRegistry>(setup);
            AssertSingle<DungeonRoomEncounterController>(setup);
            AssertSingle<DungeonRuntimeController>(setup);
        }

        [Test]
        public void Setup_DoesNotAddPrototypeComponents()
        {
            GameObject setup = CreateSetupExpectWarnings();

            Component[] components = setup.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                string assembly = components[i].GetType().Assembly.GetName().Name;
                Assert.That(assembly, Is.Not.EqualTo("DungeonWeaver.Samples.Prototype"));
            }
        }

        [Test]
        public void Setup_IsUndoableAndPreservesUnrelatedObjects()
        {
            var unrelated = new GameObject("Unrelated Consumer Object");
            unrelated.transform.position = new Vector3(7f, 8f, 9f);
            SceneManager.MoveGameObjectToScene(unrelated, testScene);

            GameObject setup = CreateSetupExpectWarnings();
            Assert.That(setup, Is.Not.Null);
            Assert.That(unrelated.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));

            Undo.PerformUndo();

            Assert.That(unrelated, Is.Not.Null);
            Assert.That(unrelated.transform.position, Is.EqualTo(new Vector3(7f, 8f, 9f)));
            Assert.That(CountComponents<DungeonRuntimeController>(), Is.Zero);
        }

        private static GameObject CreateSetupExpectWarnings()
        {
            LogAssert.Expect(LogType.Warning,
                "[Dungeon Weaver] Setup created. Assign Floor Tile and Wall Tile on DungeonTilemapRenderer before generation.");
            LogAssert.Expect(LogType.Warning,
                "[Dungeon Weaver] Setup created without an actor. Assign a Transform with Collider2D through DungeonPlayerSpawner or DungeonRuntimeController before generation.");
            return DungeonSetupMenu.CreateDungeonSetup();
        }

        private int CountNamedObjects(string name)
        {
            int count = 0;
            GameObject[] roots = testScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                    if (transforms[j].name == name) count++;
            }
            return count;
        }

        private int CountComponents<T>() where T : Component
        {
            int count = 0;
            GameObject[] roots = testScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }

        private static void AssertSingle<T>(GameObject target) where T : Component
        {
            Assert.That(target.GetComponents<T>().Length, Is.EqualTo(1), typeof(T).Name);
        }
    }
}
