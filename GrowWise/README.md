# GrowWise: My Learning Garden

**ปลูกเป็น: สวนทดลองของเรา** เป็นเกมเรียนรู้การปลูกผักแบบ 2D Isometric Pixel Art สร้างด้วย Godot 4.6.3 ภาษาไทยเป็นค่าเริ่มต้น และสลับเป็น English ได้ในเกม

โปรเจกต์อยู่ในโฟลเดอร์ `GrowWise/` แยกจาก WorldForge เดิมเพื่อไม่เขียนทับระบบหรือทรัพย์สินของเกมเดิม

## ระบบเกม

### Phase 1 — Core Farming

- แผนที่ Isometric ขนาด 10×8 ช่อง ใช้ Tile 128×64 px
- ตัวละครและ Crop frame 64×64 px
- เดินด้วย WASD หรือปุ่มลูกศร และเลือกช่องด้วยเมาส์
- พรวนดิน ปลูก รดน้ำ ใส่ปุ๋ย ถอนวัชพืช ตรวจพืช เก็บเกี่ยว และถอนต้นตาย
- Inventory, Auto Save, Manual Save/Load 3 ช่อง และ Backup recovery

### Phase 2 — Educational Systems

- พืช 5 ชนิด: ผักบุ้ง คะน้า พริก มะเขือเทศ และแตงกวา
- พืชแต่ละชนิดมีวงจรเติบโตและสถานะผิดปกติ เช่น ขาดน้ำ น้ำมาก ดินไม่สมบูรณ์ แมลง โรค และตาย
- Workflow วินิจฉัย: สังเกตข้อมูล → เลือกสาเหตุ → รับผลตอบกลับ
- ครูเมล็ดพันธุ์ ภารกิจการเรียนรู้ 7 บท และสมุดความรู้ที่ปลดล็อกจากเหตุการณ์จริง
- ค่า pH และ NPK แสดงหลังซื้อเครื่องมือตรวจ

### Phase 3 — Ecosystem

- เพลี้ย หนอนกินใบ หอยทาก และแมลงหวี่ขาว
- เต่าทอง ผึ้ง ผีเสื้อ ไส้เดือน กบ และนกตัวเล็ก
- โรค วัชพืช ระบบแมลงที่มีประโยชน์ และ biological control
- เศษอินทรีย์ → ปุ๋ยหมัก → ฟื้นฟูดิน
- 9 สภาพอากาศและ 4 ฤดูกาล ส่งผลต่อความชื้น การระเหย แสง อุณหภูมิ โรค แมลง และการเติบโต

### Phase 4 — Experiment and Economy

- ห้องทดลอง A/B/C เปรียบเทียบการให้น้ำสามแนวทาง
- กราฟการเติบโต ผลผลิต น้ำที่ใช้ ต้นทุน และคุณภาพ
- ร้านเมล็ด เครื่องมือ ปุ๋ยหมัก ปุ๋ยอินทรีย์ และสเปรย์ชีวภาพ
- ตลาดรับซื้อผลผลิตตามชนิด ปริมาณ และคุณภาพ
- บันทึกต้นทุน รายได้ กำไร สุขภาพดิน ประสิทธิภาพน้ำ และความหลากหลายทางชีวภาพ
- สรุปผลเมื่อเปลี่ยนฤดูกาล

### Phase 5 — Polish

- Animation แบบ Pixel Art และเอฟเฟกต์ฝน/หมอก
- Sound effect ต้นฉบับที่สร้างด้วยระบบ Procedural Audio ภายในโปรเจกต์
- Tutorial/Quest progression จากการกระทำจริง
- Accessibility: High Contrast, Reduced Motion, Large Text และเปิด/ปิดเสียง
- ภาษาไทยและ English จากไฟล์ JSON
- Portable Windows build พร้อม README และ SHA-256 checksum

## การควบคุม

| การควบคุม | การทำงาน |
|---|---|
| WASD / Arrow Keys | เดินตัวละคร |
| Mouse Move | เลือกช่องแปลง |
| Left Click | ใช้เครื่องมือหรือเลือกปุ่ม |
| Right Click | ตรวจพืชอย่างรวดเร็ว |
| Space | หยุด/เดินเวลา |
| `-` / `=` | ลด/เพิ่มความเร็ว x1/x2/x4 |
| `1`–`5` | เลือกผักบุ้ง คะน้า พริก มะเขือเทศ หรือแตงกวา |
| F1 / F2 / F3 | เลือกช่องบันทึก 1–3 |
| Esc | ปิดหน้าต่างหรือเปิดเมนูหลัก |

## เปิดใน Godot

1. ติดตั้ง Godot 4.6.3
2. เปิด `GrowWise/project.godot`
3. กด Run Project

ข้อมูล Static Definition อยู่ใน `data/game_data.json` และข้อความอยู่ใน `localization/th.json` กับ `localization/en.json` เพื่อให้แก้เนื้อหาโดยไม่ผูกกับ UI

## Windows Portable Build

GitHub Actions workflow `Build GrowWise Windows` ทำงานเมื่อมีการเปลี่ยนแปลงใน `GrowWise/` โดยดำเนินการ:

1. Import ด้วย Godot 4.6.3
2. ตรวจ GDScript parse/compile error
3. รัน Gameplay smoke marker `GROWWISE_SMOKE_OK`
4. รัน Simulation marker `GROWWISE_PHASES_2_5_OK`
5. Export `GrowWise.exe`
6. สร้าง `README_PLAY.txt` และ `SHA256SUMS.txt`
7. อัปโหลด Artifact ชื่อ `GrowWise-Windows-Complete`

ไฟล์หลัก:

```text
GrowWise/builds/windows/GrowWise.exe
```

## ระบบภาพและเสียง

Asset ทั้งหมดเป็นงานต้นฉบับจาก `scripts/art_factory.gd` และ `scripts/audio_factory.gd` ไม่ใช้ Sprite, Icon หรือเสียงจากเกมอื่น

- Atlas PNG: 2048×1152 px พื้นหลังโปร่งใส
- Tile: 128×64 px
- Icon/Crop/Character: 64×64 px
- Building: 128×128 px
- Nearest-neighbor filtering
- แสงด้านบนซ้าย เงาด้านล่างขวา

เมื่อเปิดเกม ระบบบันทึก Atlas ที่ใช้จริงไว้สำหรับ QA ที่:

```text
user://generated_assets/growwise_full_atlas.png
```

## Save Location

Save อยู่ที่ `user://saves` จึงรองรับ Windows user path ภาษาไทยและไม่ต้องใช้สิทธิ์ Administrator

## ขอบเขตของรุ่นนี้

ระบบทุก Phase ตาม Roadmap ถูกเชื่อมเป็น playable educational farming game แล้ว แต่ยังเป็นเกมขนาดกะทัดรัด ไม่ใช่ production game เชิงพาณิชย์ขนาดใหญ่ ไม่มี online multiplayer, cloud save, voice acting หรือ content editor ภายนอกเกม
