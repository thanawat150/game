extends "res://scripts/visual_density_fix.gd"

var progression_data: Dictionary = {}
var farm_level: int = 1
var farm_xp: int = 0
var unlocked_features: Dictionary = {}
var season_history: Array = []
var season_start_stats: Dictionary = {}
var season_actions: Dictionary = {}
var relationships: Dictionary = {}
var current_daily_event: Dictionary = {}
var daily_event_completed: bool = false
var diagnosis_correct_count: int = 0
var collection: Dictionary = {}
var collection_tab: int = 0
var crafted_recipe_ids: Dictionary = {}
var total_crafts: int = 0
var auto_rules: Dictionary = {}
var auto_queue: Array = []
var auto_history: Array = []
var floating_feedback: Array = []
var stage_flashes: Dictionary = {}
var hover_position: Vector2 = Vector2.ZERO

func _ready() -> void:
	progression_data = load_json("res://data/expansion.json")
	super._ready()
	print("GROWWISE_PROGRESSION_EVENTS_OK")

func tx(key_name: String) -> String:
	var custom: Dictionary = {
		"ui.progression":{"th":"ความก้าวหน้า","en":"Progression"},
		"ui.collection":{"th":"สมุดสะสม","en":"Collection"},
		"ui.events":{"th":"เหตุการณ์","en":"Events"},
		"ui.auto_rules":{"th":"ตั้งค่าออโต้","en":"Auto Rules"},
		"ui.level":{"th":"ระดับสวน","en":"Farm Level"},
		"ui.next_unlock":{"th":"ปลดล็อกถัดไป","en":"Next Unlock"}
	}
	if custom.has(key_name):
		var value: Dictionary = custom[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	farm_level = 1
	farm_xp = 0
	unlocked_features = {}
	season_history = []
	season_start_stats = current_stat_snapshot()
	season_actions = default_season_actions()
	relationships = {"teacher":0,"merchant":0,"researcher":0,"neighbor":0}
	current_daily_event = {}
	daily_event_completed = false
	diagnosis_correct_count = 0
	collection = {"crops":{},"symptoms":{},"creatures":{},"weather":{},"recipes":{},"seasons":{},"events":{}}
	collection_tab = 0
	crafted_recipe_ids = {}
	total_crafts = 0
	auto_rules = default_auto_rules()
	auto_queue = []
	auto_history = []
	floating_feedback = []
	stage_flashes = {}
	apply_level_unlocks(false)
	collect_entry("weather", current_weather)
	collect_entry("seasons", str(current_season))
	build_buttons()

func default_season_actions() -> Dictionary:
	return {"water":0,"fertilizer":0,"bio":0,"harvest":0,"diagnosis":0,"craft":0,"event":0}

func default_auto_rules() -> Dictionary:
	return {
		"water_enabled":true,"water_threshold":35,
		"harvest_enabled":true,"weed_enabled":true,"pest_enabled":true,
		"fertilizer_enabled":false,"plant_enabled":true,"keep_seed":5,
		"sell_enabled":true,"sell_threshold":20,"craft_enabled":true,"spray_minimum":2
	}

func current_stat_snapshot() -> Dictionary:
	return {
		"harvest":harvest_total,"water":water_used,"revenue":revenue,"expenses":expenses,
		"money":money,"knowledge":knowledge,"soil":soil_score,"eco":eco_score,"biodiversity":biodiversity_score
	}

func build_buttons() -> void:
	super.build_buttons()
	buttons.append(button("progression", Rect2(764, 500, 76, 54), "settings", "ui.progression"))

func handle_button(button_id: String) -> void:
	var hour: int = int(minutes / 60.0)
	if button_id == "shop" and (hour < 8 or hour >= 18):
		notify("ร้านค้าเปิด 08:00–18:00 น.", "error")
		return
	if button_id == "market" and (hour < 7 or hour >= 20):
		notify("ตลาดเปิด 07:00–20:00 น.", "error")
		return
	if button_id == "progression":
		overlay = "progression"
		return
	super.handle_button(button_id)

func _process(delta: float) -> void:
	super._process(delta)
	update_feedback(delta)
	refresh_auto_queue()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		hover_position = event.position
	if event is InputEventKey and event.pressed and not event.echo and mode == "game":
		match event.keycode:
			KEY_P:
				overlay = "progression"
				return
			KEY_J:
				overlay = "collection"
				return
			KEY_N:
				overlay = "npc_events"
				return
			KEY_R:
				overlay = "auto_rules"
				return
			KEY_Z:
				go_to_sleep()
				return
	super._unhandled_input(event)

func xp_needed_for_level(level_value: int) -> int:
	return 70 + (level_value - 1) * 35

func level_title() -> String:
	var titles: Array = array_value(progression_data, "level_titles")
	if titles.is_empty():
		return "ผู้ดูแลสวน"
	return String(titles[clampi(farm_level - 1, 0, titles.size() - 1)])

func add_farm_xp(amount: int, reason: String = "", position: Vector2 = Vector2.ZERO) -> void:
	if amount <= 0 or farm_level >= 20:
		return
	farm_xp += amount
	if not reason.is_empty():
		add_feedback("+%d XP • %s" % [amount, reason], position if position != Vector2.ZERO else Vector2(640, 140), GOLD)
	while farm_level < 20 and farm_xp >= xp_needed_for_level(farm_level):
		farm_xp -= xp_needed_for_level(farm_level)
		farm_level += 1
		money += farm_level * 15
		knowledge += 3
		apply_level_unlocks(true)
		add_feedback("ระดับสวน %d • %s" % [farm_level, level_title()], Vector2(640, 148), TEAL)
		if farm_level == 10:
			unlock_achievement("level_10")

func apply_level_unlocks(show_message: bool) -> void:
	var definitions: Array = array_value(progression_data, "unlocks")
	for value: Variant in definitions:
		var definition: Dictionary = value as Dictionary
		var required: int = int_value(definition, "level", 99)
		var unlock_id: String = string_value(definition, "id")
		if farm_level >= required and not bool(unlocked_features.get(unlock_id, false)):
			unlocked_features[unlock_id] = true
			if show_message:
				notify("ปลดล็อก: %s" % string_value(definition, "name"), "success")

func apply_tool(cell: Vector2i) -> void:
	var key_string: String = tile_key(cell)
	var before: Dictionary = dictionary_value(tiles, key_string).duplicate(true)
	var tool_before: String = selected_tool
	var seed_before: String = selected_seed
	super.apply_tool(cell)
	var after: Dictionary = dictionary_value(tiles, key_string)
	var position: Vector2 = iso(Vector2(cell)) + Vector2(0, -52)
	match tool_before:
		"hoe":
			if not bool(before.get("tilled", false)) and bool(after.get("tilled", false)):
				add_farm_xp(4, "เตรียมดิน", position)
				add_feedback("ดินพร้อมปลูก", position, Color("d8c28f"))
		"seed":
			if string_value(before, "crop").is_empty() and not string_value(after, "crop").is_empty():
				add_farm_xp(7, "ปลูก%s" % crop_name(seed_before), position)
				collect_entry("crops", seed_before)
				add_feedback("-1 เมล็ด%s" % crop_name(seed_before), position, CREAM)
		"water":
			if float_value(after, "moisture") > float_value(before, "moisture"):
				season_actions["water"] = int(season_actions.get("water", 0)) + 1
				add_farm_xp(2, "ให้น้ำพอดี", position)
				add_feedback("+ความชื้น", position, BLUE)
		"fertilize", "compost":
			if float_value(after, "fertility") > float_value(before, "fertility"):
				season_actions["fertilizer"] = int(season_actions.get("fertilizer", 0)) + 1
				add_farm_xp(4, "ฟื้นฟูดิน", position)
				add_feedback("+ความสมบูรณ์ดิน", position, GREEN)
		"bio":
			if float_value(after, "pest") < float_value(before, "pest"):
				season_actions["bio"] = int(season_actions.get("bio", 0)) + 1
				add_farm_xp(5, "ดูแลระบบนิเวศ", position)
				add_feedback("ลดศัตรูพืช", position, TEAL)
		"weed":
			if float_value(after, "weed") < float_value(before, "weed"):
				add_farm_xp(3, "ถอนวัชพืช", position)
				add_feedback("-วัชพืช", position, GREEN)
		"inspect":
			if not string_value(after, "crop").is_empty():
				add_farm_xp(1, "สังเกตพืช", position)
				collect_entry("symptoms", GrowWiseSimulation.primary_symptom(after))

func harvest_tile(tile: Dictionary, key_string: String) -> void:
	var crop_id: String = string_value(tile, "crop")
	var quality_value: int = GrowWiseSimulation.harvest_quality(tile)
	var before_total: int = harvest_total
	var parts: PackedStringArray = key_string.split(",")
	var cell: Vector2i = selected
	if parts.size() == 2:
		cell = Vector2i(int(parts[0]), int(parts[1]))
	super.harvest_tile(tile, key_string)
	var gained: int = harvest_total - before_total
	if gained > 0:
		season_actions["harvest"] = int(season_actions.get("harvest", 0)) + 1
		add_farm_xp(15 + quality_value / 10, "เก็บเกี่ยว%s" % crop_name(crop_id), iso(Vector2(cell)) + Vector2(0, -62))
		add_feedback("+%d %s • Q%d" % [gained, crop_name(crop_id), quality_value], iso(Vector2(cell)) + Vector2(0, -70), GOLD)
		unlock_achievement("first_harvest")
		if quality_value >= 90:
			unlock_achievement("quality_90")
		check_achievements()

func craft_recipe(index: int, automatic: bool = false) -> void:
	var recipes: Array = array_value(crafting_data, "recipes")
	var recipe_id: String = ""
	if index >= 0 and index < recipes.size():
		recipe_id = string_value(recipes[index] as Dictionary, "id")
	var before_state: String = JSON.stringify(inventory)
	super.craft_recipe(index, automatic)
	if JSON.stringify(inventory) != before_state:
		total_crafts += 1
		season_actions["craft"] = int(season_actions.get("craft", 0)) + 1
		if not recipe_id.is_empty():
			crafted_recipe_ids[recipe_id] = true
			collect_entry("recipes", recipe_id)
		add_farm_xp(12, "คราฟต์ของใช้", Vector2(640, 150))
		check_achievements()

func sell_crop(crop_id: String) -> void:
	var before_money: int = money
	super.sell_crop(crop_id)
	var earned: int = money - before_money
	if earned > 0:
		if bool(unlocked_features.get("market_bonus", false)):
			var bonus: int = maxi(1, int(round(float(earned) * 0.05)))
			money += bonus
			revenue += bonus
			earned += bonus
		add_farm_xp(5, "บริหารตลาด", Vector2(760, 150))
		add_feedback("รายได้ +%d" % earned, Vector2(760, 150), GOLD)

func lab_click(position: Vector2) -> void:
	var before_result: String = JSON.stringify(experiment_results)
	super.lab_click(position)
	if JSON.stringify(experiment_results) != before_result:
		var multiplier: int = 2 if bool(unlocked_features.get("expert_lab", false)) else 1
		add_farm_xp(20 * multiplier, "ทดลองเปรียบเทียบ", Vector2(640, 150))
		relationships["researcher"] = mini(100, int(relationships.get("researcher", 0)) + 2)

func cycle_autoplay() -> void:
	var next_mode: int = (autoplay_mode + 1) % 4
	if next_mode == GrowWiseAutoPlay.MODE_CARE and farm_level < 2:
		notify("ออโต้ช่วยดูแลปลดล็อกที่ระดับสวน 2", "error")
		return
	if next_mode in [GrowWiseAutoPlay.MODE_FULL, GrowWiseAutoPlay.MODE_LEARNING] and farm_level < 6:
		notify("ฟาร์มอัตโนมัติปลดล็อกที่ระดับสวน 6", "error")
		return
	autoplay_mode = next_mode
	autoplay_timer = 0.0
	notify("%s: %s" % [tx("ui.autoplay"), GrowWiseAutoPlay.mode_name(autoplay_mode, language)], "success")

func run_autoplay_step() -> void:
	var action: Dictionary = GrowWiseAutoPlay.choose_action(tiles, inventory, CROP_IDS, autoplay_mode, day, money)
	if action.is_empty():
		if bool(auto_rules.get("craft_enabled", true)) and try_auto_craft():
			log_auto("คราฟต์ของจำเป็น")
			return
		autoplay_last_action = {"action":"idle"}
		return
	var action_id: String = String(action.get("action", ""))
	if not auto_action_allowed(action_id, action):
		autoplay_last_action = {"action":"idle","reason":"rule"}
		return
	if action_id == "sell":
		sell_crop(String(action.get("crop", "")))
		log_auto("ขายผลผลิตตามเกณฑ์")
	elif action_id == "restock":
		auto_restock(String(action.get("crop", "")))
		log_auto("ซื้อเมล็ดเติมคลัง")
	else:
		var cell: Vector2i = action.get("cell", Vector2i(-1, -1)) as Vector2i
		if valid_cell(cell):
			selected = cell
			player_position = Vector2(float(cell.x) + 0.25, float(cell.y) + 0.45)
			selected_tool = action_id
			if action_id == "seed":
				selected_seed = String(action.get("seed", selected_seed))
			apply_tool(cell)
			log_auto(auto_action_text(action_id))
	autoplay_last_action = action
	autoplay_actions_total += 1
	if autoplay_mode == GrowWiseAutoPlay.MODE_LEARNING:
		notify(auto_action_text(action_id), "success")

func auto_action_allowed(action_id: String, action: Dictionary) -> bool:
	match action_id:
		"water":
			if not bool(auto_rules.get("water_enabled", true)):
				return false
			var cell: Vector2i = action.get("cell", Vector2i(-1, -1)) as Vector2i
			if valid_cell(cell):
				return float_value(dictionary_value(tiles, tile_key(cell)), "moisture") < float(auto_rules.get("water_threshold", 35))
		"harvest": return bool(auto_rules.get("harvest_enabled", true))
		"weed": return bool(auto_rules.get("weed_enabled", true))
		"bio": return bool(auto_rules.get("pest_enabled", true))
		"fertilize", "compost": return bool(auto_rules.get("fertilizer_enabled", false))
		"seed":
			if not bool(auto_rules.get("plant_enabled", true)):
				return false
			var crop_id: String = String(action.get("seed", selected_seed))
			return int(inventory.get("seed_" + crop_id, 0)) > int(auto_rules.get("keep_seed", 5))
		"sell":
			if not bool(auto_rules.get("sell_enabled", true)):
				return false
			var sell_id: String = String(action.get("crop", ""))
			return int(inventory.get("produce_" + sell_id, 0)) >= int(auto_rules.get("sell_threshold", 20))
	return true

func refresh_auto_queue() -> void:
	auto_queue = []
	if autoplay_mode == GrowWiseAutoPlay.MODE_OFF:
		return
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if not string_value(tile, "crop").is_empty():
			if bool(auto_rules.get("harvest_enabled", true)) and int_value(tile, "stage") >= 5:
				auto_queue.append("เก็บเกี่ยวแปลง " + key_string)
			elif bool(auto_rules.get("water_enabled", true)) and float_value(tile, "moisture") < float(auto_rules.get("water_threshold", 35)):
				auto_queue.append("รดน้ำแปลง " + key_string)
			elif bool(auto_rules.get("pest_enabled", true)) and float_value(tile, "pest") >= 35.0:
				auto_queue.append("ใช้ชีวภัณฑ์แปลง " + key_string)
			elif bool(auto_rules.get("weed_enabled", true)) and float_value(tile, "weed") >= 32.0:
				auto_queue.append("ถอนวัชพืชแปลง " + key_string)
		if auto_queue.size() >= 5:
			break

func log_auto(text_value: String) -> void:
	auto_history.push_front("วัน %d %02d:%02d • %s" % [day, int(minutes / 60.0), int(minutes) % 60, text_value])
	if auto_history.size() > 12:
		auto_history.resize(12)

func advance_day() -> void:
	var previous_stages: Dictionary = {}
	for key_value: Variant in tiles.keys():
		previous_stages[String(key_value)] = int_value(dictionary_value(tiles, String(key_value)), "stage")
	super.advance_day()
	add_farm_xp(2, "เรียนรู้ครบหนึ่งวัน", Vector2(620, 120))
	collect_entry("weather", current_weather)
	collect_entry("seasons", str(current_season))
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if int_value(tile, "stage") > int(previous_stages.get(key_string, int_value(tile, "stage"))):
			stage_flashes[key_string] = 2.5
	assign_daily_event()
	check_achievements()

func assign_daily_event() -> void:
	var events: Array = array_value(progression_data, "daily_events")
	if events.is_empty():
		return
	current_daily_event = (events[posmod(day + farm_level, events.size())] as Dictionary).duplicate(true)
	daily_event_completed = false
	collect_entry("events", string_value(current_daily_event, "id"))

func complete_daily_event() -> void:
	if current_daily_event.is_empty() or daily_event_completed:
		return
	var event_id: String = string_value(current_daily_event, "id")
	var completed: bool = false
	match event_id:
		"merchant_order":
			for crop_id: String in CROP_IDS:
				var item_id: String = "produce_" + crop_id
				if int(inventory.get(item_id, 0)) >= 3:
					inventory[item_id] = int(inventory.get(item_id, 0)) - 3
					completed = true
					break
		"research_sample":
			if int(inventory.get("compost", 0)) >= 1 and int(inventory.get("herb", 0)) >= 2:
				inventory["compost"] = int(inventory.get("compost", 0)) - 1
				inventory["herb"] = int(inventory.get("herb", 0)) - 2
				completed = true
		"neighbor_help":
			if int(inventory.get("water_bottle", 0)) >= 1:
				inventory["water_bottle"] = int(inventory.get("water_bottle", 0)) - 1
				completed = true
		"pest_alert":
			if int(inventory.get("bio_spray", 0)) >= 1:
				inventory["bio_spray"] = int(inventory.get("bio_spray", 0)) - 1
				completed = true
	if not completed:
		notify("ของที่ต้องส่งยังไม่ครบ", "error")
		return
	daily_event_completed = true
	var reward_money: int = int_value(current_daily_event, "reward_money")
	var reward_knowledge: int = int_value(current_daily_event, "reward_knowledge")
	money += reward_money
	knowledge += reward_knowledge
	add_farm_xp(int_value(current_daily_event, "reward_xp"), "ช่วยเหลือชุมชน", Vector2(650, 150))
	var npc_id: String = string_value(current_daily_event, "npc", "neighbor")
	relationships[npc_id] = mini(100, int(relationships.get(npc_id, 0)) + 8)
	season_actions["event"] = int(season_actions.get("event", 0)) + 1
	notify("ส่งงานสำเร็จ • ความสัมพันธ์เพิ่ม", "success")

func build_season_report() -> void:
	super.build_season_report()
	var previous: Dictionary = season_history[0] as Dictionary if not season_history.is_empty() else {}
	var result: Dictionary = season_report.duplicate(true)
	result["previous_yield"] = int(previous.get("yield", 0))
	result["previous_profit"] = int(previous.get("profit", 0))
	result["grade"] = season_grade(result)
	result["strengths"] = season_strengths(result)
	result["improvements"] = season_improvements(result)
	season_report = result
	season_history.push_front(result.duplicate(true))
	if season_history.size() > 8:
		season_history.resize(8)
	season_start_stats = current_stat_snapshot()
	season_actions = default_season_actions()
	if int(result.get("profit", 0)) >= 500:
		unlock_achievement("profit_500")
	if water_efficiency >= 80:
		unlock_achievement("water_saver")

func season_grade(report: Dictionary) -> String:
	var score: int = clampi(int(report.get("profit", 0)) / 10, -20, 35)
	score += int(report.get("soil", 0)) / 5
	score += int(report.get("biodiversity", 0)) / 6
	score += water_efficiency / 6
	if score >= 70: return "S"
	if score >= 55: return "A"
	if score >= 40: return "B"
	return "C"

func season_strengths(report: Dictionary) -> Array[String]:
	var result: Array[String] = []
	if int(report.get("profit", 0)) > 0: result.append("สวนทำกำไรได้")
	if water_efficiency >= 75: result.append("ใช้น้ำมีประสิทธิภาพ")
	if int(report.get("soil", 0)) >= 70: result.append("สุขภาพดินดี")
	if int(report.get("biodiversity", 0)) >= 60: result.append("สวนมีความหลากหลาย")
	if result.is_empty(): result.append("ผ่านฤดูกาลและมีข้อมูลให้เรียนรู้")
	return result.slice(0, mini(2, result.size()))

func season_improvements(report: Dictionary) -> Array[String]:
	var result: Array[String] = []
	if int(report.get("profit", 0)) <= 0: result.append("ลดต้นทุนและแปรรูปก่อนขาย")
	if water_efficiency < 70: result.append("รดตามความชื้นหรือใช้ระบบน้ำ")
	if int(report.get("soil", 0)) < 65: result.append("เพิ่มปุ๋ยหมักและพักแปลง")
	if int(report.get("biodiversity", 0)) < 55: result.append("ลดสารและเพิ่มพื้นที่แมลงดี")
	if result.is_empty(): result.append("ทดลองพืชและวิธีให้น้ำใหม่")
	return result.slice(0, mini(2, result.size()))

func collect_entry(category: String, entry_id: String) -> void:
	if entry_id.is_empty(): return
	var category_data: Dictionary = dictionary_value(collection, category)
	category_data[entry_id] = int(category_data.get(entry_id, 0)) + 1
	collection[category] = category_data

func unlock_achievement(achievement_id: String) -> void:
	if bool(achievements.get(achievement_id, false)):
		return
	var definitions: Array = array_value(progression_data, "achievements")
	for value: Variant in definitions:
		var definition: Dictionary = value as Dictionary
		if string_value(definition, "id") == achievement_id:
			achievements[achievement_id] = true
			var reward: int = int_value(definition, "reward")
			money += reward
			add_feedback("Achievement: %s +%d" % [string_value(definition, "name"), reward], Vector2(640, 150), GOLD)
			return

func check_achievements() -> void:
	if care_streak >= 7: unlock_achievement("care_7")
	if dictionary_value(collection, "crops").size() >= 5: unlock_achievement("all_crops")
	if crafted_recipe_ids.size() >= 5: unlock_achievement("master_crafter")
	if dictionary_value(collection, "creatures").size() >= 5: unlock_achievement("biodiversity_5")
	if diagnosis_correct_count >= 10: unlock_achievement("diagnosis_10")

func add_feedback(text_value: String, position: Vector2, color_value: Color) -> void:
	floating_feedback.append({"text":text_value,"position":position,"color":color_value,"time":2.2})
	if floating_feedback.size() > 12:
		floating_feedback.pop_front()

func update_feedback(delta: float) -> void:
	for index: int in range(floating_feedback.size() - 1, -1, -1):
		var item: Dictionary = floating_feedback[index] as Dictionary
		item["time"] = float(item.get("time", 0.0)) - delta
		item["position"] = (item.get("position", Vector2.ZERO) as Vector2) + Vector2(0, -18.0 * delta)
		if float(item.get("time", 0.0)) <= 0.0:
			floating_feedback.remove_at(index)
		else:
			floating_feedback[index] = item
	for key_value: Variant in stage_flashes.keys():
		var key_string: String = String(key_value)
		stage_flashes[key_string] = float(stage_flashes[key_string]) - delta
		if float(stage_flashes[key_string]) <= 0.0:
			stage_flashes.erase(key_string)

func go_to_sleep() -> void:
	if int(minutes / 60.0) < 18:
		notify("นอนได้หลัง 18:00 น.", "error")
		return
	minutes = 1439.0
	add_feedback("พักผ่อนและเริ่มวันใหม่", Vector2(640, 170), BLUE)

func save_game(slot_number: int, automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number, automatic)
	if not result: return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty(): return result
	payload["farm_level"] = farm_level
	payload["farm_xp"] = farm_xp
	payload["unlocked_features"] = unlocked_features
	payload["season_history"] = season_history
	payload["season_start_stats"] = season_start_stats
	payload["season_actions"] = season_actions
	payload["relationships"] = relationships
	payload["current_daily_event"] = current_daily_event
	payload["daily_event_completed"] = daily_event_completed
	payload["diagnosis_correct_count"] = diagnosis_correct_count
	payload["collection"] = collection
	payload["crafted_recipe_ids"] = crafted_recipe_ids
	payload["total_crafts"] = total_crafts
	payload["auto_rules"] = auto_rules
	payload["auto_history"] = auto_history
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(payload))
		file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result: return false
	var payload: Dictionary = read_save("%s/slot_%d.json" % [SAVE_DIR, slot_number])
	farm_level = int_value(payload, "farm_level", 1)
	farm_xp = int_value(payload, "farm_xp", 0)
	unlocked_features = dictionary_value(payload, "unlocked_features")
	season_history = array_value(payload, "season_history")
	season_start_stats = dictionary_value(payload, "season_start_stats")
	season_actions = dictionary_value(payload, "season_actions")
	relationships = dictionary_value(payload, "relationships")
	current_daily_event = dictionary_value(payload, "current_daily_event")
	daily_event_completed = bool(payload.get("daily_event_completed", false))
	diagnosis_correct_count = int_value(payload, "diagnosis_correct_count", 0)
	collection = dictionary_value(payload, "collection")
	crafted_recipe_ids = dictionary_value(payload, "crafted_recipe_ids")
	total_crafts = int_value(payload, "total_crafts", 0)
	auto_rules = dictionary_value(payload, "auto_rules")
	if auto_rules.is_empty(): auto_rules = default_auto_rules()
	auto_history = array_value(payload, "auto_history")
	apply_level_unlocks(false)
	return true

