extends "res://scripts/main.gd"

const GrowWiseAutoPlay = preload("res://scripts/autoplay_manager.gd")

var crafting_data: Dictionary = {}
var autoplay_mode: int = GrowWiseAutoPlay.MODE_OFF
var autoplay_timer: float = 0.0
var autoplay_last_action: Dictionary = {}
var autoplay_actions_total: int = 0
var forage_left: int = 4
var forage_day: int = 1
var upgrade_day: int = 1
var selected_recipe: int = 0
var care_streak: int = 0
var achievements: Dictionary = {}
var extension_loaded: bool = false

func _ready() -> void:
	crafting_data = load_json("res://data/crafting.json")
	super._ready()
	var auto_test: Dictionary = GrowWiseAutoPlay.self_test()
	if bool(auto_test.get("ok", false)):
		print("GROWWISE_AUTOPLAY_OK")
	else:
		push_error("Autoplay self-test failed: %s" % JSON.stringify(auto_test))
	print("GROWWISE_CRAFTING_ETA_INVENTORY_OK")

func tx(key_name: String) -> String:
	var custom: Dictionary = {
		"ui.inventory_full":{"th":"คลัง","en":"Inventory"},
		"ui.crafting":{"th":"คราฟต์","en":"Craft"},
		"ui.autoplay":{"th":"ออโต้","en":"Auto"},
		"ui.gather":{"th":"เก็บวัตถุดิบ","en":"Gather"},
		"ui.remaining":{"th":"คงเหลือ","en":"Remaining"},
		"ui.have_need":{"th":"มี / ต้องใช้","en":"Have / Need"},
		"ui.next_stage":{"th":"ถึงระยะถัดไป","en":"Next stage"},
		"ui.harvest_eta":{"th":"ถึงเก็บเกี่ยว","en":"Harvest ETA"},
		"ui.tomorrow":{"th":"พรุ่งนี้","en":"Tomorrow"},
		"ui.care_streak":{"th":"ดูแลต่อเนื่อง","en":"Care streak"},
		"ui.materials":{"th":"วัตถุดิบ","en":"Materials"},
		"ui.supplies":{"th":"ของใช้","en":"Supplies"},
		"ui.upgrades":{"th":"อัปเกรด","en":"Upgrades"},
		"ui.seeds":{"th":"เมล็ด","en":"Seeds"},
		"ui.produce":{"th":"ผลผลิต","en":"Produce"},
		"msg.no_material":{"th":"วัตถุดิบไม่พอ","en":"Not enough materials"},
		"msg.crafted":{"th":"คราฟต์สำเร็จ","en":"Crafted"},
		"msg.gather_empty":{"th":"วันนี้เก็บวัตถุดิบครบแล้ว","en":"No gathering attempts left today"},
		"msg.auto_idle":{"th":"ออโต้กำลังรอเวลาและสภาพแปลง","en":"Auto is waiting for the farm"},
		"msg.seed_refund":{"th":"ถาดเพาะช่วยคืนเมล็ด 1 เมล็ด","en":"Seed tray refunded one seed"}
	}
	if custom.has(key_name):
		var value: Dictionary = custom[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	var additions: Dictionary = {
		"wood":4,"stone":3,"fiber":4,"herb":3,"scrap":2,"glass":1,"rubber":1,
		"water_bottle":3,"dry_leaves":4,"mineral":2,
		"upgrade_scarecrow":0,"upgrade_drip_kit":0,"upgrade_seed_tray":0,
		"upgrade_weather_station":0,"upgrade_compost_bin":0
	}
	for item_id: String in additions:
		inventory[item_id] = additions[item_id]
	autoplay_mode = GrowWiseAutoPlay.MODE_OFF
	autoplay_timer = 0.0
	autoplay_last_action = {}
	autoplay_actions_total = 0
	forage_left = 4
	forage_day = day
	upgrade_day = day
	care_streak = 0
	achievements = {}
	extension_loaded = true
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	buttons.append(button("inventory_full", Rect2(1010, 500, 76, 54), "market", "ui.inventory_full"))
	buttons.append(button("crafting", Rect2(1092, 500, 76, 54), "compost", "ui.crafting"))
	buttons.append(button("autoplay", Rect2(1174, 500, 86, 54), "settings", "ui.autoplay"))

func _process(delta: float) -> void:
	super._process(delta)
	if mode != "game":
		return
	if day != forage_day:
		forage_day = day
		forage_left = 4 + (1 if int(inventory.get("upgrade_compost_bin", 0)) > 0 else 0)
	if day != upgrade_day:
		upgrade_day = day
		apply_daily_upgrades()
	if autoplay_mode != GrowWiseAutoPlay.MODE_OFF and overlay.is_empty() and not paused:
		autoplay_timer += delta
		if autoplay_timer >= GrowWiseAutoPlay.action_delay(autoplay_mode):
			autoplay_timer = 0.0
			run_autoplay_step()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game":
		if event.keycode == KEY_I:
			overlay = "inventory_full"
			return
		if event.keycode == KEY_C:
			overlay = "crafting"
			return
		if event.keycode == KEY_F4:
			cycle_autoplay()
			return
	super._unhandled_input(event)

func handle_button(button_id: String) -> void:
	match button_id:
		"inventory_full": overlay = "inventory_full"
		"crafting": overlay = "crafting"
		"autoplay": cycle_autoplay()
		_: super.handle_button(button_id)

func cycle_autoplay() -> void:
	autoplay_mode = (autoplay_mode + 1) % 4
	autoplay_timer = 0.0
	notify("%s: %s" % [tx("ui.autoplay"), GrowWiseAutoPlay.mode_name(autoplay_mode, language)], "success")

func apply_tool(cell: Vector2i) -> void:
	var before_seed: int = int(inventory.get("seed_" + selected_seed, 0))
	var tool_before: String = selected_tool
	super.apply_tool(cell)
	if tool_before == "seed" and int(inventory.get("upgrade_seed_tray", 0)) > 0:
		var after_seed: int = int(inventory.get("seed_" + selected_seed, 0))
		if after_seed < before_seed and posmod(day + cell.x * 3 + cell.y * 5, 4) == 0:
			inventory["seed_" + selected_seed] = after_seed + 1
			notify(tx("msg.seed_refund"), "success")

func harvest_tile(tile: Dictionary, key_string: String) -> void:
	var crop_id: String = string_value(tile, "crop")
	var before_amount: int = int(inventory.get("produce_" + crop_id, 0))
	var quality_before: int = GrowWiseSimulation.harvest_quality(tile)
	super.harvest_tile(tile, key_string)
	var gained: int = int(inventory.get("produce_" + crop_id, 0)) - before_amount
	if gained > 0 and quality_before >= 88:
		inventory["seed_" + crop_id] = int(inventory.get("seed_" + crop_id, 0)) + 1
		achievements["premium_harvest"] = true
		notify("ผลผลิตคุณภาพเยี่ยม: ได้เมล็ดโบนัส 1", "success")

func apply_daily_upgrades() -> void:
	var healthy: bool = true
	var crop_count: int = 0
	var driest_key: String = ""
	var driest_value: float = 999.0
	for key_value: Variant in tiles.keys():
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		if int(inventory.get("upgrade_scarecrow", 0)) > 0:
			tile["pest"] = maxf(0.0, float_value(tile, "pest") - 7.0)
		var crop_id: String = string_value(tile, "crop")
		if not crop_id.is_empty():
			crop_count += 1
			if float_value(tile, "health") < 68.0:
				healthy = false
			if float_value(tile, "moisture") < driest_value:
				driest_value = float_value(tile, "moisture")
				driest_key = key_string
		tiles[key_string] = tile
	if int(inventory.get("upgrade_drip_kit", 0)) > 0 and not driest_key.is_empty() and driest_value < 48.0:
		var driest: Dictionary = dictionary_value(tiles, driest_key)
		driest["moisture"] = minf(100.0, float_value(driest, "moisture") + 24.0)
		driest["water_total"] = float_value(driest, "water_total") + 12.0
		tiles[driest_key] = driest
		water_used += 12.0
	if int(inventory.get("upgrade_compost_bin", 0)) > 0 and organic_waste > 0:
		compost_progress += 18.0
		if compost_progress >= 100.0:
			compost_progress -= 100.0
			organic_waste = maxi(0, organic_waste - 2)
			inventory["compost"] = int(inventory.get("compost", 0)) + 1
	if crop_count > 0 and healthy:
		care_streak += 1
	else:
		care_streak = 0
	if care_streak >= 3:
		achievements["care_3"] = true
	if care_streak >= 7:
		achievements["care_7"] = true
		money += 100

func run_autoplay_step() -> void:
	var action: Dictionary = GrowWiseAutoPlay.choose_action(tiles, inventory, CROP_IDS, autoplay_mode, day, money)
	if action.is_empty():
		if autoplay_mode in [GrowWiseAutoPlay.MODE_FULL, GrowWiseAutoPlay.MODE_LEARNING]:
			if try_auto_craft():
				return
			if forage_left > 0:
				gather_resources(true)
				return
		autoplay_last_action = {"action":"idle"}
		return
	var action_id: String = String(action.get("action", ""))
	if action_id == "sell":
		sell_crop(String(action.get("crop", "")))
	elif action_id == "restock":
		auto_restock(String(action.get("crop", "")))
	else:
		var cell: Vector2i = action.get("cell", Vector2i(-1, -1)) as Vector2i
		if valid_cell(cell):
			selected = cell
			player_position = Vector2(float(cell.x) + 0.25, float(cell.y) + 0.45)
			selected_tool = action_id
			if action_id == "seed":
				selected_seed = String(action.get("seed", selected_seed))
			apply_tool(cell)
	autoplay_last_action = action
	autoplay_actions_total += 1
	if autoplay_mode == GrowWiseAutoPlay.MODE_LEARNING:
		notify(auto_action_text(action_id), "success")

func auto_action_text(action_id: String) -> String:
	var names: Dictionary = {
		"hoe":"ออโต้พรวนดินเพื่อเตรียมแปลง","seed":"ออโต้เลือกพืชตามเมล็ดคงเหลือ","water":"ออโต้รดเมื่อดินเริ่มแห้ง",
		"harvest":"ออโต้เก็บเมื่อโตเต็มระยะ","weed":"ออโต้ถอนวัชพืช","bio":"ออโต้ใช้ชีวภัณฑ์เมื่อความเสี่ยงสูง",
		"fertilize":"ออโต้ฟื้นฟูธาตุอาหาร","compost":"ออโต้ปรับปรุงดิน","remove":"ออโต้ถอนต้นที่ตาย","sell":"ออโต้ขายผลผลิตสะสม"
	}
	return String(names.get(action_id, "ออโต้กำลังดูแลสวน"))

func auto_restock(crop_id: String) -> void:
	var shop_items: Array = array_value(data, "shop")
	for item_value: Variant in shop_items:
		var item: Dictionary = item_value as Dictionary
		if string_value(item, "id") == "seed_" + crop_id:
			var price: int = int_value(item, "price")
			if money >= price:
				money -= price
				expenses += price
				inventory["seed_" + crop_id] = int(inventory.get("seed_" + crop_id, 0)) + int_value(item, "amount", 1)
				notify("ออโต้ซื้อเมล็ด " + crop_name(crop_id), "shop")
			return

func try_auto_craft() -> bool:
	var wanted: Array[String] = []
	if int(inventory.get("bio_spray", 0)) <= 0:
		wanted.append("bio_spray")
	if int(inventory.get("organic_fertilizer", 0)) <= 0:
		wanted.append("organic_fertilizer")
	if int(inventory.get("upgrade_drip_kit", 0)) <= 0:
		wanted.append("drip_kit")
	var recipes: Array = array_value(crafting_data, "recipes")
	for index: int in range(recipes.size()):
		var recipe: Dictionary = recipes[index] as Dictionary
		if string_value(recipe, "id") in wanted and can_craft(recipe):
			craft_recipe(index, true)
			return true
	return false

func item_label(item_id: String) -> String:
	if item_id.begins_with("seed_"):
		return ("เมล็ด " if language == "th" else "Seed ") + crop_name(item_id.trim_prefix("seed_"))
	if item_id.begins_with("produce_"):
		return crop_name(item_id.trim_prefix("produce_"))
	var materials: Dictionary = dictionary_value(crafting_data, "materials")
	if materials.has(item_id):
		var definition: Dictionary = dictionary_value(materials, item_id)
		return String(definition.get(language, definition.get("th", item_id)))
	var labels: Dictionary = {
		"compost":{"th":"ปุ๋ยหมัก","en":"Compost"},"organic_fertilizer":{"th":"ปุ๋ยอินทรีย์","en":"Organic Fertilizer"},
		"bio_spray":{"th":"สเปรย์ชีวภาพ","en":"Bio Spray"},"moisture_meter":{"th":"เครื่องวัดความชื้น","en":"Moisture Meter"},
		"ph_meter":{"th":"เครื่องวัด pH/NPK","en":"pH/NPK Meter"},"upgrade_scarecrow":{"th":"หุ่นไล่นก","en":"Scarecrow"},
		"upgrade_drip_kit":{"th":"ชุดน้ำหยด","en":"Drip Kit"},"upgrade_seed_tray":{"th":"ถาดเพาะเมล็ด","en":"Seed Tray"},
		"upgrade_weather_station":{"th":"สถานีอากาศ","en":"Weather Station"},"upgrade_compost_bin":{"th":"ถังปุ๋ยหมัก","en":"Compost Bin"}
	}
	if labels.has(item_id):
		var value: Dictionary = labels[item_id] as Dictionary
		return String(value.get(language, value.get("th", item_id)))
	return item_id

func gather_resources(automatic: bool = false) -> void:
	if forage_left <= 0:
		if not automatic:
			notify(tx("msg.gather_empty"), "error")
		return
	var pools: Array[Array] = [
		["wood","fiber"],["stone","mineral"],["herb","dry_leaves"],["scrap","glass"],["rubber","water_bottle"]
	]
	var pool: Array = pools[posmod(day + forage_left + autoplay_actions_total, pools.size())]
	var first: String = String(pool[0])
	var second: String = String(pool[1])
	var first_amount: int = 2 + posmod(day + forage_left, 2)
	var second_amount: int = 1 + posmod(day, 2)
	inventory[first] = int(inventory.get(first, 0)) + first_amount
	inventory[second] = int(inventory.get(second, 0)) + second_amount
	forage_left -= 1
	notify("เก็บได้ %s %d, %s %d" % [item_label(first), first_amount, item_label(second), second_amount], "success")

func can_craft(recipe: Dictionary) -> bool:
	var ingredients: Dictionary = dictionary_value(recipe, "ingredients")
	for item_id: String in ingredients:
		if int(inventory.get(item_id, 0)) < int(ingredients[item_id]):
			return false
	if int_value(recipe, "organic_waste", 0) > organic_waste:
		return false
	var output: Dictionary = dictionary_value(recipe, "output")
	for output_id: String in output:
		if output_id.begins_with("upgrade_") and int(inventory.get(output_id, 0)) > 0:
			return false
	return true

func craft_recipe(index: int, automatic: bool = false) -> void:
	var recipes: Array = array_value(crafting_data, "recipes")
	if index < 0 or index >= recipes.size():
		return
	var recipe: Dictionary = recipes[index] as Dictionary
	if not can_craft(recipe):
		if not automatic:
			notify(tx("msg.no_material"), "error")
		return
	var ingredients: Dictionary = dictionary_value(recipe, "ingredients")
	for item_id: String in ingredients:
		inventory[item_id] = int(inventory.get(item_id, 0)) - int(ingredients[item_id])
	organic_waste -= int_value(recipe, "organic_waste", 0)
	var output: Dictionary = dictionary_value(recipe, "output")
	for output_id: String in output:
		inventory[output_id] = int(inventory.get(output_id, 0)) + int(output[output_id])
	achievements["first_craft"] = true
	var name_value: String = String(recipe.get(language, recipe.get("th", recipe.get("id", ""))))
	notify("%s: %s" % [tx("msg.crafted"), name_value], "success")

func growth_factor_estimate(tile: Dictionary, crop_def: Dictionary) -> float:
	var weather_data: Dictionary = dictionary_value(data, "weather")
	var weather_def: Dictionary = dictionary_value(weather_data, current_weather)
	var ideal_water: float = float_value(crop_def, "ideal_water", 65.0)
	var tolerance: float = maxf(1.0, float_value(crop_def, "water_tolerance", 18.0))
	var water_score: float = clampf(1.0 - absf(float_value(tile, "moisture") - ideal_water) / (tolerance * 2.0), 0.0, 1.0)
	var fertility_score: float = clampf(float_value(tile, "fertility") / 75.0, 0.0, 1.0)
	var light_score: float = clampf(float_value(weather_def, "light", 80.0) / maxf(1.0, float_value(crop_def, "ideal_light", 75.0)), 0.0, 1.0)
	var temperature_score: float = clampf(1.0 - absf(float_value(weather_def, "temperature", 28.0) - float_value(crop_def, "ideal_temperature", 28.0)) / 18.0, 0.0, 1.0)
	var spacing_score: float = clampf(1.0 - float_value(tile, "spacing_penalty") / 100.0, 0.25, 1.0)
	var factor: float = (water_score + fertility_score + light_score + temperature_score + spacing_score) / 5.0
	var season_value: Variant = crop_def.get("season_bonus", [])
	if season_value is Array and current_season in (season_value as Array):
		factor *= 1.12
	factor *= clampf(1.0 - (float_value(tile, "pest") + float_value(tile, "disease")) / 260.0, 0.2, 1.0)
	return maxf(0.08, factor)

func growth_eta(tile: Dictionary, target_stage: int) -> float:
	var crop_id: String = string_value(tile, "crop")
	if crop_id.is_empty() or bool(tile.get("dead", false)):
		return -1.0
	var crop_def: Dictionary = dictionary_value(dictionary_value(data, "crops"), crop_id)
	var total_days: float = float_value(crop_def, "growth_days", 6.0)
	var target_growth: float = total_days * float(clampi(target_stage, 1, 5)) / 5.0
	var remaining: float = maxf(0.0, target_growth - float_value(tile, "growth"))
	return remaining / growth_factor_estimate(tile, crop_def)

func format_eta(days_value: float) -> String:
	if days_value < 0.0:
		return "–"
	if days_value < 0.08:
		return "ใกล้แล้ว" if language == "th" else "Soon"
	if days_value < 1.0:
		var hours: int = maxi(1, int(round(days_value * 24.0)))
		return ("ประมาณ %d ชม." % hours) if language == "th" else ("about %d h" % hours)
	return ("ประมาณ %.1f วัน" % days_value) if language == "th" else ("about %.1f days" % days_value)

func stage_name(stage: int) -> String:
	var thai: Array[String] = ["เมล็ด","เริ่มงอก","ต้นอ่อน","กำลังโต","ใกล้เต็มวัย","พร้อมเก็บ"]
	var english: Array[String] = ["Seed","Sprout","Seedling","Growing","Maturing","Ready"]
	return (thai if language == "th" else english)[clampi(stage, 0, 5)]

func save_game(slot_number: int, automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number, automatic)
	if not result:
		return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty():
		return result
	payload["autoplay_mode"] = autoplay_mode
	payload["autoplay_actions_total"] = autoplay_actions_total
	payload["forage_left"] = forage_left
	payload["forage_day"] = forage_day
	payload["care_streak"] = care_streak
	payload["achievements"] = achievements
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(payload))
		file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result:
		return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	autoplay_mode = int_value(payload, "autoplay_mode", GrowWiseAutoPlay.MODE_OFF)
	autoplay_actions_total = int_value(payload, "autoplay_actions_total", 0)
	forage_left = int_value(payload, "forage_left", 4)
	forage_day = int_value(payload, "forage_day", day)
	care_streak = int_value(payload, "care_streak", 0)
	achievements = dictionary_value(payload, "achievements")
	upgrade_day = day
	return true

