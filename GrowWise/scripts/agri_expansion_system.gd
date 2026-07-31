extends RefCounted
class_name GrowWiseAgriExpansion

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

static func default_survey_state() -> Dictionary:
	return {
		"attempts": 3,
		"attempt_day": 1,
		"samples": [],
		"lab_queue": [],
		"reports": [],
		"selected_report": -1,
		"total_surveys": 0,
		"total_tests": 0
	}

static func default_animal_state(data: Dictionary) -> Dictionary:
	var buildings: Dictionary = {}
	for value: Variant in array_value(data, "animal_buildings"):
		var definition: Dictionary = value as Dictionary
		buildings[String(definition.get("id", ""))] = 0
	var animals: Dictionary = {}
	for value: Variant in array_value(data, "animals"):
		var definition: Dictionary = value as Dictionary
		animals[String(definition.get("id", ""))] = {
			"count": 0,
			"health": 100.0,
			"happiness": 70.0,
			"product_progress": 0,
			"days_owned": 0
		}
	return {
		"buildings": buildings,
		"animals": animals,
		"pending_products": {},
		"manure": 0,
		"daily_feed_used": 0,
		"daily_products": 0,
		"total_products": 0,
		"total_animals_bought": 0
	}

static func area_definition(data: Dictionary, area_id: String) -> Dictionary:
	return find_definition(array_value(data, "survey_areas"), area_id)

static func lab_definition(data: Dictionary, test_id: String) -> Dictionary:
	return find_definition(array_value(data, "lab_tests"), test_id)

static func animal_definition(data: Dictionary, animal_id: String) -> Dictionary:
	return find_definition(array_value(data, "animals"), animal_id)

static func animal_building_definition(data: Dictionary, building_id: String) -> Dictionary:
	return find_definition(array_value(data, "animal_buildings"), building_id)

static func generate_sample(data: Dictionary, area_id: String, day: int, sample_index: int) -> Dictionary:
	var area: Dictionary = area_definition(data, area_id)
	if area.is_empty():
		return {}
	var base: Dictionary = dictionary_value(area, "base")
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = day * 7919 + sample_index * 104729 + area_id.hash()
	var sample: Dictionary = {
		"id": "%s_%d_%d" % [area_id, day, sample_index],
		"area_id": area_id,
		"area_name": String(area.get("name", area_id)),
		"collected_day": day,
		"ph": clampf(float(base.get("ph", 6.5)) + rng.randf_range(-0.35, 0.35), 3.5, 9.0),
		"n": clampf(float(base.get("n", 50.0)) + rng.randf_range(-8.0, 8.0), 0.0, 100.0),
		"p": clampf(float(base.get("p", 50.0)) + rng.randf_range(-8.0, 8.0), 0.0, 100.0),
		"k": clampf(float(base.get("k", 50.0)) + rng.randf_range(-8.0, 8.0), 0.0, 100.0),
		"organic": clampf(float(base.get("organic", 3.0)) + rng.randf_range(-0.45, 0.45), 0.1, 12.0),
		"drainage": clampf(float(base.get("drainage", 60.0)) + rng.randf_range(-9.0, 9.0), 0.0, 100.0),
		"salinity": clampf(float(base.get("salinity", 5.0)) + rng.randf_range(-5.0, 5.0), 0.0, 100.0),
		"moisture": clampf(55.0 + rng.randf_range(-18.0, 18.0), 0.0, 100.0)
	}
	return sample

static func queue_lab_test(sample: Dictionary, test_definition: Dictionary, current_day: int) -> Dictionary:
	if sample.is_empty() or test_definition.is_empty():
		return {}
	return {
		"sample": sample.duplicate(true),
		"test_id": String(test_definition.get("id", "")),
		"test_name": String(test_definition.get("name", "")),
		"ready_day": current_day + int(test_definition.get("days", 1)),
		"fields": array_value(test_definition, "fields").duplicate()
	}

