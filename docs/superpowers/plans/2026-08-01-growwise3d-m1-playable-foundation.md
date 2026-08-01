# GrowWise3D M1 Playable 3D Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a realistic, playable Godot 4.6.3 isometric farm foundation with player locomotion, camera, navigation, three NPCs, plot interaction, responsive HUD, save v2, diagnostics, and Windows export.

**Architecture:** `Main3D.tscn` composes a true Node3D world, focused actor scenes, small application systems under `Systems`, and CanvasLayer UI. Typed signals connect gameplay to UI; interaction and persistence are centralized so legacy GrowWise data and presentation remain isolated.

**Tech Stack:** Godot 4.6.3, typed GDScript, `.tscn` text scenes, NavigationServer3D, JSON persistence, GitHub Actions on Ubuntu, Windows x86-64 export.

## Global Constraints

- Modify only `GrowWise3D/`, GrowWise3D-related documentation, and GrowWise3D-specific workflows.
- Do not modify `GrowWise/`, `WorldForge/`, `WorldForge.Godot/`, legacy saves, or existing legacy workflows.
- Use Node3D for the world, CharacterBody3D for actors, Camera3D for view, and CanvasLayer for UI.
- Do not use Node2D, `_draw()`, or SubViewport as the primary game world.
- Save only to `user://growwise3d_save_v2.json` with `SAVE_VERSION = 2`.
- Preserve realistic scale, PBR-style material values, natural lighting, readable shadows, and non-coplanar geometry.
- Never claim a visual/manual check passed unless it was actually performed.
- Target 1280×720, 1920×1080, 2560×1440, and 3840×2160.

---

## File Responsibility Map

- `Main3D.tscn`: composition and node ownership only.
- `scripts/player/player_controller.gd`: input, physics, and player state.
- `scripts/player/player_animation_bridge.gd`: abstract state to placeholder animation.
- `scripts/camera/camera_rig.gd`: smooth follow, zoom, reset, and camera basis.
- `scripts/world/world_bootstrap.gd`: placement-data loading and PackedScene instancing only.
- `scripts/world/time_of_day_controller.gd`: four coordinated lighting profiles.
- `scripts/interaction/interactable_3d.gd`: reusable interaction contract.
- `scripts/interaction/interaction_manager.gd`: candidate selection and global interact input.
- `scripts/farming/farm_plot.gd`: plot state, highlighting, work points, serialization.
- `scripts/npc/npc_controller.gd`: navigation schedule and NPC state machine.
- `scripts/save/save_manager.gd`: isolated v2 JSON persistence and recovery.
- `scripts/ui/hud_controller.gd`: signal-driven HUD presentation.
- `scripts/debug/diagnostics.gd`: overlay snapshots and runtime markers.
- `scripts/tests/m1_smoke_test.gd`: deterministic structural/runtime assertions.

### Task 1: Baseline Guard and Test Harness

**Files:**
- Create: `GrowWise3D/scripts/tests/m1_smoke_test.gd`
- Modify: `GrowWise3D/project.godot`
- Create: `GrowWise3D/docs/M1_TEST_REPORT.md`

**Interfaces:**
- Produces: `M1SmokeTest.run_checks(root: Node) -> bool` and `GROWWISE3D_M1_TESTS_OK`.

- [ ] **Step 1: Write the initial failing structural test** with exact required node paths and types, NPC count `3`, plot count `24`, unique IDs, important movement/camera/navigation properties, and the resolved save path `user://growwise3d_save_v2.json`; make `run_checks` call `push_error` and return `false` for every absent or incorrect requirement.
- [ ] **Step 2: Run baseline test** using `Godot_v4.6.3-stable_win64.exe --headless --path GrowWise3D --quit-after 5`; expect existing scaffold markers but no `GROWWISE3D_M1_TESTS_OK`.
- [ ] **Step 3: Add a `--m1-smoke` command-line hook** in `game_root.gd` that instantiates the test only when requested, keeping normal runtime unaffected.
- [ ] **Step 4: Record exact baseline output** in `M1_TEST_REPORT.md`, including the successful import/runtime and incomplete M1 checks.
- [ ] **Step 5: Commit** with `git commit -m "chore(growwise3d): validate scaffold and add M1 harness"`.

### Task 2: Player Locomotion and Animation Boundary

**Files:**
- Modify: `GrowWise3D/scenes/player/Player.tscn`
- Modify: `GrowWise3D/scripts/player/player_controller.gd`
- Create: `GrowWise3D/scripts/player/player_animation_bridge.gd`
- Modify: `GrowWise3D/project.godot`

