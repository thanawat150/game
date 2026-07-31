extends RefCounted
class_name GrowWiseWorkforce

static func dictionary_value(source: Dictionary, key_name: String, fallback: Dictionary = {}) -> Dictionary:
	var value: Variant = source.get(key_name, fallback)
	return value as Dictionary if value is Dictionary else fallback

static func array_value(source: Dictionary, key_name: String, fallback: Array = []) -> Array:
	var value: Variant = source.get(key_name, fallback)
	return value as Array if value is Array else fallback

static func role_definition(data: Dictionary, role_id: String) -> Dictionary:
	for value: Variant in array_value(data, "roles"):
		var role: Dictionary = value as Dictionary
		if String(role.get("id", "")) == role_id:
			return role
	return {}

static func unlocked_roles(data: Dictionary, farm_level: int) -> Array[String]:
	var result: Array[String] = []
	for value: Variant in array_value(data, "roles"):
		var role: Dictionary = value as Dictionary
		if farm_level >= int(role.get("unlock_level", 1)):
			result.append(String(role.get("id", "")))
	return result

static func generate_candidates(data: Dictionary, day: int, farm_level: int, count: int = 6) -> Array:
	var roles: Array[String] = unlocked_roles(data, farm_level)
	var names: Array = array_value(data, "names")
	var traits: Array = array_value(data, "traits")
	var result: Array = []
	if roles.is_empty() or names.is_empty() or traits.is_empty():
		return result
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = day * 104729 + farm_level * 7919 + 20260731
	for index: int in range(count):
		var role_id: String = roles[rng.randi_range(0, roles.size() - 1)]
		var role: Dictionary = role_definition(data, role_id)
		var trait: Dictionary = traits[rng.randi_range(0, traits.size() - 1)] as Dictionary
		var skill: int = rng.randi_range(35, 72) + mini(12, farm_level)
		var wage: int = int(role.get("base_wage", 25)) + int(round(float(skill) / 9.0))
		var name_index: int = posmod(index * 3 + rng.randi_range(0, names.size() - 1), names.size())
		result.append({
			"id": "%d_%d_%s" % [day, index, role_id],
			"name": String(names[name_index]),
			"role": role_id,
			"skill": clampi(skill, 1, 100),
			"wage": wage,
			"morale": rng.randi_range(68, 88),
			"fatigue": rng.randi_range(0, 12),
			"experience": 0,
			"trait_id": String(trait.get("id", "")),
			"trait_name_th": String(trait.get("name_th", "")),
			"trait_name_en": String(trait.get("name_en", "")),
			"resting": false,
			"actions_today": 0,
			"last_action": ""
		})
	return result

static func capacity(town_metrics: Dictionary, building_levels: Dictionary) -> int:
	var population: int = int(town_metrics.get("population", 1))
	var house_level: int = int(building_levels.get("house", 0))
	var villager_level: int = int(building_levels.get("villager_house", 0))
	var hall_level: int = int(building_levels.get("town_hall", 0))
	return clampi(1 + int(population / 5) + house_level + villager_level + hall_level, 1, 8)

static func signing_cost(candidate: Dictionary) -> int:
	return int(candidate.get("wage", 25)) * 2

static func trait_definition(data: Dictionary, trait_id: String) -> Dictionary:
	for value: Variant in array_value(data, "traits"):
		var trait: Dictionary = value as Dictionary
		if String(trait.get("id", "")) == trait_id:
			return trait
	return {}

static func productivity(data: Dictionary, worker: Dictionary) -> float:
	var skill: float = float(worker.get("skill", 50))
	var morale: float = float(worker.get("morale", 70))
	var fatigue: float = float(worker.get("fatigue", 0))
	var trait: Dictionary = trait_definition(data, String(worker.get("trait_id", "")))
	var value: float = 0.55 + skill / 100.0 * 0.55 + morale / 100.0 * 0.25 - fatigue / 100.0 * 0.35
	value += float(trait.get("productivity", 0.0))
	return clampf(value, 0.35, 1.75)

static func daily_action_limit(data: Dictionary, worker: Dictionary) -> int:
	var productivity_value: float = productivity(data, worker)
	if productivity_value >= 1.35:
		return 3
	if productivity_value >= 0.9:
		return 2
	return 1

static func apply_experience(data: Dictionary, worker: Dictionary, amount: int) -> Dictionary:
	var result: Dictionary = worker.duplicate(true)
	var trait: Dictionary = trait_definition(data, String(result.get("trait_id", "")))
	var multiplier: float = 1.0 + float(trait.get("experience", 0.0))
	var experience: int = int(result.get("experience", 0)) + int(round(float(amount) * multiplier))
	while experience >= 100 and int(result.get("skill", 1)) < 100:
		experience -= 100
		result["skill"] = mini(100, int(result.get("skill", 1)) + 1)
	result["experience"] = experience
	return result

static func self_test(data: Dictionary) -> Dictionary:
	var candidates: Array = generate_candidates(data, 5, 6, 4)
	if candidates.size() != 4:
		return {"ok": false, "reason": "candidate generation"}
	var candidate: Dictionary = candidates[0] as Dictionary
	if String(candidate.get("role", "")).is_empty() or int(candidate.get("wage", 0)) <= 0:
		return {"ok": false, "reason": "invalid candidate"}
	var capacity_value: int = capacity({"population": 12}, {"house": 1, "villager_house": 1, "town_hall": 0})
	if capacity_value < 4:
		return {"ok": false, "reason": "capacity"}
	if productivity(data, candidate) <= 0.0:
		return {"ok": false, "reason": "productivity"}
	return {"ok": true, "candidate": candidate, "capacity": capacity_value}