static func process_lab_queue(data: Dictionary, queue: Array, current_day: int) -> Dictionary:
	var remaining: Array = []
	var reports: Array = []
	for value: Variant in queue:
		var job: Dictionary = value as Dictionary
		if int(job.get("ready_day", current_day + 1)) > current_day:
			remaining.append(job)
			continue
		var sample: Dictionary = dictionary_value(job, "sample")
		var report: Dictionary = {
			"sample_id": String(sample.get("id", "")),
			"area_id": String(sample.get("area_id", "")),
			"area_name": String(sample.get("area_name", "")),
			"test_id": String(job.get("test_id", "")),
			"test_name": String(job.get("test_name", "")),
			"completed_day": current_day,
			"values": {},
			"suitability": crop_suitability(data, sample),
			"recommendations": soil_recommendations(sample)
		}
		var values: Dictionary = {}
		for field_value: Variant in array_value(job, "fields"):
			var field_name: String = String(field_value)
			values[field_name] = sample.get(field_name, 0.0)
		report["values"] = values
		reports.append(report)
	return {"remaining": remaining, "reports": reports}

static func crop_suitability(data: Dictionary, sample: Dictionary) -> Array:
	var result: Array = []
	var requirements: Dictionary = dictionary_value(data, "crop_requirements")
	for crop_id: String in requirements:
		var requirement: Dictionary = dictionary_value(requirements, crop_id)
		var score: float = 100.0
		var reasons: Array[String] = []
		score -= range_penalty(float(sample.get("ph", 6.5)), array_value(requirement, "ph"), 18.0, reasons, "pH")
		score -= range_penalty(float(sample.get("n", 50.0)), array_value(requirement, "n"), 0.8, reasons, "N")
		score -= range_penalty(float(sample.get("p", 50.0)), array_value(requirement, "p"), 0.65, reasons, "P")
		score -= range_penalty(float(sample.get("k", 50.0)), array_value(requirement, "k"), 0.65, reasons, "K")
		score -= range_penalty(float(sample.get("organic", 3.0)), array_value(requirement, "organic"), 7.0, reasons, "อินทรียวัตถุ")
		score -= range_penalty(float(sample.get("drainage", 60.0)), array_value(requirement, "drainage"), 0.7, reasons, "การระบายน้ำ")
		var salinity_max: float = float(requirement.get("salinity_max", 20.0))
		if float(sample.get("salinity", 0.0)) > salinity_max:
			var difference: float = float(sample.get("salinity", 0.0)) - salinity_max
			score -= difference * 1.1
			reasons.append("ความเค็มสูงเกิน")
		result.append({
			"crop_id": crop_id,
			"name": String(requirement.get("name", crop_id)),
			"score": clampi(int(round(score)), 0, 100),
			"grade": suitability_grade(score),
			"reasons": reasons
		})
	result.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return int(a.get("score", 0)) > int(b.get("score", 0)))
	return result

static func range_penalty(value: float, range_values: Array, multiplier: float, reasons: Array[String], label: String) -> float:
	if range_values.size() < 2:
		return 0.0
	var minimum: float = float(range_values[0])
	var maximum: float = float(range_values[1])
	if value < minimum:
		reasons.append(label + " ต่ำ")
		return (minimum - value) * multiplier
	if value > maximum:
		reasons.append(label + " สูง")
		return (value - maximum) * multiplier
	return 0.0

static func suitability_grade(score: float) -> String:
	if score >= 85.0:
		return "เหมาะมาก"
	if score >= 70.0:
		return "เหมาะ"
	if score >= 50.0:
		return "พอปลูกได้เมื่อปรับปรุง"
	return "ไม่แนะนำ"

