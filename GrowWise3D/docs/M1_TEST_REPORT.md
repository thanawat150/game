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

- Godot import: baseline passed.
- Scaffold runtime: baseline passed.
- M1 structural smoke: expected RED before implementation.
- Windows export: pending.
- Legacy workflows: pending CI; legacy files remain unmodified.

## Manual Test Status

`MANUAL_VISUAL_TEST_PENDING`

No M1 visual quality, resolution, interaction, navigation, save/load, or 30-minute stability claim has been made.
