# 05 — UI/UX Specification

## Design Goal
ลดความแน่นของหน้าจอเดิม แสดงเฉพาะข้อมูลที่ใช้ตัดสินใจทันที และย้ายระบบรองไปยังเมนูแบบ Tab/Radial โดยห้ามข้อความซ้อนหรือปุ่มตัดคำผิด

## HUD Layout

### Top Left
- Day / Time / Season / Weather
- Money
- Stamina

### Top Center
- Contextual status only
- Auto task current action
- Warning banner

### Top Right
- Tracked quest
- Notification stack max 3

### Bottom Center
- Toolbelt 8 slots
- Equipped item
- Quick quantity

### Bottom Right
- Mini map
- World map button
- Vehicle state

### Context Prompt
- `[E] ตรวจแปลง`
- `[F] ใช้เครื่องมือ`
- แสดงระยะและเหตุผลเมื่อใช้ไม่ได้

## Main Tabs
1. Inventory
2. Crafting
3. Map
4. Journal
5. Farming
6. Animals
7. Machinery
8. Town
9. Settings

## Responsive Rules
- ใช้ MarginContainer, VBoxContainer, HBoxContainer, GridContainer
- ห้ามใช้ absolute position ยกเว้น overlay เฉพาะกิจ
- รองรับ 1280×720, 1920×1080, 2560×1440, 3840×2160
- UI scale 80–160%
- Text wrapping เปิดเฉพาะ description
- Button title ไม่เกิน 18 ตัวอักษรใน HUD
- ชื่อเต็มแสดงใน tooltip

## Auto Farm Panel
- Toggle: Off / Assist / Full Auto / Learning
- Current task
- Next 3 tasks
- Blocked reason
- Energy/fuel
- Recent history 10 entries
- Emergency stop

## Plot Inspector
- Crop and stage
- Moisture
- Fertility
- Water level
- Health
- Pest/disease/weed
- Recommendation
- Estimated harvest time

## Color Semantics
- Green: ready/safe
- Yellow: attention
- Orange: urgent
- Red: blocked/danger
- Blue: water/research
- Purple: automation/technology

## Accessibility
- Thai default
- English optional
- Font size slider
- High contrast mode
- Reduced motion
- Colorblind-safe status icons
- Hold/toggle sprint option
- Rebindable controls
- Subtitle and sound cue labels

## Acceptance
- ไม่มีข้อความซ้อนในทุก target resolution
- เมนูเปิดแล้วหยุด input โลก
- Mouse, keyboard และ controller navigation ผ่าน
- ทุกปุ่ม disabled มีคำอธิบาย
- ผู้เล่นใหม่เริ่มทำฟาร์มได้โดยไม่จำคีย์ลัด
