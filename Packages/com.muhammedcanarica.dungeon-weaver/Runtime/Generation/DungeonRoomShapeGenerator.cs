using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    internal static class DungeonRoomShapeGenerator
    {
        private const int MinimumShapeDimension = 8;
        private const int MinimumInteriorCellCount = 9;
        private const int PreferredDoorwaySpan = 5;

        public static RoomData CreateRoom(int roomId, RectInt bounds,
            DungeonRoomShapeType requestedShape, int variant)
        {
            return CreateRoom(roomId, bounds, DungeonRoomArchetype.Standard,
                requestedShape, variant, 25, MinimumInteriorCellCount);
        }

        public static RoomData CreateRoom(int roomId, RectInt bounds,
            DungeonRoomArchetype archetype, DungeonRoomShapeType requestedShape,
            int variant, int minimumOccupiedCells, int minimumInteriorCells)
        {
            if (requestedShape == DungeonRoomShapeType.Rectangle
                || bounds.width < MinimumShapeDimension || bounds.height < MinimumShapeDimension)
                return new RoomData(roomId, bounds, archetype,
                    DungeonRoomShapeType.Rectangle, null);

            var occupied = new HashSet<Vector2Int>();
            switch (requestedShape)
            {
                case DungeonRoomShapeType.LShape:
                    BuildLShape(bounds, variant, occupied);
                    break;
                case DungeonRoomShapeType.Cross:
                    BuildCross(bounds, occupied);
                    break;
                case DungeonRoomShapeType.Alcove:
                    BuildAlcove(bounds, variant, occupied);
                    break;
                default:
                    return new RoomData(roomId, bounds, archetype,
                        DungeonRoomShapeType.Rectangle, null);
            }

            var room = new RoomData(roomId, bounds, archetype, requestedShape, occupied);
            return IsUsable(room, minimumOccupiedCells, minimumInteriorCells)
                ? room : new RoomData(roomId, bounds, archetype,
                    DungeonRoomShapeType.Rectangle, null);
        }

        private static void BuildLShape(RectInt bounds, int variant,
            HashSet<Vector2Int> occupied)
        {
            AddRectangle(bounds, occupied);
            int cutWidth = Mathf.Clamp(bounds.width / 2, 2, bounds.width - 5);
            int cutHeight = Mathf.Clamp(bounds.height / 2, 2, bounds.height - 5);
            bool cutRight = (variant & 1) != 0;
            bool cutTop = (variant & 2) != 0;
            int xMin = cutRight ? bounds.xMax - cutWidth : bounds.xMin;
            int yMin = cutTop ? bounds.yMax - cutHeight : bounds.yMin;
            RemoveRectangle(new RectInt(xMin, yMin, cutWidth, cutHeight), occupied);
        }

        private static void BuildCross(RectInt bounds, HashSet<Vector2Int> occupied)
        {
            int verticalWidth = Mathf.Min(bounds.width, Mathf.Max(5, bounds.width / 2));
            int horizontalHeight = Mathf.Min(bounds.height, Mathf.Max(5, bounds.height / 2));
            int verticalX = bounds.xMin + (bounds.width - verticalWidth) / 2;
            int horizontalY = bounds.yMin + (bounds.height - horizontalHeight) / 2;
            AddRectangle(new RectInt(verticalX, bounds.yMin, verticalWidth, bounds.height), occupied);
            AddRectangle(new RectInt(bounds.xMin, horizontalY, bounds.width, horizontalHeight), occupied);
        }

        private static void BuildAlcove(RectInt bounds, int variant,
            HashSet<Vector2Int> occupied)
        {
            int orientation = PositiveModulo(variant, 4);
            bool vertical = orientation < 2;
            int depth = 2;
            if (vertical)
            {
                int extensionWidth = Mathf.Min(bounds.width, Mathf.Max(5, bounds.width / 2));
                int extensionX = bounds.xMin + (bounds.width - extensionWidth) / 2;
                bool top = orientation == 0;
                RectInt main = top
                    ? new RectInt(bounds.xMin, bounds.yMin, bounds.width, bounds.height - depth)
                    : new RectInt(bounds.xMin, bounds.yMin + depth, bounds.width, bounds.height - depth);
                RectInt extension = top
                    ? new RectInt(extensionX, bounds.yMax - depth, extensionWidth, depth)
                    : new RectInt(extensionX, bounds.yMin, extensionWidth, depth);
                AddRectangle(main, occupied);
                AddRectangle(extension, occupied);
            }
            else
            {
                int extensionHeight = Mathf.Min(bounds.height, Mathf.Max(5, bounds.height / 2));
                int extensionY = bounds.yMin + (bounds.height - extensionHeight) / 2;
                bool right = orientation == 2;
                RectInt main = right
                    ? new RectInt(bounds.xMin, bounds.yMin, bounds.width - depth, bounds.height)
                    : new RectInt(bounds.xMin + depth, bounds.yMin, bounds.width - depth, bounds.height);
                RectInt extension = right
                    ? new RectInt(bounds.xMax - depth, extensionY, depth, extensionHeight)
                    : new RectInt(bounds.xMin, extensionY, depth, extensionHeight);
                AddRectangle(main, occupied);
                AddRectangle(extension, occupied);
            }
        }

        private static bool IsUsable(RoomData room, int minimumOccupiedCells,
            int minimumInteriorCells)
        {
            if (room.OccupiedCellCount < Mathf.Max(minimumOccupiedCells,
                    room.Bounds.width * room.Bounds.height / 2)
                || room.InteriorCellCount < Mathf.Max(MinimumInteriorCellCount,
                    minimumInteriorCells)
                || !IsConnected(room.OccupiedCells)) return false;

            int usableDoorwayFaces = 0;
            for (int i = 0; i < room.BoundaryCells.Count; i++)
            {
                Vector2Int cell = room.BoundaryCells[i];
                if (!room.IsOuterBoundaryCell(cell)) continue;
                if (cell.x == room.Bounds.xMin
                    && room.CanPlaceDoorway(cell, cell + Vector2Int.left, PreferredDoorwaySpan)) usableDoorwayFaces++;
                if (cell.x == room.Bounds.xMax - 1
                    && room.CanPlaceDoorway(cell, cell + Vector2Int.right, PreferredDoorwaySpan)) usableDoorwayFaces++;
                if (cell.y == room.Bounds.yMin
                    && room.CanPlaceDoorway(cell, cell + Vector2Int.down, PreferredDoorwaySpan)) usableDoorwayFaces++;
                if (cell.y == room.Bounds.yMax - 1
                    && room.CanPlaceDoorway(cell, cell + Vector2Int.up, PreferredDoorwaySpan)) usableDoorwayFaces++;
            }
            return usableDoorwayFaces >= 2;
        }

        private static bool IsConnected(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) return false;
            var all = new HashSet<Vector2Int>(cells);
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(cells[0]);
            queue.Enqueue(cells[0]);
            Vector2Int[] directions =
            {
                Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
            };
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int next = current + directions[i];
                    if (all.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Count == all.Count;
        }

        private static void AddRectangle(RectInt area, HashSet<Vector2Int> cells)
        {
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    cells.Add(new Vector2Int(x, y));
        }

        private static void RemoveRectangle(RectInt area, HashSet<Vector2Int> cells)
        {
            for (int y = area.yMin; y < area.yMax; y++)
                for (int x = area.xMin; x < area.xMax; x++)
                    cells.Remove(new Vector2Int(x, y));
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
