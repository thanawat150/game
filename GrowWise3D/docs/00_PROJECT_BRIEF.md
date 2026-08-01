# 00 — Project Brief

## ชื่อโครงการ
GrowWise 3D Open World v2

## Product Vision
เกมจำลองฟาร์ม ชุมชน และระบบนิเวศแบบ 3D Isometric Open World ที่ผู้เล่นเริ่มจากแปลงเรียนรู้ขนาดเล็ก แล้วสำรวจพื้นที่ วิเคราะห์ดิน จัดการน้ำ เลี้ยงสัตว์ แปรรูป ขนส่ง และพัฒนาเป็นชุมชนที่ยั่งยืน

## กลุ่มผู้เล่น
- ผู้เล่นเกมฟาร์มและ Life Simulation
- ผู้สนใจเกษตร ดิน น้ำ สิ่งแวดล้อม และการจัดการพื้นที่
- ผู้เรียนที่ต้องการเข้าใจระบบเกษตรผ่านการทดลอง
- ผู้ชมคอนเทนต์ AI-assisted game development

## เสาหลักของเกม
1. Farming with visible cause and effect
2. Explore, sample, analyze, decide
3. Water and landscape management
4. Farm-to-town production chain
5. Living NPC community
6. Automation that remains observable
7. Thai-first accessible interface

## Core Loop
สำรวจ → เก็บตัวอย่าง → วิเคราะห์ → วางแผน → ปลูก/เลี้ยง → แก้ปัญหา → เก็บเกี่ยว → แปรรูป → ขนส่ง → ขยายพื้นที่และชุมชน

## Vertical Slice แรก
- บ้านผู้เล่น
- แปลง 24 ช่อง
- โรงเก็บของ
- บ่อน้ำ
- NPC 3 คน
- รถพรวนและสปริงเกลอร์
- พืช 2 ชนิด: ผักบุ้งและคะน้า
- พรวน ปลูก รดน้ำ เติบโต เก็บเกี่ยว
- Auto Farm แบบเห็นการเคลื่อนไหว
- กลางวัน–กลางคืน 1 วัน
- Save/Load 1 Slot

## Non-goals ในรอบแรก
- โลกครบ 6 เขต
- Multiplayer
- Combat
- ระบบเศรษฐกิจเชิงลึก
- โมเดลสุดท้ายทุกชนิด
- Mobile export

## Platform
- Godot 4.6.3
- Windows 11 x86-64
- Thai default, English optional
- Target resolution 1920×1080, รองรับ 1280×720 ถึง 4K

## Quality Bar
- ตัวละครเดินและทำ Animation จริง
- NPC เดินรอบแปลงได้
- ไม่มีวัตถุซ้อนผิดปกติ
- แสงอ่านง่ายและไม่ขาวโพลน
- UI Responsive
- 45–60 FPS บนเครื่องระดับกลาง
- เล่นต่อเนื่อง 30 นาทีโดยไม่ Crash
