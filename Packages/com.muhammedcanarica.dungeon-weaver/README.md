# Dungeon Weaver

Dungeon Weaver is a seed-based, deterministic, reusable 2D procedural dungeon generation and runtime package for Unity 6. It generates rooms, a connected graph, corridors, doorway records, Tilemap output, room exploration state, physical doors, and a generic encounter lifecycle.

## Requirements

- Unity 6.0 or newer
- Unity 2D Tilemap module

The Core package does not depend on the Input System or Unity UI.

## Installation

In Unity, open **Window > Package Management > Package Manager**, select **Add package from git URL**, and enter:

```text
https://github.com/muhammedcanarica/dungeon-weaver.git?path=/Packages/com.muhammedcanarica.dungeon-weaver
```

You can also declare the same URL in the consuming project's `Packages/manifest.json`.

## Main components

- `DungeonGenerator`: deterministic room placement from a seed.
- `DungeonGraphBuilder`: deterministic connected room graph.
- `DungeonCorridorBuilder`: corridor floor-cell generation.
- `DungeonDoorwayBuilder`: doorway and corridor entrance data.
- `DungeonTilemapRenderer`: floor and wall Tilemap rendering.
- `DungeonRoomRoleAssigner`: start, normal, and boss room roles.
- `DungeonPlayerSpawner`: places any tracked actor with a `Collider2D` in the start room.
- `DungeonAreaTracker`: tracks the actor's current room, corridor, or outside state.
- `DungeonRoomStateController`: exploration and visit state.
- `DungeonDoorRegistry`: physical door creation and lookup.
- `DungeonRoomEncounterController`: generic room lock and clear lifecycle.
- `DungeonRuntimeController`: coordinates generation and regeneration.

## Minimum scene setup

1. Create a `Grid` with separate floor and wall `Tilemap` children.
2. Add the generation, rendering, role, tracking, room-state, door, encounter, actor-spawner, and runtime components to one dungeon coordinator GameObject.
3. Assign the Grid, Tilemaps, floor/wall tiles, and a tracked actor `Transform` with a `Collider2D`.
4. Assign a `TopDownCameraFollow`, or provide another camera integration around the generated Tilemap.
5. Call `DungeonRuntimeController.GenerateDungeon(seed)` or enable startup generation in the Inspector.

Calling `GenerateDungeon` again performs deterministic regeneration and clears the previous runtime state first.

## Determinism

Generation uses stable ordering and explicit seeds. The same package version, settings, and seed produce the same room, graph, corridor, doorway, door, and Core runtime layout.

## Encounter integration

Dungeon Weaver intentionally contains no combat system, enemy AI, health model, or gameplay input. Listen to `DungeonRuntimeController.RuntimeClearing` and `GenerationCompleted` to rebuild game-specific state. Use `DungeonRoomEncounterController` room lifecycle events to spawn enemies, start puzzles, or activate boss logic, then call `TryClearActiveRoom()` when that external objective is complete.

The repository containing this package is also its development and prototype sample project. Prototype player, enemy, attack, UI, and debug-input implementations remain under `Assets/Scripts/ProceduralDungeon/Samples/Prototype` and are not imported with the Core package.

Repository: https://github.com/muhammedcanarica/dungeon-weaver
