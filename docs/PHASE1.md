# Phase 1 — World Sandbox Core

## Scope implemented

- Deterministic world generation from a stable seed
- 256×256 default prototype and 512×512 test configuration
- Six biome/terrain classes
- Chunked rendering, 64×64 tiles per chunk
- Pan and zoom camera
- Terrain and water editing through a real brush command
- Undo for the last terrain commands
- Fixed simulation tick independent from rendering
- Pause and speed controls
- Versioned JSON save with generator version, RNG state placeholder, checksum, delta edits, backup, and recovery
- Debug metrics
- Pure C# tests and CI workflow
- Windows export preset and build script

## Acceptance criteria status

| Criterion | Status | Evidence |
|---|---|---|
| Project files and main scene exist | implemented_not_executed | `project.godot`, `Main.tscn` |
| Same seed creates same terrain | automated_test_added | `SameSeedAndConfigProduceSameWorldChecksum` |
| Ocean and land exist | implemented_not_executed | classification in `WorldGenerator` |
| At least five biomes | implemented | six data definitions |
| Real terrain brush | implemented_not_executed | `TerrainEditor.Paint` and Godot input |
| Camera pan/zoom | implemented_not_executed | `MainController._UnhandledInput` |
| Pause/time speed | automated_test_added | `SimulationClock` tests |
| Save/load preserves terrain | automated_test_added | checksum round-trip test |
| Debug overlay | implemented_not_executed | runtime metrics in `MainController` |
| Medium world does not crash | automated_test_added | 512×512 generation test |
| Desktop export | configured_not_built | `export_presets.cfg`, build script |
| Original assets only | passed | terrain visuals are generated colors; no external game assets |

## Test execution status

The repository was authored in an environment without the Godot .NET editor and without the .NET SDK. Therefore:

- Source and JSON static validation: performed before commit
- Core tests: added but not executed locally
- Godot compile: not executed locally
- Runtime UI/input test: not executed locally
- Windows export: not executed locally

GitHub Actions runs the pure C# tests after the pull request is opened. Godot desktop compilation still needs a machine with Godot 4.7.1 .NET.

## Performance design

- No Node per tile
- One generated texture and Sprite2D per chunk
- Dirty chunk rebuild after terrain edits
- Fixed tick with maximum step budget
- Terrain stored in packed arrays
- Save records only seed/config plus modified tiles

## Known limitations

- River routing, lake generation, erosion, plants, animals, settlements, kingdoms, minimap, and overlays are intentionally out of scope.
- Tile rendering does not yet use distance LOD or a world overview texture.
- The current brush preview is square while terrain application uses a circular radius.
- Localization JSON exists, but Phase 1 UI currently uses Thai labels directly.
- Autosave, multiple slots, thumbnail generation, and schema migration beyond version 1 are not implemented.
- Export templates must be installed manually before a Windows build.

## Status

`in_progress`
