# GrowWise3D M1 Test Report

## Baseline

Date: 2026-08-01  
Branch source: `feature/growwise-3d-openworld-v2-scaffold`  
Godot: `4.6.3.stable.official.7d41c59c4`

### Headless Import

- Result: exit code 0.
- Six typed GDScript classes registered.
- No parse or compile error was reported.
- CSV documentation files were imported as translation sources by the editor; these generated imports are not implementation assets.

### Runtime Smoke

- Result: exit code 0.
- Observed markers:
  - `GROWWISE3D_WORLD_SCAFFOLD_OK`
  - `GROWWISE3D_SCAFFOLD_OK`

### M1 Structural Smoke — Expected RED

Command:

```powershell
Godot_v4.6.3-stable_win64_console.exe --headless --path GrowWise3D --script res://scripts/tests/m1_smoke_test.gd
```

Result: exit code 1 with `GROWWISE3D_M1_TESTS_FAILED count=20`.

The test failed for the intended missing M1 requirements: composed `World3D` ownership, Pivot/SpringArm camera hierarchy, Systems and CanvasLayer/UI, unique NPC IDs, final camera limits/default, and isolated SaveManager. Existing plot generation did produce 24 unique plot IDs. This RED result is the baseline for the implementation sequence.

## Automated Test Status

- Godot 4.6.3 import: exit code 0 after implementation.
- Runtime smoke: exit code 0 with all eight required markers.
- M1 structural smoke: exit code 0 with `GROWWISE3D_M1_TESTS_OK`.
- Component contracts: Player, Camera, World ownership, time presets, FarmPlot, interaction LOS, NPC/navigation, HUD, Save, runtime markers, and release pipeline passed locally.
- Automated screenshots: six PNG files generated locally; explicitly not a Manual Visual Test.
- Windows export: pending matching local export templates or GitHub Actions.
- Legacy workflows: pending CI; legacy files remain unmodified.

## Manual Test Status

`MANUAL_VISUAL_TEST_PENDING`

No M1 visual quality, resolution, interaction, navigation, save/load, or 30-minute stability claim has been made.

## Runtime Markers Observed

- `GROWWISE3D_SCAFFOLD_OK`
- `GROWWISE3D_WORLD_SCAFFOLD_OK`
- `GROWWISE3D_PLAYER_OK`
- `GROWWISE3D_CAMERA_OK`
- `GROWWISE3D_NAVIGATION_OK`
- `GROWWISE3D_NPC_OK`
- `GROWWISE3D_INTERACTION_OK`
- `GROWWISE3D_M1_FOUNDATION_OK`

## Known Issues

- Visuals are realistically proportioned procedural prototypes, not final photorealistic production assets.
- Automated screenshots show the debug panel occupying excessive vertical space; responsive structure is present but manual layout tuning remains required.
- Navigation is baked from static collision at runtime. Runtime and contract checks pass, but NPC obstacle avoidance and overlap duration still need manual observation.
- Player movement, camera feel, interactions, save/load UI flow, Thai glyph quality, and 30-minute stability have not been manually verified.
- Local Windows export was not run because matching Godot 4.6.3 export templates are not installed; the dedicated workflow installs them in CI.
