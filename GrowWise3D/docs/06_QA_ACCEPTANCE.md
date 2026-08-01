# 06 — QA & Acceptance Criteria

## Gate A — Project Health
- Godot 4.6.3 import without Parse/Compile Error
- Main3D opens without missing resource
- Headless smoke marker `GROWWISE3D_SCAFFOLD_OK`
- Windows x86-64 export succeeds
- Legacy GrowWise and WorldForge workflows still pass

## Gate B — Player
- Walk in 8 directions
- Sprint toggle/hold
- Smooth rotation
- Gravity and slope handling
- Collision with building, tree, fence and plot border
- No input while menu is open
- Animation state returns to Idle

## Gate C — Camera
- Follow without jitter
- Zoom limits work
- No clipping through terrain
- Reset view works
- Isometric framing keeps player and work target visible

## Gate D — Farming
- Select correct plot by raycast
- Walk to work point before action
- Hoe/plant/water/harvest animation visible
- Inventory changes exactly once
- Plot visual refreshes after state change
- Save/load preserves all plot values

## Gate E — NPC
- At least 3 NPCs follow schedules
- Avoid obstacles
- Do not overlap longer than 2 seconds
- Stop and face player during dialogue
- Resume schedule after dialogue
- Save/load preserves schedule state

## Gate F — Auto Farm
- Visible movement begins within 3 seconds
- Current task and next tasks shown
- Task states follow queue lifecycle
- Blocked task gives reason
- Stop button prevents new tasks
- No duplicate harvest or double inventory change

## Gate G — Visual
- Exposure retains details in sunlit objects
- Shadows readable but not black
- Day/night transitions smooth
- Rain darkens scene and wets ground
- No Z-fighting or transparent sorting error
- No object visibly floating or sinking

## Gate H — UI
Test at 1280×720, 1920×1080, 2560×1440 and 4K:
- No text overlap
- No button outside safe area
- Tooltips readable
- Thai glyphs complete
- Scale slider works
- Keyboard/controller focus order correct

## Gate I — Save
- New save
- Load same session
- Restart application and load
- 20 save/load cycles
- Legacy v1 migration
- Corrupt save fallback
- Newer unsupported version is read-only

## Performance Targets
- 60 FPS target at 1080p medium
- Minimum 45 FPS under stress scene
- Initial load under 15 seconds on SSD
- Zone streaming spike under 50 ms average frame hitch
- Memory under 4 GB for vertical slice

## Stress Scenario
- 24 mature crop plots
- 20 NPCs
- Rain and wind
- 2 active machines
- Auto Farm queue 20 tasks
- Vehicle moving
- UI inventory 500 entries

## Manual Playtest Script
1. Start new game
2. Walk around entire farm
3. Talk to all NPCs
4. Till, plant, water and harvest
5. Enable Auto Farm
6. Trigger rain
7. Save and reload
8. Change resolution
9. Play for 30 minutes
10. Exit normally

## Release Status Values
- blocked
- failed
- passed_with_warnings
- release_candidate
- approved

## No-Merge Rule
ห้าม Merge เข้า main ถ้า Gate A–F ไม่ผ่านครบ หรือยังมี Runtime Error ที่กระทบ Player, Camera, Interaction, Save หรือ Auto Farm