func overlay_click(position: Vector2) -> void:
	if overlay in ["progression","collection","npc_events","auto_rules"]:
		if Rect2(1012, 76, 42, 36).has_point(position):
			overlay = ""
			return
		if overlay == "collection":
			for index: int in range(4):
				if Rect2(250 + index * 190, 120, 175, 38).has_point(position):
					collection_tab = index
					return
		elif overlay == "npc_events":
			if Rect2(690, 525, 270, 48).has_point(position):
				complete_daily_event()
				return
		elif overlay == "auto_rules":
			handle_auto_rules_click(position)
			return
	super.overlay_click(position)

func handle_auto_rules_click(position: Vector2) -> void:
	var keys: Array[String] = ["water_enabled","harvest_enabled","weed_enabled","pest_enabled","fertilizer_enabled","plant_enabled","sell_enabled","craft_enabled"]
	for index: int in range(keys.size()):
		var column: int = index % 2
		var row: int = int(index / 2)
		if Rect2(260 + column * 365, 160 + row * 58, 340, 46).has_point(position):
			auto_rules[keys[index]] = not bool(auto_rules.get(keys[index], false))
			return
	if Rect2(310, 425, 55, 40).has_point(position): auto_rules["water_threshold"] = maxi(10, int(auto_rules.get("water_threshold",35)) - 5)
	if Rect2(370, 425, 55, 40).has_point(position): auto_rules["water_threshold"] = mini(90, int(auto_rules.get("water_threshold",35)) + 5)
	if Rect2(665, 425, 55, 40).has_point(position): auto_rules["sell_threshold"] = maxi(5, int(auto_rules.get("sell_threshold",20)) - 5)
	if Rect2(725, 425, 55, 40).has_point(position): auto_rules["sell_threshold"] = mini(99, int(auto_rules.get("sell_threshold",20)) + 5)