**Interfaces:**
- Produces: `signal state_changed(state: PlayerState, speed_ratio: float)`, `set_input_locked(locked: bool)`, `get_state_name() -> String`, and `get_camera_relative_direction(input: Vector2) -> Vector3`.

- [ ] **Step 1: Extend the smoke test** to require `ModelRoot`, `AnimationPlayer`, `InteractionArea`, `ToolSocket`, and `GroundCheck`; expect failure against the scaffold.
- [ ] **Step 2: Add input actions** for arrow movement, `camera_reset`, `debug_toggle`, `time_cycle`, `save_game`, and `load_game` while retaining WASD, Shift, E, and Escape.
- [ ] **Step 3: Implement typed `PlayerState { IDLE, WALK, RUN, INTERACT, WORK }`** with exported defaults `walk_speed=3.5`, `run_speed=6.0`, `acceleration=12.0`, `deceleration=16.0`, and `rotation_speed=10.0`; transform input through the active camera's flattened basis, apply gravity, use separate deceleration, and emit state changes.
- [ ] **Step 4: Build modular placeholder animation** in `AnimationPlayer` and route state/speed through `player_animation_bridge.gd`; keep bone and GLB names out of the controller.
- [ ] **Step 5: Run import and smoke**; expect Player structure checks and `GROWWISE3D_PLAYER_OK` to pass without parse errors.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): implement player locomotion and states"`.

### Task 3: Isometric Camera Rig

**Files:**
- Modify: `GrowWise3D/Main3D.tscn`
- Modify: `GrowWise3D/scripts/camera/camera_rig.gd`

**Interfaces:**
- Produces: `get_planar_basis() -> Basis`, `get_zoom() -> float`, `set_zoom(value: float)`, and `reset_view()`.

- [ ] **Step 1: Update the smoke test** to require `CameraRig/Pivot/SpringArm3D/Camera3D` and zoom bounds; verify it fails first.
- [ ] **Step 2: Recompose the rig** with exported `follow_smoothing=8.0`, `min_zoom=8.0`, `max_zoom=22.0`, `default_zoom=14.0`, a fixed isometric yaw/pitch, and a SpringArm3D child.
- [ ] **Step 3: Implement exponential follow and zoom smoothing** using `1.0 - exp(-rate * delta)`, update after actor movement, clamp target zoom, and restore initial rotation/zoom on `camera_reset`.
- [ ] **Step 4: Run import/runtime smoke**; expect `GROWWISE3D_CAMERA_OK` and no missing NodePath.
- [ ] **Step 5: Commit** with `git commit -m "feat(growwise3d): add smooth isometric camera rig"`.

### Task 4: Realistic World Composition, Collision, and Lighting

**Files:**
- Modify: `GrowWise3D/Main3D.tscn`
- Modify: `GrowWise3D/scripts/world/world_bootstrap.gd`
- Create: `GrowWise3D/scripts/world/time_of_day_controller.gd`
- Create: `GrowWise3D/scenes/world/Terrain.tscn`
- Create: `GrowWise3D/scenes/world/PlayerHouse.tscn`
- Create: `GrowWise3D/scenes/world/StorageShed.tscn`
- Create: `GrowWise3D/scenes/world/Well.tscn`
- Create: `GrowWise3D/scenes/world/FenceSection.tscn`
- Create: `GrowWise3D/scenes/world/Tree.tscn`
- Create: `GrowWise3D/scenes/world/Rock.tscn`
- Create: `GrowWise3D/data/m1_world_placements.json`
- Modify: `GrowWise3D/docs/01_ARCHITECTURE.md`

**Interfaces:**
- Produces: `TimeOfDayController.cycle_preset()`, `get_preset_name() -> String`, `serialize() -> Dictionary`, and `deserialize(data: Dictionary)`.

