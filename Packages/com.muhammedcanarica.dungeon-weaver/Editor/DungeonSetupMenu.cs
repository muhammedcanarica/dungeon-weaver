using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon.Editor
{
    public static class DungeonSetupMenu
    {
        private const string MenuPath = "Tools/Dungeon Weaver/Create Dungeon Setup";

        [MenuItem(MenuPath)]
        public static void CreateDungeonSetupFromMenu()
        {
            GameObject setup = CreateDungeonSetup();
            Selection.activeGameObject = setup;
            EditorGUIUtility.PingObject(setup);
        }

        public static GameObject CreateDungeonSetup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Dungeon Weaver] No valid active scene is available.");
                return null;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Dungeon Weaver Setup");

            GameObject coordinator = FindExistingCoordinator(scene);
            if (coordinator == null)
            {
                string name = GameObjectUtility.GetUniqueNameForSibling(null, "DungeonWeaver");
                coordinator = CreateGameObject(name, null, scene);
            }

            GameObject gridObject = EnsureChild(coordinator.transform, "Grid", scene);
            Grid grid = EnsureComponent<Grid>(gridObject);
            SetGridCellSize(grid, new Vector3(1f, 1f, 0f));

            Tilemap floorTilemap = EnsureTilemap(gridObject.transform, "Floor", scene);
            Tilemap wallTilemap = EnsureTilemap(gridObject.transform, "Walls", scene);
            EnsureComponent<TilemapCollider2D>(wallTilemap.gameObject);

            EnsureComponent<DungeonGenerator>(coordinator);
            EnsureComponent<DungeonGraphBuilder>(coordinator);
            EnsureComponent<DungeonCorridorBuilder>(coordinator);
            EnsureComponent<DungeonDoorwayBuilder>(coordinator);
            DungeonTilemapRenderer renderer = EnsureComponent<DungeonTilemapRenderer>(coordinator);
            EnsureComponent<DungeonRoomRoleAssigner>(coordinator);
            DungeonPlayerSpawner spawner = EnsureComponent<DungeonPlayerSpawner>(coordinator);
            DungeonAreaTracker tracker = EnsureComponent<DungeonAreaTracker>(coordinator);
            EnsureComponent<DungeonRoomStateController>(coordinator);
            DungeonDoorRegistry doors = EnsureComponent<DungeonDoorRegistry>(coordinator);
            EnsureComponent<DungeonRoomEncounterController>(coordinator);

            GameObject cameraObject = EnsureChild(coordinator.transform, "Dungeon Camera", scene);
            Camera camera = EnsureComponent<Camera>(cameraObject);
            SetCameraDefaults(camera);
            TopDownCameraFollow cameraFollow = EnsureComponent<TopDownCameraFollow>(cameraObject);

            DungeonRuntimeController runtime = EnsureComponent<DungeonRuntimeController>(coordinator);
            SetObjectReference(renderer, "floorTilemap", floorTilemap);
            SetObjectReference(renderer, "wallTilemap", wallTilemap);
            SetObjectReference(spawner, "floorTilemap", floorTilemap);
            SetObjectReference(tracker, "grid", grid);
            SetObjectReference(doors, "grid", grid);
            SetObjectReference(cameraFollow, "floorTilemap", floorTilemap);
            SetObjectReference(runtime, "cameraFollow", cameraFollow);
            SetBoolean(runtime, "generateOnStart", false);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);

            if (!HasTileReferences(renderer))
                Debug.LogWarning("[Dungeon Weaver] Setup created. Assign Floor Tile and Wall Tile on DungeonTilemapRenderer before generation.", coordinator);
            if (spawner.TrackedActor == null)
                Debug.LogWarning("[Dungeon Weaver] Setup created without an actor. Assign a Transform with Collider2D through DungeonPlayerSpawner or DungeonRuntimeController before generation.", coordinator);

            return coordinator;
        }

        private static GameObject FindExistingCoordinator(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                DungeonRuntimeController runtime =
                    roots[i].GetComponentInChildren<DungeonRuntimeController>(true);
                if (runtime != null) return runtime.gameObject;
            }
            return null;
        }

        private static GameObject EnsureChild(Transform parent, string name, Scene scene)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            return CreateGameObject(name, parent, scene);
        }

        private static GameObject CreateGameObject(string name, Transform parent, Scene scene)
        {
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create Dungeon Weaver Setup");
            if (parent != null) created.transform.SetParent(parent, false);
            else SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static Tilemap EnsureTilemap(Transform parent, string name, Scene scene)
        {
            GameObject tilemapObject = EnsureChild(parent, name, scene);
            Tilemap tilemap = EnsureComponent<Tilemap>(tilemapObject);
            EnsureComponent<TilemapRenderer>(tilemapObject);
            return tilemap;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            Undo.RecordObject(target, "Configure Dungeon Weaver Setup");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetBoolean(Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) return;
            Undo.RecordObject(target, "Configure Dungeon Weaver Setup");
            property.boolValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetGridCellSize(Grid grid, Vector3 cellSize)
        {
            if (grid.cellSize == cellSize) return;
            Undo.RecordObject(grid, "Configure Dungeon Weaver Grid");
            grid.cellSize = cellSize;
        }

        private static void SetCameraDefaults(Camera camera)
        {
            if (!camera.orthographic)
            {
                Undo.RecordObject(camera, "Configure Dungeon Weaver Camera");
                camera.orthographic = true;
            }

            Vector3 localPosition = new Vector3(0f, 0f, -10f);
            if (camera.transform.localPosition == localPosition) return;
            Undo.RecordObject(camera.transform, "Configure Dungeon Weaver Camera");
            camera.transform.localPosition = localPosition;
        }

        private static bool HasTileReferences(DungeonTilemapRenderer renderer)
        {
            var serialized = new SerializedObject(renderer);
            return serialized.FindProperty("floorTile").objectReferenceValue != null
                && serialized.FindProperty("wallTile").objectReferenceValue != null;
        }
    }
}