func draw_overlay() -> void:
	match overlay:
		"progression": draw_progression_overlay()
		"collection": draw_collection_overlay()
		"npc_events": draw_events_overlay()
		"auto_rules": draw_auto_rules_overlay()
		"season_report": draw_complete_season_report()
		_: super.draw_overlay()

func draw_expansion_shell(title: String) -> void:
	draw_rect(Rect2(0, 0, 1280, 720), Color(0.03, 0.05, 0.04, 0.72))
	panel(Rect2(210, 58, 860, 600), CREAM)
	panel(Rect2(1012, 76, 42, 36), RED)
	draw_text("×", Vector2(1022, 104), 23, Color.WHITE, 22.0, HORIZONTAL_ALIGNMENT_CENTER)
	draw_text(title, Vector2(245, 105), 28, GREEN)

func draw_progression_overlay() -> void:
	draw_expansion_shell(tx("ui.progression"))
	var needed: int = xp_needed_for_level(farm_level)
	draw_text("ระดับ %d/20 • %s" % [farm_level, level_title()], Vector2(255, 155), 24, GREEN)
	draw_bar(Rect2(255, 175, 760, 28), float(farm_xp) / maxf(1.0, float(needed)) * 100.0, GOLD, "%d/%d XP" % [farm_xp, needed])
	draw_text(tx("ui.next_unlock"), Vector2(255, 240), 19, INK)
	var definitions: Array = array_value(progression_data, "unlocks")
	var y: float = 275.0
	for value: Variant in definitions:
		var definition: Dictionary = value as Dictionary
		var level_value: int = int_value(definition, "level")
		if level_value < farm_level or y > 445.0: continue
		var unlocked: bool = farm_level >= level_value
		draw_text("%s Lv.%d • %s" % ["✓" if unlocked else "○", level_value, string_value(definition,"name")], Vector2(270, y), 16, TEAL if unlocked else INK)
		draw_text(string_value(definition,"description"), Vector2(455, y), 13, INK, 540.0)
		y += 36.0
	draw_text("Achievement %d/%d" % [achievements.size(), array_value(progression_data,"achievements").size()], Vector2(255, 510), 18, GOLD)
	draw_text("P ความก้าวหน้า • J สมุดสะสม • N เหตุการณ์ • R ตั้งค่าออโต้ • Z นอน", Vector2(255, 575), 14, INK)

