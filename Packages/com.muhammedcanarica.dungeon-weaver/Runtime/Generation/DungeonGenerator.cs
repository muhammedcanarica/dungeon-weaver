using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public sealed class DungeonGenerator : MonoBehaviour
    {
        [Header("Determinism")]
        [SerializeField] private int seed = 12345;

        [Header("Room Generation")]
        [SerializeField] private int targetRoomCount = 15;
        [SerializeField] private RectInt generationArea = new RectInt(-40, -25, 80, 50);
        [SerializeField] private Vector2Int minRoomSize = new Vector2Int(8, 8);
        [SerializeField] private Vector2Int maxRoomSize = new Vector2Int(14, 12);
        [SerializeField] private int minimumRoomSpacing = 2;
        [SerializeField] private int maxPlacementAttempts = 1500;

        [Header("Room Shape Variety")]
        [SerializeField, Min(0)] private int rectangleWeight = 4;
        [SerializeField, Min(0)] private int lShapeWeight = 3;
        [SerializeField, Min(0)] private int crossShapeWeight = 2;
        [SerializeField, Min(0)] private int alcoveWeight = 2;

        [SerializeField, HideInInspector]
        private List<RoomData> rooms = new List<RoomData>();

        public int Seed => seed;
        public int TargetRoomCount => targetRoomCount;
        public RectInt GenerationArea => generationArea;
        public Vector2Int MinRoomSize => minRoomSize;
        public Vector2Int MaxRoomSize => maxRoomSize;
        public int MinimumRoomSpacing => minimumRoomSpacing;
        public int MaxPlacementAttempts => maxPlacementAttempts;
        public int RectangleWeight => Mathf.Max(0, rectangleWeight);
        public int LShapeWeight => Mathf.Max(0, lShapeWeight);
        public int CrossShapeWeight => Mathf.Max(0, crossShapeWeight);
        public int AlcoveWeight => Mathf.Max(0, alcoveWeight);
        public IReadOnlyList<RoomData> Rooms => rooms;
        public long RoomLayoutSignature => CalculateRoomLayoutSignature();

        public void SetSeed(int newSeed)
        {
            seed = newSeed;
        }

        [ContextMenu("Generate Dungeon")]
        public void Generate()
        {
            if (!TryValidateSettings(out string validationError))
            {
                Debug.LogError($"[DungeonGenerator] Generation cancelled: {validationError}", this);
                return;
            }

            EnsureRoomList();
            rooms.Clear();

            int effectiveMaxWidth = Math.Min(maxRoomSize.x, generationArea.width);
            int effectiveMaxHeight = Math.Min(maxRoomSize.y, generationArea.height);

            // The generation area is a hard physical limit, so oversized configured
            // maximums are capped locally without changing the Inspector settings.
            if (effectiveMaxWidth < minRoomSize.x || effectiveMaxHeight < minRoomSize.y)
            {
                Debug.LogWarning(
                    $"[DungeonGenerator] Generated 0/{targetRoomCount} rooms in 0 attempts with seed {seed}. " +
                    "The minimum room size does not fit inside the generation area.",
                    this);
                return;
            }

            var random = new System.Random(seed);
            int attemptsUsed = 0;

            while (rooms.Count < targetRoomCount && attemptsUsed < maxPlacementAttempts)
            {
                attemptsUsed++;

                int width = random.Next(minRoomSize.x, effectiveMaxWidth + 1);
                int height = random.Next(minRoomSize.y, effectiveMaxHeight + 1);
                int x = random.Next(generationArea.xMin, generationArea.xMax - width + 1);
                int y = random.Next(generationArea.yMin, generationArea.yMax - height + 1);

                var candidate = new RectInt(x, y, width, height);

                if (!IsInsideGenerationArea(candidate) || OverlapsExistingRoom(candidate))
                {
                    continue;
                }

                rooms.Add(CreateRoom(rooms.Count, candidate));
            }

            string summary =
                $"[DungeonGenerator] Generated {rooms.Count}/{targetRoomCount} rooms " +
                $"in {attemptsUsed} attempts with seed {seed}.";

            if (rooms.Count < targetRoomCount)
            {
                Debug.LogWarning(summary + " The placement budget was exhausted before reaching the target.", this);
            }
            else
            {
                Debug.Log(summary, this);
            }
        }

        [ContextMenu("Clear Dungeon")]
        public void Clear()
        {
            EnsureRoomList();
            rooms.Clear();
        }

        private RoomData CreateRoom(int roomId, RectInt roomBounds)
        {
            int rectangle = Mathf.Clamp(rectangleWeight, 0, 1000000);
            int lShape = Mathf.Clamp(lShapeWeight, 0, 1000000);
            int cross = Mathf.Clamp(crossShapeWeight, 0, 1000000);
            int alcove = Mathf.Clamp(alcoveWeight, 0, 1000000);
            int total = rectangle + lShape + cross + alcove;
            if (total <= 0) return new RoomData(roomId, roomBounds);

            var shapeRandom = new System.Random(DeriveShapeSeed(seed, roomId, roomBounds));
            int roll = shapeRandom.Next(total);
            DungeonRoomShapeType shape;
            if (roll < rectangle) shape = DungeonRoomShapeType.Rectangle;
            else if ((roll -= rectangle) < lShape) shape = DungeonRoomShapeType.LShape;
            else if ((roll -= lShape) < cross) shape = DungeonRoomShapeType.Cross;
            else shape = DungeonRoomShapeType.Alcove;
            return DungeonRoomShapeGenerator.CreateRoom(roomId, roomBounds, shape, shapeRandom.Next());
        }

        private long CalculateRoomLayoutSignature()
        {
            const ulong offset = 14695981039346656037UL;
            ulong hash = offset;
            AddInt(ref hash, seed);
            AddInt(ref hash, RectangleWeight);
            AddInt(ref hash, LShapeWeight);
            AddInt(ref hash, CrossShapeWeight);
            AddInt(ref hash, AlcoveWeight);
            AddInt(ref hash, rooms?.Count ?? 0);
            if (rooms != null)
                for (int i = 0; i < rooms.Count; i++) AddLong(ref hash,
                    rooms[i] != null ? rooms[i].GeometrySignature : long.MinValue);
            return unchecked((long)hash);
        }

        private static int DeriveShapeSeed(int dungeonSeed, int roomId, RectInt roomBounds)
        {
            unchecked
            {
                int hash = dungeonSeed * 486187739 + roomId * 16777619 + (int)0x6D2B79F5;
                hash = hash * 397 ^ roomBounds.x;
                hash = hash * 397 ^ roomBounds.y;
                hash = hash * 397 ^ roomBounds.width;
                return hash * 397 ^ roomBounds.height;
            }
        }

        private static void AddInt(ref ulong hash, int value)
        {
            uint bits = unchecked((uint)value);
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash = unchecked(hash * 1099511628211UL);
            }
        }

        private static void AddLong(ref ulong hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash = unchecked(hash * 1099511628211UL);
            }
        }

        private bool TryValidateSettings(out string validationError)
        {
            if (targetRoomCount < 0)
            {
                validationError = "Target Room Count cannot be negative.";
                return false;
            }

            if (maxPlacementAttempts < 0)
            {
                validationError = "Max Placement Attempts cannot be negative.";
                return false;
            }

            if (minimumRoomSpacing < 0)
            {
                validationError = "Minimum Room Spacing cannot be negative.";
                return false;
            }

            if (generationArea.width <= 0 || generationArea.height <= 0)
            {
                validationError = "Generation Area width and height must both be positive.";
                return false;
            }

            if (minRoomSize.x <= 0 || minRoomSize.y <= 0)
            {
                validationError = "Min Room Size width and height must both be positive.";
                return false;
            }

            if (maxRoomSize.x <= 0 || maxRoomSize.y <= 0)
            {
                validationError = "Max Room Size width and height must both be positive.";
                return false;
            }

            if (minRoomSize.x > maxRoomSize.x || minRoomSize.y > maxRoomSize.y)
            {
                validationError = "Min Room Size cannot be greater than Max Room Size on either axis.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private bool IsInsideGenerationArea(RectInt candidate)
        {
            return candidate.xMin >= generationArea.xMin
                && candidate.xMax <= generationArea.xMax
                && candidate.yMin >= generationArea.yMin
                && candidate.yMax <= generationArea.yMax;
        }

        private bool OverlapsExistingRoom(RectInt candidate)
        {
            long spacing = minimumRoomSpacing;

            for (int i = 0; i < rooms.Count; i++)
            {
                RectInt existing = rooms[i].Bounds;

                bool separated =
                    (long)candidate.xMin >= (long)existing.xMax + spacing
                    || (long)candidate.xMax + spacing <= existing.xMin
                    || (long)candidate.yMin >= (long)existing.yMax + spacing
                    || (long)candidate.yMax + spacing <= existing.yMin;

                if (!separated)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureRoomList()
        {
            if (rooms == null)
            {
                rooms = new List<RoomData>();
            }
        }
    }
}
