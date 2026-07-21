using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    [Serializable]
    public sealed class CorridorData
    {
        [SerializeField] private int roomAId;
        [SerializeField] private int roomBId;
        [SerializeField] private bool isPrimaryConnection;
        [SerializeField] private bool usedFallbackPathfinding;
        [SerializeField] private List<Vector2Int> pathCells = new List<Vector2Int>();
        private IReadOnlyList<Vector2Int> readOnlyPathCells;

        public int RoomAId => roomAId;
        public int RoomBId => roomBId;
        public bool IsPrimaryConnection => isPrimaryConnection;
        public bool UsedFallbackPathfinding => usedFallbackPathfinding;
        public int CellCount => pathCells?.Count ?? 0;
        public Vector2Int StartDoorCell => CellCount > 0 ? pathCells[0] : default;
        public Vector2Int EndDoorCell => CellCount > 0 ? pathCells[pathCells.Count - 1] : default;
        public IReadOnlyList<Vector2Int> PathCells
        {
            get
            {
                if (pathCells == null) pathCells = new List<Vector2Int>();
                return readOnlyPathCells ??= pathCells.AsReadOnly();
            }
        }

        public CorridorData(int roomAId, int roomBId, bool primary, bool fallback, IReadOnlyList<Vector2Int> cells)
        {
            this.roomAId = Math.Min(roomAId, roomBId);
            this.roomBId = Math.Max(roomAId, roomBId);
            isPrimaryConnection = primary;
            usedFallbackPathfinding = fallback;
            pathCells = new List<Vector2Int>(cells?.Count ?? 0);
            if (cells != null) for (int i = 0; i < cells.Count; i++) pathCells.Add(cells[i]);
        }
    }
}
