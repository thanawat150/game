# WorldForge Phase 2–7 Vertical Slice

Status: `in_progress`

This increment connects the later roadmap phases to one deterministic, serializable simulation state. It is an MVP vertical slice, not the final production implementation of every feature in the original design charter.

## Implemented systems

### Phase 2 — Ecosystem

- Grazer, predator, settler, monster, and fish entity types
- Hunger, health, energy, daily needs processing
- Predator/prey interaction
- Terrain-aware food gathering
- Death cleanup and Chronicle events

### Phase 3 — Civilization

- Settlement founding from a real group of settlers
- Camp, village, town, city, and capital stages
- Food, wood, stone, gold, housing, happiness, and technology state
- Campfire, house, farm, and market construction rules
- Settlement leader selection and territory seed tile

### Phase 4 — Kingdoms

- Kingdom founding from a settlement
- Capital, ruler, government, economy, army strength, and stability
- Symmetric diplomacy values from -100 to 100
- War, hostile, neutral, friendly, and alliance relation states

### Phase 5 — World depth

- Culture and religion influence state
- Data-driven technology IDs and deterministic research attempts
- World ages and Chronicle history
- Versioned advanced simulation save/load

### Phase 6 — God powers

- Forest creation
- Knowledge, blessing, curse, lightning, plague, and meteor effects
- Powers modify real terrain or entity state and write Chronicle events

### Phase 7 — Advanced systems

- Disease infection, spread, duration, and mortality
- Basic heritable trait-ready entity model
- JSON-compatible advanced state
- Mod manifest validation for IDs, versions, dependencies, and content types

## Validation

`GrandSimulationTests.cs` covers:

- predator/prey behavior
- settlement and kingdom creation
- diplomacy symmetry
- real god-power effects
- advanced save/load round trip
- disease processing
- mod validation

## Not yet production-complete

- visual entity sprites and animation
- full movement/pathfinding and migration
- reproduction and family UI
- naval combat and sea-route simulation
- detailed war armies, siege, occupation, and annexation
- procedural culture/religion naming and schisms
- full economy prices, crafting, tax, and trade routes
- full political succession, rebellion, election, and civil war
- mod file discovery and runtime asset loading
- mobile optimization and controls
- full localization coverage
- performance validation with tens of thousands of entities

These remain follow-up production milestones. The current increment establishes functional core behavior and stable state models so those systems can be expanded without returning to mock-only architecture.
