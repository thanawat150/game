extends RefCounted
class_name GrowWiseSimulation

static func _dict(source: Dictionary, key: String, fallback: Dictionary = {}) -> Dictionary:
	var value: Variant = source.get(key, fallback)
	return value as Dictionary if value is Dictionary else fallback

static func _float(source: Dictionary, key: String, fallback: float = 0.0) -> float:
	var value: Variant = source.get(key, fallback)
	return float(value)

static func _int(source: Dictionary, key: String, fallback: int = 0) -> int:
	var value: Variant = source.get(key, fallback)
	return int(value)

static func _string(source: Dictionary, key: String, fallback: String = "") -> String:
	var value: Variant = source.get(key, fallback)
	return String(value)

static func new_tile(is_farm: bool) -> Dictionary:
	return {
		"farm": is_farm,
		"soil_type": "loam",
		"tilled": false,
		"moisture": 28.0,
		"fertility": 68.0,
		"ph": 6.4,
		"nitrogen": 66.0,
		"phosphorus": 58.0,
		"potassium": 62.0,
		"drainage": 70.0,
		"light": 80.0,
		"crop": "",
		"growth": 0.0,
		"stage": 0,
		"health": 100.0,
		"quality": 70.0,
		"pest": 0.0,
		"disease": 0.0,
		"weed": 0.0,
		"beneficial": 0.0,
		"dead": false,
		"spacing_penalty": 0.0,
		"last_watered": 0,
		"last_fertilized": 0,
		"water_total": 0.0,
		"cost_total": 0.0,
		"experiment_group": ""
	}

static func season_index(day: int) -> int:
	return int((maxi(day, 1) - 1) / 7) % 4

static func weather_for_day(day: int, season: int) -> String:
	var hot: Array[String] = ["clear", "hot", "clear", "windy", "cloudy", "hot", "clear"]
	var rainy: Array[String] = ["cloudy", "light_rain", "heavy_rain", "clear", "light_rain", "storm", "cloudy"]
	var cool: Array[String] = ["cool", "clear", "fog", "cool", "cloudy", "clear", "windy"]
	var transition: Array[String] = ["clear", "cloudy", "light_rain", "windy", "hot", "fog", "clear"]
	var tables: Array[Array] = [hot, rainy, cool, transition]
	var chosen: Array = tables[clampi(season, 0, 3)]
	return String(chosen[(maxi(day, 1) - 1) % chosen.size()])

static func primary_symptom(tile: Dictionary) -> String:
	if bool(tile.get("dead", false)):
		return "dead"
	if _float(tile, "disease") >= 42.0:
		return "disease"
	if _float(tile, "pest") >= 38.0:
		return "pest"
	if _float(tile, "moisture") <= 18.0:
		return "dry"
	if _float(tile, "moisture") >= 86.0:
		return "overwater"
	if _float(tile, "fertility") <= 34.0:
		return "poor_soil"
	if _float(tile, "weed") >= 45.0:
		return "weed"
	if _float(tile, "spacing_penalty") >= 22.0:
		return "slow"
	return "healthy"

static func simulate_day(tile: Dictionary, crop_def: Dictionary, weather_def: Dictionary, season: int, rng: RandomNumberGenerator) -> Dictionary:
	var result: Dictionary = tile.duplicate(true)
	if not bool(result.get("farm", false)):
		return result
	var moisture: float = _float(result, "moisture")
	var drainage: float = _float(result, "drainage", 70.0)
	var rain: float = _float(weather_def, "rain")
	var evaporation: float = _float(weather_def, "evaporation")
	moisture += rain * (1.0 - drainage / 180.0)
	moisture -= evaporation
	result["moisture"] = clampf(moisture, 0.0, 100.0)
	if bool(result.get("tilled", false)):
		var weed_gain: float = 2.0 + rain * 0.08
		result["weed"] = clampf(_float(result, "weed") + weed_gain, 0.0, 100.0)
	var beneficial: float = _float(result, "beneficial")
	if _float(result, "fertility") > 55.0 and rng.randf() < 0.22:
		beneficial = clampf(beneficial + rng.randf_range(2.0, 7.0), 0.0, 100.0)
	else:
		beneficial = maxf(0.0, beneficial - 1.0)
	result["beneficial"] = beneficial
	var crop_id: String = _string(result, "crop")
	if crop_id.is_empty() or bool(result.get("dead", false)):
		return result
	var pest_risk: float = _float(crop_def, "pest_risk") + _float(weather_def, "pest")
	var disease_risk: float = _float(crop_def, "disease_risk") + _float(weather_def, "disease")
	var pest: float = _float(result, "pest")
	var disease: float = _float(result, "disease")
	if rng.randf() < pest_risk:
		pest += rng.randf_range(4.0, 12.0)
	if rng.randf() < disease_risk or moisture > 90.0:
		disease += rng.randf_range(3.0, 10.0)
	pest = maxf(0.0, pest - beneficial * 0.08)
	result["pest"] = clampf(pest, 0.0, 100.0)
	result["disease"] = clampf(disease, 0.0, 100.0)
	var ideal_water: float = _float(crop_def, "ideal_water", 65.0)
	var water_tolerance: float = maxf(1.0, _float(crop_def, "water_tolerance", 18.0))
	var water_score: float = clampf(1.0 - absf(moisture - ideal_water) / (water_tolerance * 2.0), 0.0, 1.0)
	var fertility_score: float = clampf(_float(result, "fertility") / 75.0, 0.0, 1.0)
	var light_score: float = clampf(_float(weather_def, "light", 80.0) / maxf(1.0, _float(crop_def, "ideal_light", 75.0)), 0.0, 1.0)
	var temperature_score: float = clampf(1.0 - absf(_float(weather_def, "temperature", 28.0) - _float(crop_def, "ideal_temperature", 28.0)) / 18.0, 0.0, 1.0)
	var spacing_score: float = clampf(1.0 - _float(result, "spacing_penalty") / 100.0, 0.25, 1.0)
	var season_bonus: float = 1.0
	var season_value: Variant = crop_def.get("season_bonus", [])
	if season_value is Array and season in (season_value as Array):
		season_bonus = 1.12
	var stress: float = 0.0
	if moisture < ideal_water - water_tolerance:
		stress += 8.0
	if moisture > ideal_water + water_tolerance:
		stress += 7.0
	stress += pest * 0.09 + disease * 0.12 + _float(result, "weed") * 0.04
	if _float(result, "fertility") < 30.0:
		stress += 7.0
	var health: float = clampf(_float(result, "health", 100.0) + 2.0 - stress, 0.0, 100.0)
	result["health"] = health
	if health <= 0.0:
		result["dead"] = true
		return result
	var growth_factor: float = (water_score + fertility_score + light_score + temperature_score + spacing_score) / 5.0
	growth_factor *= season_bonus
	growth_factor *= clampf(1.0 - (pest + disease) / 260.0, 0.2, 1.0)
	result["growth"] = _float(result, "growth") + growth_factor
	var growth_days: float = maxf(1.0, _float(crop_def, "growth_days", 6.0))
	result["stage"] = clampi(int(floor(_float(result, "growth") / growth_days * 5.0)), 0, 5)
	var quality: float = _float(result, "quality", 70.0)
	quality += (growth_factor - 0.55) * 5.0
	quality -= (pest + disease + _float(result, "weed")) * 0.015
	result["quality"] = clampf(quality, 0.0, 100.0)
	result["fertility"] = clampf(_float(result, "fertility") - 1.1, 0.0, 100.0)
	result["nitrogen"] = clampf(_float(result, "nitrogen") - 1.4, 0.0, 100.0)
	result["phosphorus"] = clampf(_float(result, "phosphorus") - 0.8, 0.0, 100.0)
	result["potassium"] = clampf(_float(result, "potassium") - 1.0, 0.0, 100.0)
	return result

