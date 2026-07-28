using System;
using UnityEngine;

namespace ProceduralDungeon
{
    [Serializable]
    public sealed class RoomData
    {
        [SerializeField] private int roomId;
        [SerializeField] private RectInt bounds;

        public int RoomId => roomId;
        public RectInt Bounds => bounds;
        public Vector2 Center => bounds.center;
        public int X => bounds.x;
        public int Y => bounds.y;
        public int Width => bounds.width;
        public int Height => bounds.height;

        public RoomData(int roomId, RectInt bounds)
        {
            this.roomId = roomId;
            this.bounds = bounds;
        }
    }
}
