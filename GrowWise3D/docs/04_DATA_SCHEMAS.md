# 04 — Data Schemas

## Save Envelope

```json
{
  "save_version": 2,
  "created_at": "ISO-8601",
  "updated_at": "ISO-8601",
  "play_seconds": 0,
  "game": {},
  "player": {},
  "world": {},
  "systems": {}
}
```

## Player

```json
{
  "position": [0.0, 0.0, 0.0],
  "rotation_y": 0.0,
  "stamina": 100.0,
  "equipped_tool": "hoe",
  "outfit_id": "outfit_farm",
  "vehicle_id": "",
  "inventory": {"item_id": 0}
}
```

## Farm Plot

```json
{
  "plot_id": "farm_00_00",
  "world_position": [0.0, 0.0, 0.0],
  "tilled": false,
  "crop_id": "",
  "growth_stage": 0,
  "growth_progress": 0.0,
  "moisture": 40.0,
  "fertility": 65.0,
  "health": 100.0,
  "water_level": 0.0,
  "weed_level": 0.0,
  "pest_level": 0.0,
  "disease_level": 0.0,
  "soil_sample_id": ""
}
```

## Crop Definition

```json
{
  "id": "water_spinach",
  "name_th": "ผักบุ้ง",
  "name_en": "Water spinach",
  "stages": 6,
  "stage_minutes": [0, 180, 300, 480, 660, 840],
  "ideal_moisture": [45, 75],
  "ideal_ph": [5.5, 7.0],
  "temperature": [22, 35],
  "yield_base": 4,
  "seed_item": "seed_water_spinach",
  "produce_item": "produce_water_spinach",
  "model_by_stage": []
}
```

## NPC

```json
{
  "npc_id": "seed_teacher",
  "display_name": "ครูเมล็ดพันธุ์",
  "position": [0.0, 0.0, 0.0],
  "rotation_y": 0.0,
  "state": "idle",
  "schedule_id": "seed_teacher_default",
  "schedule_step": 0,
  "relationship": 0,
  "current_task": "",
  "conversation_flags": []
}
```

## Task Queue

```json
{
  "task_id": "task_uuid",
  "type": "water_plot",
  "actor_id": "player_or_machine",
  "target_id": "farm_00_00",
  "state": "queued",
  "priority": 80,
  "created_minute": 480,
  "started_minute": null,
  "blocked_reason": "",
  "payload": {}
}
```

## Construction

```json
{
  "construction_id": "canal_0001",
  "type": "drainage_channel",
  "transform": {},
  "control_points": [],
  "level": 1,
  "condition": 100.0,
  "enabled": true
}
```

## World Chunk

```json
{
  "chunk_id": "farm_0_0",
  "zone_id": "learning_farm",
  "bounds": [0, 0, 128, 128],
  "discovered": true,
  "resource_nodes": [],
  "construction_ids": [],
  "npc_ids": []
}
```

## Save Migration v1 → v2

- แปลงตำแหน่งแปลง Vector2 เป็น Vector3
- Map inventory key เดิมไป Item ID ใหม่
- Auto mode เดิมแปลงเป็น AutoFarmPolicy
- Workforce data ไม่ย้ายกลับมา
- Machinery data ย้ายเป็น Actor + Condition
- Open-world discovery ย้ายเป็น chunk discovery
- เก็บไฟล์ต้นฉบับเป็น `.legacy.bak`

## Validation Rules

- ค่า percentage อยู่ 0–100
- ทุก ID ต้องมี definition
- ตำแหน่งต้องเป็นตัวเลข finite
- Task actor/target ต้องมีอยู่จริงหรือถูก mark orphaned
- Save version ที่สูงกว่าโปรแกรมต้องไม่ถูกเขียนทับ
- Save เสียต้อง fallback ไป backup ล่าสุด
