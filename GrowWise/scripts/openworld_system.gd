extends RefCounted
class_name GrowWiseOpenWorld

static func array_value(source: Dictionary, key_name: String) -> Array:
	var value: Variant = source.get(key_name, [])
	return value as Array if value is Array else []

static func dictionary_value(source: Dictionary, key_name: String) -> Dictionary:
	var value: Variant = source.get(key_name, {})
	return value as Dictionary if value is Dictionary else {}

static func vector_from(source: Dictionary) -> Vector2:
	return Vector2(float(source.get("x", 0.0)), float(source.get("y", 0.0)))

static func rect_from(source: Dictionary) -> Rect2:
	return Rect2(
		float(source.get("x", 0.0)),
		float(source.get("y", 0.0)),
		float(source.get("w", 0.0)),
		float(source.get("h", 0.0))
	)

static func world_bounds(data: Dictionary) -> Rect2:
	return rect_from(dictionary_value(data, "bounds"))

static func start_position(data: Dictionary) -> Vector2:
	return vector_from(dictionary_value(data, "start"))

static func region_definition(data: Dictionary, region_id: String) -> Dictionary:
	for value: Variant in array_value(data, "regions"):
		var region: Dictionary = value as Dictionary
		if String(region.get("id", "")) == region_id:
			return region
	return {}

static func region_at(data: Dictionary, position: Vector2) -> Dictionary:
	for value: Variant in array_value(data, "regions"):
		var region: Dictionary = value as Dictionary
		if rect_from(dictionary_value(region, "rect")).has_point(position):
			return region
	return {}

static func point_definition(data: Dictionary, point_id: String) -> Dictionary:
	for value: Variant in array_value(data, "points"):
		var point: Dictionary = value as Dictionary
		if String(point.get("id", "")) == point_id:
			return point
	return {}

static func point_position(point: Dictionary) -> Vector2:
	return Vector2(float(point.get("x", 0.0)), float(point.get("y", 0.0)))

static func nearest_point(data: Dictionary, position: Vector2, maximum_distance: float) -> Dictionary:
	var nearest: Dictionary = {}
	var nearest_distance: float = maximum_distance
	for value: Variant in array_value(data, "points"):
		var point: Dictionary = value as Dictionary
		var distance_value: float = position.distance_to(point_position(point))
		if distance_value <= nearest_distance:
			nearest_distance = distance_value
			nearest = point
	return nearest

static func nearest_resource(data: Dictionary, position: Vector2, maximum_distance: float, collected_days: Dictionary, current_day: int) -> Dictionary:
	var nearest: Dictionary = {}
	var nearest_distance: float = maximum_distance
	for value: Variant in array_value(data, "resources"):
		var resource: Dictionary = value as Dictionary
		var resource_id: String = String(resource.get("id", ""))
		if int(collected_days.get(resource_id, -999)) == current_day:
			continue
		var resource_position: Vector2 = Vector2(float(resource.get("x", 0.0)), float(resource.get("y", 0.0)))
		var distance_value: float = position.distance_to(resource_position)
		if distance_value <= nearest_distance:
			nearest_distance = distance_value
			nearest = resource
	return nearest

static func resource_reward(resource_type: String, day_value: int, resource_id: String) -> Dictionary:
	var seed_value: int = abs(hash(resource_id)) + day_value * 97
	var amount: int = 1 + posmod(seed_value, 3)
	match resource_type:
		"wood": return {"item":"wood", "amount":amount, "name":"ไม้"}
		"stone": return {"item":"stone", "amount":amount, "name":"หิน"}
		"fiber": return {"item":"fiber", "amount":amount + 1, "name":"เส้นใย"}
		"herb": return {"item":"herb", "amount":amount, "name":"สมุนไพร"}
		"scrap": return {"item":"scrap", "amount":amount, "name":"เศษโลหะ"}
		"mineral": return {"item":"mineral", "amount":1, "name":"แร่ธาตุ"}
		"clay": return {"item":"clay", "amount":amount, "name":"ดินเหนียว"}
		"seed":
			var crop_ids: Array[String] = ["water_spinach", "kale", "chili", "tomato", "cucumber"]
			var crop_index: int = posmod(seed_value, crop_ids.size())
			return {"item":"seed_" + crop_ids[crop_index], "amount":1, "name":"เมล็ดพันธุ์ป่า"}
	return {"item":"fiber", "amount":1, "name":"วัสดุธรรมชาติ"}

