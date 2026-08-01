# 09 — Package Index

## GitHub Technical Scaffold
- `project.godot` — project configuration
- `Main3D.tscn` — runnable root scene
- `scenes/player/Player.tscn` — 3D player placeholder
- `scenes/farming/FarmPlot.tscn` — reusable plot
- `scenes/npc/NPCBase.tscn` — reusable NPC
- `scripts/player/player_controller.gd` — locomotion
- `scripts/camera/camera_rig.gd` — isometric camera
- `scripts/world/world_bootstrap.gd` — farm grid and NPC spawn
- `scripts/farming/farm_plot.gd` — plot state boundary
- `scripts/npc/npc_controller.gd` — movement scaffold

## Documentation
- `00_PROJECT_BRIEF.md` — product vision, audience, core loop and vertical slice
- `01_ARCHITECTURE.md` — architecture, layers, signals, state machines and migration
- `02_ROADMAP_BACKLOG.md` — milestones and priority order
- `03_ASSET_MANIFEST.csv` — models, textures, animation, VFX and audio list
- `04_DATA_SCHEMAS.md` — save, player, plot, crop, NPC, task and world schemas
- `05_UI_UX_SPEC.md` — responsive HUD, tabs, accessibility and Auto Farm UI
- `06_QA_ACCEPTANCE.md` — quality gates, stress test and no-merge rules
- `07_CODEX_MASTER_PROMPT.md` — ready-to-use implementation instruction
- `08_TASK_BACKLOG.csv` — trackable engineering backlog

## Recommended Drive Folder Structure

```text
GrowWise 3D Open World v2/
├─ 00_Project_Brief/
├─ 01_Game_Design/
├─ 02_Technical_Architecture/
├─ 03_Art_Assets/
├─ 04_Data_Save/
├─ 05_UI_UX/
├─ 06_QA_Testing/
├─ 07_Codex_Prompts/
├─ 08_Builds/
├─ 09_Screenshots_Video/
└─ 10_Archive/
```

## Governance
- GitHub is source of truth for code and versioned technical docs
- Drive is source of truth for large binary assets, concept art, references, builds and review media
- Every Drive asset must use the same `asset_id` as the manifest
- Do not store secrets, API keys or private credentials in either location
- Large binary files should use clear version suffixes such as `_v001`, `_v002`
