using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField, Min(0f)] private float followSmoothTime = 0.15f;
        [SerializeField] private bool clampToDungeonBounds = true;
        [SerializeField, Min(0f)] private float boundsPadding = 0.5f;

        private Camera cameraComponent;
        private Vector3 followVelocity;
        private BoundsInt cachedCellBounds;
        private float cachedAspect = -1f;
        private float cachedOrthographicSize = -1f;
        private Vector2 dungeonMin;
        private Vector2 dungeonMax;
        private bool hasBounds;

        private void Awake()
        {
            if (!TryGetComponent(out cameraComponent)) enabled = false;
        }
        private void OnEnable() => RefreshBounds();

        private void LateUpdate()
        {
            if (target == null) return;
            if (cameraComponent == null && !TryGetComponent(out cameraComponent)) return;
            RefreshBoundsIfNeeded();
            float z = transform.position.z;
            Vector3 desired = new Vector3(target.position.x, target.position.y, z);
            if (clampToDungeonBounds && hasBounds) desired = Clamp(desired, z);
            Vector3 next = Vector3.SmoothDamp(transform.position, desired, ref followVelocity, followSmoothTime);
            if (clampToDungeonBounds && hasBounds) next = Clamp(next, z);
            next.z = z;
            transform.position = next;
        }

        public void RefreshBounds()
        {
            InvalidateDungeonBounds();
            if (floorTilemap == null || floorTilemap.GetUsedTilesCount() == 0) return;
            if (cameraComponent == null && !TryGetComponent(out cameraComponent)) return;
            cachedCellBounds = floorTilemap.cellBounds;
            cachedAspect = cameraComponent.aspect;
            cachedOrthographicSize = cameraComponent.orthographicSize;
            Vector3 min = floorTilemap.CellToWorld(cachedCellBounds.min);
            Vector3 max = floorTilemap.CellToWorld(cachedCellBounds.max);
            dungeonMin = new Vector2(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y));
            dungeonMax = new Vector2(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            hasBounds = true;
        }

        public void RefreshDungeonBounds()
        {
            RefreshBounds();
            followVelocity = Vector3.zero;
            if (target == null) return;
            if (cameraComponent == null && !TryGetComponent(out cameraComponent)) return;

            float z = transform.position.z;
            Vector3 targetPosition = new Vector3(target.position.x, target.position.y, z);
            if (clampToDungeonBounds && hasBounds) targetPosition = Clamp(targetPosition, z);
            targetPosition.z = z;
            transform.position = targetPosition;
        }

        public void InvalidateDungeonBounds()
        {
            hasBounds = false;
            cachedCellBounds = default;
            cachedAspect = -1f;
            cachedOrthographicSize = -1f;
            dungeonMin = Vector2.zero;
            dungeonMax = Vector2.zero;
            followVelocity = Vector3.zero;
        }

        private void RefreshBoundsIfNeeded()
        {
            if (floorTilemap == null || cameraComponent == null) return;
            if (floorTilemap.cellBounds != cachedCellBounds || !Mathf.Approximately(cameraComponent.aspect, cachedAspect)
                || !Mathf.Approximately(cameraComponent.orthographicSize, cachedOrthographicSize)) RefreshBounds();
        }

        private Vector3 Clamp(Vector3 value, float z)
        {
            float halfHeight = cameraComponent.orthographicSize;
            float halfWidth = halfHeight * cameraComponent.aspect;
            float minX = dungeonMin.x + halfWidth + boundsPadding, maxX = dungeonMax.x - halfWidth - boundsPadding;
            float minY = dungeonMin.y + halfHeight + boundsPadding, maxY = dungeonMax.y - halfHeight - boundsPadding;
            value.x = minX <= maxX ? Mathf.Clamp(value.x, minX, maxX) : (dungeonMin.x + dungeonMax.x) * 0.5f;
            value.y = minY <= maxY ? Mathf.Clamp(value.y, minY, maxY) : (dungeonMin.y + dungeonMax.y) * 0.5f;
            value.z = z; return value;
        }
    }
}
