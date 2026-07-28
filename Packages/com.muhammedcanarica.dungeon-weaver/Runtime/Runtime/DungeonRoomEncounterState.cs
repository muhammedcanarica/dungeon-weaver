namespace ProceduralDungeon
{
    public enum RoomEncounterState
    {
        Inactive,
        Locked,
        Cleared
    }

    public sealed class DungeonRoomEncounterRuntimeState
    {
        public int RoomId { get; }
        public RoomEncounterState State { get; private set; }

        internal DungeonRoomEncounterRuntimeState(int roomId, RoomEncounterState state)
        {
            RoomId = roomId;
            State = state;
        }

        internal void SetState(RoomEncounterState state)
        {
            State = state;
        }
    }

    public readonly struct DungeonRoomEncounterChange
    {
        public int RoomId { get; }
        public RoomEncounterState PreviousState { get; }
        public RoomEncounterState CurrentState { get; }

        public DungeonRoomEncounterChange(int roomId, RoomEncounterState previousState,
            RoomEncounterState currentState)
        {
            RoomId = roomId;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }
}