- [ ] **Step 1: Add structural assertions** for `World3D/Environment`, `Terrain`, `Navigation`, `Buildings`, `Props`, `Farm`, `NPCs`, and `Player`; expect failure.
- [ ] **Step 2: Recompose Main3D ownership** and document collision layers 1–7 in Architecture.
- [ ] **Step 3: Construct realistic stand-ins as focused `.tscn` scenes** at one-meter scale for terrain, house, shed, fence, trees, rocks, and well; each scene owns its geometry, plausible material values, and shape-appropriate StaticBody3D collision.
- [ ] **Step 4: Restrict `world_bootstrap.gd` to placement** by loading `m1_world_placements.json`, instantiating declared PackedScenes, assigning configuration, and parenting them; add a smoke assertion that it contains no `Mesh.new`, shape construction, material construction, or gameplay state transitions.
- [ ] **Step 5: Implement Morning/Day/Evening/Night profiles** that coordinate sun, environment, fog, tonemap, exposure, and sky/background without overbright materials.
- [ ] **Step 6: Run import/runtime smoke** and verify `GROWWISE3D_WORLD_SCAFFOLD_OK`; inspect logs for missing resources and Z-fighting cannot be claimed visually yet.
- [ ] **Step 7: Commit** with `git commit -m "feat(growwise3d): build realistic farm world and lighting"`.

### Task 5: FarmPlot Interaction Model

**Files:**
- Modify: `GrowWise3D/scenes/farming/FarmPlot.tscn`
- Modify: `GrowWise3D/scripts/farming/farm_plot.gd`

**Interfaces:**
- Produces: `signal selected(plot: GrowWiseFarmPlot)`, `set_selected(value: bool)`, `get_prompt() -> String`, `interact(actor: Node3D)`, `serialize() -> Dictionary`, and `deserialize(data: Dictionary)`.

- [ ] **Step 1: Add tests** for 24 unique IDs, four work points, required data keys, collision, and highlight default hidden; expect failure.
- [ ] **Step 2: Add complete typed state**: tilled, moisture, fertility, health, crop_id, growth_stage, and water_level with clamped deserialization defaults.
- [ ] **Step 3: Build raised soil geometry** with border depth, four Marker3D work points, an Area3D interaction shape, and a non-coplanar selection highlight.
- [ ] **Step 4: Implement selection** so E emits a snapshot only after target selection and toggles exactly one plot highlight.
- [ ] **Step 5: Run smoke**; expect plot count/data/work-point checks to pass.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): implement reusable interactive farm plots"`.

### Task 6: Central Interaction Framework

**Files:**
- Create: `GrowWise3D/scripts/interaction/interactable_3d.gd`
- Create: `GrowWise3D/scripts/interaction/interaction_manager.gd`
- Create: `GrowWise3D/scenes/interaction/WorldInteractable.tscn`
- Modify: `GrowWise3D/Main3D.tscn`

**Interfaces:**
- Produces: `signal target_changed(target: Node3D, prompt: String)`, `signal interaction_started(target: Node3D)`, `set_input_locked(locked: bool)`, and target contract methods `get_interaction_prompt() -> String`, `get_interaction_priority() -> int`, `can_interact(actor: Node3D) -> bool`, `interact(actor: Node3D)`.

- [ ] **Step 1: Add deterministic candidate tests** for nearest-distance selection, priority tie-break, stale target clearing, input lock, clear line of sight, and obstruction by world-static collision; expect failure.
- [ ] **Step 2: Implement the reusable base contract** without reading global input inside interactables.
- [ ] **Step 3: Implement InteractionManager** using the player's Area3D overlap set, weak-reference validation, distance/priority ordering, one owner of the E action, and a PhysicsRayQueryParameters3D line-of-sight query that blocks prompts/actions through houses, walls, fences, and large obstacles.
- [ ] **Step 4: Add well and shed endpoints** with Thai prompts and placeholder informational results.
- [ ] **Step 5: Run smoke**; expect prompt selection and `GROWWISE3D_INTERACTION_OK`.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): add centralized interaction framework"`.

### Task 7: Navigation and Three NPC Schedules

**Files:**
- Modify: `GrowWise3D/scenes/npc/NPCBase.tscn`
- Modify: `GrowWise3D/scripts/npc/npc_controller.gd`
- Modify: `GrowWise3D/scripts/world/world_bootstrap.gd`
- Modify: `GrowWise3D/Main3D.tscn`

**Interfaces:**
- Produces: `NPCState { IDLE, WALK, WAIT, TALK, WORK, RETURN }`, `begin_talk(player: Node3D)`, `end_talk()`, `request_path_with_retry(target: Vector3)`, `get_navigation_diagnostics() -> Dictionary`, `get_state_name() -> String`, `serialize() -> Dictionary`, and `deserialize(data: Dictionary)`.

