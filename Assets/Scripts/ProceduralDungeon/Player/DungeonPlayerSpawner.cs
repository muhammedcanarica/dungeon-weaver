using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonRoomRoleAssigner), typeof(DungeonTilemapRenderer))]
    public sealed class DungeonPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private Transform prototypePlayer;
        [SerializeField] private Tilemap floorTilemap;

        private void Start()
        {
            if (!TryPlacePlayer(out string error))
            {
                DisablePlayerController();
                Debug.LogError($"[DungeonPlayerSpawner] Runtime spawn rejected: {error}", this);
                return;
            }
            Debug.Log("[DungeonPlayerSpawner] Placed PrototypePlayer at the deterministic start room.", this);
        }

        [ContextMenu("Place Player At Start")]
        public void PlacePlayerAtStart()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && prototypePlayer != null) Undo.RecordObject(prototypePlayer, "Place Player At Start");
#endif
            if (!TryPlacePlayer(out string error)) { Debug.LogWarning($"[DungeonPlayerSpawner] Placement rejected: {error}", this); return; }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(prototypePlayer);
                Rigidbody2D body = prototypePlayer.GetComponent<Rigidbody2D>(); if (body != null) EditorUtility.SetDirty(body);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            Debug.Log("[DungeonPlayerSpawner] Placed PrototypePlayer at the deterministic start room.", this);
        }

        private bool TryPlacePlayer(out string error)
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonRoomRoleAssigner roles = GetComponent<DungeonRoomRoleAssigner>();
            DungeonTilemapRenderer renderer = GetComponent<DungeonTilemapRenderer>();
            if (generator == null || roles == null || renderer == null || !roles.IsRoleDataCurrent || !renderer.IsTilemapCurrent)
            { error = "Role or Tilemap data is stale."; return false; }
            if (prototypePlayer == null || floorTilemap == null) { error = "PrototypePlayer or FloorTilemap reference is missing."; return false; }
            RoomData start = null;
            for (int i = 0; i < generator.Rooms.Count; i++) if (generator.Rooms[i].RoomId == roles.StartRoomId) { start = generator.Rooms[i]; break; }
            if (start == null) { error = "Start room was not found."; return false; }
            Vector3Int cell = new Vector3Int(start.Bounds.xMin + (start.Bounds.width - 1) / 2, start.Bounds.yMin + (start.Bounds.height - 1) / 2, 0);
            if (!floorTilemap.HasTile(cell)) { error = "Start room center is not a rendered floor cell."; return false; }
            Vector3 world = floorTilemap.GetCellCenterWorld(cell);
            Rigidbody2D body = prototypePlayer.GetComponent<Rigidbody2D>();
            if (body == null) { error = "PrototypePlayer Rigidbody2D is missing."; return false; }
            prototypePlayer.position = new Vector3(world.x, world.y, prototypePlayer.position.z);
            body.linearVelocity = Vector2.zero; body.position = new Vector2(world.x, world.y);
            TopDownPlayerController controller = prototypePlayer.GetComponent<TopDownPlayerController>();
            if (controller == null) { error = "TopDownPlayerController is missing."; return false; }
            controller.enabled = true; error = string.Empty; return true;
        }

        private void DisablePlayerController()
        {
            if (prototypePlayer == null) return;
            TopDownPlayerController controller = prototypePlayer.GetComponent<TopDownPlayerController>(); if (controller != null) controller.enabled = false;
            Rigidbody2D body = prototypePlayer.GetComponent<Rigidbody2D>(); if (body != null) body.linearVelocity = Vector2.zero;
        }
    }
}