static func harvest_quality(tile: Dictionary) -> int:
	var score: float = _float(tile, "quality", 60.0)
	score += _float(tile, "health", 80.0) * 0.18
	score -= _float(tile, "pest") * 0.18
	score -= _float(tile, "disease") * 0.22
	return clampi(int(round(score)), 1, 100)

static func run_experiment(crop_def: Dictionary) -> Dictionary:
	var results: Dictionary = {}
	var strategies: Array[String] = ["daily", "when_dry", "twice_daily"]
	for strategy: String in strategies:
		var rng: RandomNumberGenerator = RandomNumberGenerator.new()
		rng.seed = 9100 + strategy.hash()
		var tile: Dictionary = new_tile(true)
		tile["tilled"] = true
		tile["crop"] = "experiment"
		tile["moisture"] = 45.0
		var water_used: float = 0.0
		var cost: float = 12.0
		for day: int in range(10):
			var moisture: float = _float(tile, "moisture")
			if strategy == "daily":
				tile["moisture"] = minf(100.0, moisture + 28.0)
				water_used += 28.0
				cost += 1.4
			elif strategy == "when_dry" and moisture < 38.0:
				tile["moisture"] = minf(100.0, moisture + 35.0)
				water_used += 35.0
				cost += 1.8
			elif strategy == "twice_daily":
				tile["moisture"] = minf(100.0, moisture + 48.0)
				water_used += 48.0
				cost += 2.4
			var weather_def: Dictionary = {"rain":0.0,"evaporation":6.0,"light":90.0,"temperature":29.0,"pest":0.01,"disease":0.02}
			tile = simulate_day(tile, crop_def, weather_def, 0, rng)
		var growth_score: float = clampf(_float(tile, "growth") * 10.0, 0.0, 100.0)
		var quality_score: float = float(harvest_quality(tile))
		var yield_score: float = clampf(growth_score * quality_score / 100.0, 0.0, 100.0)
		results[strategy] = {"growth":growth_score,"yield":yield_score,"water":water_used,"cost":cost,"quality":quality_score}
	return results

static func self_test(data: Dictionary) -> Dictionary:
	var crops: Dictionary = _dict(data, "crops")
	var weather: Dictionary = _dict(data, "weather")
	if crops.size() < 5 or weather.size() < 9:
		return {"ok":false,"reason":"definitions"}
	var crop_def: Dictionary = _dict(crops, "water_spinach")
	var weather_def: Dictionary = _dict(weather, "clear")
	var rng: RandomNumberGenerator = RandomNumberGenerator.new()
	rng.seed = 20260731
	var tile: Dictionary = new_tile(true)
	tile["tilled"] = true
	tile["crop"] = "water_spinach"
	tile["moisture"] = 70.0
	for index: int in range(12):
		tile["moisture"] = minf(100.0, _float(tile, "moisture") + 16.0)
		tile = simulate_day(tile, crop_def, weather_def, 0, rng)
	var experiment: Dictionary = run_experiment(crop_def)
	var ok: bool = _int(tile, "stage") >= 4 and experiment.size() == 3 and experiment.has("when_dry")
	return {"ok":ok,"stage":_int(tile,"stage"),"experiment_count":experiment.size(),"symptom":primary_symptom(tile)}
