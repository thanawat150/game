extends RefCounted
class_name GrowWiseMachinery

static func machine_definitions(data: Dictionary) -> Array:
	var value: Variant = data.get("machines", [])
	return value as Array if value is Array else []

static func machine_definition(data: Dictionary, machine_id: String) -> Dictionary:
	for value: Variant in machine_definitions(data):
		var definition: Dictionary = value as Dictionary
		if String(definition.get("id", "")) == machine_id:
			return definition
	return {}

static func default_levels(data: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for value: Variant in machine_definitions(data):
		var definition: Dictionary = value as Dictionary
		result[String(definition.get("id", ""))] = 0
	return result

static func default_enabled(data: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for value: Variant in machine_definitions(data):
		var definition: Dictionary = value as Dictionary
		result[String(definition.get("id", ""))] = true
	return result

static func default_durability(data: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for value: Variant in machine_definitions(data):
		var definition: Dictionary = value as Dictionary
		result[String(definition.get("id", ""))] = 100.0
	return result

static func max_level(definition: Dictionary) -> int:
	return int(definition.get("max_level", 1))

static func unlock_level(definition: Dictionary) -> int:
	return int(definition.get("unlock_level", 1))

static func indexed_int(definition: Dictionary, field_name: String, level_value: int, fallback: int = 0) -> int:
	var value: Variant = definition.get(field_name, [])
	if not (value is Array):
		return fallback
	var values: Array = value as Array
	if values.is_empty() or level_value <= 0:
		return fallback
	return int(values[clampi(level_value - 1, 0, values.size() - 1)])

static func next_cost(definition: Dictionary, current_level: int) -> Dictionary:
	var value: Variant = definition.get("costs", [])
	if not (value is Array):
		return {}
	var costs: Array = value as Array
	if current_level < 0 or current_level >= costs.size():
		return {}
	return costs[current_level] as Dictionary

static func can_pay(cost: Dictionary, inventory: Dictionary, money: int) -> bool:
	for item_id: String in cost:
		var amount: int = int(cost[item_id])
		if item_id == "money":
			if money < amount:
				return false
		elif int(inventory.get(item_id, 0)) < amount:
			return false
	return true

static func daily_maintenance(data: Dictionary, levels: Dictionary) -> int:
	var total: int = 0
	for value: Variant in machine_definitions(data):
		var definition: Dictionary = value as Dictionary
		var machine_id: String = String(definition.get("id", ""))
		var level_value: int = int(levels.get(machine_id, 0))
		if level_value > 0:
			total += indexed_int(definition, "maintenance", level_value, 0)
	return total

static func energy_capacity(data: Dictionary, levels: Dictionary) -> float:
	var energy_data: Variant = data.get("energy", {})
	var base_capacity: float = 100.0
	if energy_data is Dictionary:
		base_capacity = float((energy_data as Dictionary).get("base_capacity", 100))
	var installed_count: int = 0
	for machine_id: String in levels:
		if int(levels[machine_id]) > 0:
			installed_count += 1
	return base_capacity + float(maxi(0, installed_count - 3) * 8)

static func daily_recharge(data: Dictionary) -> float:
	var energy_data: Variant = data.get("energy", {})
	if energy_data is Dictionary:
		return float((energy_data as Dictionary).get("daily_recharge", 72))
	return 72.0

static func self_test(data: Dictionary) -> Dictionary:
	var definitions: Array = machine_definitions(data)
	if definitions.size() < 8:
		return {"ok": false, "reason": "missing_machine_definitions"}
	var levels: Dictionary = default_levels(data)
	var enabled: Dictionary = default_enabled(data)
	var durability: Dictionary = default_durability(data)
	if levels.size() != definitions.size() or enabled.size() != definitions.size() or durability.size() != definitions.size():
		return {"ok": false, "reason": "default_state_size"}
	var tiller: Dictionary = machine_definition(data, "mini_tiller")
	var bait_station: Dictionary = machine_definition(data, "bait_station")
	if tiller.is_empty() or bait_station.is_empty():
		return {"ok": false, "reason": "required_machine_missing"}
	if indexed_int(tiller, "energy", 1, 0) <= 0:
		return {"ok": false, "reason": "invalid_energy"}
	return {"ok": true, "machines": definitions.size()}