static func active_event(data: Dictionary, day_value: int) -> Dictionary:
	for value: Variant in array_value(data, "events"):
		var event_data: Dictionary = value as Dictionary
		var mod_value: int = maxi(1, int(event_data.get("mod", 1)))
		var offset_value: int = int(event_data.get("offset", 0))
		if posmod(day_value, mod_value) == posmod(offset_value, mod_value):
			return event_data
	return {}

static func npc_position(npc: Dictionary, time_minutes: float) -> Vector2:
	var route_value: Variant = npc.get("route", [])
	if not (route_value is Array):
		return Vector2.ZERO
	var route: Array = route_value as Array
	if route.is_empty():
		return Vector2.ZERO
	if route.size() == 1:
		var single: Array = route[0] as Array
		return Vector2(float(single[0]), float(single[1])) if single.size() >= 2 else Vector2.ZERO
	var phase: float = fposmod(time_minutes / 240.0, float(route.size()))
	var index_a: int = int(floor(phase))
	var index_b: int = posmod(index_a + 1, route.size())
	var fraction: float = phase - floor(phase)
	var source_a: Array = route[index_a] as Array
	var source_b: Array = route[index_b] as Array
	if source_a.size() < 2 or source_b.size() < 2:
		return Vector2.ZERO
	var point_a: Vector2 = Vector2(float(source_a[0]), float(source_a[1]))
	var point_b: Vector2 = Vector2(float(source_b[0]), float(source_b[1]))
	return point_a.lerp(point_b, fraction)

static func map_position(world_position: Vector2, bounds: Rect2, map_rect: Rect2) -> Vector2:
	if bounds.size.x <= 0.0 or bounds.size.y <= 0.0:
		return map_rect.position
	var normalized: Vector2 = Vector2(
		(world_position.x - bounds.position.x) / bounds.size.x,
		(world_position.y - bounds.position.y) / bounds.size.y
	)
	return map_rect.position + normalized * map_rect.size

static func world_position_from_map(map_position_value: Vector2, bounds: Rect2, map_rect: Rect2) -> Vector2:
	if map_rect.size.x <= 0.0 or map_rect.size.y <= 0.0:
		return bounds.position
	var normalized: Vector2 = Vector2(
		(map_position_value.x - map_rect.position.x) / map_rect.size.x,
		(map_position_value.y - map_rect.position.y) / map_rect.size.y
	)
	return bounds.position + normalized * bounds.size

static func self_test(data: Dictionary) -> Dictionary:
	var errors: Array[String] = []
	var bounds: Rect2 = world_bounds(data)
	if bounds.size.x < 2000.0 or bounds.size.y < 1500.0:
		errors.append("world bounds are too small")
	var regions: Array = array_value(data, "regions")
	if regions.size() < 5:
		errors.append("at least five regions are required")
	var points: Array = array_value(data, "points")
	if points.size() < 10:
		errors.append("at least ten points of interest are required")
	var has_farm: bool = false
	var has_town: bool = false
	var has_fishing: bool = false
	var point_ids: Dictionary = {}
	for value: Variant in points:
		var point: Dictionary = value as Dictionary
		var point_id: String = String(point.get("id", ""))
		if point_id.is_empty() or point_ids.has(point_id):
			errors.append("point id is missing or duplicated")
		point_ids[point_id] = true
		var action_value: String = String(point.get("action", ""))
		has_farm = has_farm or action_value == "enter_farm"
		has_town = has_town or action_value == "town"
		has_fishing = has_fishing or action_value == "fishing"
	if not has_farm:
		errors.append("farm entrance is missing")
	if not has_town:
		errors.append("town hub is missing")
	if not has_fishing:
		errors.append("fishing location is missing")
	if array_value(data, "resources").size() < 12:
		errors.append("not enough resource nodes")
	if array_value(data, "npcs").size() < 4:
		errors.append("not enough world NPCs")
	return {"ok":errors.is_empty(), "errors":errors, "regions":regions.size(), "points":points.size()}
