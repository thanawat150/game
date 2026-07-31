extends "res://scripts/autoplay_runtime_fix.gd"

const GrowWiseWorkforce = preload("res://scripts/workforce_system.gd")

var workforce_data: Dictionary = {}
var hired_workers: Array = []
var workforce_candidates: Array = []
var workforce_log: Array = []
var selected_candidate_index: int = 0
var selected_worker_index: int = 0
var workforce_candidate_day: int = 0
var workforce_timer: float = 0.0
var workforce_cursor: int = 0
var workforce_daily_wages: int = 0
var workforce_daily_value: int = 0
var workforce_total_hires: int = 0
var workforce_total_actions: int = 0
var workforce_auto_assign: bool = true

func _ready() -> void:
	workforce_data = load_json("res://data/workforce.json")
	super._ready()
	var test_result: Dictionary = GrowWiseWorkforce.self_test(workforce_data)
	if bool(test_result.get("ok", false)):
		print("GROWWISE_WORKFORCE_OK")
	else:
		push_error("Workforce self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var labels: Dictionary = {
		"ui.workforce":{"th":"จ้างงาน","en":"Workers"},
		"ui.candidates":{"th":"ผู้สมัคร","en":"Candidates"},
		"ui.employees":{"th":"พนักงาน","en":"Employees"},
		"ui.hire":{"th":"รับเข้าทำงาน","en":"Hire"},
		"ui.change_role":{"th":"เปลี่ยนงาน","en":"Change Role"},
		"ui.rest_worker":{"th":"ให้พัก","en":"Rest"},
		"ui.fire_worker":{"th":"เลิกจ้าง","en":"Dismiss"},
		"ui.auto_assign":{"th":"มอบหมายอัตโนมัติ","en":"Auto Assign"},
		"ui.refresh_candidates":{"th":"รับสมัครใหม่","en":"Refresh"}
	}
	if labels.has(key_name):
		var value: Dictionary = labels[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	hired_workers = []
	workforce_candidates = []
	workforce_log = []
	selected_candidate_index = 0
	selected_worker_index = 0
	workforce_candidate_day = 0
	workforce_timer = 0.0
	workforce_cursor = 0
	workforce_daily_wages = 0
	workforce_daily_value = 0
	workforce_total_hires = 0
	workforce_total_actions = 0
	workforce_auto_assign = true
	refresh_workforce_candidates(true)
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	var kept: Array[Dictionary] = []
	for button_data: Dictionary in buttons:
		var button_id: String = String(button_data.get("id", ""))
		if button_id not in ["inventory_full", "crafting", "auto_rules_visible", "workforce", "autoplay_primary"]:
			kept.append(button_data)
	buttons = kept
	buttons.append(button("inventory_full", Rect2(1010, 492, 58, 52), "market", "ui.inventory_full"))
	buttons.append(button("crafting", Rect2(1073, 492, 58, 52), "compost", "ui.crafting"))
	buttons.append(button("auto_rules_visible", Rect2(1136, 492, 58, 52), "settings", "ui.auto_settings_visible"))
	buttons.append(button("workforce", Rect2(1199, 492, 61, 52), "quest", "ui.workforce"))
	buttons.append(button("autoplay_primary", Rect2(1010, 550, 250, 62), "", "ui.autoplay_primary"))

func handle_button(button_id: String) -> void:
	if button_id == "workforce":
		overlay = "workforce"
		refresh_workforce_candidates(false)
		return
	super.handle_button(button_id)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game" and event.keycode == KEY_Y:
		overlay = "workforce"
		refresh_workforce_candidates(false)
		return
	super._unhandled_input(event)

func _process(delta: float) -> void:
	super._process(delta)
	if mode != "game" or paused or not overlay.is_empty() or not workforce_auto_assign or hired_workers.is_empty():
		return
	workforce_timer += delta
	if workforce_timer >= 3.5:
		workforce_timer = 0.0
		run_workforce_tick()

func advance_day() -> void:
	super.advance_day()
	pay_and_reset_workforce()
	refresh_workforce_candidates(false)

func workforce_capacity() -> int:
	return GrowWiseWorkforce.capacity(town_metrics, building_levels)

func payroll_total() -> int:
	var total: int = 0
	for value: Variant in hired_workers:
		var worker: Dictionary = value as Dictionary
		total += int(worker.get("wage", 0))
	return total

func refresh_workforce_candidates(force_refresh: bool) -> void:
	if not force_refresh and workforce_candidate_day > 0 and day - workforce_candidate_day < 3:
		return
	workforce_candidates = GrowWiseWorkforce.generate_candidates(workforce_data, day, farm_level, 6)
	workforce_candidate_day = day
	selected_candidate_index = 0
	log_workforce("รายชื่อผู้สมัครรอบใหม่พร้อมแล้ว")

func pay_and_reset_workforce() -> void:
	workforce_daily_wages = payroll_total()
	workforce_daily_value = 0
	var paid: int = mini(money, workforce_daily_wages)
	money -= paid
	expenses += paid
	var fully_paid: bool = paid >= workforce_daily_wages
	for index: int in range(hired_workers.size()):
		var worker: Dictionary = (hired_workers[index] as Dictionary).duplicate(true)
		worker["actions_today"] = 0
		if bool(worker.get("resting", false)):
			worker["fatigue"] = maxf(0.0, float(worker.get("fatigue", 0.0)) - 42.0)
			worker["morale"] = minf(100.0, float(worker.get("morale", 70.0)) + 4.0)
			worker["resting"] = false
		else:
			worker["fatigue"] = maxf(0.0, float(worker.get("fatigue", 0.0)) - 20.0)
			worker["morale"] = clampf(float(worker.get("morale", 70.0)) + (1.5 if fully_paid else -15.0), 0.0, 100.0)
		hired_workers[index] = worker
	if workforce_daily_wages > 0:
		if fully_paid:
			log_workforce("จ่ายค่าแรงประจำวัน %d" % workforce_daily_wages)
		else:
			log_workforce("เงินไม่พอจ่ายค่าแรง • จ่าย %d/%d" % [paid, workforce_daily_wages])
			notify("เงินไม่พอจ่ายค่าแรง พนักงานกำลังใจลดลง", "error")

func run_workforce_tick() -> void:
	if hired_workers.is_empty():
		return
	for offset: int in range(hired_workers.size()):
		var index: int = posmod(workforce_cursor + offset, hired_workers.size())
		var worker: Dictionary = (hired_workers[index] as Dictionary).duplicate(true)
		if bool(worker.get("resting", false)) or float(worker.get("fatigue", 0.0)) >= 95.0:
			continue
		var limit_value: int = GrowWiseWorkforce.daily_action_limit(workforce_data, worker)
		if int(worker.get("actions_today", 0)) >= limit_value:
			continue
		var result: Dictionary = perform_worker_action(worker)
		if not bool(result.get("ok", false)):
			worker["last_action"] = "กำลังรองาน"
			hired_workers[index] = worker
			continue
		worker["actions_today"] = int(worker.get("actions_today", 0)) + 1
		var trait_data: Dictionary = GrowWiseWorkforce.trait_definition(workforce_data, String(worker.get("trait_id", "")))
		var fatigue_gain: float = 14.0 * (1.0 + float(trait_data.get("fatigue", 0.0)))
		worker["fatigue"] = clampf(float(worker.get("fatigue", 0.0)) + fatigue_gain, 0.0, 100.0)
		worker["morale"] = clampf(float(worker.get("morale", 70.0)) + 0.7, 0.0, 100.0)
		worker["last_action"] = String(result.get("text", "ทำงาน"))
		var experience_gain: int = 8 + int(round(float(result.get("value", 0)) / 10.0))
		worker = GrowWiseWorkforce.apply_experience(workforce_data, worker, experience_gain)
		workforce_daily_value += int(result.get("value", 0))
		workforce_total_actions += 1
		log_workforce("%s • %s" % [String(worker.get("name", "พนักงาน")), String(result.get("text", "ทำงาน"))])
		add_feedback(String(result.get("text", "พนักงานกำลังทำงาน")), Vector2(790, 150), TEAL)
		hired_workers[index] = worker
		workforce_cursor = posmod(index + 1, hired_workers.size())
		return
	workforce_cursor = posmod(workforce_cursor + 1, maxi(1, hired_workers.size()))

func perform_worker_action(worker: Dictionary) -> Dictionary:
	var role_id: String = String(worker.get("role", "farmhand"))
	match role_id:
		"farmhand": return worker_farm_action()
		"irrigator": return worker_irrigation_action()
		"animal_keeper": return worker_animal_action()
		"processor": return worker_processing_action()
		"driver": return worker_driver_action()
		"field_technician": return worker_lab_action()
	return {"ok": false}

func worker_farm_action() -> Dictionary:
	var action: Dictionary = AutoPlayRuntime.choose_action(tiles, inventory, CROP_IDS, AutoPlayRuntime.MODE_FULL, day, money)
	if action.is_empty():
		return {"ok": false}
	var action_id: String = String(action.get("action", ""))
	if action_id in ["sell", "restock"]:
		return {"ok": false}
	var cell_value: Variant = action.get("cell", Vector2i(-1, -1))
	if not (cell_value is Vector2i):
		return {"ok": false}
	var cell: Vector2i = cell_value as Vector2i
	if not valid_cell(cell):
		return {"ok": false}
	selected = cell
	selected_tool = action_id
	if action_id == "seed":
		selected_seed = String(action.get("seed", selected_seed))
	apply_tool(cell)
	return {"ok": true, "text": auto_action_text(action_id), "value": 18}

func worker_irrigation_action() -> Dictionary:
	var wet_key: String = ""
	var wet_value: float = 88.0
	var dry_key: String = ""
	var dry_value: float = 46.0
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if not bool(tile.get("farm", false)) or not bool(tile.get("tilled", false)):
			continue
		var moisture: float = float_value(tile, "moisture")
		if moisture > wet_value:
			wet_value = moisture
			wet_key = key_string
		if moisture < dry_value:
			dry_value = moisture
			dry_key = key_string
	if not wet_key.is_empty():
		var wet_tile: Dictionary = dictionary_value(tiles, wet_key)
		wet_tile["moisture"] = maxf(35.0, float_value(wet_tile, "moisture") - 18.0)
		tiles[wet_key] = wet_tile
		if bool(water_state.get("pond_built", false)):
			water_state["pond_level"] = minf(500.0, float(water_state.get("pond_level", 0.0)) + 12.0)
		return {"ok": true, "text": "ช่างน้ำระบายน้ำส่วนเกิน", "value": 22}
	if not dry_key.is_empty():
		var parts: PackedStringArray = dry_key.split(",")
		if parts.size() == 2:
			var cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
			selected = cell
			selected_tool = "water"
			apply_tool(cell)
			return {"ok": true, "text": "ช่างน้ำรดแปลงที่แห้งที่สุด", "value": 20}
	return {"ok": false}

func worker_animal_action() -> Dictionary:
	var pending: Dictionary = dictionary_value(animal_state, "pending_products")
	var pending_total: int = 0
	for item_id: String in pending:
		pending_total += int(pending[item_id])
	var manure_amount: int = int(animal_state.get("manure", 0))
	if pending_total + manure_amount > 0:
		collect_animal_products()
		return {"ok": true, "text": "เก็บผลผลิตสัตว์และปุ๋ยคอก", "value": (pending_total + manure_amount) * 8}
	var animals: Dictionary = dictionary_value(animal_state, "animals")
	var animal_count: int = 0
	for animal_id: String in animals:
		var group: Dictionary = dictionary_value(animals, animal_id)
		var count: int = int(group.get("count", 0))
		if count <= 0:
			continue
		animal_count += count
		group["health"] = minf(100.0, float(group.get("health", 100.0)) + 3.0)
		group["happiness"] = minf(100.0, float(group.get("happiness", 70.0)) + 4.0)
		animals[animal_id] = group
	animal_state["animals"] = animals
	if animal_count > 0:
		return {"ok": true, "text": "ดูแลสุขภาพสัตว์ %d ตัว" % animal_count, "value": animal_count * 4}
	return {"ok": false}

func worker_processing_action() -> Dictionary:
	var recipes: Array = array_value(agri_data, "processing_recipes")
	for index: int in range(recipes.size()):
		var recipe: Dictionary = recipes[index] as Dictionary
		var building_id: String = string_value(recipe, "building")
		var available: bool = int(building_levels.get(building_id, 0)) > 0 or int(dictionary_value(animal_state, "buildings").get(building_id, 0)) > 0
		if not available:
			continue
		var test_inventory: Dictionary = inventory.duplicate(true)
		if GrowWiseAgriExpansion.consume_requirements(dictionary_value(recipe, "requires"), test_inventory):
			process_agri_recipe(index)
			return {"ok": true, "text": "แปรรูป %s" % string_value(recipe, "name"), "value": int_value(recipe, "value", 30)}
	return {"ok": false}

func worker_driver_action() -> Dictionary:
	if not dictionary_value(logistics_state, "active_trip").is_empty():
		return {"ok": false}
	start_city_delivery()
	var active_trip: Dictionary = dictionary_value(logistics_state, "active_trip")
	if not active_trip.is_empty():
		return {"ok": true, "text": "ออกส่งสินค้าเข้าเมือง", "value": int(active_trip.get("cargo_value", 50))}
	return {"ok": false}

func worker_lab_action() -> Dictionary:
	var queue: Array = array_value(survey_state, "lab_queue")
	if not queue.is_empty():
		var job: Dictionary = (queue[0] as Dictionary).duplicate(true)
		job["ready_day"] = maxi(day, int(job.get("ready_day", day + 1)) - 1)
		queue[0] = job
		survey_state["lab_queue"] = queue
		knowledge += 1
		return {"ok": true, "text": "เร่งตรวจตัวอย่างในแล็บ", "value": 24}
	if not array_value(survey_state, "reports").is_empty():
		knowledge += 1
		return {"ok": true, "text": "สรุปคำแนะนำจากผลแล็บ", "value": 12}
	return {"ok": false}

func hire_selected_candidate() -> void:
	if selected_candidate_index < 0 or selected_candidate_index >= workforce_candidates.size():
		return
	if hired_workers.size() >= workforce_capacity():
		notify("ที่พักพนักงานเต็ม • สร้างบ้านหรือบ้านชาวเมืองเพิ่ม", "error")
		return
	var candidate: Dictionary = (workforce_candidates[selected_candidate_index] as Dictionary).duplicate(true)
	var cost: int = GrowWiseWorkforce.signing_cost(candidate)
	if money < cost:
		notify("เงินค่ารับเข้าทำงานไม่พอ • ต้องใช้ %d" % cost, "error")
		return
	money -= cost
	expenses += cost
	hired_workers.append(candidate)
	workforce_candidates.remove_at(selected_candidate_index)
	selected_candidate_index = clampi(selected_candidate_index, 0, maxi(0, workforce_candidates.size() - 1))
	selected_worker_index = hired_workers.size() - 1
	workforce_total_hires += 1
	base_town_reputation += 1
	add_farm_xp(12, "สร้างงานในชุมชน", Vector2(780, 150))
	log_workforce("รับ %s เข้าทำงานเป็น%s" % [String(candidate.get("name", "พนักงาน")), workforce_role_name(String(candidate.get("role", "")))])
	notify("รับ%sเข้าทำงานแล้ว" % String(candidate.get("name", "พนักงาน")), "success")

func change_selected_worker_role() -> void:
	if selected_worker_index < 0 or selected_worker_index >= hired_workers.size():
		return
	var worker: Dictionary = (hired_workers[selected_worker_index] as Dictionary).duplicate(true)
	var roles: Array[String] = GrowWiseWorkforce.unlocked_roles(workforce_data, farm_level)
	if roles.is_empty():
		return
	var current_index: int = roles.find(String(worker.get("role", "farmhand")))
	var next_role: String = roles[posmod(current_index + 1, roles.size())]
	var definition: Dictionary = GrowWiseWorkforce.role_definition(workforce_data, next_role)
	worker["role"] = next_role
	worker["wage"] = int(definition.get("base_wage", 25)) + int(round(float(worker.get("skill", 50)) / 9.0))
	worker["last_action"] = "เปลี่ยนหน้าที่"
	hired_workers[selected_worker_index] = worker
	log_workforce("%s เปลี่ยนเป็น%s" % [String(worker.get("name", "พนักงาน")), workforce_role_name(next_role)])

func toggle_selected_worker_rest() -> void:
	if selected_worker_index < 0 or selected_worker_index >= hired_workers.size():
		return
	var worker: Dictionary = (hired_workers[selected_worker_index] as Dictionary).duplicate(true)
	worker["resting"] = not bool(worker.get("resting", false))
	hired_workers[selected_worker_index] = worker
	log_workforce("%s: %s" % [String(worker.get("name", "พนักงาน")), "พักงาน" if bool(worker.get("resting", false)) else "กลับเข้ากะ"])

func dismiss_selected_worker() -> void:
	if selected_worker_index < 0 or selected_worker_index >= hired_workers.size():
		return
	var worker: Dictionary = hired_workers[selected_worker_index] as Dictionary
	var severance: int = mini(money, int(worker.get("wage", 0)))
	money -= severance
	expenses += severance
	log_workforce("เลิกจ้าง%s • ชดเชย %d" % [String(worker.get("name", "พนักงาน")), severance])
	hired_workers.remove_at(selected_worker_index)
	selected_worker_index = clampi(selected_worker_index, 0, maxi(0, hired_workers.size() - 1))
	base_town_reputation = maxi(0, base_town_reputation - 1)

func workforce_role_name(role_id: String) -> String:
	var definition: Dictionary = GrowWiseWorkforce.role_definition(workforce_data, role_id)
	var field_name: String = "name_th" if language == "th" else "name_en"
	return String(definition.get(field_name, role_id))

func workforce_trait_name(worker: Dictionary) -> String:
	var field_name: String = "trait_name_th" if language == "th" else "trait_name_en"
	return String(worker.get(field_name, ""))

func log_workforce(text_value: String) -> void:
	workforce_log.push_front("วัน %d %02d:%02d • %s" % [day, int(minutes / 60.0), int(minutes) % 60, text_value])
	if workforce_log.size() > 14:
		workforce_log.resize(14)

func overlay_click(position: Vector2) -> void:
	if overlay != "workforce":
		super.overlay_click(position)
		return
	if Rect2(1012, 76, 42, 36).has_point(position):
		overlay = ""
		return
	for index: int in range(mini(6, workforce_candidates.size())):
		if Rect2(245, 165 + index * 56, 370, 48).has_point(position):
			selected_candidate_index = index
			return
	for index: int in range(mini(6, hired_workers.size())):
		if Rect2(650, 165 + index * 56, 380, 48).has_point(position):
			selected_worker_index = index
			return
	if Rect2(245, 515, 170, 46).has_point(position):
		hire_selected_candidate()
	elif Rect2(425, 515, 190, 46).has_point(position):
		if money >= 10:
			money -= 10
			expenses += 10
			workforce_candidate_day = 0
			refresh_workforce_candidates(true)
	elif Rect2(650, 515, 115, 46).has_point(position):
		change_selected_worker_role()
	elif Rect2(775, 515, 105, 46).has_point(position):
		toggle_selected_worker_rest()
	elif Rect2(890, 515, 140, 46).has_point(position):
		dismiss_selected_worker()
	elif Rect2(650, 570, 380, 42).has_point(position):
		workforce_auto_assign = not workforce_auto_assign

func draw_overlay() -> void:
	if overlay == "workforce":
		draw_workforce_overlay()
		return
	super.draw_overlay()

func draw_workforce_overlay() -> void:
	draw_expansion_shell(tx("ui.workforce"))
	draw_text("พนักงาน %d/%d • ค่าแรง/วัน %d • มูลค่างานวันนี้ %d" % [hired_workers.size(), workforce_capacity(), payroll_total(), workforce_daily_value], Vector2(245, 128), 16, GOLD, 785.0)
	draw_text(tx("ui.candidates"), Vector2(245, 157), 19, GREEN)
	draw_text(tx("ui.employees"), Vector2(650, 157), 19, GREEN)
	for index: int in range(mini(6, workforce_candidates.size())):
		var candidate: Dictionary = workforce_candidates[index] as Dictionary
		var rect_value: Rect2 = Rect2(245, 165 + index * 56, 370, 48)
		panel(rect_value, GOLD if selected_candidate_index == index else Color("dce8d5"))
		draw_text("%s • %s" % [String(candidate.get("name", "-")), workforce_role_name(String(candidate.get("role", "")))], rect_value.position + Vector2(8, 20), 14, GREEN)
		draw_text("ทักษะ %d • ค่าแรง %d • %s" % [int(candidate.get("skill", 0)), int(candidate.get("wage", 0)), workforce_trait_name(candidate)], rect_value.position + Vector2(8, 41), 12, INK, 350.0)
	for index: int in range(mini(6, hired_workers.size())):
		var worker: Dictionary = hired_workers[index] as Dictionary
		var rect_value: Rect2 = Rect2(650, 165 + index * 56, 380, 48)
		panel(rect_value, GOLD if selected_worker_index == index else Color("dce8d5"))
		var rest_text: String = " • พัก" if bool(worker.get("resting", false)) else ""
		draw_text("%s • %s%s" % [String(worker.get("name", "-")), workforce_role_name(String(worker.get("role", ""))), rest_text], rect_value.position + Vector2(8, 20), 14, GREEN)
		draw_text("Skill %d • ใจ %d • เหนื่อย %d • ค่าแรง %d" % [int(worker.get("skill", 0)), int(round(float(worker.get("morale", 0.0)))), int(round(float(worker.get("fatigue", 0.0)))), int(worker.get("wage", 0))], rect_value.position + Vector2(8, 41), 12, INK, 360.0)
	panel(Rect2(245, 515, 170, 46), TEAL)
	draw_text(tx("ui.hire"), Vector2(252, 546), 15, Color.WHITE, 156.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(425, 515, 190, 46), WOOD_LIGHT)
	draw_text(tx("ui.refresh_candidates") + " 10", Vector2(432, 546), 14, Color.WHITE, 176.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(650, 515, 115, 46), WOOD_LIGHT)
	draw_text(tx("ui.change_role"), Vector2(655, 546), 13, Color.WHITE, 105.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(775, 515, 105, 46), BLUE)
	draw_text(tx("ui.rest_worker"), Vector2(780, 546), 13, Color.WHITE, 95.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(890, 515, 140, 46), RED)
	draw_text(tx("ui.fire_worker"), Vector2(895, 546), 13, Color.WHITE, 130.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(650, 570, 380, 42), TEAL if workforce_auto_assign else MIST)
	draw_text("%s: %s" % [tx("ui.auto_assign"), "ON" if workforce_auto_assign else "OFF"], Vector2(660, 598), 14, Color.WHITE if workforce_auto_assign else INK, 360.0, HORIZONTAL_ALIGNMENT_CENTER)
	if not workforce_log.is_empty():
		draw_text(String(workforce_log[0]), Vector2(245, 630), 12, INK, 785.0)

func save_game(slot_number: int, automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number, automatic)
	if not result:
		return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty():
		return result
	payload["hired_workers"] = hired_workers
	payload["workforce_candidates"] = workforce_candidates
	payload["workforce_log"] = workforce_log
	payload["workforce_candidate_day"] = workforce_candidate_day
	payload["workforce_daily_wages"] = workforce_daily_wages
	payload["workforce_daily_value"] = workforce_daily_value
	payload["workforce_total_hires"] = workforce_total_hires
	payload["workforce_total_actions"] = workforce_total_actions
	payload["workforce_auto_assign"] = workforce_auto_assign
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(payload))
		file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result:
		return false
	var payload: Dictionary = read_save("%s/slot_%d.json" % [SAVE_DIR, slot_number])
	hired_workers = array_value(payload, "hired_workers")
	workforce_candidates = array_value(payload, "workforce_candidates")
	workforce_log = array_value(payload, "workforce_log")
	workforce_candidate_day = int_value(payload, "workforce_candidate_day", 0)
	workforce_daily_wages = int_value(payload, "workforce_daily_wages", 0)
	workforce_daily_value = int_value(payload, "workforce_daily_value", 0)
	workforce_total_hires = int_value(payload, "workforce_total_hires", 0)
	workforce_total_actions = int_value(payload, "workforce_total_actions", 0)
	workforce_auto_assign = bool(payload.get("workforce_auto_assign", true))
	if workforce_candidates.is_empty():
		refresh_workforce_candidates(true)
	build_buttons()
	return true
