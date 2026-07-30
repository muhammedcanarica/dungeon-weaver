namespace ProceduralDungeon
{
    public static class DungeonRoomArchetypePolicy
    {
        public static bool IsShapeCompatible(DungeonRoomArchetype archetype,
            DungeonRoomShapeType shape)
        {
            switch (archetype)
            {
                case DungeonRoomArchetype.Compact:
                    return shape != DungeonRoomShapeType.Cross;
                case DungeonRoomArchetype.Arena:
                    return shape == DungeonRoomShapeType.Rectangle
                        || shape == DungeonRoomShapeType.Alcove;
                default:
                    return true;
            }
        }

        public static int StartRank(DungeonRoomArchetype archetype)
        {
            switch (archetype)
            {
                case DungeonRoomArchetype.Standard: return 0;
                case DungeonRoomArchetype.Large: return 1;
                case DungeonRoomArchetype.Arena: return 2;
                default: return 3;
            }
        }
    }
}
