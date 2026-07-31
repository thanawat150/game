extends RefCounted
class_name GrowWiseInfrastructure

static func dictionary_value(source: Dictionary, key_name: String, fallback: Dictionary = {}) -> Dictionary:
	var value: Variant = source.get(key_name, fallback)
	return value as Dictionary if value is Dictionary else fallback

static func array_value(source: Dictionary, key_name: String, fallback: Array = []) -> Array:
	var value: Variant = source.get(key_name, fallback)
	return value as Array if value is Array else fallback

static func find_definition(definitions: Array, definition_id: String) -> Dictionary:
	for value: Variant in definitions:
		var definition: Dictionary = value as Dictionary
		if String(definition.get("id", "")) == definition_id:
			return definition
	return {}

static func default_water_state() -> Dictionary:
	return {
		"channels": {},
		"levees": {},
		"pond_built": false,
		"pond_level": 0.0,
		"pond_capacity": 900.0,
		"gate_built": false,
		"gate_open": true,
		"pump_built": false,
		"pump_on": false,
		"daily_drained": 0.0,
		"daily_irrigated": 0.0
	}

static func default_logistics_state() -> Dictionary:
	return {
		"owned_vehicles": {"handcart": false, "pickup": false, "farm_truck": false, "electric_truck": false},
		"selected_vehicle": "handcart",
		"active_trip": {},
		"trip_history": [],
		"total_delivered": 0,
		"total_transport_profit": 0
	}

static func default_wardrobe_state() -> Dictionary:
	return {
		"owned": {"garden": true},
		"selected": "garden",
		"skin_tone": 0,
		"hair_style": 0,
		"accessory": 0
	}

static func water_tool_definition(data: Dictionary, tool_id: String) -> Dictionary:
	return find_definition(array_value(data, "water_tools"), tool_id)

static func vehicle_definition(data: Dictionary, vehicle_id: String) -> Dictionary:
	return find_definition(array_value(data, "vehicles"), vehicle_id)

static func outfit_definition(data: Dictionary, outfit_id: String) -> Dictionary:
	return find_definition(array_value(data, "outfits"), outfit_id)

static func can_pay(cost: Dictionary, inventory: Dictionary, money: int) -> bool:
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
		"money":"เงิน","wood":"ไม้","stone":"หิน","fiber":"เส้นใย","scrap":"เศษโลหะ",
		"glass":"แก้ว","rubber":"ยาง","herb":"สมุนไพร"
	}
	var parts: PackedStringArray = []
	for item_id: String in cost:
		parts.append("%s %d" % [String(labels.get(item_id, item_id)), int(cost[item_id])])
	return " • ".join(parts)

static func direction_vector(direction: int) -> Vector2i:
	var directions: Array[Vector2i] = [Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0), Vector2i(0, -1)]
	return directions[posmod(direction, directions.size())]

static func direction_name(direction: int) -> String:
	var names: Array[String] = ["ตะวันออก", "ใต้", "ตะวันตก", "เหนือ"]
	return names[posmod(direction, names.size())]

static func channel_key(cell: Vector2i) -> String:
	return "%d,%d" % [cell.x, cell.y]

static func add_channel(water_state: Dictionary, cell: Vector2i, mode: String, direction: int) -> Dictionary:
	var result: Dictionary = water_state.duplicate(true)
	var channels: Dictionary = dictionary_value(result, "channels")
	channels[channel_key(cell)] = {"mode": mode, "direction": posmod(direction, 4)}
	result["channels"] = channels
	return result

static func remove_water_structure(water_state: Dictionary, cell: Vector2i) -> Dictionary:
	var result: Dictionary = water_state.duplicate(true)
	var key_string: String = channel_key(cell)
	var channels: Dictionary = dictionary_value(result, "channels")
	var levees: Dictionary = dictionary_value(result, "levees")
	channels.erase(key_string)
	levees.erase(key_string)
	result["channels"] = channels
	result["levees"] = levees
	return result

static func add_levee(water_state: Dictionary, cell: Vector2i, direction: int) -> Dictionary:
	var result: Dictionary = water_state.duplicate(true)
	var levees: Dictionary = dictionary_value(result, "levees")
	levees[channel_key(cell)] = {"direction": posmod(direction, 4)}
	result["levees"] = levees
	return result