func overlay_click(position: Vector2) -> void:
	if overlay in ["inventory_full", "crafting"]:
		if Rect2(1012, 76, 42, 36).has_point(position):
			overlay = ""
			return
		if overlay == "inventory_full":
			if Rect2(760, 545, 240, 46).has_point(position):
				gather_resources()
			elif Rect2(500, 545, 240, 46).has_point(position):
				overlay = "crafting"
			return
		var recipes: Array = array_value(crafting_data, "recipes")
		for index: int in range(recipes.size()):
			var column: int = index % 2
			var row: int = int(index / 2)
			var rect_value: Rect2 = Rect2(255 + column * 390, 150 + row * 94, 370, 82)
			if rect_value.has_point(position):
				selected_recipe = index
				craft_recipe(index)
				return
		if Rect2(760, 545, 240, 46).has_point(position):
			gather_resources()
		return
	super.overlay_click(position)

func draw_overlay() -> void:
	if overlay == "inventory_full":
		draw_inventory_overlay()
	elif overlay == "crafting":
		draw_crafting_overlay()
	else:
		super.draw_overlay()

func draw_overlay_shell(title: String) -> void:
	draw_rect(Rect2(0, 0, 1280, 720), Color(0.03, 0.05, 0.04, 0.72))
	panel(Rect2(210, 58, 860, 600), CREAM)
	panel(Rect2(1012, 76, 42, 36), RED)
	draw_text("×", Vector2(1022, 104), 23, Color.WHITE, 22.0, HORIZONTAL_ALIGNMENT_CENTER)
	draw_text(title, Vector2(245, 105), 28, GREEN)

