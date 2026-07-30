using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum DungeonRoomShapeType
    {
        Rectangle,
        LShape,
        Cross,
        Alcove
    }

    [Serializable]
    public sealed class RoomData
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        [SerializeField] private int roomId;
        [SerializeField] private RectInt bounds;
        [SerializeField] private DungeonRoomArchetype archetype = DungeonRoomArchetype.Standard;
        [SerializeField] private DungeonRoomShapeType shapeType;
        [SerializeField] private List<Vector2Int> occupiedCells = new List<Vector2Int>();
        [SerializeField] private List<Vector2Int> interiorCells = new List<Vector2Int>();
        [SerializeField] private List<Vector2Int> boundaryCells = new List<Vector2Int>();

        [NonSerialized] private HashSet<Vector2Int> occupiedLookup;
        [NonSerialized] private HashSet<Vector2Int> interiorLookup;
        [NonSerialized] private HashSet<Vector2Int> boundaryLookup;
        [NonSerialized] private IReadOnlyList<Vector2Int> readOnlyOccupiedCells;
        [NonSerialized] private IReadOnlyList<Vector2Int> readOnlyInteriorCells;
        [NonSerialized] private IReadOnlyList<Vector2Int> readOnlyBoundaryCells;
        [NonSerialized] private bool geometryReady;

        public int RoomId => roomId;
        public RectInt Bounds => bounds;
        public Vector2 Center => bounds.center;
        public int X => bounds.x;
        public int Y => bounds.y;
        public int Width => bounds.width;
        public int Height => bounds.height;
        public DungeonRoomArchetype Archetype => archetype;
        public DungeonRoomShapeType ShapeType
        {
            get { EnsureGeometry(); return shapeType; }
        }
        public IReadOnlyList<Vector2Int> OccupiedCells
        {
            get { EnsureGeometry(); return readOnlyOccupiedCells ??= occupiedCells.AsReadOnly(); }
        }
        public IReadOnlyList<Vector2Int> InteriorCells
        {
            get { EnsureGeometry(); return readOnlyInteriorCells ??= interiorCells.AsReadOnly(); }
        }
        public IReadOnlyList<Vector2Int> BoundaryCells
        {
            get { EnsureGeometry(); return readOnlyBoundaryCells ??= boundaryCells.AsReadOnly(); }
        }
        public int OccupiedCellCount => OccupiedCells.Count;
        public int InteriorCellCount => InteriorCells.Count;
        public int BoundaryCellCount => BoundaryCells.Count;
        public Vector2Int SpawnCell => FindCellNearestBoundsCenter(InteriorCells.Count > 0
            ? InteriorCells : OccupiedCells);
        public long GeometrySignature => CalculateGeometrySignature();

        public RoomData(int roomId, RectInt bounds)
            : this(roomId, bounds, DungeonRoomArchetype.Standard,
                DungeonRoomShapeType.Rectangle, null)
        {
        }

        public RoomData(int roomId, RectInt bounds, DungeonRoomShapeType shapeType,
            IReadOnlyCollection<Vector2Int> occupiedCells)
            : this(roomId, bounds, DungeonRoomArchetype.Standard, shapeType, occupiedCells)
        {
        }

        public RoomData(int roomId, RectInt bounds, DungeonRoomArchetype archetype,
            DungeonRoomShapeType shapeType, IReadOnlyCollection<Vector2Int> occupiedCells)
        {
            this.roomId = roomId;
            this.bounds = bounds;
            this.archetype = archetype;
            this.shapeType = shapeType;
            SetGeometry(occupiedCells);
        }

        public bool ContainsCell(Vector2Int cell)
        {
            EnsureGeometry();
            return occupiedLookup.Contains(cell);
        }

        public bool IsInteriorCell(Vector2Int cell)
        {
            EnsureGeometry();
            return interiorLookup.Contains(cell);
        }

        public bool IsBoundaryCell(Vector2Int cell)
        {
            EnsureGeometry();
            return boundaryLookup.Contains(cell);
        }

        public bool IsOuterBoundaryCell(Vector2Int cell)
        {
            return IsBoundaryCell(cell) && (cell.x == bounds.xMin || cell.x == bounds.xMax - 1
                || cell.y == bounds.yMin || cell.y == bounds.yMax - 1);
        }

        public bool CanPlaceDoorway(Vector2Int entranceCell, Vector2Int firstCorridorCell,
            int requiredSpan)
        {
            Vector2Int outward = firstCorridorCell - entranceCell;
            if (Mathf.Abs(outward.x) + Mathf.Abs(outward.y) != 1
                || !IsBoundaryFace(entranceCell, outward)) return false;
            return GetCenteredDoorwaySpan(entranceCell, outward) >= Mathf.Max(1, requiredSpan);
        }

        public int GetCenteredDoorwaySpan(Vector2Int entranceCell, Vector2Int outward)
        {
            if (Mathf.Abs(outward.x) + Mathf.Abs(outward.y) != 1
                || !IsBoundaryFace(entranceCell, outward)) return 0;

            Vector2Int tangent = outward.x == 0 ? Vector2Int.right : Vector2Int.up;
            int negative = 0;
            while (IsBoundaryFace(entranceCell - tangent * (negative + 1), outward)) negative++;
            int positive = 0;
            while (IsBoundaryFace(entranceCell + tangent * (positive + 1), outward)) positive++;
            return Mathf.Min(negative, positive) * 2 + 1;
        }

        private bool IsBoundaryFace(Vector2Int cell, Vector2Int outward)
        {
            return IsBoundaryCell(cell) && !ContainsCell(cell + outward);
        }

        private void SetGeometry(IReadOnlyCollection<Vector2Int> source)
        {
            occupiedCells = new List<Vector2Int>();
            var unique = new HashSet<Vector2Int>();
            if (source != null)
            {
                foreach (Vector2Int cell in source)
                    if (bounds.Contains(cell) && unique.Add(cell)) occupiedCells.Add(cell);
            }
            if (occupiedCells.Count == 0)
            {
                shapeType = DungeonRoomShapeType.Rectangle;
                AddRectangleCells(occupiedCells, bounds);
            }
            occupiedCells.Sort(CompareCells);
            RebuildDerivedGeometry();
        }

        private void EnsureGeometry()
        {
            if (geometryReady) return;
            if (occupiedCells == null || occupiedCells.Count == 0)
            {
                shapeType = DungeonRoomShapeType.Rectangle;
                occupiedCells = new List<Vector2Int>();
                AddRectangleCells(occupiedCells, bounds);
            }
            occupiedCells.Sort(CompareCells);
            RebuildDerivedGeometry();
        }

        private void RebuildDerivedGeometry()
        {
            occupiedLookup = new HashSet<Vector2Int>(occupiedCells);
            interiorCells = new List<Vector2Int>();
            boundaryCells = new List<Vector2Int>();
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                Vector2Int cell = occupiedCells[i];
                bool interior = occupiedLookup.Contains(cell + Vector2Int.left)
                    && occupiedLookup.Contains(cell + Vector2Int.right)
                    && occupiedLookup.Contains(cell + Vector2Int.up)
                    && occupiedLookup.Contains(cell + Vector2Int.down);
                if (interior) interiorCells.Add(cell);
                else boundaryCells.Add(cell);
            }
            interiorLookup = new HashSet<Vector2Int>(interiorCells);
            boundaryLookup = new HashSet<Vector2Int>(boundaryCells);
            readOnlyOccupiedCells = null;
            readOnlyInteriorCells = null;
            readOnlyBoundaryCells = null;
            geometryReady = true;
        }

        private Vector2Int FindCellNearestBoundsCenter(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return new Vector2Int(bounds.xMin, bounds.yMin);
            int centerX2 = bounds.xMin + bounds.xMax - 1;
            int centerY2 = bounds.yMin + bounds.yMax - 1;
            Vector2Int best = cells[0];
            long bestDistance = DistanceSquaredTimesFour(best, centerX2, centerY2);
            for (int i = 1; i < cells.Count; i++)
            {
                long distance = DistanceSquaredTimesFour(cells[i], centerX2, centerY2);
                if (distance < bestDistance || (distance == bestDistance
                    && CompareCells(cells[i], best) < 0))
                {
                    best = cells[i];
                    bestDistance = distance;
                }
            }
            return best;
        }

        private long CalculateGeometrySignature()
        {
            EnsureGeometry();
            ulong hash = Offset;
            AddInt(ref hash, roomId);
            AddInt(ref hash, bounds.x);
            AddInt(ref hash, bounds.y);
            AddInt(ref hash, bounds.width);
            AddInt(ref hash, bounds.height);
            AddInt(ref hash, (int)archetype);
            AddInt(ref hash, (int)shapeType);
            AddInt(ref hash, occupiedCells.Count);
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                AddInt(ref hash, occupiedCells[i].x);
                AddInt(ref hash, occupiedCells[i].y);
            }
            return unchecked((long)hash);
        }

        private static long DistanceSquaredTimesFour(Vector2Int cell, int centerX2, int centerY2)
        {
            long dx = (long)cell.x * 2 - centerX2;
            long dy = (long)cell.y * 2 - centerY2;
            return dx * dx + dy * dy;
        }

        private static void AddRectangleCells(List<Vector2Int> cells, RectInt area)
        {
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    cells.Add(new Vector2Int(x, y));
        }

        private static int CompareCells(Vector2Int left, Vector2Int right)
        {
            int result = left.y.CompareTo(right.y);
            return result != 0 ? result : left.x.CompareTo(right.x);
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash = unchecked(hash * Prime);
            }
        }
    }
}
