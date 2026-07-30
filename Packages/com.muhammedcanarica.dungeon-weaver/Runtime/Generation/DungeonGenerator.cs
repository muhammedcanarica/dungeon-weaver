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
        [SerializeField, HideInInspector] private Vector2Int minRoomSize = new Vector2Int(8, 8);
        [SerializeField, HideInInspector] private Vector2Int maxRoomSize = new Vector2Int(14, 12);
        [SerializeField] private int minimumRoomSpacing = 2;
        [SerializeField] private int maxPlacementAttempts = 1500;

        [Header("Room Archetype Profiles")]
        [SerializeField] private RoomArchetypeSettings compactProfile =
            RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Compact);
        [SerializeField] private RoomArchetypeSettings standardProfile =
            RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Standard);
        [SerializeField] private RoomArchetypeSettings largeProfile =
            RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Large);
        [SerializeField] private RoomArchetypeSettings arenaProfile =
            RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Arena);

        [Header("Room Shape Variety")]
        [SerializeField, Min(0)] private int rectangleWeight = 4;
        [SerializeField, Min(0)] private int lShapeWeight = 3;
        [SerializeField, Min(0)] private int crossShapeWeight = 2;
        [SerializeField, Min(0)] private int alcoveWeight = 2;

        [SerializeField, HideInInspector]
        private List<RoomData> rooms = new List<RoomData>();
        [SerializeField, HideInInspector] private int lastPlacementAttempts;

        public int Seed => seed;
        public int TargetRoomCount => targetRoomCount;
        public RectInt GenerationArea => generationArea;
        public Vector2Int MinRoomSize => HasAllProfiles
            ? new Vector2Int(Mathf.Min(compactProfile.MinimumSize.x,
                    standardProfile.MinimumSize.x, largeProfile.MinimumSize.x,
                    arenaProfile.MinimumSize.x),
                Mathf.Min(compactProfile.MinimumSize.y, standardProfile.MinimumSize.y,
                    largeProfile.MinimumSize.y, arenaProfile.MinimumSize.y))
            : minRoomSize;
        public Vector2Int MaxRoomSize => HasAllProfiles
            ? new Vector2Int(Mathf.Max(compactProfile.MaximumSize.x,
                    standardProfile.MaximumSize.x, largeProfile.MaximumSize.x,
                    arenaProfile.MaximumSize.x),
                Mathf.Max(compactProfile.MaximumSize.y, standardProfile.MaximumSize.y,
                    largeProfile.MaximumSize.y, arenaProfile.MaximumSize.y))
            : maxRoomSize;
        public int MinimumRoomSpacing => minimumRoomSpacing;
        public int MaxPlacementAttempts => maxPlacementAttempts;
        public int RectangleWeight => Mathf.Max(0, rectangleWeight);
        public int LShapeWeight => Mathf.Max(0, lShapeWeight);
        public int CrossShapeWeight => Mathf.Max(0, crossShapeWeight);
        public int AlcoveWeight => Mathf.Max(0, alcoveWeight);
        public RoomArchetypeSettings CompactProfile => GetArchetypeSettings(DungeonRoomArchetype.Compact);
        public RoomArchetypeSettings StandardProfile => GetArchetypeSettings(DungeonRoomArchetype.Standard);
        public RoomArchetypeSettings LargeProfile => GetArchetypeSettings(DungeonRoomArchetype.Large);
        public RoomArchetypeSettings ArenaProfile => GetArchetypeSettings(DungeonRoomArchetype.Arena);
        public int LastPlacementAttempts => lastPlacementAttempts;
        public IReadOnlyList<RoomData> Rooms => rooms;
        public long RoomLayoutSignature => CalculateRoomLayoutSignature();
        private bool HasAllProfiles => compactProfile != null && standardProfile != null
            && largeProfile != null && arenaProfile != null;

        public void SetSeed(int newSeed)
        {
            seed = newSeed;
        }

        [ContextMenu("Generate Dungeon")]
        public void Generate()
        {
            EnsureArchetypeSettings();
            if (!TryValidateSettings(out string validationError))
            {
                Debug.LogError($"[DungeonGenerator] Generation cancelled: {validationError}", this);
                return;
            }

            EnsureRoomList();
            rooms.Clear();
            lastPlacementAttempts = 0;

            var random = new System.Random(seed);
            int attemptsUsed = 0;
            int failedAttemptsForCurrentRoom = 0;

            while (rooms.Count < targetRoomCount && attemptsUsed < maxPlacementAttempts)
            {
                attemptsUsed++;
                DungeonRoomArchetype archetype = SelectPlacementArchetype(
                    rooms.Count, failedAttemptsForCurrentRoom);
                RoomArchetypeSettings profile = GetArchetypeSettings(archetype);
                int effectiveMaxWidth = Math.Min(profile.MaximumSize.x, generationArea.width);
                int effectiveMaxHeight = Math.Min(profile.MaximumSize.y, generationArea.height);
                int width = random.Next(profile.MinimumSize.x, effectiveMaxWidth + 1);
                int height = random.Next(profile.MinimumSize.y, effectiveMaxHeight + 1);
                int x = random.Next(generationArea.xMin, generationArea.xMax - width + 1);
                int y = random.Next(generationArea.yMin, generationArea.yMax - height + 1);

                var candidate = new RectInt(x, y, width, height);

                if (!IsInsideGenerationArea(candidate) || OverlapsExistingRoom(candidate))
                {
                    failedAttemptsForCurrentRoom++;
                    continue;
                }

                rooms.Add(CreateRoom(rooms.Count, candidate, archetype, profile));
                failedAttemptsForCurrentRoom = 0;
            }
            lastPlacementAttempts = attemptsUsed;

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

        public RoomArchetypeSettings GetArchetypeSettings(DungeonRoomArchetype archetype)
        {
            EnsureArchetypeSettings();
            switch (archetype)
            {
                case DungeonRoomArchetype.Compact: return compactProfile;
                case DungeonRoomArchetype.Large: return largeProfile;
                case DungeonRoomArchetype.Arena: return arenaProfile;
                default: return standardProfile;
            }
        }

        private RoomData CreateRoom(int roomId, RectInt roomBounds,
            DungeonRoomArchetype archetype, RoomArchetypeSettings profile)
        {
            int rectangle = CompatibleWeight(archetype, DungeonRoomShapeType.Rectangle, rectangleWeight);
            int lShape = CompatibleWeight(archetype, DungeonRoomShapeType.LShape, lShapeWeight);
            int cross = CompatibleWeight(archetype, DungeonRoomShapeType.Cross, crossShapeWeight);
            int alcove = CompatibleWeight(archetype, DungeonRoomShapeType.Alcove, alcoveWeight);
            int total = rectangle + lShape + cross + alcove;
            if (total <= 0)
                return new RoomData(roomId, roomBounds, archetype,
                    DungeonRoomShapeType.Rectangle, null);

            var shapeRandom = new System.Random(DeriveShapeSeed(seed, roomId, roomBounds));
            int roll = shapeRandom.Next(total);
            DungeonRoomShapeType shape;
            if (roll < rectangle) shape = DungeonRoomShapeType.Rectangle;
            else if ((roll -= rectangle) < lShape) shape = DungeonRoomShapeType.LShape;
            else if ((roll -= lShape) < cross) shape = DungeonRoomShapeType.Cross;
            else shape = DungeonRoomShapeType.Alcove;
            return DungeonRoomShapeGenerator.CreateRoom(roomId, roomBounds, archetype,
                shape, shapeRandom.Next(), profile.MinimumUsableFloorArea,
                profile.MinimumInteriorArea);
        }

        private DungeonRoomArchetype SelectArchetype(int roomId)
        {
            if (roomId == 0) return DungeonRoomArchetype.Standard;
            if (roomId == 1 && targetRoomCount > 1) return DungeonRoomArchetype.Arena;

            int compact = compactProfile.SelectionWeight;
            int standard = standardProfile.SelectionWeight;
            int large = largeProfile.SelectionWeight;
            int arena = arenaProfile.SelectionWeight;
            int total = compact + standard + large + arena;
            if (total <= 0) return DungeonRoomArchetype.Standard;

            var random = new System.Random(DeriveArchetypeSeed(seed, roomId));
            int roll = random.Next(total);
            if (roll < compact) return DungeonRoomArchetype.Compact;
            if ((roll -= compact) < standard) return DungeonRoomArchetype.Standard;
            if ((roll -= standard) < large) return DungeonRoomArchetype.Large;
            return DungeonRoomArchetype.Arena;
        }

        private DungeonRoomArchetype SelectPlacementArchetype(int roomId,
            int failedAttempts)
        {
            DungeonRoomArchetype selected = SelectArchetype(roomId);
            if (roomId <= 1) return selected;
            if (failedAttempts >= 160 && selected == DungeonRoomArchetype.Standard)
                return DungeonRoomArchetype.Compact;
            if (failedAttempts >= 80 && (selected == DungeonRoomArchetype.Large
                || selected == DungeonRoomArchetype.Arena))
                return DungeonRoomArchetype.Standard;
            return selected;
        }

        private static int CompatibleWeight(DungeonRoomArchetype archetype,
            DungeonRoomShapeType shape, int configuredWeight)
        {
            return DungeonRoomArchetypePolicy.IsShapeCompatible(archetype, shape)
                ? Mathf.Clamp(configuredWeight, 0, 1000000) : 0;
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
            AddProfile(ref hash, compactProfile);
            AddProfile(ref hash, standardProfile);
            AddProfile(ref hash, largeProfile);
            AddProfile(ref hash, arenaProfile);
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

        private static int DeriveArchetypeSeed(int dungeonSeed, int roomId)
        {
            unchecked
            {
                int hash = dungeonSeed * 1103515245 + roomId * 486187739;
                return hash ^ (int)0x41C64E6D;
            }
        }

        private static void AddProfile(ref ulong hash, RoomArchetypeSettings profile)
        {
            if (profile == null) { AddInt(ref hash, int.MinValue); return; }
            AddInt(ref hash, (int)profile.Archetype);
            AddInt(ref hash, profile.SelectionWeight);
            AddInt(ref hash, profile.MinimumSize.x);
            AddInt(ref hash, profile.MinimumSize.y);
            AddInt(ref hash, profile.MaximumSize.x);
            AddInt(ref hash, profile.MaximumSize.y);
            AddInt(ref hash, profile.MinimumUsableFloorArea);
            AddInt(ref hash, profile.MinimumInteriorArea);
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

            if (!TryValidateProfile(compactProfile, out validationError)
                || !TryValidateProfile(standardProfile, out validationError)
                || !TryValidateProfile(largeProfile, out validationError)
                || !TryValidateProfile(arenaProfile, out validationError)) return false;

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

        private bool TryValidateProfile(RoomArchetypeSettings profile, out string error)
        {
            if (profile == null)
            {
                error = "Room archetype profile is missing.";
                return false;
            }
            Vector2Int minimum = profile.MinimumSize;
            Vector2Int maximum = profile.MaximumSize;
            if (minimum.x <= 0 || minimum.y <= 0 || maximum.x < minimum.x
                || maximum.y < minimum.y)
            {
                error = $"{profile.Archetype} profile has an invalid size range.";
                return false;
            }
            if (minimum.x > generationArea.width || minimum.y > generationArea.height)
            {
                error = $"{profile.Archetype} profile does not fit inside the generation area.";
                return false;
            }
            long minimumFloor = (long)minimum.x * minimum.y;
            long minimumInterior = (long)Mathf.Max(0, minimum.x - 2)
                * Mathf.Max(0, minimum.y - 2);
            if (profile.MinimumUsableFloorArea > minimumFloor
                || profile.MinimumInteriorArea > minimumInterior)
            {
                error = $"{profile.Archetype} minimum area requirements exceed its minimum rectangle size.";
                return false;
            }
            error = string.Empty;
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

        private void EnsureArchetypeSettings()
        {
            compactProfile ??= RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Compact);
            standardProfile ??= RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Standard);
            largeProfile ??= RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Large);
            arenaProfile ??= RoomArchetypeSettings.CreateDefault(DungeonRoomArchetype.Arena);
            compactProfile.SetArchetype(DungeonRoomArchetype.Compact);
            standardProfile.SetArchetype(DungeonRoomArchetype.Standard);
            largeProfile.SetArchetype(DungeonRoomArchetype.Large);
            arenaProfile.SetArchetype(DungeonRoomArchetype.Arena);
        }
    }
}
