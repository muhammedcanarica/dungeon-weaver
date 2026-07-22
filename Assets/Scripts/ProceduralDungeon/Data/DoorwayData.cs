using System;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum DoorwayDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    [Serializable]
    public sealed class DoorwayData
    {
        [SerializeField] private int roomId;
        [SerializeField] private int connectionIndex;
        [SerializeField] private Vector2Int entranceCell;
        [SerializeField] private DoorwayDirection outwardDirection;
        [SerializeField] private Vector2Int firstCorridorCell;

        public int RoomId => roomId;
        public int ConnectionIndex => connectionIndex;
        public Vector2Int EntranceCell => entranceCell;
        public DoorwayDirection OutwardDirection => outwardDirection;
        public Vector2Int FirstCorridorCell => firstCorridorCell;

        public DoorwayData(int roomId, int connectionIndex, Vector2Int entranceCell,
            DoorwayDirection outwardDirection, Vector2Int firstCorridorCell)
        {
            this.roomId = roomId;
            this.connectionIndex = connectionIndex;
            this.entranceCell = entranceCell;
            this.outwardDirection = outwardDirection;
            this.firstCorridorCell = firstCorridorCell;
        }
    }
}
