extends "res://scripts/autoplay_runtime_fix.gd"

const GrowWiseMachineSystem = preload("res://scripts/machinery_system.gd")

var machinery_data: Dictionary = {}
var machine_levels: Dictionary = {}
var machine_enabled: Dictionary = {}
var machine_durability: Dictionary = {}
var machine_energy: float = 0.0
var machine_timer: float = 0.0
var machine_cursor: int = 0
var machine_selected_index: int = 0
var machine_log: Array = []
var machine_total_actions: int = 0
var machine_daily_cost: int = 0
var machinery_master_enabled: bool = true

func _ready() -> void:
	machinery_data = load_json("res://data/machinery.json")
	super._ready()
	var test_result: Dictionary = GrowWiseMachineSystem.self_test(machinery_data)
	if bool(test_result.get("ok", false)):
		print("GROWWISE_MACHINERY_OK")
	else:
		push_error("Machinery self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var labels: Dictionary = {
		"ui.machinery":{"th":"เครื่องจักร","en":"Machinery"},
		"ui.machine_upgrade":{"th":"สร้าง / อัปเกรด","en":"Build / Upgrade"},
		"ui.machine_toggle":{"th":"เปิด / ปิด","en":"Enable / Disable"},
		"ui.machine_repair":{"th":"ซ่อมบำรุง","en":"Repair"},
		"ui.buy_bait":{"th":"ซื้อเหยื่อ 5 ชิ้น","en":"Buy 5 Bait"}
	}
	if labels.has(key_name):
		var value: Dictionary = labels[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	machine_levels = GrowWiseMachineSystem.default_levels(machinery_data)
	machine_enabled = GrowWiseMachineSystem.default_enabled(machinery_data)
	machine_durability = GrowWiseMachineSystem.default_durability(machinery_data)
	machine_levels["mini_tiller"] = 1
	machine_levels["bait_station"] = 1
	machine_energy = GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels)
	machine_timer = 0.0
	machine_cursor = 0
	machine_selected_index = 0
	machine_log = []
	machine_total_actions = 0
	machine_daily_cost = 0
	machinery_master_enabled = true
	log_machine("ติดตั้งรถพรวนและสถานีผลิตเหยื่อเริ่มต้น")
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	var kept: Array[Dictionary] = []
	for button_data: Dictionary in buttons:
		var button_id: String = String(button_data.get("id", ""))
		if button_id not in ["inventory_full", "crafting", "auto_rules_visible", "workforce", "machinery", "autoplay_primary"]:
			kept.append(button_data)
	buttons = kept
	buttons.append(button("inventory_full", Rect2(1010, 492, 58, 52), "market", "ui.inventory_full"))
	buttons.append(button("crafting", Rect2(1073, 492, 58, 52), "compost", "ui.crafting"))
	buttons.append(button("auto_rules_visible", Rect2(1136, 492, 58, 52), "settings", "ui.auto_settings_visible"))
	buttons.append(button("machinery", Rect2(1199, 492, 61, 52), "lab", "ui.machinery"))
	buttons.append(button("autoplay_primary", Rect2(1010, 550, 250, 62), "", "ui.autoplay_primary"))

func handle_button(button_id: String) -> void:
	if button_id == "machinery":
		overlay = "machinery"
		return
	super.handle_button(button_id)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game" and event.keycode == KEY_M:
		overlay = "machinery"
		return
	super._unhandled_input(event)

func _process(delta: float) -> void:
	super._process(delta)
	if mode != "game" or paused or not overlay.is_empty() or not machinery_master_enabled:
		return
	machine_timer += delta
	if machine_timer >= 3.0:
		machine_timer = 0.0
		run_machine_tick()