- [ ] **Step 1: Add tests** requiring one NavigationRegion3D, a NavigationMesh, three uniquely identified NPCs, NavigationAgent3D children, distinct routes/start delays, and serialized state; expect failure.
- [ ] **Step 2: Configure navigation** for walkable terrain with plot/building/well exclusions and agent radius suitable for non-overlap.
- [ ] **Step 3: Implement the typed NPC state machine** with path following, randomized-but-deterministic waits, smooth facing, avoidance, arrival handling, WAIT timeout, bounded retry count/interval, and diagnostics containing retry count, elapsed wait, target, and failure reason; never teleport on failure.
- [ ] **Step 4: Configure the three requested roles and Thai dialogue**: seed teacher, soil researcher, and water technician; stop/focus on player during TALK, then resume the same schedule step.
- [ ] **Step 5: Run smoke** after NavigationServer map synchronization; expect `GROWWISE3D_NAVIGATION_OK` and `GROWWISE3D_NPC_OK`.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): implement NPC navigation schedules"`.

### Task 8: Responsive HUD and Diagnostics

**Files:**
- Create: `GrowWise3D/scenes/ui/HUD.tscn`
- Create: `GrowWise3D/scripts/ui/hud_controller.gd`
- Create: `GrowWise3D/scripts/debug/diagnostics.gd`
- Modify: `GrowWise3D/Main3D.tscn`

**Interfaces:**
- Consumes: player state, interaction target/prompt, selected plot, time preset, NPC states, and save version.
- Produces: `set_context_prompt(text: String)`, `show_plot(plot: GrowWiseFarmPlot)`, `show_message(text: String)`, and `set_debug_visible(value: bool)`.

- [ ] **Step 1: Add structural UI tests** requiring MarginContainer, VBoxContainer, HBoxContainer, and PanelContainer regions with anchors instead of fixed screen coordinates; expect failure.
- [ ] **Step 2: Build CanvasLayer HUD** for top-left day/time/weather, bottom-left controls, bottom-center prompt, top-right debug state/FPS, and right plot inspector.
- [ ] **Step 3: Bind typed signals** and ensure hidden panels release mouse input; menu/input-lock signal controls Player and InteractionManager together.
- [ ] **Step 4: Implement diagnostics toggle** with player position/velocity/state, target, navigation, NPC states, FPS, scene, and save version.
- [ ] **Step 5: Run headless smoke at four window sizes** and verify Control bounds do not report overlap; reserve actual glyph/readability approval for GUI testing.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): add responsive HUD and diagnostics"`.

### Task 9: SaveManager v2

**Files:**
- Create: `GrowWise3D/scripts/save/save_manager.gd`
- Modify: `GrowWise3D/Main3D.tscn`
- Modify: `GrowWise3D/project.godot`

**Interfaces:**
- Produces: constants `SAVE_VERSION := 2`, `SAVE_PATH := "user://growwise3d_save_v2.json"`; signals `save_completed(path: String)`, `load_completed()`, `save_warning(message: String)`; methods `new_game()`, `save_game() -> Error`, and `load_game() -> Error`.

- [ ] **Step 1: Add tests** in an isolated `--user-data-dir` for missing save, round trip, missing fields, temporary-file validation, valid backup retention, corrupt primary recovery from validated backup, corrupt JSON preservation, unsupported future version, and legacy filename exclusion; expect failure.
- [ ] **Step 2: Implement snapshot collection** for player transform, camera zoom, selected plot, NPC transforms/states, and time preset.
- [ ] **Step 3: Implement validated atomic JSON write**: write temporary, read/parse/schema-check temporary, retain the latest known-good primary as a validated backup, then atomically promote; on corrupt primary preserve it as timestamped `.corrupt.bak`, recover only from a validated backup, and never delete the last good save.
- [ ] **Step 4: Bind New Game/Save/Load actions** and HUD notifications without referencing the legacy save.
- [ ] **Step 5: Run all persistence checks** and verify no file named like the old GrowWise save is created.
- [ ] **Step 6: Commit** with `git commit -m "feat(growwise3d): add isolated save v2 scaffold"`.

### Task 10: Runtime Marker Completion

**Files:**
- Modify: `GrowWise3D/scripts/core/game_root.gd`
- Modify: `GrowWise3D/scripts/tests/m1_smoke_test.gd`

**Interfaces:**
- Produces all required markers only after their corresponding checks pass.

- [ ] **Step 1: Make marker test fail** unless all eight exact strings are observed.
- [ ] **Step 2: Gate markers** `GROWWISE3D_SCAFFOLD_OK`, `WORLD_SCAFFOLD_OK`, `PLAYER_OK`, `CAMERA_OK`, `NAVIGATION_OK`, `NPC_OK`, `INTERACTION_OK`, and `M1_FOUNDATION_OK` behind validated nodes/state.
- [ ] **Step 3: Run import and runtime smoke**; expect all markers plus `GROWWISE3D_M1_TESTS_OK`, zero parse errors, and exit code 0.
- [ ] **Step 4: Commit** with `git commit -m "test(growwise3d): validate M1 runtime foundation"`.

