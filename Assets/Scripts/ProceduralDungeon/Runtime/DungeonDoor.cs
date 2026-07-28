using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum DungeonDoorOrientation
    {
        Horizontal,
        Vertical
    }

    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public sealed class DungeonDoor : MonoBehaviour
    {
        private static readonly Color OpenColor = new Color(0.2f, 0.9f, 0.75f, 0.35f);
        private static readonly Color ClosedColor = new Color(0.95f, 0.25f, 0.12f, 1f);

        private readonly List<int> connectionIndices = new List<int>();
        private IReadOnlyList<int> readOnlyConnectionIndices;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D doorCollider;
        private int roomId = DungeonRoomStateController.InvalidId;
        private Vector2Int entranceCell;
        private DoorwayDirection outwardDirection;
        private DungeonDoorOrientation orientation;
        private Vector2 worldSize;
        private bool isOpen = true;

        public int RoomId => roomId;
        public int ConnectionIndex => connectionIndices.Count > 0
            ? connectionIndices[0] : DungeonRoomStateController.InvalidId;
        public IReadOnlyList<int> ConnectionIndices =>
            readOnlyConnectionIndices ??= connectionIndices.AsReadOnly();
        public Vector2Int EntranceCell => entranceCell;
        public DoorwayDirection OutwardDirection => outwardDirection;
        public DungeonDoorOrientation Orientation => orientation;
        public Vector2 WorldSize => worldSize;
        public float PassageSpan => orientation == DungeonDoorOrientation.Horizontal
            ? worldSize.x : worldSize.y;
        public Bounds ColliderBounds => new Bounds(transform.position,
            new Vector3(worldSize.x, worldSize.y, 0.1f));
        public bool IsOpen => isOpen;
        public SpriteRenderer DoorRenderer => spriteRenderer;
        public BoxCollider2D DoorCollider => doorCollider;

        internal void Initialize(int sourceRoomId, Vector2Int sourceEntranceCell,
            DoorwayDirection sourceDirection, DungeonDoorOrientation sourceOrientation,
            Vector2 sourceWorldSize, IReadOnlyList<int> sourceConnections, Sprite sprite)
        {
            roomId = sourceRoomId;
            entranceCell = sourceEntranceCell;
            outwardDirection = sourceDirection;
            orientation = sourceOrientation;
            worldSize = sourceWorldSize;
            connectionIndices.Clear();
            for (int i = 0; i < sourceConnections.Count; i++) connectionIndices.Add(sourceConnections[i]);
            connectionIndices.Sort();

            spriteRenderer = GetComponent<SpriteRenderer>();
            doorCollider = GetComponent<BoxCollider2D>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 20;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            doorCollider.size = Vector2.one;
            doorCollider.offset = Vector2.zero;
            doorCollider.isTrigger = false;
            Open();
        }

        public void Open()
        {
            isOpen = true;
            if (doorCollider != null) doorCollider.enabled = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = OpenColor;
            }
        }

        public void Close()
        {
            isOpen = false;
            if (doorCollider != null) doorCollider.enabled = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = ClosedColor;
            }
        }

        public bool RepresentsConnection(int connectionIndex)
        {
            for (int i = 0; i < connectionIndices.Count; i++)
                if (connectionIndices[i] == connectionIndex) return true;
            return false;
        }
    }
}
