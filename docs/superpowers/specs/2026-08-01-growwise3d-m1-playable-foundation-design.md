# GrowWise3D M1 — Playable 3D Foundation Design

## Objective

Build a playable Godot 4.6.3 vertical slice in `GrowWise3D/` that translates the existing GrowWise farming experience into a realistic 3D isometric world. Preserve the original game's farming systems and information hierarchy while replacing its flat visual language with believable scale, grounded materials, natural lighting, and readable realistic forms. Keep `GrowWise/`, its saves, WorldForge projects, and existing workflows unchanged.

The slice is complete when the player can walk and sprint around a collision-safe farm, inspect any of 24 plots, talk to three independently scheduled NPCs, use a smooth isometric camera, save and load M1 state, and read a responsive Thai-first HUD. Headless checks, Windows export, and honest manual-test reporting are part of the deliverable.

## Scope

### Included

- A composed `Node3D` game root with explicit World, Camera, Systems, Audio, and CanvasLayer ownership.
- Camera-relative player locomotion, gravity, acceleration/deceleration, smooth facing, input locking, and placeholder animation states.
- An isometric camera rig with Pivot, SpringArm3D, Camera3D, smooth follow, bounded zoom, and reset.
- A realistic prototype farm with terrain, paths, buildings, fence, trees, rocks, well, physically plausible materials, collision layers, and four time-of-day presets.
- NavigationRegion3D and three CharacterBody3D NPCs with staggered schedules, navigation agents, avoidance, talk state, and return to routine.
- A centralized proximity interaction system for NPCs, plots, well, and shed.
- Twenty-four reusable 3D farm plots with complete M1 data, four work points, selection highlight, and inspector output.
- Responsive Thai-first HUD, context prompt, plot inspector, and toggleable diagnostics.
- An isolated JSON save scaffold at `user://growwise3d_save_v2.json`, `SAVE_VERSION = 2`, defaults, and corrupt-file backup behavior.
- Automated structural/runtime tests, a separate Godot 4.6.3 Windows workflow, export preset, manual checklist, and M1 report.

### Excluded

- Full crop growth, inventory migration, tools, farming work animations, vehicles, machines, chunk streaming, and open-world regions.
- Final purchased or commissioned GLB characters and environment assets. M1 uses modular, realistically proportioned procedural stand-ins and materials, with clean replacement boundaries for production assets.
- Legacy GrowWise save migration. M1 only guarantees a separate v2 scaffold and never reads or writes the old save.
- Claims that visual or 30-minute testing passed unless those tests are actually performed.

## Architecture

`Main3D.tscn` is composition-only. Its root is `GameRoot: Node3D` with these direct areas:

- `World3D`: Environment, Terrain, Navigation, Buildings, Props, Farm, NPCs, and Player.
- `CameraRig`: Pivot, SpringArm3D, and Camera3D.
- `Audio`: future-facing ownership boundary.
- `Systems`: InteractionManager, SaveManager, TimeOfDayController, and Diagnostics.
- `CanvasLayer/UI`: responsive HUD and overlays.

Each system has one responsibility. Actors expose state through typed methods and signals; UI observes signals and does not mutate domain data directly. Interactables expose an action label, priority, interaction point, and `interact(actor)` contract. SaveManager receives snapshots from registered providers and restores them with defaults.

Major world objects are reusable `.tscn` scenes. `world_bootstrap.gd` may only read placement data, instantiate PackedScenes, assign IDs/configuration, and attach instances to their owned roots. It must not generate the world's primary geometry, collision shapes, materials, navigation behavior, or gameplay logic.

Collision layers are standardized as: 1 World Static, 2 Player, 3 NPC, 4 Interactable, 5 Vehicle, 6 Water, and 7 Trigger. Player and NPC physical masks exclude inappropriate trigger-only shapes. Interactable detection uses areas rather than allowing each object to own the global input action.

## Components

### Player