static func apply_water_network(
	water_state: Dictionary,
	tiles: Dictionary,
	width: int,
	height: int,
	rain_amount: float,
	pump_cost_available: bool
) -> Dictionary:
	var new_state: Dictionary = water_state.duplicate(true)
	var new_tiles: Dictionary = tiles.duplicate(true)
	new_state["daily_drained"] = 0.0
	new_state["daily_irrigated"] = 0.0
	var gate_active: bool = not bool(new_state.get("gate_built", false)) or bool(new_state.get("gate_open", true))
	var pump_bonus: float = 1.35 if bool(new_state.get("pump_built", false)) and bool(new_state.get("pump_on", false)) and pump_cost_available else 1.0
	var pond_level: float = float(new_state.get("pond_level", 0.0))
	var pond_capacity: float = float(new_state.get("pond_capacity", 900.0))
	var channels: Dictionary = dictionary_value(new_state, "channels")
	var levees: Dictionary = dictionary_value(new_state, "levees")

	# Levees reduce rainfall entering protected farm cells.
	if rain_amount > 0.0:
		for key_value: Variant in levees.keys():
			var key_string: String = String(key_value)
			if new_tiles.has(key_string):
				var tile: Dictionary = dictionary_value(new_tiles, key_string)
				tile["moisture"] = maxf(0.0, float(tile.get("moisture", 0.0)) - rain_amount * 0.35)
				new_tiles[key_string] = tile

	if gate_active:
		# Drain channels pull excess water from their source and pass it toward the next cell or pond.
		for key_value: Variant in channels.keys():
			var source_key: String = String(key_value)
			var channel: Dictionary = dictionary_value(channels, source_key)
			if String(channel.get("mode", "")) != "drain" or not new_tiles.has(source_key):
				continue
			var source_tile: Dictionary = dictionary_value(new_tiles, source_key)
			var excess: float = maxf(0.0, float(source_tile.get("moisture", 0.0)) - 72.0)
			var moved: float = minf(excess, 22.0 * pump_bonus)
			if moved <= 0.0:
				continue
			source_tile["moisture"] = maxf(0.0, float(source_tile.get("moisture", 0.0)) - moved)
			new_tiles[source_key] = source_tile
			var parts: PackedStringArray = source_key.split(",")
			if parts.size() != 2:
				continue
			var source_cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
			var destination: Vector2i = source_cell + direction_vector(int(channel.get("direction", 0)))
			var destination_key: String = channel_key(destination)
			if destination.x >= 0 and destination.y >= 0 and destination.x < width and destination.y < height and new_tiles.has(destination_key):
				var destination_tile: Dictionary = dictionary_value(new_tiles, destination_key)
				var accepted: float = minf(moved, maxf(0.0, 78.0 - float(destination_tile.get("moisture", 0.0))))
				destination_tile["moisture"] = minf(100.0, float(destination_tile.get("moisture", 0.0)) + accepted)
				new_tiles[destination_key] = destination_tile
				pond_level += moved - accepted
			else:
				pond_level += moved
			new_state["daily_drained"] = float(new_state.get("daily_drained", 0.0)) + moved

		# Irrigation channels draw from the pond and target dry cells in their selected direction.
		if bool(new_state.get("pond_built", false)) and pond_level > 0.0:
			for key_value: Variant in channels.keys():
				var source_key: String = String(key_value)
				var channel: Dictionary = dictionary_value(channels, source_key)
				if String(channel.get("mode", "")) != "irrigate":
					continue
				var parts: PackedStringArray = source_key.split(",")
				if parts.size() != 2:
					continue
				var source_cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
				var destination: Vector2i = source_cell + direction_vector(int(channel.get("direction", 0)))
				var destination_key: String = channel_key(destination)
				if not new_tiles.has(destination_key):
					continue
				var destination_tile: Dictionary = dictionary_value(new_tiles, destination_key)
				var need: float = maxf(0.0, 58.0 - float(destination_tile.get("moisture", 0.0)))
				var moved: float = minf(need, minf(pond_level, 18.0 * pump_bonus))
				if moved <= 0.0:
					continue
				destination_tile["moisture"] = minf(100.0, float(destination_tile.get("moisture", 0.0)) + moved)
				new_tiles[destination_key] = destination_tile
				pond_level -= moved
				new_state["daily_irrigated"] = float(new_state.get("daily_irrigated", 0.0)) + moved

	new_state["pond_level"] = clampf(pond_level, 0.0, pond_capacity)
	return {"water_state": new_state, "tiles": new_tiles}

static func overwater_advice(moisture: float, has_drain: bool, pond_built: bool) -> String:
	if moisture < 85.0:
		return ""
	if not has_drain:
		return "น้ำมาก: ขุดร่องระบายจากแปลงไปทางพื้นที่ต่ำ"
	if not pond_built:
		return "น้ำมาก: สร้างบ่อรับน้ำเพื่อเก็บน้ำจากร่องระบาย"
	return "น้ำมาก: เปิดประตูน้ำและหันร่องเข้าหาบ่อรับน้ำ"

static func best_vehicle(logistics_data: Dictionary, logistics_state: Dictionary) -> String:
	var owned: Dictionary = dictionary_value(logistics_state, "owned_vehicles")
	var order: Array[String] = ["electric_truck", "farm_truck", "pickup", "handcart"]
	for vehicle_id: String in order:
		if bool(owned.get(vehicle_id, false)):
			return vehicle_id
	return ""

