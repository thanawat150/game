# WorldForge: Pixel Gods

Phase 1 implementation for a deterministic 2D pixel world sandbox built with **Godot 4.7.1 .NET** and a pure **C# simulation core**.

> Status: `in_progress` — the deterministic world core, terrain editing, save/load, debug overlay, tests, and Windows export configuration are implemented. A Godot desktop build still needs to be executed on a machine with Godot 4.7.1 .NET and .NET 8 installed.

## What is included

- Seed-based deterministic world generation
- 256×256 prototype world, configurable up to 512×512 in code
- Six terrain/biome classes: deep ocean, shallow water, beach, grassland, forest, mountain
- Chunked texture renderer (64×64 tiles per chunk)
- Camera pan and zoom
- Real terrain and water brush tools
- Undo for terrain edits
- Pause, x1, x2, x4, x8, and maximum simulation speed
- Versioned save format with checksum, backup, and recovery
- Debug overlay with FPS, simulation TPS, tick count, chunk count, and checksum
- Pure C# automated tests for seed reproducibility, editing, clock behavior, and save/load
- GitHub Actions workflow for the simulation core tests
- Windows export preset and PowerShell build scripts

## Requirements

- Godot Engine **4.7.1 .NET** (not the standard non-.NET editor)
- .NET SDK **8.0 or newer**
- Windows 10/11 for the provided desktop build script

## Run in the editor

```powershell
./tools/run_editor.ps1 -GodotPath "C:\Tools\Godot_v4.7.1-stable_mono_win64.exe"
```

Or open `project.godot` with the Godot 4.7.1 .NET editor and press **F6/F5**.

## Run the core tests

```powershell
dotnet test ./tests/WorldForge.Core.Tests/WorldForge.Core.Tests.csproj -c Release
```

## Build Windows desktop

Install the Godot 4.7.1 .NET export templates first, then run:

```powershell
./tools/build_windows.ps1 -GodotPath "C:\Tools\Godot_v4.7.1-stable_mono_win64.exe"
```

Expected artifact:

```text
builds/windows/WorldForgePixelGods.exe
```

## Controls

- Middle mouse drag: pan camera
- Mouse wheel: zoom
- Left mouse: paint selected terrain
- Terrain selector: choose biome/terrain
- Brush size slider: change radius
- Undo button: restore the last brush command
- Save/Load: use `user://saves/slot_1.wfg.json`

## Architecture

```text
Godot Presentation
  Camera / UI / Input / Chunk Renderer
             │
             ▼
Pure C# Simulation Core
  World / Noise / Time / Editing / Save
```

The simulation core does not depend on Godot nodes, so it can run in tests or future headless simulations.

See [`docs/PHASE1.md`](docs/PHASE1.md) for acceptance criteria, test status, and known limitations.
