using System;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum DungeonRoomArchetype
    {
        Standard,
        Compact,
        Large,
        Arena
    }

    [Serializable]
    public sealed class RoomArchetypeSettings
    {
        [SerializeField, HideInInspector] private DungeonRoomArchetype archetype;
        [SerializeField, Min(0)] private int selectionWeight;
        [SerializeField] private Vector2Int minimumSize;
        [SerializeField] private Vector2Int maximumSize;
        [SerializeField, Min(1)] private int minimumUsableFloorArea;
        [SerializeField, Min(1)] private int minimumInteriorArea;

        public DungeonRoomArchetype Archetype => archetype;
        public int SelectionWeight => Mathf.Clamp(selectionWeight, 0, 1000000);
        public Vector2Int MinimumSize => minimumSize;
        public Vector2Int MaximumSize => maximumSize;
        public int MinimumUsableFloorArea => Mathf.Max(1, minimumUsableFloorArea);
        public int MinimumInteriorArea => Mathf.Max(1, minimumInteriorArea);

        public bool ContainsSize(int width, int height)
        {
            return width >= minimumSize.x && width <= maximumSize.x
                && height >= minimumSize.y && height <= maximumSize.y;
        }

        internal void SetArchetype(DungeonRoomArchetype value)
        {
            archetype = value;
        }

        internal static RoomArchetypeSettings CreateDefault(DungeonRoomArchetype archetype)
        {
            switch (archetype)
            {
                case DungeonRoomArchetype.Compact:
                    return Create(archetype, 4, new Vector2Int(8, 8),
                        new Vector2Int(9, 9), 48, 20);
                case DungeonRoomArchetype.Large:
                    return Create(archetype, 2, new Vector2Int(11, 10),
                        new Vector2Int(14, 12), 90, 40);
                case DungeonRoomArchetype.Arena:
                    return Create(archetype, 0, new Vector2Int(13, 11),
                        new Vector2Int(16, 13), 130, 75);
                default:
                    return Create(DungeonRoomArchetype.Standard, 8,
                        new Vector2Int(9, 8), new Vector2Int(12, 10), 60, 24);
            }
        }

        private static RoomArchetypeSettings Create(DungeonRoomArchetype archetype,
            int weight, Vector2Int minimum, Vector2Int maximum,
            int minimumFloorArea, int minimumInteriorArea)
        {
            return new RoomArchetypeSettings
            {
                archetype = archetype,
                selectionWeight = weight,
                minimumSize = minimum,
                maximumSize = maximum,
                minimumUsableFloorArea = minimumFloorArea,
                minimumInteriorArea = minimumInteriorArea
            };
        }
    }
}
