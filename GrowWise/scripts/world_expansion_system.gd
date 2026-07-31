extends RefCounted
class_name GrowWiseWorldExpansion

static func dictionary_value(source: Dictionary, key_name: String, fallback: Dictionary = {}) -> Dictionary:
	var value: Variant = source.get(key_name, fallback)
	return value as Dictionary if value is Dictionary else fallback

static func array_value(source: Dictionary, key_name: String, fallback: Array = []) -> Array:
	var value: Variant = source.get(key_name, fallback)
	return value as Array if value is Array else fallback

static func default_zone_state(world_data: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	var zones: Array = array_value(world_data, "zones")
	for zone_value: Variant in zones:
		var zone: Dictionary = zone_value as Dictionary
		var zone_id: String = String(zone.get("id", ""))
		result[zone_id] = zone_id == "farm"
	return result

static func default_building_state(world_data: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	var buildings: Array = array_value(world_data, "buildings")
	for building_value: Variant in buildings:
		var building: Dictionary = building_value as Dictionary
		result[String(building.get("id", ""))] = 0
	return result

static func find_definition(definitions: Array, definition_id: String) -> Dictionary:
	for value: Variant in definitions:
		var definition: Dictionary = value as Dictionary
		if String(definition.get("id", "")) == definition_id:
			return definition
	return {}

static func building_definition(world_data: Dictionary, building_id: String) -> Dictionary:
	return find_definition(array_value(world_data, "buildings"), building_id)

static func zone_definition(world_data: Dictionary, zone_id: String) -> Dictionary:
	return find_definition(array_value(world_data, "zones"), zone_id)

static func next_building_cost(world_data: Dictionary, building_id: String, current_level: int) -> Dictionary:
	var definition: Dictionary = building_definition(world_data, building_id)
	var costs: Array = array_value(definition, "cost")
	if current_level < 0 or current_level >= costs.size():
		return {}
	return costs[current_level] as Dictionary

static func can_pay_cost(cost: Dictionary, inventory: Dictionary, money: int) -> bool:
	for item_id: String in cost:
		var required: int = int(cost[item_id])
		if item_id == "money":
			if money < required:
				return false
		elif int(inventory.get(item_id, 0)) < required:
			return false
	return true

static func cost_text(cost: Dictionary) -> String:
	var labels: Dictionary = {
		"money":"เงิน", "wood":"ไม้", "stone":"หิน", "glass":"แก้ว",
		"scrap":"เศษโลหะ", "fiber":"เส้นใย", "herb":"สมุนไพร"
	}
	var parts: PackedStringArray = []
	for item_id: String in cost:
		parts.append("%s %d" % [String(labels.get(item_id, item_id)), int(cost[item_id])])
	return " • ".join(parts)

static func calculate_town_metrics(world_data: Dictionary, building_levels: Dictionary, base_reputation: int = 0) -> Dictionary:
	var population: int = 1
	var happiness: int = 45
	var income: int = 0
	var knowledge: int = 0
	var reputation: int = base_reputation
	var storage: int = 20
	for building_id: String in building_levels:
		var level: int = int(building_levels[building_id])
		if level <= 0:
			continue
		var definition: Dictionary = building_definition(world_data, building_id)
		population += indexed_bonus(definition, "population", level)
		happiness += indexed_bonus(definition, "happiness", level)
		income += indexed_bonus(definition, "income", level)
		knowledge += indexed_bonus(definition, "knowledge", level)
		reputation += indexed_bonus(definition, "reputation", level)
		storage += indexed_bonus(definition, "storage", level)
	happiness = clampi(happiness, 0, 100)
	var town_level: int = 1
	var town_name: String = "บ้านสวน"
	var levels: Array = array_value(world_data, "town_levels")
	for level_value: Variant in levels:
		var level_def: Dictionary = level_value as Dictionary
		if population >= int(level_def.get("population", 9999)) and reputation >= int(level_def.get("reputation", 9999)):
			town_level = int(level_def.get("level", town_level))
			town_name = String(level_def.get("name", town_name))
	return {
		"population": population,
		"happiness": happiness,
		"income": income,
		"knowledge": knowledge,
		"reputation": reputation,
		"storage": storage,
		"level": town_level,
		"name": town_name
	}

static func indexed_bonus(definition: Dictionary, key_name: String, level: int) -> int:
	var values: Array = array_value(definition, key_name)
	if values.is_empty() or level <= 0:
		return 0
	return int(values[clampi(level - 1, 0, values.size() - 1)])

static func eligible_fish(world_data: Dictionary, season: int, weather_id: String, dock_level: int) -> Array:
	var result: Array = []
	var fish_list: Array = array_value(world_data, "fish")
	for fish_value: Variant in fish_list:
		var fish: Dictionary = fish_value as Dictionary
		var seasons: Array = array_value(fish, "seasons")
		var weather: Array = array_value(fish, "weather")
		if not season in seasons:
			continue
		if not weather_id in weather:
			continue
		var rarity: String = String(fish.get("rarity", "common"))
		if rarity == "rare" and dock_level < 2:
			continue
		if rarity == "legendary" and dock_level < 3:
			continue
		result.append(fish)
	if result.is_empty():
		for fish_value: Variant in fish_list:
			var fallback: Dictionary = fish_value as Dictionary
			if String(fallback.get("rarity", "common")) == "common":
				result.append(fallback)
	return result

static func choose_fish(world_data: Dictionary, season: int, weather_id: String, dock_level: int, seed_value: int) -> Dictionary:
	var candidates: Array = eligible_fish(world_data, season, weather_id, dock_level)
	if candidates.is_empty():
		return {}
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = seed_value
	var weighted: Array = []
	for fish_value: Variant in candidates:
		var fish: Dictionary = fish_value as Dictionary
		var rarity: String = String(fish.get("rarity", "common"))
		var weight: int = 8
		match rarity:
			"uncommon": weight = 4
			"rare": weight = 2
			"legendary": weight = 1
		for _index: int in range(weight):
			weighted.append(fish)
	return weighted[rng.randi_range(0, weighted.size() - 1)] as Dictionary

static func fishing_target_width(fish: Dictionary, dock_level: int) -> float:
	var difficulty: float = float(fish.get("difficulty", 0.4))
	return clampf(0.30 - difficulty * 0.18 + float(dock_level) * 0.025, 0.08, 0.30)

static func fishing_quality(distance_from_center: float, target_width: float, dock_level: int) -> int:
	var normalized: float = clampf(1.0 - distance_from_center / maxf(0.01, target_width), 0.0, 1.0)
	return clampi(int(round(45.0 + normalized * 45.0 + float(dock_level) * 3.0)), 1, 100)

static func self_test(world_data: Dictionary) -> Dictionary:
	var zones: Dictionary = default_zone_state(world_data)
	var buildings: Dictionary = default_building_state(world_data)
	buildings["house"] = 1
	buildings["dock"] = 2
	buildings["villager_house"] = 1
	var metrics: Dictionary = calculate_town_metrics(world_data, buildings, 10)
	var fish: Dictionary = choose_fish(world_data, 0, "clear", 1, 42)
	var cost: Dictionary = next_building_cost(world_data, "house", 0)
	var payment_ok: bool = can_pay_cost(cost, {"wood": 10, "stone": 10}, 500)
	var ok: bool = bool(zones.get("farm", false)) and int(metrics.get("population", 0)) >= 5 and not fish.is_empty() and payment_ok
	return {
		"ok": ok,
		"population": metrics.get("population", 0),
		"fish": fish.get("id", ""),
		"house_cost": cost
	}