func advance_day() -> void:
	super.advance_day()
	machine_daily_cost = GrowWiseMachineSystem.daily_maintenance(machinery_data, machine_levels)
	var paid: int = mini(money, machine_daily_cost)
	money -= paid
	expenses += paid
	var full_service: bool = paid >= machine_daily_cost
	if not full_service and machine_daily_cost > 0:
		for machine_id: String in machine_durability:
			if int(machine_levels.get(machine_id, 0)) > 0:
				machine_durability[machine_id] = maxf(0.0, float(machine_durability.get(machine_id, 100.0)) - 6.0)
		log_machine("เงินบำรุงไม่พอ • เครื่องจักรสึกหรอเพิ่ม")
	var recharge: float = GrowWiseMachineSystem.daily_recharge(machinery_data)
	if not full_service:
		recharge *= 0.55
	machine_energy = minf(GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels), machine_energy + recharge)
	if machine_daily_cost > 0:
		log_machine("ค่าบำรุงประจำวัน %d • พลังงาน %.0f" % [paid, machine_energy])

func run_machine_tick() -> void:
	var definitions: Array = GrowWiseMachineSystem.machine_definitions(machinery_data)
	if definitions.is_empty():
		return
	for offset: int in range(definitions.size()):
		var index: int = posmod(machine_cursor + offset, definitions.size())
		var definition: Dictionary = definitions[index] as Dictionary
		var machine_id: String = String(definition.get("id", ""))
		var level_value: int = int(machine_levels.get(machine_id, 0))
		if level_value <= 0 or not bool(machine_enabled.get(machine_id, true)):
			continue
		if float(machine_durability.get(machine_id, 100.0)) <= 0.0:
			continue
		var energy_cost: int = GrowWiseMachineSystem.indexed_int(definition, "energy", level_value, 1)
		if machine_energy < float(energy_cost):
			continue
		var result: Dictionary = perform_machine_action(machine_id, level_value)
		if not bool(result.get("ok", false)):
			continue
		machine_energy = maxf(0.0, machine_energy - float(energy_cost))
		var wear: float = maxf(0.7, 2.8 - float(level_value) * 0.45)
		machine_durability[machine_id] = maxf(0.0, float(machine_durability.get(machine_id, 100.0)) - wear)
		machine_total_actions += 1
		machine_cursor = posmod(index + 1, definitions.size())
		var text_value: String = String(result.get("text", "เครื่องจักรกำลังทำงาน"))
		log_machine(text_value)
		add_feedback(text_value, Vector2(790, 150), TEAL)
		return
	machine_cursor = posmod(machine_cursor + 1, definitions.size())

func perform_machine_action(machine_id: String, level_value: int) -> Dictionary:
	match machine_id:
		"mini_tiller": return machine_till(level_value)
		"smart_sprinkler": return machine_water(level_value)
		"crop_drone": return machine_drone(level_value)
		"auto_harvester": return machine_harvest(level_value)
		"feed_dispenser": return machine_animals(level_value)
		"processing_line": return machine_process(level_value)
		"delivery_terminal": return machine_delivery(level_value)
		"bait_station": return machine_bait(level_value)
	return {"ok": false}

func machine_till(level_value: int) -> Dictionary:
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if bool(tile.get("farm", false)) and not bool(tile.get("tilled", false)) and string_value(tile, "crop").is_empty():
			var parts: PackedStringArray = key_string.split(",")
			if parts.size() != 2:
				continue
			var cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
			selected = cell
			selected_tool = "hoe"
			apply_tool(cell)
			return {"ok": true, "text": "รถพรวนเตรียมแปลง %s" % key_string}
	return {"ok": false}

func machine_water(level_value: int) -> Dictionary:
	var wet_key: String = ""
	var wet_value: float = 85.0
	var dry_key: String = ""
	var dry_value: float = 45.0 + float(level_value * 2)
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
		wet_tile["moisture"] = maxf(42.0, float_value(wet_tile, "moisture") - (12.0 + float(level_value * 4)))
		tiles[wet_key] = wet_tile
		if bool(water_state.get("pond_built", false)):
			water_state["pond_level"] = minf(500.0, float(water_state.get("pond_level", 0.0)) + 10.0)
		return {"ok": true, "text": "สปริงเกลอร์ระบายน้ำจากแปลง %s" % wet_key}
	if not dry_key.is_empty():
		var parts: PackedStringArray = dry_key.split(",")
		if parts.size() == 2:
			var cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
			selected = cell
			selected_tool = "water"
			apply_tool(cell)
			return {"ok": true, "text": "สปริงเกลอร์รดแปลง %s" % dry_key}
	return {"ok": false}