func draw_collection_overlay() -> void:
	draw_expansion_shell(tx("ui.collection"))
	var tabs: Array[String] = ["พืช","อาการ","อากาศ/ฤดู","สูตร/เหตุการณ์"]
	for index: int in range(tabs.size()):
		panel(Rect2(250 + index * 190, 120, 175, 38), GOLD if collection_tab == index else MIST)
		draw_text(tabs[index], Vector2(255 + index * 190, 147), 15, INK, 165.0, HORIZONTAL_ALIGNMENT_CENTER)
	var categories: Array[String] = ["crops","symptoms","weather","recipes"]
	var category: Dictionary = dictionary_value(collection, categories[collection_tab])
	var y: float = 195.0
	if category.is_empty():
		draw_text("ยังไม่ค้นพบข้อมูลในหมวดนี้", Vector2(260, 225), 18, INK)
	else:
		for entry_id: String in category:
			draw_text("✓ %s" % entry_id, Vector2(270, y), 16, TEAL)
			draw_text("พบ %d ครั้ง" % int(category[entry_id]), Vector2(700, y), 14, GOLD)
			y += 32.0
			if y > 550.0: break

func draw_events_overlay() -> void:
	draw_expansion_shell(tx("ui.events"))
	if current_daily_event.is_empty():
		draw_text("วันนี้ยังไม่มีคำขอใหม่", Vector2(270, 185), 20, INK)
		return
	var npc_names: Dictionary = dictionary_value(progression_data, "npc_names")
	var npc_id: String = string_value(current_daily_event, "npc")
	draw_text(String(npc_names.get(npc_id, npc_id)), Vector2(270, 160), 21, GREEN)
	draw_text(string_value(current_daily_event,"title"), Vector2(270, 205), 26, GOLD)
	draw_text(string_value(current_daily_event,"description"), Vector2(270, 250), 18, INK, 700.0)
	draw_text("ความสัมพันธ์: %d/100" % int(relationships.get(npc_id,0)), Vector2(270, 315), 17, TEAL)
	draw_text("รางวัล: เงิน %d • ความรู้ %d • XP %d" % [int_value(current_daily_event,"reward_money"),int_value(current_daily_event,"reward_knowledge"),int_value(current_daily_event,"reward_xp")], Vector2(270, 360), 16, INK)
	panel(Rect2(690, 525, 270, 48), TEAL if not daily_event_completed else MIST)
	draw_text("ส่งของให้ NPC" if not daily_event_completed else "ส่งแล้ววันนี้", Vector2(700, 558), 18, Color.WHITE if not daily_event_completed else INK, 250.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_auto_rules_overlay() -> void:
	draw_expansion_shell(tx("ui.auto_rules"))
	var labels: Array[String] = ["รดน้ำ","เก็บเกี่ยว","ถอนวัชพืช","ใช้ชีวภัณฑ์","ใส่ปุ๋ย","ปลูกพืช","ขายผลผลิต","คราฟต์ของ"]
	var keys: Array[String] = ["water_enabled","harvest_enabled","weed_enabled","pest_enabled","fertilizer_enabled","plant_enabled","sell_enabled","craft_enabled"]
	for index: int in range(keys.size()):
		var column: int = index % 2
		var row: int = int(index / 2)
		var enabled: bool = bool(auto_rules.get(keys[index], false))
		var rect_value: Rect2 = Rect2(260 + column * 365, 160 + row * 58, 340, 46)
		panel(rect_value, TEAL if enabled else MIST)
		draw_text(("✓ " if enabled else "○ ") + labels[index], rect_value.position + Vector2(10,31), 16, Color.WHITE if enabled else INK)
	draw_text("รดเมื่อความชื้นต่ำกว่า %d%%" % int(auto_rules.get("water_threshold",35)), Vector2(260, 410), 17, INK)
	panel(Rect2(310,425,55,40),WOOD_LIGHT); draw_text("−",Vector2(326,454),22,Color.WHITE)
	panel(Rect2(370,425,55,40),WOOD_LIGHT); draw_text("+",Vector2(386,454),22,Color.WHITE)
	draw_text("ขายเมื่อมีอย่างน้อย %d" % int(auto_rules.get("sell_threshold",20)), Vector2(615, 410), 17, INK)
	panel(Rect2(665,425,55,40),WOOD_LIGHT); draw_text("−",Vector2(681,454),22,Color.WHITE)
	panel(Rect2(725,425,55,40),WOOD_LIGHT); draw_text("+",Vector2(741,454),22,Color.WHITE)
	draw_text("คิวงานถัดไป", Vector2(260, 510), 18, GREEN)
	for index: int in range(mini(4,auto_queue.size())):
		draw_text("%d. %s" % [index+1,String(auto_queue[index])], Vector2(275,540+index*25), 14, INK)

func draw_complete_season_report() -> void:
	draw_expansion_shell("สรุปปลายฤดู")
	var grade: String = String(season_report.get("grade","C"))
	draw_text("อันดับฤดูกาล %s" % grade, Vector2(260, 160), 38, GOLD)
	var rows: Array[Array] = [
		["ผลผลิต",int(season_report.get("yield",0)),int(season_report.get("previous_yield",0))],
		["น้ำที่ใช้",int(season_report.get("water",0)),0],
		["รายได้",int(season_report.get("revenue",0)),0],
		["ต้นทุน",int(season_report.get("expenses",0)),0],
		["กำไรสุทธิ",int(season_report.get("profit",0)),int(season_report.get("previous_profit",0))],
		["สุขภาพดิน",int(season_report.get("soil",0)),0],
		["ความหลากหลายฯ",int(season_report.get("biodiversity",0)),0]
	]
	for index: int in range(rows.size()):
		var row: Array = rows[index]
		draw_text(String(row[0]), Vector2(270, 220+index*36), 17, INK)
		draw_text(str(row[1]), Vector2(550,220+index*36), 18, GREEN,100.0,HORIZONTAL_ALIGNMENT_RIGHT)
		if int(row[2]) != 0:
			var delta_value: int = int(row[1])-int(row[2])
			draw_text("%+d" % delta_value, Vector2(680,220+index*36), 14, TEAL if delta_value>=0 else RED)
	draw_text("จุดเด่น", Vector2(760, 220), 18, GREEN)
	var strengths: Array = array_value(season_report,"strengths")
	for index: int in range(strengths.size()): draw_text("✓ "+String(strengths[index]),Vector2(770,250+index*30),15,INK)
	draw_text("ควรปรับปรุง", Vector2(760, 340), 18, RED)
	var improvements: Array = array_value(season_report,"improvements")
	for index: int in range(improvements.size()): draw_text("• "+String(improvements[index]),Vector2(770,370+index*30),15,INK)
	draw_text("กด Esc เพื่อเริ่มฤดูถัดไป",Vector2(270,575),16,INK)

func draw_world() -> void:
	super.draw_world()
	draw_day_night_tint()
	draw_npc_schedule()
	for key_value: Variant in stage_flashes.keys():
		var parts: PackedStringArray = String(key_value).split(",")
		if parts.size()==2:
			var p: Vector2 = iso(Vector2(int(parts[0]),int(parts[1]))) + Vector2(0,-48)
			draw_circle(p,12.0+sin(Time.get_ticks_msec()*0.01)*3.0,Color(1.0,0.9,0.35,0.55))
	for value: Variant in floating_feedback:
		var item: Dictionary = value as Dictionary
		draw_text(String(item.get("text","")),item.get("position",Vector2.ZERO) as Vector2,14,item.get("color",Color.WHITE) as Color,260.0,HORIZONTAL_ALIGNMENT_CENTER)

func draw_day_night_tint() -> void:
	var hour: float = minutes / 60.0
	var tint: Color = Color(0,0,0,0)
	if hour < 5.0: tint = Color(0.05,0.08,0.22,0.48)
	elif hour < 7.0: tint = Color(0.32,0.20,0.30,0.22)
	elif hour >= 19.0 and hour < 21.0: tint = Color(0.30,0.16,0.27,0.25)
	elif hour >= 21.0: tint = Color(0.04,0.07,0.20,0.50)
	if tint.a > 0.0: draw_rect(Rect2(238,102,772,482),tint)

func draw_npc_schedule() -> void:
	var hour: int = int(minutes/60.0)
	var positions: Array[Vector2] = []
	if hour < 8: positions = [iso(Vector2(8.7,5.8)),iso(Vector2(8.4,6.3))]
	elif hour < 17: positions = [iso(Vector2(8.0,3.1)),iso(Vector2(7.6,1.2)),iso(Vector2(8.5,4.3))]
	else: positions = [iso(Vector2(7.8,5.8)),iso(Vector2(8.6,6.0))]
	for index: int in range(positions.size()):
		draw_circle(positions[index]+Vector2(index*5,-46),5.0,Color("f3e5c2"))

func draw_hud() -> void:
	super.draw_hud()
	draw_text("Lv.%d %s • XP %d/%d" % [farm_level,level_title(),farm_xp,xp_needed_for_level(farm_level)],Vector2(480,92),13,GREEN,330.0,HORIZONTAL_ALIGNMENT_CENTER)
