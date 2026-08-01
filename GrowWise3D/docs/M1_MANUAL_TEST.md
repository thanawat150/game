# GrowWise3D M1 Manual Test

Status: `MANUAL_VISUAL_TEST_PENDING`

Automated screenshots were generated for Morning, Day, Evening, Night, selected plot, and NPC overview. Their manifest states `AUTOMATED_SCREENSHOT_ARTIFACT_NOT_MANUAL_VISUAL_TEST`; they do not mark any checklist item below as passed.

## Checklist

- [ ] เปิดเกมได้
- [ ] Player เดินได้
- [ ] Player วิ่งได้
- [ ] Player หมุนตามทิศ
- [ ] Player ไม่เดินทะลุบ้าน
- [ ] Player ไม่เดินทับแปลง
- [ ] กล้องติดตามนุ่ม
- [ ] Zoom ได้
- [ ] NPC ครบ 3 คน
- [ ] NPC เดินอ้อมสิ่งกีดขวาง
- [ ] NPC ไม่ซ้อนกัน
- [ ] กด E คุยกับ NPC ได้
- [ ] กด E ตรวจแปลงได้
- [ ] UI ไม่ซ้อนที่ 720p
- [ ] UI ไม่ซ้อนที่ 1080p
- [ ] Save และ Load ได้
- [ ] เล่นต่อเนื่อง 30 นาทีไม่ Crash

## Automated Evidence Only

- Headless scene/runtime validation confirms Player, Camera, NavigationRegion3D, 3 unique NPC IDs, 24 unique plot IDs, InteractionManager, SaveManager v2, and all runtime markers exist.
- A real physics test confirms World Static collision blocks interaction line of sight.
- Isolated persistence tests confirm valid JSON, backup retention, corrupt-primary recovery, and future-version read-only behavior.
- Screenshot automation completed locally with six non-empty PNG files.

## Pending Manual Work

- Keyboard movement and collision feel.
- Camera follow and zoom feel.
- NPC obstacle avoidance and overlap duration during play.
- Interaction UX with plots and all NPCs.
- Thai glyph readability and UI overlap at target resolutions.
- 30-minute stability session.