func machine_drone(level_value: int) -> Dictionary:
	var target_key: String = ""
	var target_risk: float = 18.0
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if string_value(tile, "crop").is_empty():
			continue
		var risk: float = float_value(tile, "pest") + float_value(tile, "disease") + float_value(tile, "weed") * 0.5
		if risk > target_risk:
			target_risk = risk
			target_key = key_string
	if target_key.is_empty():
		return {"ok": false}
	var target: Dictionary = dictionary_value(tiles, target_key)
	target["pest"] = maxf(0.0, float_value(target, "pest") - (5.0 + float(level_value * 3)))
	target["disease"] = maxf(0.0, float_value(target, "disease") - (4.0 + float(level_value * 2)))
	target["weed"] = maxf(0.0, float_value(target, "weed") - float(level_value * 2))
	tiles[target_key] = target
	knowledge += 1
	return {"ok": true, "text": "โดรนตรวจและช่วยแปลง %s" % target_key}

func machine_harvest(level_value: int) -> Dictionary:
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if not string_value(tile, "crop").is_empty() and int_value(tile, "stage") >= 5 and not bool(tile.get("dead", false)):
			var parts: PackedStringArray = key_string.split(",")
			if parts.size() != 2:
				continue
			var cell: Vector2i = Vector2i(int(parts[0]), int(parts[1]))
			selected = cell
			selected_tool = "harvest"
			apply_tool(cell)
			return {"ok": true, "text": "เครื่องเก็บเกี่ยวทำงานที่แปลง %s" % key_string}
	return {"ok": false}

func machine_animals(level_value: int) -> Dictionary:
	var pending: Dictionary = dictionary_value(animal_state, "pending_products")
	var pending_total: int = 0
	for item_id: String in pending:
		pending_total += int(pending[item_id])
	var manure_amount: int = int(animal_state.get("manure", 0))
	if pending_total + manure_amount > 0:
		collect_animal_products()
		return {"ok": true, "text": "เครื่องให้อาหารเก็บผลผลิตสัตว์"}
	var animals: Dictionary = dictionary_value(animal_state, "animals")
	var animal_count: int = 0
	for animal_id: String in animals:
		var group: Dictionary = dictionary_value(animals, animal_id)
		var count: int = int(group.get("count", 0))
		if count <= 0:
			continue
		animal_count += count
		group["health"] = minf(100.0, float(group.get("health", 100.0)) + float(1 + level_value))
		group["happiness"] = minf(100.0, float(group.get("happiness", 70.0)) + float(2 + level_value))
		animals[animal_id] = group
	animal_state["animals"] = animals
	if animal_count > 0:
		return {"ok": true, "text": "เครื่องให้อาหารดูแลสัตว์ %d ตัว" % animal_count}
	return {"ok": false}

func machine_process(level_value: int) -> Dictionary:
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
			return {"ok": true, "text": "สายการผลิตแปรรูป %s" % string_value(recipe, "name")}
	return {"ok": false}

func machine_delivery(level_value: int) -> Dictionary:
	if not dictionary_value(logistics_state, "active_trip").is_empty():
		return {"ok": false}
	start_city_delivery()
	if not dictionary_value(logistics_state, "active_trip").is_empty():
		return {"ok": true, "text": "ศูนย์สั่งรถส่งสินค้าเข้าเมือง"}
	return {"ok": false}

