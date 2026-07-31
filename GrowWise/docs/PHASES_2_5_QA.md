# GrowWise Phases 2–5 QA Matrix

Build target: Godot 4.6.3, Windows 11 x86-64 portable package.

## Phase 2 — Educational Systems

- Five crop definitions load from `data/game_data.json`.
- Each crop exposes growth and stress states.
- Diagnosis compares the selected cause with the simulated primary symptom.
- Seven learning quests progress from recorded gameplay events.
- Knowledge notebook unlocks entries from observed events.
- Thai and English text load from separate JSON files.

## Phase 3 — Ecosystem

- Daily simulation updates soil moisture from rain, drainage and evaporation.
- Pest, disease, weed and beneficial-creature values change through deterministic seeded simulation.
- Biological spray lowers pest pressure.
- Organic waste advances compost production.
- Compost restores fertility, pH balance and beneficial activity.
- Four seasons and nine weather states affect crop simulation.

## Phase 4 — Experiment and Economy

- Lab runs A/B/C watering strategies with the same crop definition.
- Result view reports growth, yield, water use, cost and quality.
- Shop transactions verify available money and update inventory and expenses.
- Market transactions use crop amount and average quality to calculate revenue.
- End-of-season report includes yield, water, costs, revenue, profit, soil, environment, biodiversity and knowledge.

## Phase 5 — Polish

- Original pixel-art atlas is generated at runtime and saved as a transparent PNG for QA.
- Original procedural sound effects are generated in the project.
- Animation, weather effects and reduced-motion setting are connected.
- High contrast, large text, sound toggle and Thai/English toggle are connected.
- Save version 5 persists gameplay, ecosystem, education, experiment, economy and accessibility state.
- CI requires `GROWWISE_SMOKE_OK` and `GROWWISE_PHASES_2_5_OK` before Windows export.
- Portable artifact contains `GrowWise.exe`, `README_PLAY.txt` and `SHA256SUMS.txt`.

## Automated Acceptance

The build is accepted only when:

1. Godot imports the project without parse or compile errors.
2. Runtime starts without GDScript errors.
3. Simulation self-test grows a crop and produces three experiment results.
4. Both smoke markers are emitted.
5. Windows export produces a non-empty executable.
6. GitHub Actions uploads the portable artifact.
