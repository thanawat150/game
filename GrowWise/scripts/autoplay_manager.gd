extends RefCounted
class_name GrowWiseAutoPlay

const MODE_OFF: int = 0
const MODE_ASSIST: int = 1
const MODE_FULL: int = 2
const MODE_LEARNING: int = 3

static func mode_name(mode: int, language: String = "th") -> String:
	var thai: Array[String] = ["ปิด", "ช่วยดูแล", "ฟาร์มอัตโนมัติ", "โหมดเรียนรู้"]
	var english: Array[String] = ["Off", "Care Assist", "Full Auto", "Learning Auto"]
	var names: Array[String] = thai if language == "th" else english
	return names[clampi(mode, MODE_OFF, MODE_LEARNING)]

static func action_delay(mode: int) -> float:
	match mode:
		MODE_ASSIST: return 1.15
		MODE_FULL: return 0.58
		MODE_LEARNING: return 1.85
		_: return 9999.0

static func _cell_from_key(key_string: String) -> Vector2i:
	var parts: PackedStringArray = key_string.split(",")
	if parts.size() != 2:
		return Vector2i(-1, -1)
	return Vector2i(int(parts[0]), int(parts[1]))

static func _candidate(priority: int, action: String, key_string: String, seed_id: String = "", detail: String = "") -> Dictionary:
	return {
		"priority": priority,
		"action": action,
		"cell": _cell_from_key(key_string),
		"seed": seed_id,
		"detail": detail
	}

static func _best_seed(inventory: Dictionary, crop_ids: Array[String], day: int) -> String:
	if crop_ids.is_empty():
		return ""
	for offset: int in range(crop_ids.size()):
		var index: int = posmod(day + offset, crop_ids.size())
		var crop_id: String = crop_ids[index]
		if int(inventory.get("seed_" + crop_id, 0)) > 0:
			return crop_id
	return ""

static func _produce_to_sell(inventory: Dictionary, crop_ids: Array[String]) -> String:
	var best_crop: String = ""
	var best_amount: int = 0
	for crop_id: String in crop_ids:
		var amount: int = int(inventory.get("produce_" + crop_id, 0))
		if amount > best_amount:
			best_amount = amount
			best_crop = crop_id
	return best_crop if best_amount >= 6 else ""

static func _seed_to_restock(inventory: Dictionary, crop_ids: Array[String]) -> String:
	var lowest_crop: String = ""
	var lowest_count: int = 999999
	for crop_id: String in crop_ids:
		var count: int = int(inventory.get("seed_" + crop_id, 0))
		if count < lowest_count:
			lowest_count = count
			lowest_crop = crop_id
	return lowest_crop if lowest_count <= 1 else ""

static func choose_action(tiles: Dictionary, inventory: Dictionary, crop_ids: Array[String], mode: int, day: int, money: int) -> Dictionary:
	if mode == MODE_OFF:
		return {}

	if mode == MODE_FULL:
		var sell_crop: String = _produce_to_sell(inventory, crop_ids)
		if not sell_crop.is_empty() and (money < 160 or int(inventory.get("produce_" + sell_crop, 0)) >= 10):
			return {"action":"sell", "crop":sell_crop, "priority":120, "detail":"sell"}
		var restock_crop: String = _seed_to_restock(inventory, crop_ids)
		if not restock_crop.is_empty() and money >= 24:
			return {"action":"restock", "crop":restock_crop, "priority":110, "detail":"restock"}

	var candidates: Array[Dictionary] = []
	var keys: Array = tiles.keys()
	keys.sort()
	for key_value: Variant in keys:
		var key_string: String = String(key_value)
		var tile_value: Variant = tiles.get(key_string, {})
		if not (tile_value is Dictionary):
			continue
		var tile: Dictionary = tile_value as Dictionary
		if not bool(tile.get("farm", false)):
			continue
		var crop_id: String = String(tile.get("crop", ""))
		var moisture: float = float(tile.get("moisture", 0.0))
		var fertility: float = float(tile.get("fertility", 0.0))
		var pest: float = float(tile.get("pest", 0.0))
		var disease: float = float(tile.get("disease", 0.0))
		var weed: float = float(tile.get("weed", 0.0))
		var stage: int = int(tile.get("stage", 0))
		var dead: bool = bool(tile.get("dead", false))

		if not crop_id.is_empty():
			if dead:
				candidates.append(_candidate(100, "remove", key_string, "", "dead"))
			elif stage >= 5:
				candidates.append(_candidate(96, "harvest", key_string, "", "ready"))
			elif (pest >= 34.0 or disease >= 30.0) and int(inventory.get("bio_spray", 0)) > 0:
				candidates.append(_candidate(92, "bio", key_string, "", "pest_disease"))
			elif weed >= 38.0:
				candidates.append(_candidate(86, "weed", key_string, "", "weed"))
			elif moisture <= 24.0:
				candidates.append(_candidate(82, "water", key_string, "", "dry"))
			elif fertility <= 36.0 and int(inventory.get("organic_fertilizer", 0)) > 0:
				candidates.append(_candidate(72, "fertilize", key_string, "", "poor_soil"))
			elif mode == MODE_FULL and fertility <= 46.0 and int(inventory.get("compost", 0)) > 0:
				candidates.append(_candidate(66, "compost", key_string, "", "restore_soil"))
		elif mode != MODE_ASSIST:
			if not bool(tile.get("tilled", false)):
				candidates.append(_candidate(38, "hoe", key_string, "", "prepare"))
			else:
				var seed_id: String = _best_seed(inventory, crop_ids, day + int(_cell_from_key(key_string).x))
				if not seed_id.is_empty():
					candidates.append(_candidate(34, "seed", key_string, seed_id, "plant"))

	if candidates.is_empty():
		return {}
	candidates.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var pa: int = int(a.get("priority", 0))
		var pb: int = int(b.get("priority", 0))
		if pa == pb:
			var ca: Vector2i = a.get("cell", Vector2i(-1, -1)) as Vector2i
			var cb: Vector2i = b.get("cell", Vector2i(-1, -1)) as Vector2i
			return (ca.y * 100 + ca.x) < (cb.y * 100 + cb.x)
		return pa > pb
	)
	return candidates[0]

static func self_test() -> Dictionary:
	var tiles: Dictionary = {
		"1,1": {"farm":true,"tilled":true,"crop":"water_spinach","stage":5,"moisture":70.0,"fertility":70.0,"pest":0.0,"disease":0.0,"weed":0.0,"dead":false},
		"2,1": {"farm":true,"tilled":true,"crop":"kale","stage":2,"moisture":10.0,"fertility":70.0,"pest":0.0,"disease":0.0,"weed":0.0,"dead":false}
	}
	var inventory: Dictionary = {"bio_spray":1,"organic_fertilizer":1,"compost":1,"seed_water_spinach":2,"seed_kale":2,"produce_water_spinach":0,"produce_kale":0}
	var result: Dictionary = choose_action(tiles, inventory, ["water_spinach","kale"], MODE_FULL, 1, 500)
	return {"ok":String(result.get("action", "")) == "harvest", "action":result.get("action", "")}