`Player.tscn` remains a CharacterBody3D and contains CollisionShape3D, ModelRoot, AnimationPlayer, InteractionArea, ToolSocket, and GroundCheck. `player_controller.gd` owns movement and the `IDLE`, `WALK`, `RUN`, `INTERACT`, and `WORK` states. Direction is transformed through the active camera's horizontal basis. Exported tuning defaults follow the request: walk 3.5, run 6.0, acceleration 12, deceleration 16, rotation 10.

`player_animation_bridge.gd` maps abstract player states and normalized movement speed to placeholder animations. The controller never refers to bones or eventual GLB internals.

### Camera

The camera rig follows the player in process timing after physics movement and smooths its target position exponentially. Orthographic size defaults to 14 and is clamped from 8 to 22. Wheel input changes a target zoom; smoothing prevents stepping. Reset restores the initial isometric orientation and zoom. SpringArm3D provides the future collision boundary even though orthographic projection makes arm length less visually prominent.

### World and Lighting

The prototype world uses realistically proportioned reusable `.tscn` scenes, layered geometry, and restrained PBR-style materials with plausible albedo, roughness, and metallic values. Terrain variation, raised plot edges, structural thickness, contact shadows, and non-coplanar placement prevent the flat or floating appearance of the scaffold. Large props receive shape-appropriate collision; small decorative props do not. Plot borders remain collidable so actors walk around them. The NavigationRegion3D owns a navigation mesh configured for actor radius and obstacles. Placement remains data-driven in `world_bootstrap.gd`; geometry, collision, materials, and behavior remain owned by focused scenes/scripts.

Time-of-day presets are Morning, Day, Evening, and Night. They change sun rotation/color/energy, sky contribution, ambient light, background, subtle fog, tonemapping, and exposure as a coordinated physically plausible profile. Shadows remain soft but directional, highlights retain detail, and night remains navigable without glowing materials. A debug action cycles presets.

### NPCs

`NPCBase.tscn` includes NavigationAgent3D, collision, ModelRoot, AnimationPlayer, and InteractionArea. `npc_controller.gd` owns `IDLE`, `WALK`, `WAIT`, `TALK`, `WORK`, and `RETURN`. Each NPC receives unique identity, dialogue, route, start delay, wait duration, and movement phase. Avoidance and distinct spawn/route timing prevent lockstep and prolonged overlap. Path requests use a bounded retry count and retry interval, then enter WAIT with a timeout and human-readable diagnostic before resuming or advancing safely. An unreachable NPC is never teleported.

When selected by InteractionManager, an NPC stops navigation, faces the player, emits its placeholder dialogue, waits for the interaction to finish, then resumes the saved schedule step.

### Interaction

`interactable_3d.gd` defines the contract. `interaction_manager.gd` collects candidates from the player's InteractionArea, filters disabled/unreachable targets, and selects the best candidate by distance and priority. A physics ray query from the player interaction origin to the target interaction point must confirm line of sight against World Static collision before the prompt or action is available; walls, houses, fences, and other large obstacles block interaction. It emits prompt and target changes. Only the manager reads the interact action, and it ignores input while a menu or interaction lock is active.

Plots highlight and emit a plot snapshot. NPCs emit dialogue. The well and shed return placeholder informational messages. This keeps future action execution separate from target selection.

### Farm Plot

Each plot stores `plot_id`, `tilled`, `moisture`, `fertility`, `health`, `crop_id`, `growth_stage`, and `water_level`. Four Marker3D work points surround the collidable soil bed. A separate highlight mesh is normally hidden and avoids coplanar geometry. Selection emits a typed signal and updates the HUD inspector; no crop simulation is implemented in M1.

### HUD and Diagnostics

HUD anchors four MarginContainer regions and composes content with PanelContainer, VBoxContainer, and HBoxContainer. Top-left shows project/day/time/weather, bottom-left controls, bottom-center interaction prompt, and top-right debug/player state/FPS in debug builds. The plot inspector uses a right-side container and hides when no plot is selected. Thai glyphs use Godot's available system fallback unless a licensed project font already exists.

Diagnostics toggles independently and reports player transform/velocity/state, current target, navigation state, NPC states, FPS, scene, and save version. Runtime markers are emitted after their corresponding systems validate their required nodes.

### Save