func machine_bait(level_value: int) -> Dictionary:
	var bait_amount: int = int(inventory.get("bait", 0))
	var target_amount: int = 10 + level_value * 6
	if bait_amount >= target_amount:
		return {"ok": false}
	if int(inventory.get("herb", 0)) <= 0 or int(inventory.get("fiber", 0)) <= 0:
		return {"ok": false}
	inventory["herb"] = int(inventory.get("herb", 0)) - 1
	inventory["fiber"] = int(inventory.get("fiber", 0)) - 1
	var produced: int = 2 + level_value
	inventory["bait"] = bait_amount + produced
	return {"ok": true, "text": "สถานีผลิตเหยื่อ +%d" % produced}

func selected_machine_definition() -> Dictionary:
	var definitions: Array = GrowWiseMachineSystem.machine_definitions(machinery_data)
	if definitions.is_empty():
		return {}
	machine_selected_index = clampi(machine_selected_index, 0, definitions.size() - 1)
	return definitions[machine_selected_index] as Dictionary

func build_or_upgrade_machine() -> void:
	var definition: Dictionary = selected_machine_definition()
	if definition.is_empty():
		return
	var machine_id: String = String(definition.get("id", ""))
	var current_level: int = int(machine_levels.get(machine_id, 0))
	var max_level: int = GrowWiseMachineSystem.max_level(definition)
	if current_level >= max_level:
		notify("เครื่องจักรเต็มระดับแล้ว", "success")
		return
	var required_level: int = GrowWiseMachineSystem.unlock_level(definition)
	if farm_level < required_level:
		notify("ปลดล็อกที่ระดับสวน %d" % required_level, "error")
		return
	var cost: Dictionary = GrowWiseMachineSystem.next_cost(definition, current_level)
	if not GrowWiseMachineSystem.can_pay(cost, inventory, money):
		notify("วัตถุดิบหรือเงินไม่พอ", "error")
		return
	for item_id: String in cost:
		var amount: int = int(cost[item_id])
		if item_id == "money":
			money -= amount
			expenses += amount
		else:
			inventory[item_id] = int(inventory.get(item_id, 0)) - amount
	machine_levels[machine_id] = current_level + 1
	machine_durability[machine_id] = 100.0
	machine_enabled[machine_id] = true
	machine_energy = minf(GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels), machine_energy + 15.0)
	if machine_id == "bait_station" and current_level == 0:
		inventory["bait"] = int(inventory.get("bait", 0)) + 5
	add_farm_xp(20 + current_level * 10, "พัฒนาเครื่องจักร", Vector2(780, 150))
	log_machine("%s ระดับ %d" % [machine_name(definition), current_level + 1])
	notify("ติดตั้งสำเร็จ: %s" % machine_name(definition), "success")

func toggle_selected_machine() -> void:
	var definition: Dictionary = selected_machine_definition()
	if definition.is_empty():
		return
	var machine_id: String = String(definition.get("id", ""))
	if int(machine_levels.get(machine_id, 0)) <= 0:
		notify("ต้องสร้างเครื่องจักรก่อน", "error")
		return
	machine_enabled[machine_id] = not bool(machine_enabled.get(machine_id, true))
	log_machine("%s: %s" % [machine_name(definition), "เปิด" if bool(machine_enabled[machine_id]) else "ปิด"])

func repair_selected_machine() -> void:
	var definition: Dictionary = selected_machine_definition()
	if definition.is_empty():
		return
	var machine_id: String = String(definition.get("id", ""))
	if int(machine_levels.get(machine_id, 0)) <= 0:
		return
	var durability: float = float(machine_durability.get(machine_id, 100.0))
	if durability >= 99.5:
		notify("เครื่องจักรยังสมบูรณ์", "success")
		return
	var cost: int = maxi(5, ceili((100.0 - durability) * 0.45))
	if money < cost:
		notify("เงินค่าซ่อมไม่พอ • ต้องใช้ %d" % cost, "error")
		return
	money -= cost
	expenses += cost
	machine_durability[machine_id] = 100.0
	log_machine("ซ่อม %s ราคา %d" % [machine_name(definition), cost])