static func soil_recommendations(sample: Dictionary) -> Array[String]:
	var result: Array[String] = []
	var ph: float = float(sample.get("ph", 6.5))
	if ph < 5.8:
		result.append("เติมวัสดุปูนอย่างค่อยเป็นค่อยไปเพื่อปรับ pH")
	elif ph > 7.3:
		result.append("เพิ่มอินทรียวัตถุและหลีกเลี่ยงปูนเพิ่มเติม")
	if float(sample.get("n", 50.0)) < 50.0:
		result.append("เพิ่มปุ๋ยหมักหรือปุ๋ยคอกเพื่อเสริมไนโตรเจน")
	if float(sample.get("p", 50.0)) < 40.0:
		result.append("เสริมฟอสฟอรัสจากวัสดุอินทรีย์หรือสูตรที่เหมาะสม")
	if float(sample.get("k", 50.0)) < 45.0:
		result.append("เพิ่มโพแทสเซียมและเศษพืชที่ย่อยสลายดี")
	if float(sample.get("organic", 3.0)) < 2.5:
		result.append("เพิ่มปุ๋ยหมัก เศษใบไม้ และพืชคลุมดิน")
	if float(sample.get("drainage", 60.0)) < 45.0:
		result.append("ขุดร่องระบาย ยกแปลง หรือสร้างบ่อรับน้ำ")
	if float(sample.get("salinity", 0.0)) > 25.0:
		result.append("ชะล้างเกลือด้วยน้ำคุณภาพดีและเพิ่มทางระบายน้ำ")
	if result.is_empty():
		result.append("ดินอยู่ในเกณฑ์ดี ควรรักษาอินทรียวัตถุและติดตามตามฤดูกาล")
	return result

static func animal_building_cost(data: Dictionary, building_id: String, current_level: int) -> Dictionary:
	var definition: Dictionary = animal_building_definition(data, building_id)
	var costs: Array = array_value(definition, "cost")
	if current_level < 0 or current_level >= costs.size():
		return {}
	return costs[current_level] as Dictionary

static func animal_capacity(data: Dictionary, building_levels: Dictionary, building_id: String) -> int:
	var level: int = int(building_levels.get(building_id, 0))
	if level <= 0:
		return 0
	var definition: Dictionary = animal_building_definition(data, building_id)
	var values: Array = array_value(definition, "capacity")
	if values.is_empty():
		return 0
	return int(values[clampi(level - 1, 0, values.size() - 1)])

static func occupied_capacity(data: Dictionary, animal_state: Dictionary, building_id: String) -> int:
	var total: int = 0
	var animals: Dictionary = dictionary_value(animal_state, "animals")
	for animal_id: String in animals:
		var definition: Dictionary = animal_definition(data, animal_id)
		if String(definition.get("building", "")) == building_id:
			total += int(dictionary_value(animals, animal_id).get("count", 0))
	return total

static func simulate_animals(data: Dictionary, animal_state: Dictionary, inventory: Dictionary) -> Dictionary:
	var state: Dictionary = animal_state.duplicate(true)
	var new_inventory: Dictionary = inventory.duplicate(true)
	var animals: Dictionary = dictionary_value(state, "animals")
	var pending: Dictionary = dictionary_value(state, "pending_products")
	var messages: Array[String] = []
	state["daily_feed_used"] = 0
	state["daily_products"] = 0
	for animal_id: String in animals:
		var group: Dictionary = dictionary_value(animals, animal_id)
		var count: int = int(group.get("count", 0))
		if count <= 0:
			continue
		var definition: Dictionary = animal_definition(data, animal_id)
		var feed_id: String = String(definition.get("feed", ""))
		var feed_need: int = int(definition.get("feed_per_day", 1)) * count
		var feed_have: int = int(new_inventory.get(feed_id, 0))
		var fed_ratio: float = clampf(float(feed_have) / maxf(1.0, float(feed_need)), 0.0, 1.0)
		var feed_used: int = mini(feed_have, feed_need)
		new_inventory[feed_id] = feed_have - feed_used
		state["daily_feed_used"] = int(state.get("daily_feed_used", 0)) + feed_used
		group["health"] = clampf(float(group.get("health", 100.0)) + (2.0 if fed_ratio >= 1.0 else -8.0 * (1.0 - fed_ratio)), 10.0, 100.0)
		group["happiness"] = clampf(float(group.get("happiness", 70.0)) + (1.5 if fed_ratio >= 1.0 else -10.0 * (1.0 - fed_ratio)), 0.0, 100.0)
		group["days_owned"] = int(group.get("days_owned", 0)) + 1
		group["product_progress"] = int(group.get("product_progress", 0)) + 1
		var product_days: int = maxi(1, int(definition.get("product_days", 1)))
		if fed_ratio >= 0.75 and float(group.get("health", 0.0)) >= 45.0 and int(group.get("product_progress", 0)) >= product_days:
			group["product_progress"] = 0
			var happiness_multiplier: float = 1.25 if float(group.get("happiness", 0.0)) >= 80.0 else 1.0
			var product_amount: int = maxi(1, int(round(float(count) * happiness_multiplier)))
			var product_id: String = String(definition.get("product", ""))
			pending[product_id] = int(pending.get(product_id, 0)) + product_amount
			state["daily_products"] = int(state.get("daily_products", 0)) + product_amount
			state["total_products"] = int(state.get("total_products", 0)) + product_amount
			messages.append("%sให้%s %d" % [String(definition.get("name", animal_id)), String(definition.get("product_name", product_id)), product_amount])
		state["manure"] = int(state.get("manure", 0)) + int(definition.get("manure", 0)) * count
		animals[animal_id] = group
	state["animals"] = animals
	state["pending_products"] = pending
	return {"state": state, "inventory": new_inventory, "messages": messages}