SaveManager writes an envelope with version and sections for player, camera, selected plot, NPCs, and time of day. Vector3 values are arrays. It validates the complete JSON envelope before promotion. A write goes to a temporary file, is read back and validated, then preserves the last known-good save as a valid backup before atomically promoting the temporary file. Loading missing fields applies defaults. Missing save returns a nonfatal result. Invalid primary JSON is preserved as a timestamped `.corrupt.bak`; recovery uses only a validated backup and never deletes or overwrites the latest known-good save. A save version newer than 2 is rejected without overwriting any file.

## Data Flow

1. GameRoot composes scenes and registers providers with Systems.
2. Player physics updates movement and emits state changes.
3. Animation bridge and HUD observe state without owning movement.
4. InteractionArea reports candidates to InteractionManager.
5. InteractionManager selects one target and emits the prompt.
6. On `E`, the target emits dialogue or plot data; HUD displays it.
7. SaveManager requests serializable snapshots and writes them atomically through a temporary file.
8. Load validates the envelope, applies provider defaults, then restores available state.

## Error Handling

- Missing required scene nodes fail early with descriptive `push_error` diagnostics and suppress the relevant success marker.
- Missing optional targets, routes, or save fields use documented defaults and warnings.
- Navigation queries wait for map synchronization before assigning paths; unreachable points enter WAIT rather than teleporting.
- Interaction validates weak references before use and clears stale prompts.
- Save uses temporary-write then replace, preserves corrupt input as backup, and never touches legacy filenames.
- CI rejects parse/compile errors, missing resources, absent markers, incorrect counts, wrong save path, or failed export.

## Verification

Automated verification covers:

- Godot 4.6.3 headless import and main-scene load.
- Runtime smoke with all eight required markers plus direct assertions of node types, node counts, unique IDs, important property values, and the resolved save path.
- GDScript parse errors and missing resources.
- Player, CameraRig, NavigationRegion3D, three NPCs, and 24 plots.
- Interaction prompt and plot selection via deterministic smoke hooks.
- Save path/version, missing-file behavior, round trip, defaults, and corrupt JSON backup in an isolated test user-data directory.
- Windows x86-64 export, README, checksum, and uniquely named artifact.
- Automated screenshots for Morning, Day, Evening, Night, selected plot, and NPC overview. They are labeled machine-generated diagnostic artifacts and are never reported as Manual Visual Test evidence.
- Git diff checks confirming no changes under `GrowWise/`, `WorldForge/`, or `WorldForge.Godot/`.

Manual verification records only observed results at 720p and 1080p, movement, collision, camera, NPC routes, interaction, save/load, and visual quality. The checklist remains unticked and states `MANUAL_VISUAL_TEST_PENDING` for any unperformed item, including the 30-minute run.

## Planned Files

Existing files to revise:

- `GrowWise3D/project.godot`
- `GrowWise3D/Main3D.tscn`
- `GrowWise3D/scenes/player/Player.tscn`
- `GrowWise3D/scenes/npc/NPCBase.tscn`
- `GrowWise3D/scenes/farming/FarmPlot.tscn`
- existing player, NPC, farm, camera, core, and world scripts
- `GrowWise3D/docs/01_ARCHITECTURE.md`

Focused files to add:

- animation bridge, interaction base/manager, save manager, time-of-day controller, HUD controller, diagnostics controller, and smoke test scripts
- reusable HUD and small world/interaction scenes where scene ownership is clearer than runtime construction
- `GrowWise3D/export_presets.cfg`
- `GrowWise3D/docs/M1_MANUAL_TEST.md`
- `GrowWise3D/docs/M1_TEST_REPORT.md`
- `.github/workflows/growwise3d-windows.yml`

## Delivery

Implementation is divided into logical commits: baseline, player/animation, camera, world/navigation, NPCs, interaction/plots, HUD/lighting, save/diagnostics, CI/export, and test documentation. The branch targets `feature/growwise-3d-openworld-v2-scaffold` through a Draft PR titled `GrowWise3D M1 — Playable 3D Foundation`. The PR is never merged automatically.
