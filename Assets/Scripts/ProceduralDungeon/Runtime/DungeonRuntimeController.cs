using System;
using UnityEngine;

namespace ProceduralDungeon
{
    [RequireComponent(typeof(DungeonGenerator), typeof(DungeonGraphBuilder), typeof(DungeonCorridorBuilder))]
    [RequireComponent(typeof(DungeonDoorwayBuilder))]
    [RequireComponent(typeof(DungeonAreaTracker))]
    [RequireComponent(typeof(DungeonRoomStateController))]
    [RequireComponent(typeof(DungeonTilemapRenderer), typeof(DungeonRoomRoleAssigner), typeof(DungeonPlayerSpawner))]
    public sealed class DungeonRuntimeController : MonoBehaviour
    {
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private int defaultSeed = 12345;
        [SerializeField] private TopDownPlayerController playerController;
        [SerializeField] private TopDownCameraFollow cameraFollow;
        [SerializeField] private bool logPipelineSteps = true;

        private DungeonGenerator generator;
        private DungeonGraphBuilder graphBuilder;
        private DungeonCorridorBuilder corridorBuilder;
        private DungeonDoorwayBuilder doorwayBuilder;
        private DungeonAreaTracker areaTracker;
        private DungeonRoomStateController roomStateController;
        private DungeonTilemapRenderer tilemapRenderer;
        private DungeonRoomRoleAssigner roleAssigner;
        private DungeonPlayerSpawner playerSpawner;
        private bool isGenerating;
        private int lastGeneratedSeed;
        private bool hasSuccessfulDungeon;
        private string lastStatusMessage = "Ready";

        public bool IsGenerating => isGenerating;
        public int LastGeneratedSeed => lastGeneratedSeed;
        public bool HasSuccessfulDungeon => hasSuccessfulDungeon;
        public string LastStatusMessage => lastStatusMessage;
        public int DefaultSeed => defaultSeed;
        public event Action<string> StatusChanged;

        private void Awake()
        {
            CacheComponents();
        }

        private void Start()
        {
            if (generateOnStart) GenerateDungeon(defaultSeed);
        }

        public bool GenerateDungeon(int requestedSeed)
        {
            if (isGenerating)
            {
                SetStatus("Generation request rejected: a dungeon is already being generated.");
                Debug.LogWarning($"[DungeonRuntimeController] {lastStatusMessage}", this);
                return false;
            }

            isGenerating = true;
            hasSuccessfulDungeon = false;
            SetStatus($"Generating seed {requestedSeed}...");

            try
            {
                CacheComponents();
                if (!HasRequiredReferences()) return FailGeneration("dependency validation");

                LogStep($"Generating seed {requestedSeed}.");
                SetPlayerMovement(false);
                StopPlayerBody();

                areaTracker.ClearTracking();
                roomStateController.ClearStates();
                tilemapRenderer.ClearDungeonTilemap();
                roleAssigner.ClearRoomRoles();
                doorwayBuilder.ClearDoorways();
                corridorBuilder.ClearCorridors();
                graphBuilder.ClearGraph();
                generator.Clear();

                generator.SetSeed(requestedSeed);
                generator.Generate();
                if (generator.Rooms == null || generator.Rooms.Count == 0)
                    return FailGeneration("room generation");
                LogStep($"Generated {generator.Rooms.Count} rooms.");

                graphBuilder.BuildGraph();
                if (!graphBuilder.IsGraphCurrent) return FailGeneration("graph building");
                LogStep($"Built {graphBuilder.Connections.Count} graph connections.");

                corridorBuilder.BuildCorridors();
                if (!corridorBuilder.IsCorridorDataCurrent) return FailGeneration("corridor building");
                LogStep($"Built {corridorBuilder.Corridors.Count} corridors.");

                doorwayBuilder.BuildDoorways();
                if (!doorwayBuilder.IsDoorwayDataCurrent) return FailGeneration("doorway building");
                LogStep($"Built {doorwayBuilder.Doorways.Count} doorways.");

                tilemapRenderer.RenderDungeonTilemap();
                if (!tilemapRenderer.IsTilemapCurrent) return FailGeneration("Tilemap rendering");
                LogStep($"Rendered {tilemapRenderer.FloorCellCount} floor and {tilemapRenderer.WallCellCount} wall cells.");

                roleAssigner.AssignRoomRoles();
                if (!roleAssigner.IsRoleDataCurrent) return FailGeneration("room role assignment");
                LogStep($"Assigned start room {roleAssigner.StartRoomId} and boss room {roleAssigner.BossRoomId}.");

                if (!areaTracker.BuildLookup() || !areaTracker.IsLookupCurrent)
                    return FailGeneration("area lookup building");
                LogStep($"Built area lookup for {areaTracker.TotalLookupCellCount} cells.");

                if (!roomStateController.InitializeStates()
                    || !roomStateController.HasRuntimeStates
                    || roomStateController.TotalRoomCount != generator.Rooms.Count)
                    return FailGeneration("room state initialization");
                LogStep($"Initialized runtime state for {roomStateController.TotalRoomCount} rooms.");

                if (!playerSpawner.TryPlacePlayerAtStart()) return FailGeneration("player spawning");
                cameraFollow.RefreshDungeonBounds();
                if (!areaTracker.RefreshPlayerArea()
                    || !areaTracker.IsInsideRoom
                    || areaTracker.CurrentRoomId != roleAssigner.StartRoomId)
                    return FailGeneration("initial area tracking");
                if (!ValidateInitialRoomState() || !roomStateController.CaptureInitialStateSignature())
                    return FailGeneration("initial room state validation");
                LogStep($"Captured initial room state signature {roomStateController.InitialStateSignature}.");
                SetPlayerMovement(true);

                lastGeneratedSeed = requestedSeed;
                hasSuccessfulDungeon = true;
                SetStatus($"Dungeon generated with seed {requestedSeed}.");
                Debug.Log($"[DungeonRuntimeController] {lastStatusMessage}", this);
                return true;
            }
            catch (Exception exception)
            {
                return FailGeneration("an unexpected runtime exception", exception);
            }
            finally
            {
                isGenerating = false;
                StatusChanged?.Invoke(lastStatusMessage);
            }
        }

