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

## Automatic scene setup

After installing the package, use **Tools > Dungeon Weaver > Create Dungeon Setup**. The command creates an idempotent, Undo-compatible hierarchy containing the coordinator, Grid, Floor/Wall Tilemaps, camera, and required Core components. It never creates gameplay actors, enemies, or Tile assets, and it does not modify unrelated scene objects.

After running the command:

1. Create or choose consumer-owned floor and wall `TileBase` assets.
2. Assign them to `DungeonTilemapRenderer`.
3. Create your actor with a `Collider2D` and optionally a `Rigidbody2D`.
4. Assign it through `DungeonPlayerSpawner.SetTrackedActor` or `DungeonRuntimeController.SetTrackedActor`.
5. Enable startup generation or call `DungeonRuntimeController.GenerateDungeon(seed)`.

## Manual minimum scene setup

1. Install the package through Package Manager.
2. Create a `Grid` with separate Floor and Wall `Tilemap` children; keep the Grid cell size at `1, 1, 0`.
3. Add `DungeonGenerator`, `DungeonGraphBuilder`, `DungeonCorridorBuilder`, `DungeonDoorwayBuilder`, `DungeonTilemapRenderer`, `DungeonRoomRoleAssigner`, `DungeonPlayerSpawner`, `DungeonAreaTracker`, `DungeonRoomStateController`, `DungeonDoorRegistry`, `DungeonRoomEncounterController`, and `DungeonRuntimeController` to one coordinator GameObject.
4. Add a Camera with `TopDownCameraFollow` or provide an equivalent camera integration.
5. Assign the Grid, Floor/Wall Tilemaps, and consumer-owned floor/wall `TileBase` assets to the relevant components.
6. Create an actor with `Transform` and `Collider2D`, then bind it with `DungeonRuntimeController.SetTrackedActor` or `DungeonPlayerSpawner.SetTrackedActor`.
7. Set the serialized seed or pass one directly to `DungeonRuntimeController.GenerateDungeon(seed)`.
8. Call `GenerateDungeon`, or enable `generateOnStart` only after all tile, camera, and actor references are assigned.

Calling `GenerateDungeon` again performs deterministic regeneration and clears the previous runtime state first.

## Determinism

Generation uses stable ordering and explicit seeds. The same package version, settings, and seed produce the same room, graph, corridor, doorway, door, and Core runtime layout.

## Encounter integration

Dungeon Weaver intentionally contains no combat system, enemy AI, health model, or gameplay input. Listen to `DungeonRuntimeController.RuntimeClearing` and `GenerationCompleted` to rebuild game-specific state. Use `DungeonRoomEncounterController` room lifecycle events to spawn enemies, start puzzles, or activate boss logic, then call `TryClearActiveRoom()` when that external objective is complete.

The repository containing this package is also its development and prototype sample project. Prototype player, enemy, attack, UI, and debug-input implementations remain under `Assets/Scripts/ProceduralDungeon/Samples/Prototype` and are not imported with the Core package.

Repository: https://github.com/muhammedcanarica/dungeon-weaver
