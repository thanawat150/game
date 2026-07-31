# GrowWise: My Learning Garden

**ปลูกเป็น: สวนทดลองของเรา** เป็นเกมเรียนรู้การปลูกผักแบบ 2D Isometric Pixel Art สร้างด้วย Godot 4 และใช้ภาษาไทยเป็นค่าเริ่มต้น

โปรเจกต์นี้อยู่ในโฟลเดอร์ `GrowWise/` แยกจากเกม WorldForge เดิมใน repository เพื่อไม่ให้ระบบหรือทรัพย์สินเดิมถูกเขียนทับ

## สถานะปัจจุบัน

Playable Phase 1 vertical slice ประกอบด้วย:

- แผนที่ Isometric ขนาด 8×8 ช่อง
- พื้นที่แปลงทดลอง 4×4 ช่อง
- เดินด้วย WASD หรือปุ่มลูกศร
- เลือก Tile ด้วยเมาส์
- พรวนดิน
- ปลูกผักบุ้งและคะน้า
- รดน้ำและดูความชื้นลดลงตามเวลา
- การเติบโต 5 ระยะ
- ตรวจดูน้ำ แสง ความสมบูรณ์ของดิน และระยะพืช
- เก็บเกี่ยวและเพิ่มผลผลิตเข้า Inventory
- ภารกิจเริ่มต้น “เมล็ดแรกของฉัน”
- เวลา Pause, x1, x2 และ x4
- Save/Load 3 ช่อง พร้อมไฟล์สำรองและ Auto Save
- UI ภาษาไทย
- Export Preset สำหรับ Windows Desktop

ระบบจาก Phase 2–5 เช่น โรค แมลง ระบบนิเวศ ห้องทดลอง ร้านค้า ตลาด และเนื้อหาการเรียนรู้ขั้นสูงยังไม่รวมอยู่ในรุ่นนี้

## การควบคุม

| การควบคุม | การทำงาน |
|---|---|
| WASD / Arrow Keys | เดินตัวละคร |
| Mouse Move | เลือกช่องแปลง |
| Left Click | ใช้เครื่องมือหรือเลือกปุ่ม |
| Space | หยุด/เดินเวลา |
| `-` / `=` | ลด/เพิ่มความเร็วเวลา |
| F1 / F2 / F3 | เลือกช่องบันทึก 1–3 |
| Esc | เปิด/ปิดเมนูหลัก |

## เปิดใน Godot

1. ติดตั้ง Godot 4.6.3 หรือ Godot 4 รุ่นที่เข้ากันได้
2. เปิดไฟล์ `GrowWise/project.godot`
3. กด Run Project

## Windows Build

GitHub Actions workflow `Build GrowWise Windows` จะทำงานเมื่อมีการเปลี่ยนแปลงใน `GrowWise/` และสร้าง Artifact ชื่อ `GrowWise-Windows`

ไฟล์หลักหลัง Export:

```text
GrowWise/builds/windows/GrowWise.exe
```

## ระบบภาพ

Asset ภาพทั้งหมดเป็น Pixel Art ต้นฉบับที่สร้างโดย `scripts/art_factory.gd` ในรูปแบบ Atlas โปร่งใส โดยกำหนด:

- Isometric Tile: 128×64 px
- Tool/UI Icon: 64×64 px
- Crop Stage: 64×64 px
- Character Frame: 64×64 px
- แสงจากด้านบนซ้าย
- เงาไปด้านล่างขวา
- Nearest-neighbor filtering

เมื่อเกมเริ่มทำงาน ระบบจะบันทึกสำเนา Atlas ที่สร้างจริงเป็น PNG ไว้ที่:

```text
user://generated_assets/growwise_phase1_atlas.png
```

ไฟล์นี้ใช้สำหรับ QA และตรวจสอบว่า Asset ที่แสดงในเกมเป็นภาพจริง ไม่ใช่กล่อง Placeholder

## Save Location

Save ถูกเขียนใน `user://saves` เพื่อรองรับ Windows user path ที่มีภาษาไทยและไม่เขียนข้อมูลลงโฟลเดอร์ติดตั้งเกม

## ขอบเขตลิขสิทธิ์

โค้ด รูปแบบ UI ไอคอน Tile พืช และตัวละครใน GrowWise เป็นงานสร้างใหม่สำหรับโปรเจกต์นี้ และไม่ใช้ Sprite หรือทรัพย์สินจากเกมฟาร์มอื่น
