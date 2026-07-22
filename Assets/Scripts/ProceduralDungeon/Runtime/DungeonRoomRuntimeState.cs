namespace ProceduralDungeon
{
    public enum DungeonRoomRole
    {
        Normal,
        Start,
        Boss
    }

    public enum RoomExplorationState
    {
        Unvisited,
        Active,
        Visited
    }

    public sealed class DungeonRoomRuntimeState
    {
        public int RoomId { get; }
        public DungeonRoomRole RoomRole { get; }
        public RoomExplorationState ExplorationState { get; private set; }
        public int VisitCount { get; private set; }
        public int FirstVisitOrder { get; private set; }
        public int LastEnteredConnectionIndex { get; private set; }
        public int LastExitedConnectionIndex { get; private set; }

        internal DungeonRoomRuntimeState(int roomId, DungeonRoomRole roomRole, int invalidId)
        {
            RoomId = roomId;
            RoomRole = roomRole;
            ExplorationState = RoomExplorationState.Unvisited;
            FirstVisitOrder = invalidId;
            LastEnteredConnectionIndex = invalidId;
            LastExitedConnectionIndex = invalidId;
        }

        internal void Enter(int connectionIndex, int firstVisitOrder)
        {
            VisitCount++;
            if (FirstVisitOrder < 0) FirstVisitOrder = firstVisitOrder;
            LastEnteredConnectionIndex = connectionIndex;
            ExplorationState = RoomExplorationState.Active;
        }

        internal void Exit(int connectionIndex)
        {
            LastExitedConnectionIndex = connectionIndex;
            ExplorationState = RoomExplorationState.Visited;
        }
    }

    public readonly struct DungeonRoomStateChange
    {
        public int RoomId { get; }
        public RoomExplorationState PreviousState { get; }
        public RoomExplorationState CurrentState { get; }
        public int VisitCount { get; }
        public int ConnectionIndex { get; }
        public UnityEngine.Vector2Int Cell { get; }
        public bool IsFirstDiscovery { get; }

        public DungeonRoomStateChange(int roomId, RoomExplorationState previousState,
            RoomExplorationState currentState, int visitCount, int connectionIndex,
            UnityEngine.Vector2Int cell, bool isFirstDiscovery)
        {
            RoomId = roomId;
            PreviousState = previousState;
            CurrentState = currentState;
            VisitCount = visitCount;
            ConnectionIndex = connectionIndex;
            Cell = cell;
            IsFirstDiscovery = isFirstDiscovery;
        }
    }

    public readonly struct DungeonActiveRoomChange
    {
        public int PreviousRoomId { get; }
        public int CurrentRoomId { get; }
        public int ConnectionIndex { get; }
        public UnityEngine.Vector2Int Cell { get; }

        public DungeonActiveRoomChange(int previousRoomId, int currentRoomId,
            int connectionIndex, UnityEngine.Vector2Int cell)
        {
            PreviousRoomId = previousRoomId;
            CurrentRoomId = currentRoomId;
            ConnectionIndex = connectionIndex;
            Cell = cell;
        }
    }
}
