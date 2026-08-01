# 07 — Codex Master Prompt

## Role
Act as a senior Godot 4.6.3 gameplay engineer, technical artist, systems architect and QA engineer. Work only on the `GrowWise3D/` project unless a task explicitly requires a read-only legacy adapter.

## Mission
Build a production-oriented 3D isometric open-world farming vertical slice. Replace placeholder systems incrementally without breaking the existing `GrowWise/` or WorldForge projects.

## Non-negotiable Requirements
- Use Godot 4.6.3
- Windows 11 x86-64 target
- Thai default language
- Node3D world, CharacterBody3D actors, Camera3D, AnimationTree
- CanvasLayer responsive UI
- No `_draw()` world renderer
- No SubViewport as the primary game world
- No large monolithic script
- All gameplay state serializable
- Every automatic action must have a visible actor, travel phase and work phase
- Existing legacy project must continue to pass CI

## Required Workflow
1. Read all files under `GrowWise3D/docs/`
2. Inspect existing legacy data and simulation scripts read-only
3. Create a scoped implementation plan
4. Work on a feature branch
5. Commit by logical unit
6. Add or update smoke tests
7. Run Godot import
8. Run headless runtime test
9. Export Windows build
10. Report exact changed files, test results, warnings and remaining risks

## First Implementation Batch

### Task 1 — Project Health
- Validate `GrowWise3D/project.godot`
- Validate `Main3D.tscn`
- Add a CI workflow for import, smoke and Windows export
- Assert markers:
  - GROWWISE3D_SCAFFOLD_OK
  - GROWWISE3D_WORLD_SCAFFOLD_OK

### Task 2 — Player
- Replace placeholder capsule with modular character scene boundary
- Keep placeholder mesh until final GLB exists
- Add locomotion state machine
- Add Idle/Walk/Run animation interfaces
- Add interaction detector
- Add input lock for menus

### Task 3 — Camera
- Add smooth isometric follow
- Add zoom
- Add reset
- Add collision avoidance
- Ensure no jitter

### Task 4 — World
- Add NavigationRegion3D
- Add fence and building collisions
- Add clean farm paths
- Keep 24 farm plots
- Create work points around each plot

### Task 5 — NPC
- Replace direct patrol movement with NavigationAgent3D
- Add Idle/Travel/Work/Talk states
- Add schedules for three NPCs
- Add separation/avoidance

### Task 6 — Interaction
- Raycast or area-based target selection
- Context prompt
- Reachability validation
- Actor walks to work point
- Only apply domain state after animation event

## Coding Standards
- Typed GDScript
- Explicit return types
- Signals for cross-system communication
- Resources/JSON for definitions
- No magic strings outside data/constants
- No duplicate source of truth
- No silent exception swallowing
- Human-readable diagnostics

## Definition of Done
A task is complete only when:
- Godot imports without script errors
- Runtime marker appears
- Feature is visibly testable
- Save impact documented
- UI tested at 1280×720 and 1920×1080
- No regression to legacy workflows

## Reporting Template

```text
Summary:
Changed files:
Implemented behavior:
Tests run:
Test results:
Runtime markers:
Windows artifact:
Known warnings:
Known limitations:
Recommended next task:
```

## Do Not
- Merge automatically before all required checks pass
- Delete legacy files
- Fake animation by changing values without movement
- Claim visual quality was manually inspected if only headless tests ran
- Add hundreds of assets before the vertical slice loop is stable
