using System;
using UnityEngine;

namespace ProceduralDungeon
{
    [Serializable]
    public sealed class RoomConnectionData
    {
        [SerializeField] private int roomAId;
        [SerializeField] private int roomBId;
        [SerializeField] private long weightSquared;
        [SerializeField] private bool isPrimaryConnection;

        public int RoomAId => roomAId;
        public int RoomBId => roomBId;
        public long WeightSquared => weightSquared;
        public bool IsPrimaryConnection => isPrimaryConnection;

        public RoomConnectionData(int roomAId, int roomBId, long weightSquared, bool isPrimaryConnection)
        {
            this.roomAId = Math.Min(roomAId, roomBId);
            this.roomBId = Math.Max(roomAId, roomBId);
            this.weightSquared = weightSquared;
            this.isPrimaryConnection = isPrimaryConnection;
        }
    }
}