        public bool RegenerateCurrentSeed()
        {
            if (!hasSuccessfulDungeon)
            {
                SetStatus("Regeneration rejected: no successful dungeon exists.");
                Debug.LogWarning($"[DungeonRuntimeController] {lastStatusMessage}", this);
                return false;
            }

            return GenerateDungeon(lastGeneratedSeed);
        }

        public void ClearRuntimeDungeon()
        {
            if (isGenerating)
            {
                SetStatus("Clear request rejected while generation is running.");
                Debug.LogWarning($"[DungeonRuntimeController] {lastStatusMessage}", this);
                return;
            }

            CacheComponents();
            SetPlayerMovement(false);
            StopPlayerBody();
            ClearGeneratedData();
            hasSuccessfulDungeon = false;
            SetStatus("Runtime dungeon cleared.");
            Debug.Log($"[DungeonRuntimeController] {lastStatusMessage}", this);
        }

        private bool FailGeneration(string stage, Exception exception = null)
        {
            SetPlayerMovement(false);
            StopPlayerBody();
            try
            {
                ClearGeneratedData();
            }
            catch (Exception cleanupException)
            {
                Debug.LogError($"[DungeonRuntimeController] Cleanup also failed: {cleanupException.Message}", this);
            }

            hasSuccessfulDungeon = false;
            SetStatus($"Dungeon generation failed during {stage}.");
            if (exception == null) Debug.LogError($"[DungeonRuntimeController] {lastStatusMessage}", this);
            else Debug.LogError($"[DungeonRuntimeController] {lastStatusMessage} {exception.Message}", this);
            return false;
        }

        private void ClearGeneratedData()
        {
            areaTracker?.ClearTracking();
            roomStateController?.ClearStates();
            tilemapRenderer?.ClearDungeonTilemap();
            roleAssigner?.ClearRoomRoles();
            doorwayBuilder?.ClearDoorways();
            corridorBuilder?.ClearCorridors();
            graphBuilder?.ClearGraph();
            generator?.Clear();
            cameraFollow?.InvalidateDungeonBounds();
        }

        private void SetPlayerMovement(bool enabled)
        {
            if (playerController != null) playerController.enabled = enabled;
        }

        private void StopPlayerBody()
        {
            if (playerController == null) return;
            Rigidbody2D body = playerController.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void CacheComponents()
        {
            if (generator == null) generator = GetComponent<DungeonGenerator>();
            if (graphBuilder == null) graphBuilder = GetComponent<DungeonGraphBuilder>();
            if (corridorBuilder == null) corridorBuilder = GetComponent<DungeonCorridorBuilder>();
            if (doorwayBuilder == null) doorwayBuilder = GetComponent<DungeonDoorwayBuilder>();
            if (areaTracker == null) areaTracker = GetComponent<DungeonAreaTracker>();
            if (roomStateController == null) roomStateController = GetComponent<DungeonRoomStateController>();
            if (tilemapRenderer == null) tilemapRenderer = GetComponent<DungeonTilemapRenderer>();
            if (roleAssigner == null) roleAssigner = GetComponent<DungeonRoomRoleAssigner>();
            if (playerSpawner == null) playerSpawner = GetComponent<DungeonPlayerSpawner>();
        }

        private bool HasRequiredReferences()
        {
            return generator != null && graphBuilder != null && corridorBuilder != null && doorwayBuilder != null
                && areaTracker != null && roomStateController != null
                && tilemapRenderer != null && roleAssigner != null && playerSpawner != null
                && playerController != null && cameraFollow != null;
        }

        private bool ValidateInitialRoomState()
        {
            if (roomStateController.CurrentActiveRoomId != roleAssigner.StartRoomId
                || roomStateController.DiscoveredRoomCount != 1
                || !roomStateController.TryGetRoomState(roleAssigner.StartRoomId,
                    out DungeonRoomRuntimeState startState))
                return false;
            return startState.RoomRole == DungeonRoomRole.Start
                && startState.ExplorationState == RoomExplorationState.Active
                && startState.VisitCount == 1
                && startState.FirstVisitOrder == 1;
        }

        private void LogStep(string message)
        {
            if (logPipelineSteps) Debug.Log($"[DungeonRuntimeController] {message}", this);
        }

        private void SetStatus(string message)
        {
            lastStatusMessage = message;
            StatusChanged?.Invoke(lastStatusMessage);
        }
    }
}