func draw_inventory_overlay() -> void:
	draw_overlay_shell(tx("ui.inventory_full"))
	draw_text("%s: %d   |   %s: %d/วัน" % [tx("ui.money"), money, tx("ui.gather"), forage_left], Vector2(650, 104), 17, GOLD)
	draw_inventory_category(tx("ui.seeds"), CROP_IDS.map(func(crop_id: String) -> String: return "seed_" + crop_id), Vector2(250, 145), 0)
	draw_inventory_category(tx("ui.produce"), CROP_IDS.map(func(crop_id: String) -> String: return "produce_" + crop_id), Vector2(480, 145), 0)
	draw_inventory_category(tx("ui.supplies"), ["compost","organic_fertilizer","bio_spray","moisture_meter","ph_meter"], Vector2(710, 145), 0)
	draw_inventory_category(tx("ui.materials"), ["wood","stone","fiber","herb","scrap","glass","rubber","water_bottle","dry_leaves","mineral"], Vector2(250, 350), 1)
	draw_inventory_category(tx("ui.upgrades"), ["upgrade_scarecrow","upgrade_drip_kit","upgrade_seed_tray","upgrade_weather_station","upgrade_compost_bin"], Vector2(710, 350), 0)
	panel(Rect2(500, 545, 240, 46), WOOD_LIGHT)
	draw_text(tx("ui.crafting"), Vector2(510, 577), 18, Color.WHITE, 220.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(760, 545, 240, 46), TEAL if forage_left > 0 else MIST)
	draw_text("%s (%d)" % [tx("ui.gather"), forage_left], Vector2(770, 577), 18, Color.WHITE if forage_left > 0 else INK, 220.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_inventory_category(title: String, item_ids: Array, origin: Vector2, columns: int) -> void:
	draw_text(title, origin, 18, GREEN)
	for index: int in range(item_ids.size()):
		var item_id: String = String(item_ids[index])
		var column: int = (index % 2) if columns == 1 else 0
		var row: int = int(index / 2) if columns == 1 else index
		var x: float = origin.x + column * 205.0
		var y: float = origin.y + 31.0 + row * 28.0
		draw_text(item_label(item_id), Vector2(x, y), 14, INK, 150.0)
		draw_text(str(int(inventory.get(item_id, 0))), Vector2(x + 155.0, y), 15, GOLD, 38.0, HORIZONTAL_ALIGNMENT_RIGHT)

func draw_crafting_overlay() -> void:
	draw_overlay_shell(tx("ui.crafting"))
	draw_text("%s: %d   |   %s: %d" % [tx("ui.gather"), forage_left, "เศษอินทรีย์", organic_waste], Vector2(680, 104), 16, GOLD)
	var recipes: Array = array_value(crafting_data, "recipes")
	for index: int in range(recipes.size()):
		var recipe: Dictionary = recipes[index] as Dictionary
		var column: int = index % 2
		var row: int = int(index / 2)
		var rect_value: Rect2 = Rect2(255 + column * 390, 150 + row * 94, 370, 82)
		var available: bool = can_craft(recipe)
		panel(rect_value, Color("dce8d5") if available else MIST)
		var name_value: String = String(recipe.get(language, recipe.get("th", recipe.get("id", ""))))
		var description_key: String = "description_" + language
		draw_text(name_value, rect_value.position + Vector2(10, 23), 16, GREEN if available else INK)
		draw_text(String(recipe.get(description_key, "")), rect_value.position + Vector2(10, 44), 11, INK, 345.0)
		var ingredient_text: String = recipe_ingredient_text(recipe)
		draw_text(ingredient_text, rect_value.position + Vector2(10, 68), 11, TEAL if available else RED, 345.0)
	panel(Rect2(760, 545, 240, 46), TEAL if forage_left > 0 else MIST)
	draw_text("%s (%d)" % [tx("ui.gather"), forage_left], Vector2(770, 577), 18, Color.WHITE if forage_left > 0 else INK, 220.0, HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("คลิกสูตรเพื่อคราฟต์ • ตัวเลขแสดง มี/ต้องใช้", Vector2(255, 620), 13, INK, 720.0, HORIZONTAL_ALIGNMENT_CENTER)

func recipe_ingredient_text(recipe: Dictionary) -> String:
	var parts: Array[String] = []
	var ingredients: Dictionary = dictionary_value(recipe, "ingredients")
	for item_id: String in ingredients:
		parts.append("%s %d/%d" % [item_label(item_id), int(inventory.get(item_id, 0)), int(ingredients[item_id])])
	var waste_need: int = int_value(recipe, "organic_waste", 0)
	if waste_need > 0:
		parts.append("เศษอินทรีย์ %d/%d" % [organic_waste, waste_need])
	return " • ".join(parts)

func draw_world() -> void:
	for diagonal: int in range(W + H - 1):
		for y: int in range(H):
			var x: int = diagonal - y
			if x < 0 or x >= W:
				continue
			var cell: Vector2i = Vector2i(x, y)
			var tile: Dictionary = dictionary_value(tiles, tile_key(cell))
			var position: Vector2 = iso(Vector2(cell))
			draw_soft_tile(position, tile, x, y)
			var crop_id: String = string_value(tile, "crop")
			if not crop_id.is_empty():
				var frames: Array = crop_textures.get(crop_id, []) as Array
				if not frames.is_empty():
					var state: int = crop_visual_state(tile)
					var bob: float = 0.0 if bool(settings.get("reduced_motion", false)) else sin(Time.get_ticks_msec() * 0.003 + x + y) * 1.2
					draw_texture(frames[state] as Texture2D, position - Vector2(32, 58 + bob))
			if cell == selected and mode == "game":
				var outline: PackedVector2Array = PackedVector2Array([position + Vector2(0,-31),position + Vector2(63,0),position + Vector2(0,31),position + Vector2(-63,0),position + Vector2(0,-31)])
				draw_polyline(outline, Color("fff4d6"), 3.0)
	draw_garden_decorations()
	var teacher_frame: int = int(Time.get_ticks_msec() / 450) % maxi(1, teacher_frames.size())
	draw_texture(teacher_frames[teacher_frame], iso(Vector2(8.0, 2.2)) - Vector2(32, 58))
	var player_bob: float = 0.0 if bool(settings.get("reduced_motion", false)) else sin(Time.get_ticks_msec() * 0.008) * 1.0
	draw_texture(player_frames[clampi(player_frame, 0, player_frames.size() - 1)], iso(player_position) - Vector2(32, 58 + player_bob))
	draw_weather_effect()

func draw_soft_tile(position: Vector2, tile: Dictionary, x: int, y: int) -> void:
	var top: Color = Color("79ad62") if posmod(x + y, 2) == 0 else Color("72a55c")
	if x >= 8:
		top = Color("c5a878")
	if bool(tile.get("farm", false)):
		top = Color("9b6a43")
		if bool(tile.get("tilled", false)):
			var moisture: float = float_value(tile, "moisture")
			top = Color("5b4035") if moisture >= 80.0 else (Color("73503c") if moisture >= 38.0 else Color("8f603f"))
			if float_value(tile, "fertility") < 30.0:
				top = Color("79664e")
	var shadow_points: PackedVector2Array = PackedVector2Array([position+Vector2(0,-27),position+Vector2(61,3),position+Vector2(0,35),position+Vector2(-61,3)])
	draw_colored_polygon(shadow_points, Color(0.12,0.18,0.12,0.22))
	var points: PackedVector2Array = PackedVector2Array([position+Vector2(0,-31),position+Vector2(63,0),position+Vector2(0,31),position+Vector2(-63,0)])
	draw_colored_polygon(points, top)
	draw_polyline(PackedVector2Array([points[0],points[1],points[2],points[3],points[0]]), top.darkened(0.22), 1.0)
	if bool(tile.get("tilled", false)):
		for line_index: int in range(3):
			var offset: float = -12.0 + line_index * 12.0
			draw_line(position + Vector2(-34 + offset * 0.5, offset * 0.25), position + Vector2(34 + offset * 0.5, -offset * 0.25), top.darkened(0.18), 2.0)
	elif not bool(tile.get("farm", false)) and x < 8:
		for speck: int in range(3):
			var sx: float = float(posmod(x * 19 + y * 11 + speck * 17, 42) - 21)
			var sy: float = float(posmod(x * 7 + y * 23 + speck * 13, 20) - 10)
			draw_circle(position + Vector2(sx, sy), 1.5, Color("a7d46f"))

func draw_garden_decorations() -> void:
	# Soft fence and orchard framing replace the heavy black grid from the prototype.
	var fence_color: Color = Color("8a5a38")
	for index: int in range(8):
		var a: Vector2 = iso(Vector2(float(index), 0.0)) + Vector2(-20,-23)
		var b: Vector2 = iso(Vector2(float(index+1), 0.0)) + Vector2(-20,-23)
		draw_line(a, b, fence_color, 5.0)
		draw_rect(Rect2(a-Vector2(3,10), Vector2(6,20)), fence_color.darkened(0.12))
	for tree_index: int in range(5):
		var p: Vector2 = iso(Vector2(0.0, float(tree_index)+0.2)) + Vector2(-55,-45)
		draw_rect(Rect2(p+Vector2(-5,18), Vector2(10,32)), Color("714831"))
		draw_circle(p, 25.0, Color("315a3a"))
		draw_circle(p+Vector2(-14,4), 19.0, Color("4f8748"))
		draw_circle(p+Vector2(14,5), 18.0, Color("78b85a"))
	var pond: Vector2 = iso(Vector2(8.4, 0.7))
	var pond_points: PackedVector2Array = PackedVector2Array([pond+Vector2(0,-24),pond+Vector2(48,0),pond+Vector2(0,24),pond+Vector2(-48,0)])
	draw_colored_polygon(pond_points, Color("4e9bb3"))
	draw_polyline(PackedVector2Array([pond_points[0],pond_points[1],pond_points[2],pond_points[3],pond_points[0]]), Color("d9c38f"), 6.0)
	for building_index: int in range(mini(4, building_textures.size())):
		var building_positions: Array[Vector2] = [Vector2(8.8,2.0),Vector2(9.2,3.8),Vector2(8.7,5.5),Vector2(7.9,7.0)]
		draw_texture(building_textures[building_index], iso(building_positions[building_index])-Vector2(64,105))

func draw_hud() -> void:
	super.draw_hud()
	# Replace the old compact stock sentence with explicit remaining quantities.
	draw_rect(Rect2(0, 588, 1000, 31), WOOD)
	var stock: String = "คลังคงเหลือ: เมล็ด %s %d | %s %d | ปุ๋ยหมัก %d | ปุ๋ยอินทรีย์ %d | สเปรย์ %d | วัตถุดิบ %d" % [
		crop_name(selected_seed), int(inventory.get("seed_" + selected_seed,0)), crop_name(CROP_IDS[(CROP_IDS.find(selected_seed)+1)%CROP_IDS.size()]),
		int(inventory.get("seed_" + CROP_IDS[(CROP_IDS.find(selected_seed)+1)%CROP_IDS.size()],0)), int(inventory.get("compost",0)),
		int(inventory.get("organic_fertilizer",0)), int(inventory.get("bio_spray",0)), material_total()
	]
	draw_text(stock, Vector2(14, 610), 13, CREAM, 970.0)
	panel(Rect2(720, 10, 250, 76), Color("e6efd9"))
	draw_text("%s: %s" % [tx("ui.autoplay"), GrowWiseAutoPlay.mode_name(autoplay_mode, language)], Vector2(735, 36), 15, GREEN)
	draw_text("ทำงานแล้ว %d ครั้ง • F4 เปลี่ยนโหมด" % autoplay_actions_total, Vector2(735, 61), 12, INK)
	if int(inventory.get("upgrade_weather_station",0)) > 0:
		var tomorrow_weather: String = GrowWiseSimulation.weather_for_day(day+1, GrowWiseSimulation.season_index(day+1))
		draw_text("%s: %s" % [tx("ui.tomorrow"), weather_name(tomorrow_weather)], Vector2(735, 82), 12, BLUE)

func material_total() -> int:
	var total: int = 0
	for item_id: String in ["wood","stone","fiber","herb","scrap","glass","rubber","water_bottle","dry_leaves","mineral"]:
		total += int(inventory.get(item_id, 0))
	return total

func draw_inspector() -> void:
	var tile: Dictionary = dictionary_value(tiles, tile_key(selected))
	draw_text("แปลง (%d,%d)" % [selected.x, selected.y], Vector2(1028, 142), 18)
	draw_bar(Rect2(1028, 158, 218, 20), float_value(tile, "moisture"), BLUE, "ความชื้น")
	draw_bar(Rect2(1028, 184, 218, 20), float_value(tile, "fertility"), GREEN, "ดิน")
	draw_bar(Rect2(1028, 210, 218, 20), float_value(tile, "health"), TEAL, "แข็งแรง")
	var crop_id: String = string_value(tile, "crop")
	if crop_id.is_empty():
		draw_text("ว่าง • เมล็ดที่เลือกคงเหลือ %d" % int(inventory.get("seed_" + selected_seed,0)), Vector2(1028, 262), 15, INK, 218.0)
		draw_text("เลือกคราฟต์หรือโหมดออโต้ด้านล่าง", Vector2(1028, 292), 12, BLUE, 218.0)
		return
	var stage: int = int_value(tile, "stage")
	draw_text(crop_name(crop_id), Vector2(1028, 262), 18, INK)
	draw_text("ระยะ %d/5 • %s • คุณภาพ %d" % [stage, stage_name(stage), int(round(float_value(tile,"quality")))], Vector2(1028, 288), 14)
	var next_stage: int = mini(5, stage + 1)
	draw_text("%s: %s" % [tx("ui.next_stage"), format_eta(growth_eta(tile, next_stage))], Vector2(1028, 318), 13, BLUE, 218.0)
	draw_text("%s: %s" % [tx("ui.harvest_eta"), format_eta(growth_eta(tile, 5))], Vector2(1028, 344), 13, GOLD, 218.0)
	var symptom: String = GrowWiseSimulation.primary_symptom(tile)
	draw_text("สถานะ: " + tx("status." + symptom), Vector2(1028, 374), 14, RED if symptom != "healthy" else TEAL)
	draw_text("แมลง %d • โรค %d • วัชพืช %d" % [int(round(float_value(tile,"pest"))),int(round(float_value(tile,"disease"))),int(round(float_value(tile,"weed")))], Vector2(1028, 404), 12)
	draw_text("เมล็ด %s คงเหลือ %d" % [crop_name(crop_id), int(inventory.get("seed_"+crop_id,0))], Vector2(1028, 432), 12, INK)
	draw_text("คลิกขวาเพื่อดูไทม์ไลน์ทุกระยะ", Vector2(1028, 460), 11, BLUE)

func draw_diagnosis() -> void:
	var tile: Dictionary = dictionary_value(tiles, tile_key(selected))
	var crop_id: String = string_value(tile, "crop")
	draw_text("ตรวจพืชและเวลาการเติบโต", Vector2(325, 145), 25, GREEN)
	draw_text("%s • ระยะ %d/5 • %s" % [crop_name(crop_id), int_value(tile,"stage"), stage_name(int_value(tile,"stage"))], Vector2(325, 180), 17)
	draw_text("สถานะ: %s | น้ำ %d | ดิน %d | แมลง %d | โรค %d" % [tx("status."+diagnosis_actual),int(round(float_value(tile,"moisture"))),int(round(float_value(tile,"fertility"))),int(round(float_value(tile,"pest"))),int(round(float_value(tile,"disease")))], Vector2(325, 210), 13, INK, 570.0)
	draw_text("ประมาณการแต่ละขนาด", Vector2(325, 250), 17, BLUE)
	for target_stage: int in range(1, 6):
		var done: bool = int_value(tile,"stage") >= target_stage
		var marker: String = "✓" if done else "○"
		var eta_text: String = "ผ่านแล้ว" if done else format_eta(growth_eta(tile,target_stage))
		draw_text("%s ระยะ %d • %s • %s" % [marker,target_stage,stage_name(target_stage),eta_text], Vector2(340, 278 + target_stage*30), 14, TEAL if done else INK)
	draw_text("เลือกสาเหตุที่คิดว่าเกิดขึ้น", Vector2(635, 250), 16, BLUE)
	for index: int in range(SYMPTOMS.size()):
		var rect_value: Rect2 = Rect2(640, 280 + index * 38, 230, 32)
		panel(rect_value, GOLD if diagnosis_choice == SYMPTOMS[index] else MIST)
		draw_text(tx("status." + SYMPTOMS[index]), rect_value.position + Vector2(6, 23), 12, INK, 218.0, HORIZONTAL_ALIGNMENT_CENTER)
	if diagnosis_choice == diagnosis_actual:
		draw_text("✓ " + tx("msg.correct"), Vector2(640, 570), 16, TEAL, 230.0, HORIZONTAL_ALIGNMENT_CENTER)