func buy_bait_pack() -> void:
	var cost: int = 20
	if money < cost:
		notify("เงินไม่พอซื้อเหยื่อ", "error")
		return
	money -= cost
	expenses += cost
	inventory["bait"] = int(inventory.get("bait", 0)) + 5
	log_machine("ซื้อเหยื่อตกปลา 5 ชิ้น")
	notify("ได้เหยื่อตกปลา 5 ชิ้น", "success")

func machine_name(definition: Dictionary) -> String:
	return String(definition.get("name_th" if language == "th" else "name_en", definition.get("id", "")))

func machine_cost_text(cost: Dictionary) -> String:
	var parts: PackedStringArray = []
	for item_id: String in cost:
		var have: int = money if item_id == "money" else int(inventory.get(item_id, 0))
		parts.append("%s %d/%d" % [item_id, have, int(cost[item_id])])
	return " • ".join(parts)

func log_machine(text_value: String) -> void:
	machine_log.push_front("วัน %d %02d:%02d • %s" % [day, int(minutes / 60.0), int(minutes) % 60, text_value])
	if machine_log.size() > 14:
		machine_log.resize(14)

func overlay_click(position: Vector2) -> void:
	if overlay != "machinery":
		super.overlay_click(position)
		return
	if Rect2(1012, 76, 42, 36).has_point(position):
		overlay = ""
		return
	var definitions: Array = GrowWiseMachineSystem.machine_definitions(machinery_data)
	for index: int in range(definitions.size()):
		if Rect2(245, 145 + index * 43, 785, 38).has_point(position):
			machine_selected_index = index
			return
	if Rect2(245, 510, 190, 46).has_point(position):
		build_or_upgrade_machine()
	elif Rect2(445, 510, 150, 46).has_point(position):
		toggle_selected_machine()
	elif Rect2(605, 510, 150, 46).has_point(position):
		repair_selected_machine()
	elif Rect2(765, 510, 265, 46).has_point(position):
		buy_bait_pack()
	elif Rect2(245, 565, 785, 42).has_point(position):
		machinery_master_enabled = not machinery_master_enabled

func draw_overlay() -> void:
	if overlay == "machinery":
		draw_machinery_overlay()
		return
	super.draw_overlay()

