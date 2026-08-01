# 02 — Roadmap & Backlog

## Milestone 0 — Foundation Lock
- Tag legacy release
- Create GrowWise3D project
- Add CI import/smoke/export
- Add save version 2 schema
- Add data adapter boundary

Exit criteria: โปรเจกต์ใหม่เปิดได้, legacy ไม่เปลี่ยน, CI ผ่าน

## Milestone 1 — Playable 3D Foundation
- CharacterBody3D controller
- Isometric camera rig
- Collision and slope handling
- 24 reusable farm plots
- Basic environment and lighting
- Responsive HUD shell
- Interaction prompt

Exit criteria: เดินรอบฟาร์ม เลือกแปลง และไม่ทะลุวัตถุ

## Milestone 2 — Visible Farming
- Tool socket and equipment models
- Hoe/plant/water/harvest animations
- Crop 3D stages for water spinach and kale
- Plot work points
- Task lifecycle
- Floating feedback
- Basic inventory bridge

Exit criteria: ทุก action มีเดินไปหา เล่น animation แล้วจึงเปลี่ยน state

## Milestone 3 — NPC & Auto Farm
- NavigationRegion3D
- NPCBase state machine
- 3 NPC schedules
- Auto task queue
- Tractor and sprinkler actors
- Blocked reason reporting

Exit criteria: NPC และเครื่องจักรเดินทางจริง ไม่มีการเปลี่ยนค่าลับหลังฉาก

## Milestone 4 — Water & Weather
- Day/night cycle
- Weather profiles
- Wet ground shader
- Drainage channel prototype
- Water storage pond
- Flood warning

Exit criteria: ฝนมีผลต่อแปลงและภาพ ระบบระบายน้ำแก้ปัญหาได้

## Milestone 5 — Save & Migration
- Save v2 writer/reader
- Legacy save importer
- Player/NPC/plot/task persistence
- Recovery backup
- Corrupt save handling

Exit criteria: Save/Load 20 รอบโดย state ไม่สูญหาย

## Milestone 6 — Vertical Slice Polish
- Replace placeholder models
- PBR materials
- Animation polish
- Sound and VFX
- UI 720p/1080p/1440p/4K
- 30-minute manual test
- Windows release artifact

Exit criteria: พร้อมส่งให้ผู้ทดสอบภายนอก

## Post Vertical Slice
- Village zone
- River/fishing
- Soil lab
- Livestock
- Processing
- Vehicles/logistics
- Forest/highland/wetland
- Full open-world streaming
- Housing/customization
- Main story chapter: flood crisis

## Priority Matrix

### P0 — Blocker
- Player movement
- Camera
- Collision
- Scene architecture
- Input
- Save compatibility
- CI

### P1 — Core Experience
- Farm interactions
- Crop visuals
- NPC navigation
- Auto task visualization
- Lighting
- Responsive UI

### P2 — Depth
- Water channels
- Weather
- Soil lab
- Livestock
- Processing
- Vehicles

### P3 — Polish
- Photo mode
- Cosmetics
- Festivals
- Advanced economy
- Additional crops

## Definition of Done per Task
- Code reviewed
- No parse/runtime error
- Unit or smoke test added
- Save impact documented
- UI impact checked at 720p and 1080p
- No legacy regression
- Documentation updated