### Task 11: Windows Export Workflow

**Files:**
- Create: `GrowWise3D/export_presets.cfg`
- Create: `.github/workflows/growwise3d-windows.yml`
- Create: `GrowWise3D/scripts/tests/capture_m1_screenshots.gd`

**Interfaces:**
- Produces artifact `GrowWise3D-Windows-M1-Foundation` containing `GrowWise3D.exe`, PCK if not embedded, `README_PLAY.txt`, and `SHA256SUMS.txt`; also produces `GrowWise3D-M1-Automated-Screenshots` with six explicitly machine-generated diagnostic images.

- [ ] **Step 1: Add local export command** and verify it fails before a preset/template exists.
- [ ] **Step 2: Define Windows Desktop x86-64 preset** isolated to GrowWise3D.
- [ ] **Step 3: Implement deterministic screenshot capture** for Morning, Day, Evening, Night, selected plot, and NPC overview; write a manifest stating `AUTOMATED_SCREENSHOT_ARTIFACT_NOT_MANUAL_VISUAL_TEST`.
- [ ] **Step 4: Implement workflow** to download Godot 4.6.3, import, run M1 smoke, capture the six screenshots with a virtual display, install matching export templates, export, generate play instructions/checksum, and upload diagnostics, screenshot artifact, and the unique Windows artifact.
- [ ] **Step 5: Validate workflow syntax and path filters**; confirm existing workflow files are byte-for-byte unchanged.
- [ ] **Step 6: Run local Windows export** with the matching template when available and record exact result.
- [ ] **Step 7: Commit** with `git commit -m "ci(growwise3d): export Windows M1 foundation"`.

### Task 12: Manual Visual QA

**Files:**
- Create: `GrowWise3D/docs/M1_MANUAL_TEST.md`
- Modify: `GrowWise3D/docs/M1_TEST_REPORT.md`

**Interfaces:**
- Produces an honest checklist and screenshot status.

- [ ] **Step 1: Create the exact requested checklist** with every item initially unchecked and `MANUAL_VISUAL_TEST_PENDING`.
- [ ] **Step 2: Launch the project in GUI** and inspect 1280×720 and 1920×1080 for realistic lighting, material readability, UI overlap, Thai glyphs, floating geometry, and Z-fighting.
- [ ] **Step 3: Manually exercise** walking, sprinting, rotation, collision, zoom/reset, three NPC routes, NPC talk, plot inspection, save/load, and time presets; tick only observed passes.
- [ ] **Step 4: Run the 30-minute stability check only if time actually elapsed**; otherwise leave it pending.
- [ ] **Step 5: Record screenshots status, limitations, and any failures** in the report without softening them.
- [ ] **Step 6: Commit** with `git commit -m "docs(growwise3d): report M1 manual verification"`.

### Task 13: Final Regression and Draft PR

**Files:**
- Modify: `GrowWise3D/README.md`
- Modify: `GrowWise3D/docs/M1_TEST_REPORT.md`

**Interfaces:**
- Produces a Draft PR targeting `feature/growwise-3d-openworld-v2-scaffold`.

- [ ] **Step 1: Run final import/runtime suite** and require all markers, counts, save checks, and no parse/missing-resource diagnostics.
- [ ] **Step 2: Run or inspect legacy checks** without changing legacy files; record whether WorldForge/GrowWise workflows ran locally or remain CI-only.
- [ ] **Step 3: Verify scope** with `git diff --name-only feature/growwise-3d-openworld-v2-scaffold...HEAD`; fail if forbidden project paths changed.
- [ ] **Step 4: Update README and report** with controls, save impact, known issues, exact automated/manual results, and M2 Visible Farming recommendation.
- [ ] **Step 5: Push the feature branch** and open a Draft PR titled `GrowWise3D M1 — Playable 3D Foundation` with every requested body section; do not merge it.
- [ ] **Step 6: Wait for CI** and report the real Windows artifact link or clearly state that it is unavailable if CI fails.

## Plan Self-Review

- Every included system in the approved design maps to a task.
- Final production assets, full farming, and legacy migration remain excluded.
- Shared interfaces use consistent method and signal names across tasks.
- Every task has a failing check, implementation, verification, and logical commit.
- No task authorizes changes to legacy projects or automatic merge.