func draw_machinery_overlay() -> void:
	draw_expansion_shell(tx("ui.machinery"))
	var capacity: float = GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels)
	draw_text("พลังงาน %.0f/%.0f • ค่าบำรุง/วัน %d • ทำงานรวม %d • เหยื่อ %d" % [machine_energy, capacity, GrowWiseMachineSystem.daily_maintenance(machinery_data, machine_levels), machine_total_actions, int(inventory.get("bait", 0))], Vector2(245, 128), 15, GOLD, 785.0)
	var definitions: Array = GrowWiseMachineSystem.machine_definitions(machinery_data)
	for index: int in range(definitions.size()):
		var definition: Dictionary = definitions[index] as Dictionary
		var machine_id: String = String(definition.get("id", ""))
		var level_value: int = int(machine_levels.get(machine_id, 0))
		var durability: int = int(round(float(machine_durability.get(machine_id, 100.0))))
		var unlocked: bool = farm_level >= GrowWiseMachineSystem.unlock_level(definition)
		var rect_value: Rect2 = Rect2(245, 145 + index * 43, 785, 38)
		panel(rect_value, GOLD if machine_selected_index == index else Color("dce8d5"))
		var status_text: String = "ยังไม่ปลดล็อก" if not unlocked else ("ยังไม่สร้าง" if level_value <= 0 else ("เปิด" if bool(machine_enabled.get(machine_id, true)) else "ปิด"))
		draw_text("%s • Lv.%d/%d • %s" % [machine_name(definition), level_value, GrowWiseMachineSystem.max_level(definition), status_text], rect_value.position + Vector2(8, 18), 13, GREEN, 490.0)
		var energy_cost: int = GrowWiseMachineSystem.indexed_int(definition, "energy", maxi(1, level_value), 0)
		draw_text("สภาพ %d%% • ใช้พลังงาน %d" % [durability, energy_cost], rect_value.position + Vector2(520, 18), 12, INK, 250.0, HORIZONTAL_ALIGNMENT_RIGHT)
		if machine_selected_index == index:
			var description: String = String(definition.get("description", ""))
			draw_text(description, rect_value.position + Vector2(8, 35), 10, INK, 500.0)
	panel(Rect2(245, 510, 190, 46), TEAL)
	draw_text(tx("ui.machine_upgrade"), Vector2(252, 541), 14, Color.WHITE, 176.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(445, 510, 150, 46), BLUE)
	draw_text(tx("ui.machine_toggle"), Vector2(452, 541), 14, Color.WHITE, 136.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(605, 510, 150, 46), WOOD_LIGHT)
	draw_text(tx("ui.machine_repair"), Vector2(612, 541), 14, Color.WHITE, 136.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(765, 510, 265, 46), GOLD)
	draw_text(tx("ui.buy_bait") + " • 20", Vector2(772, 541), 14, INK, 251.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(245, 565, 785, 42), TEAL if machinery_master_enabled else MIST)
	draw_text("ระบบเครื่องจักรอัตโนมัติ: %s" % ("ON" if machinery_master_enabled else "OFF"), Vector2(255, 593), 14, Color.WHITE if machinery_master_enabled else INK, 765.0, HORIZONTAL_ALIGNMENT_CENTER)
	var definition: Dictionary = selected_machine_definition()
	if not definition.is_empty():
		var machine_id: String = String(definition.get("id", ""))
		var current_level: int = int(machine_levels.get(machine_id, 0))
		if current_level < GrowWiseMachineSystem.max_level(definition):
			var cost: Dictionary = GrowWiseMachineSystem.next_cost(definition, current_level)
			draw_text("ค่าอัปเกรด: " + machine_cost_text(cost), Vector2(245, 628), 11, INK, 785.0)
		elif not machine_log.is_empty():
			draw_text(String(machine_log[0]), Vector2(245, 628), 11, INK, 785.0)

func save_game(slot_number: int, automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number, automatic)
	if not result:
		return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty():
		return result
	payload["machine_levels"] = machine_levels
	payload["machine_enabled"] = machine_enabled
	payload["machine_durability"] = machine_durability
	payload["machine_energy"] = machine_energy
	payload["machine_log"] = machine_log
	payload["machine_total_actions"] = machine_total_actions
	payload["machinery_master_enabled"] = machinery_master_enabled
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
	var has_machinery: bool = payload.has("machine_levels")
	machine_levels = dictionary_value(payload, "machine_levels", GrowWiseMachineSystem.default_levels(machinery_data))
	machine_enabled = dictionary_value(payload, "machine_enabled", GrowWiseMachineSystem.default_enabled(machinery_data))
	machine_durability = dictionary_value(payload, "machine_durability", GrowWiseMachineSystem.default_durability(machinery_data))
	machine_energy = float(payload.get("machine_energy", GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels)))
	machine_log = array_value(payload, "machine_log")
	machine_total_actions = int_value(payload, "machine_total_actions", 0)
	machinery_master_enabled = bool(payload.get("machinery_master_enabled", true))
	if not has_machinery:
		machine_levels["mini_tiller"] = 1
		machine_levels["bait_station"] = 1
		machine_energy = GrowWiseMachineSystem.energy_capacity(machinery_data, machine_levels)
		var old_workers: Array = array_value(payload, "hired_workers")
		if not old_workers.is_empty():
			var compensation: int = old_workers.size() * 75
			money += compensation
			log_machine("ยกเลิกระบบพนักงาน • คืนเงินลงทุน %d" % compensation)
	build_buttons()
	return true