static func cargo_unit_value(item_id: String) -> int:
	if item_id.begins_with("meal_"):
		return 95
	if item_id.begins_with("processed_"):
		return 72
	if item_id.begins_with("fish_"):
		return 48
	if item_id.begins_with("produce_"):
		return 24
	return 0

static func build_cargo(inventory: Dictionary, capacity: int) -> Dictionary:
	var candidates: Array[Dictionary] = []
	for item_id: String in inventory:
		var unit_value: int = cargo_unit_value(item_id)
		var amount: int = int(inventory.get(item_id, 0))
		if unit_value > 0 and amount > 0:
			candidates.append({"id": item_id, "value": unit_value, "amount": amount})
	candidates.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return int(a["value"]) > int(b["value"]))
	var cargo: Dictionary = {}
	var remaining: int = capacity
	for candidate: Dictionary in candidates:
		if remaining <= 0:
			break
		var load_amount: int = mini(remaining, int(candidate["amount"]))
		cargo[String(candidate["id"])] = load_amount
		remaining -= load_amount
	return cargo

static func cargo_count(cargo: Dictionary) -> int:
	var total: int = 0
	for item_id: String in cargo:
		total += int(cargo[item_id])
	return total

static func cargo_base_value(cargo: Dictionary) -> int:
	var total: int = 0
	for item_id: String in cargo:
		total += cargo_unit_value(item_id) * int(cargo[item_id])
	return total

static func create_trip(data: Dictionary, logistics_state: Dictionary, inventory: Dictionary, selected_outfit: String) -> Dictionary:
	var vehicle_id: String = String(logistics_state.get("selected_vehicle", ""))
	var vehicle: Dictionary = vehicle_definition(data, vehicle_id)
	if vehicle.is_empty():
		return {}
	var capacity: int = int(vehicle.get("capacity", 0))
	var cargo: Dictionary = build_cargo(inventory, capacity)
	if cargo.is_empty():
		return {}
	var outfit_time_multiplier: float = 0.92 if selected_outfit == "driver" else 1.0
	var hours: float = float(vehicle.get("trip_hours", 8.0)) * outfit_time_multiplier
	var base_value: int = cargo_base_value(cargo)
	var gross: int = int(round(float(base_value) * float(vehicle.get("price_multiplier", 1.0))))
	var transport_cost: int = int(vehicle.get("transport_cost", 0))
	return {
		"vehicle": vehicle_id,
		"cargo": cargo,
		"capacity": capacity,
		"hours_total": hours,
		"hours_left": hours,
		"gross": gross,
		"transport_cost": transport_cost,
		"net": gross - transport_cost
	}

static func advance_trip(logistics_state: Dictionary, game_hours: float) -> Dictionary:
	var result: Dictionary = logistics_state.duplicate(true)
	var trip: Dictionary = dictionary_value(result, "active_trip")
	if trip.is_empty():
		return {"state": result, "completed": {}}
	trip["hours_left"] = maxf(0.0, float(trip.get("hours_left", 0.0)) - game_hours)
	if float(trip.get("hours_left", 0.0)) > 0.0:
		result["active_trip"] = trip
		return {"state": result, "completed": {}}
	var history: Array = array_value(result, "trip_history")
	history.push_front(trip)
	if history.size() > 20:
		history.resize(20)
	result["trip_history"] = history
	result["active_trip"] = {}
	result["total_delivered"] = int(result.get("total_delivered", 0)) + cargo_count(dictionary_value(trip, "cargo"))
	result["total_transport_profit"] = int(result.get("total_transport_profit", 0)) + int(trip.get("net", 0))
	return {"state": result, "completed": trip}

static func outfit_bonus_text(outfit: Dictionary) -> String:
	return String(outfit.get("bonus", ""))

static func self_test(data: Dictionary) -> Dictionary:
	var water: Dictionary = default_water_state()
	water["pond_built"] = true
	water["pond_level"] = 120.0
	water = add_channel(water, Vector2i(1, 1), "drain", 0)
	var tiles: Dictionary = {
		"1,1":{"moisture":96.0},
		"2,1":{"moisture":30.0}
	}
	var flow: Dictionary = apply_water_network(water, tiles, 3, 3, 0.0, true)
	var logistics: Dictionary = default_logistics_state()
	logistics["owned_vehicles"] = {"handcart":true,"pickup":false,"farm_truck":false,"electric_truck":false}
	logistics["selected_vehicle"] = "handcart"
	var trip: Dictionary = create_trip(data, logistics, {"produce_kale":8,"meal_grilled_fish":2}, "garden")
	var okay: bool = float(dictionary_value(flow, "water_state").get("daily_drained", 0.0)) > 0.0 and not trip.is_empty()
	return {"ok":okay,"drained":dictionary_value(flow,"water_state").get("daily_drained",0.0),"trip_net":trip.get("net",0)}
