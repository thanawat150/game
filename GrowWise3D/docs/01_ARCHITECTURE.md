# 01 — Architecture

## Architectural Direction
สร้างโปรเจกต์ 3D ใหม่แยกจาก GrowWise เดิม แล้วค่อยย้าย Domain Logic ที่ผ่านการทดสอบมาใช้ผ่าน Adapter ห้ามสืบทอดสคริปต์ภาพ 2D เดิมหรือใช้ `_draw()` เป็น World Renderer

## Layers

### Presentation
- Node3D scenes
- CharacterBody3D
- AnimationTree
- Camera3D
- CanvasLayer UI
- Audio/VFX

### Application
- InteractionManager
- TaskQueue
- AutoFarmCoordinator
- QuestManager
- NPCScheduleManager
- ConstructionManager

### Domain
- FarmingSimulation
- SoilModel
- WaterModel
- LivestockModel
- ProcessingModel
- EconomyModel
- TimeWeatherModel

### Infrastructure
- SaveManager
- DataRepository
- AssetLoader
- ChunkStreamer
- Telemetry/Diagnostics

## Autoloads ที่เสนอ
- GameState.gd
- DataRepository.gd
- SaveManager.gd
- TimeManager.gd
- AudioManager.gd
- SceneRouter.gd

## Scene Ownership
- Main3D: lifecycle และ composition เท่านั้น
- WorldRoot: terrain, zones, streaming
- Player: input, locomotion, animation
- NPC: state machine, schedule, navigation
- FarmPlot: visual + interaction endpoint
- Systems: ห้ามอ้าง UI โดยตรง
- UI: อ่าน ViewModel/Signal เท่านั้น

## Signal Contract
- day_changed(day)
- time_changed(minutes)
- weather_changed(weather_id)
- plot_changed(plot_id, snapshot)
- inventory_changed(snapshot)
- task_started(task_id)
- task_finished(task_id, result)
- interaction_available(target_id, prompt)
- save_completed(slot)

## State Machines

### Player
Idle → Walk → Run → Interact → Work → Carry → Vehicle

### NPC
Idle → Travel → Work → Talk → Eat → Rest → ReturnHome

### Auto Task
Queued → Reserved → Traveling → Performing → Completed / Failed / Blocked

## Coordinate Convention
- Y ขึ้น
- Z เดินหน้า/หลัง
- 1 World Unit = 1 เมตร
- Farm Plot มาตรฐาน 2×2 เมตร
- Origin ของแต่ละ Zone อยู่กึ่งกลางพื้นที่

## Collision Layers

1. World Static — terrain, buildings, fences, trees, rocks, wells and plot borders
2. Player
3. NPC
4. Interactable areas
5. Vehicle
6. Water
7. Trigger volumes

Physical actors collide with World Static and the actor layers they need. Interaction queries use layer 4 for candidate discovery and layer 1 for line-of-sight blocking.

## World Scene Ownership

วัตถุหลักของโลกต้องเป็น `.tscn` แยกและเป็นเจ้าของ Geometry, Collision และ Material ของตัวเอง เช่น Terrain, PlayerHouse, StorageShed, Well, FenceSection, Tree และ Rock

`world_bootstrap.gd` ทำได้เฉพาะอ่านข้อมูลตำแหน่ง, instantiate PackedScene, กำหนด ID/configuration และ parent instance ไปยัง root ที่ถูกต้อง ห้ามสร้าง Mesh, Collision Shape, Material, Navigation Behavior หรือ Gameplay Logic ส่วนใหญ่ใน bootstrap

## Naming Convention
- Scene: PascalCase.tscn
- Script: snake_case.gd
- Resource ID: category_name_variant
- Save key: snake_case
- Signal: past tenseหรือ event noun

## Performance Rules
- MultiMesh สำหรับพืชจำนวนมาก
- LOD 0/1/2 สำหรับต้นไม้ อาคาร เครื่องจักร
- Chunk 128×128 เมตร
- จำกัด Shadow Distance
- NPC ระยะไกลใช้ Simplified Simulation
- Async load asset หนัก
- Texture compression ตาม target Windows

## Migration Strategy
1. Freeze legacy visual scripts
2. Copy data schemas
3. Create adapters for crops/inventory/time
4. Migrate one vertical slice
5. Verify save migration
6. Expand zone by zone

## Forbidden Patterns
- God script ขนาดใหญ่ไฟล์เดียว
- UI แก้ domain state โดยตรง
- Hard-code พิกัดเมนูทุกความละเอียด
- เปลี่ยน state โดยไม่ emit signal
- Auto Farm เปลี่ยนผลลัพธ์ทันทีโดยไม่มี task lifecycle
- NPC teleport เพื่อทำงานโดยไม่บันทึกเหตุผล
