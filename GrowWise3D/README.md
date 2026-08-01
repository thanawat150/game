# GrowWise 3D Open World v2 — M1 Playable Foundation

โครงโปรเจกต์ใหม่สำหรับย้าย GrowWise จากระบบ 2D/ภาพ 3D ซ้อน ไปเป็นเกม 3D จริงใน Godot 4.6.3 โดยแยกจาก `GrowWise/` เดิมเพื่อป้องกันระบบปัจจุบันเสียหาย

## สถานะ

- เปิดเป็น Godot Project แยกได้จากโฟลเดอร์ `GrowWise3D/`
- มีโลก Node3D แบบ scene-first, Player CharacterBody3D, กล้อง Isometric, NavigationRegion3D, NPC 3 คน, FarmPlot 24 ช่อง, Interaction, HUD และ Save v2
- งานภาพเป็น realistic procedural prototype ที่วางขอบเขตสำหรับเปลี่ยนเป็น production GLB/PBR ภายหลัง
- ห้าม Merge เข้า `main` จนผ่าน Acceptance Criteria ใน `docs/06_QA_ACCEPTANCE.md`

## วิธีเปิด

1. เปิด Godot 4.6.3
2. Import `GrowWise3D/project.godot`
3. กด Run Project

## การควบคุม

- WASD / ลูกศร: เดิน
- Shift: วิ่ง
- E: โต้ตอบ
- Mouse Wheel: Zoom
- R: คืนมุมกล้อง
- T: สลับ Morning / Day / Evening / Night
- F3: เปิด–ปิด Diagnostics
- F5: Save
- F9: Load
- ESC: ออก

## Save

- Version: `2`
- Path: `user://growwise3d_save_v2.json`
- แยกจาก Save ของ GrowWise เดิม
- ใช้ temporary write, JSON/schema validation, valid backup และ corrupt recovery

## เป้าหมาย Vertical Slice

พื้นที่แรกประกอบด้วย บ้านผู้เล่น แปลงฟาร์ม โรงเก็บของ บ่อน้ำ NPC 3 คน เครื่องจักร 2 ชนิด และระบบฟาร์มที่เห็นตัวละครเดินไปทำงานจริง

## หลักการสำคัญ

1. โลกทั้งหมดใช้ Node3D/CharacterBody3D/Camera3D
2. UI อยู่บน CanvasLayer และใช้ Container
3. ระบบ Simulation แยกจากงานภาพ
4. ทุก Action ต้องมี Movement + Animation + State Change
5. Auto Farm ต้องแสดงคิวและมีวัตถุเคลื่อนไหวจริง
6. Save เก่าต้องมี Migration Path

## โครงสร้าง

```text
GrowWise3D/
├─ Main3D.tscn
├─ project.godot
├─ scenes/
│  ├─ farming/FarmPlot.tscn
│  ├─ npc/NPCBase.tscn
│  └─ player/Player.tscn
├─ scripts/
│  ├─ camera/camera_rig.gd
│  ├─ core/game_root.gd
│  ├─ farming/farm_plot.gd
│  ├─ npc/npc_controller.gd
│  ├─ player/player_controller.gd
│  └─ world/world_bootstrap.gd
└─ docs/
```

## งานถัดไป

- เปลี่ยนโมเดล placeholder เป็น GLB พร้อม AnimationTree
- เพิ่ม Farming Actions ที่มีการเดินไป Work Point และ Animation Event ก่อนเปลี่ยนข้อมูล
- เชื่อมข้อมูลพืช/คลัง/เวลา/อากาศจากเกมเดิม
- เปลี่ยน procedural prototype เป็น production assets และปรับ HUD จากผล manual QA
