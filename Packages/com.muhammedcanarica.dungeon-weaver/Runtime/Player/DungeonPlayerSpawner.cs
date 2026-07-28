using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonRoomRoleAssigner),
        typeof(DungeonTilemapRenderer))]
    public sealed class DungeonPlayerSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("prototypePlayer")]
        [SerializeField] private Transform trackedActor;
        [SerializeField] private Tilemap floorTilemap;

        public Transform TrackedActor => trackedActor;
        public Collider2D TrackedActorCollider => trackedActor != null
            ? trackedActor.GetComponent<Collider2D>() : null;
        public Rigidbody2D TrackedActorBody => trackedActor != null
            ? trackedActor.GetComponent<Rigidbody2D>() : null;

        private void Start()
        {
            // DungeonRuntimeController owns startup generation when it is present.
            if (TryGetComponent(out DungeonRuntimeController _)) return;

            if (!TryPlaceTrackedActor(out string error))
            {
                Debug.LogError($"[DungeonPlayerSpawner] Runtime spawn rejected: {error}", this);
                return;
            }

            Debug.Log("[DungeonPlayerSpawner] Placed the tracked actor in the deterministic start room.", this);
        }

        public void SetTrackedActor(Transform actorTransform)
        {
            trackedActor = actorTransform;
        }

        public bool TryPlacePlayerAtStart()
        {
            return TryPlaceTrackedActor(out _);
        }

        public bool TryPlaceTrackedActorAtStart()
        {
            return TryPlaceTrackedActor(out _);
        }

        [ContextMenu("Place Tracked Actor At Start")]
        public void PlaceTrackedActorAtStart()
        {
            if (!TryPlaceTrackedActor(out string error))
            {
                Debug.LogWarning($"[DungeonPlayerSpawner] Placement rejected: {error}", this);
                return;
            }
            Debug.Log("[DungeonPlayerSpawner] Placed the tracked actor in the deterministic start room.", this);
        }

        [ContextMenu("Place Player At Start")]
        public void PlacePlayerAtStart()
        {
            PlaceTrackedActorAtStart();
        }

        public bool ValidateTrackedActor(out string error)
        {
            if (trackedActor == null)
            {
                error = "Tracked actor Transform is missing.";
                return false;
            }
            if (TrackedActorCollider == null)
            {
                error = "Tracked actor Collider2D is missing.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryPlaceTrackedActor(out string error)
        {
            DungeonGenerator generator = GetComponent<DungeonGenerator>();
            DungeonRoomRoleAssigner roles = GetComponent<DungeonRoomRoleAssigner>();
            DungeonTilemapRenderer renderer = GetComponent<DungeonTilemapRenderer>();
            if (generator == null || roles == null || renderer == null
                || !roles.IsRoleDataCurrent || !renderer.IsTilemapCurrent)
            {
                error = "Role or Tilemap data is stale.";
                return false;
            }
            if (!ValidateTrackedActor(out error) || floorTilemap == null)
            {
                if (string.IsNullOrEmpty(error)) error = "FloorTilemap reference is missing.";
                return false;
            }

            RoomData start = null;
            for (int i = 0; i < generator.Rooms.Count; i++)
            {
                if (generator.Rooms[i].RoomId != roles.StartRoomId) continue;
                start = generator.Rooms[i];
                break;
            }
            if (start == null)
            {
                error = "Start room was not found.";
                return false;
            }

            var cell = new Vector3Int(start.Bounds.xMin + (start.Bounds.width - 1) / 2,
                start.Bounds.yMin + (start.Bounds.height - 1) / 2, 0);
            if (!floorTilemap.HasTile(cell))
            {
                error = "Start room center is not a rendered floor cell.";
                return false;
            }

            Vector3 world = floorTilemap.GetCellCenterWorld(cell);
            trackedActor.position = new Vector3(world.x, world.y, trackedActor.position.z);
            Rigidbody2D body = TrackedActorBody;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = new Vector2(world.x, world.y);
            }
            error = string.Empty;
            return true;
        }
    }
}