static func processing_definition(data: Dictionary, recipe_id: String) -> Dictionary:
	return find_definition(array_value(data, "processing_recipes"), recipe_id)

static func feed_definition(data: Dictionary, feed_id: String) -> Dictionary:
	return find_definition(array_value(data, "feeds"), feed_id)

static func can_consume_requirements(requirements: Dictionary, inventory: Dictionary) -> bool:
	var test: Dictionary = inventory.duplicate(true)
	return consume_requirements(requirements, test)

static func consume_requirements(requirements: Dictionary, inventory: Dictionary) -> bool:
	for requirement_id: String in requirements:
		var amount: int = int(requirements[requirement_id])
		if requirement_id == "produce_any":
			if not consume_prefix(inventory, "produce_", amount):
				return false
		elif requirement_id == "fish_any":
			if not consume_prefix(inventory, "fish_", amount):
				return false
		elif int(inventory.get(requirement_id, 0)) >= amount:
			inventory[requirement_id] = int(inventory.get(requirement_id, 0)) - amount
		else:
			return false
	return true

static func consume_prefix(inventory: Dictionary, prefix: String, amount: int) -> bool:
	var remaining: int = amount
	for item_id: String in inventory:
		if item_id.begins_with(prefix) and int(inventory.get(item_id, 0)) > 0:
			var used: int = mini(remaining, int(inventory.get(item_id, 0)))
			inventory[item_id] = int(inventory.get(item_id, 0)) - used
			remaining -= used
			if remaining <= 0:
				return true
	return false

static func self_test(data: Dictionary) -> Dictionary:
	var sample: Dictionary = generate_sample(data, "farm_edge", 3, 1)
	var suitability: Array = crop_suitability(data, sample)
	var state: Dictionary = default_animal_state(data)
	var buildings: Dictionary = dictionary_value(state, "buildings")
	buildings["coop"] = 1
	state["buildings"] = buildings
	var animals: Dictionary = dictionary_value(state, "animals")
	var chicken: Dictionary = dictionary_value(animals, "chicken")
	chicken["count"] = 2
	animals["chicken"] = chicken
	state["animals"] = animals
	var simulated: Dictionary = simulate_animals(data, state, {"grain_feed": 4})
	var okay: bool = not sample.is_empty() and not suitability.is_empty() and int(dictionary_value(simulated, "state").get("daily_feed_used", 0)) == 2
	return {
		"ok": okay,
		"best_crop": (suitability[0] as Dictionary).get("crop_id", "") if not suitability.is_empty() else "",
		"feed_used": dictionary_value(simulated, "state").get("daily_feed_used", 0)
	}
