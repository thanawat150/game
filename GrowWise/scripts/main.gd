extends Node2D

const GrowWiseArtFactory = preload("res://scripts/art_factory.gd")
const GrowWiseSimulation = preload("res://scripts/simulation.gd")
const GrowWiseAudioFactory = preload("res://scripts/audio_factory.gd")

const W: int = 10
const H: int = 8
const TW: float = 128.0
const TH: float = 64.0
const ORIGIN: Vector2 = Vector2(555.0, 142.0)
const SAVE_VERSION: int = 5
const SAVE_DIR: String = "user://saves"
const CROP_IDS: Array[String] = ["water_spinach", "kale", "chili", "tomato", "cucumber"]
const SYMPTOMS: Array[String] = ["dry", "overwater", "poor_soil", "pest", "disease", "weed", "slow"]
const CREAM: Color = Color("f3e5c2")
const INK: Color = Color("29302a")
const WOOD: Color = Color("714831")
const WOOD_LIGHT: Color = Color("b97a4d")
const GREEN: Color = Color("4f8748")
const TEAL: Color = Color("4c927e")
const GOLD: Color = Color("e9b84d")
const MIST: Color = Color("d8e2d5")
const RED: Color = Color("c65a4b")
const BLUE: Color = Color("4e9bb3")

var atlas: Texture2D
var terrain: Array[Texture2D] = []
var selector_texture: Texture2D
var icons: Dictionary = {}
var crop_textures: Dictionary = {}
var player_frames: Array[Texture2D] = []
var teacher_frames: Array[Texture2D] = []
var npc_textures: Array[Texture2D] = []
var creature_textures: Array[Texture2D] = []
var weather_textures: Array[Texture2D] = []
var building_textures: Array[Texture2D] = []
var data: Dictionary = {}
var locale_data: Dictionary = {}
var tiles: Dictionary = {}
var inventory: Dictionary = {}
var quality_bank: Dictionary = {}
var unlocked_lessons: Dictionary = {}
var settings: Dictionary = {}
var experiment_results: Dictionary = {}
var player_position: Vector2 = Vector2(4.5, 6.5)
var selected: Vector2i = Vector2i(4, 3)
var selected_tool: String = "hoe"
var selected_seed: String = "water_spinach"
var selected_lab_crop: String = "water_spinach"
var mode: String = "menu"
var overlay: String = ""
var language: String = "th"
var day: int = 1
var minutes: float = 480.0
var speed: int = 1
var paused: bool = false
var save_slot: int = 1
var money: int = 600
var knowledge: int = 0
var eco_score: int = 50
var soil_score: int = 68
var biodiversity_score: int = 40
var water_efficiency: int = 70
var revenue: int = 0
var expenses: int = 0
var water_used: float = 0.0
var harvest_total: int = 0
var organic_waste: int = 0
var compost_progress: float = 0.0
var current_weather: String = "clear"
var current_season: int = 0
var quest_index: int = 0
var quest_step: int = 0
var tutorial_step: int = 0
var diagnosis_actual: String = "healthy"
var diagnosis_choice: String = ""
var message: String = ""
var message_time: float = 0.0
var autosave_timer: float = 0.0
var animation_timer: float = 0.0
var player_frame: int = 0
var buttons: Array[Dictionary] = []
var overlay_buttons: Array[Dictionary] = []
var audio_player: AudioStreamPlayer
var rng: RandomNumberGenerator = RandomNumberGenerator.new()
var season_report: Dictionary = {}

func _ready() -> void:
	data = load_json("res://data/game_data.json")
	locale_data = load_json("res://localization/th.json")
	load_atlas()
	build_regions()
	audio_player = AudioStreamPlayer.new()
	add_child(audio_player)
	rng.seed = 20260731
	new_game()
	var self_test: Dictionary = GrowWiseSimulation.self_test(data)
	if bool(self_test.get("ok", false)):
		print("GROWWISE_PHASES_2_5_OK")
	else:
		push_error("GrowWise simulation self-test failed: %s" % JSON.stringify(self_test))
	print("GROWWISE_SMOKE_OK")
	queue_redraw()

func load_json(path: String) -> Dictionary:
	var file: FileAccess = FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("Unable to open data file: " + path)
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	file.close()
	return parsed as Dictionary if parsed is Dictionary else {}

func dictionary_value(source: Dictionary, key_name: String, fallback: Dictionary = {}) -> Dictionary:
	var value: Variant = source.get(key_name, fallback)
	return value as Dictionary if value is Dictionary else fallback

func array_value(source: Dictionary, key_name: String, fallback: Array = []) -> Array:
	var value: Variant = source.get(key_name, fallback)
	return value as Array if value is Array else fallback

func int_value(source: Dictionary, key_name: String, fallback: int = 0) -> int:
	return int(source.get(key_name, fallback))

func float_value(source: Dictionary, key_name: String, fallback: float = 0.0) -> float:
	return float(source.get(key_name, fallback))

func string_value(source: Dictionary, key_name: String, fallback: String = "") -> String:
	return String(source.get(key_name, fallback))

func load_atlas() -> void:
	var image: Image = GrowWiseArtFactory.build_atlas()
	atlas = ImageTexture.create_from_image(image)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("user://generated_assets"))
	var save_error: Error = image.save_png("user://generated_assets/growwise_full_atlas.png")
	if save_error != OK:
		push_warning("Could not save generated atlas PNG: %s" % error_string(save_error))

func region(x: int, y: int, width: int, height: int) -> AtlasTexture:
	var texture: AtlasTexture = AtlasTexture.new()
	texture.atlas = atlas
	texture.region = Rect2(x, y, width, height)
	texture.filter_clip = true
	return texture

func build_regions() -> void:
	terrain.clear()
	for index: int in range(10):
		terrain.append(region(index * 128, 0, 128, 64))
	selector_texture = region(1280, 0, 128, 64)
	var icon_ids: Array[String] = ["hoe","water","seed","inspect","harvest","save","load","fertilize","weed","bio","compost","remove","shop","market","lab","notebook","quest","settings","money","knowledge","eco","ph","npk","language"]
	for index: int in range(icon_ids.size()):
		icons[icon_ids[index]] = region((index % 24) * 64, 96 + int(index / 24) * 64, 64, 64)
	for crop_index: int in range(CROP_IDS.size()):
		var frames: Array[Texture2D] = []
		for state: int in range(12):
			frames.append(region(state * 64, 192 + crop_index * 64, 64, 64))
		crop_textures[CROP_IDS[crop_index]] = frames
	for frame_index: int in range(12):
		player_frames.append(region(frame_index * 64, 544, 64, 64))
	for frame_index: int in range(4):
		teacher_frames.append(region(frame_index * 64, 608, 64, 64))
	for npc_index: int in range(6):
		npc_textures.append(region(npc_index * 64, 672, 64, 64))
	for creature_index: int in range(10):
		creature_textures.append(region(creature_index * 64, 736, 64, 64))
	for weather_index: int in range(9):
		weather_textures.append(region(weather_index * 64, 800, 64, 64))
	for building_index: int in range(8):
		building_textures.append(region(building_index * 128, 864, 128, 128))

func translate(key_name: String) -> String:
	return String(locale_data.get(key_name, key_name))

func switch_language() -> void:
	language = "en" if language == "th" else "th"
	locale_data = load_json("res://localization/%s.json" % language)
	build_buttons()
	notify(translate("access.language") + ": " + language.to_upper(), "success")

func crop_name(crop_id: String) -> String:
	var crops: Dictionary = dictionary_value(data, "crops")
	var crop_def: Dictionary = dictionary_value(crops, crop_id)
	return translate(string_value(crop_def, "name_key", crop_id))

func weather_name(weather_id: String) -> String:
	var weather_data: Dictionary = dictionary_value(data, "weather")
	var weather_def: Dictionary = dictionary_value(weather_data, weather_id)
	return translate(string_value(weather_def, "name_key", weather_id))

func season_name(index: int) -> String:
	var seasons: Array = array_value(data, "seasons")
	if seasons.is_empty():
		return ""
	return translate(String(seasons[clampi(index, 0, seasons.size() - 1)]))

func new_game() -> void:
	tiles.clear()
	for y: int in range(H):
		for x: int in range(W):
			var is_farm: bool = x >= 1 and x <= 7 and y >= 1 and y <= 6
			tiles[tile_key(Vector2i(x, y))] = GrowWiseSimulation.new_tile(is_farm)
	inventory = {
		"seed_water_spinach":6,"seed_kale":6,"seed_chili":3,"seed_tomato":3,"seed_cucumber":3,
		"produce_water_spinach":0,"produce_kale":0,"produce_chili":0,"produce_tomato":0,"produce_cucumber":0,
		"compost":1,"organic_fertilizer":1,"bio_spray":1,"moisture_meter":0,"ph_meter":0
	}
	quality_bank = {"water_spinach":0.0,"kale":0.0,"chili":0.0,"tomato":0.0,"cucumber":0.0}
	unlocked_lessons = {}
	settings = {"high_contrast":false,"reduced_motion":false,"large_text":false,"sound":true,"time_in_panels":false}
	experiment_results = {}
	player_position = Vector2(4.5, 6.5)
	selected = Vector2i(4, 3)
	selected_tool = "hoe"
	selected_seed = "water_spinach"
	selected_lab_crop = "water_spinach"
	day = 1
	minutes = 480.0
	speed = 1
	paused = false
	money = 600
	knowledge = 0
	eco_score = 50
	soil_score = 68
	biodiversity_score = 40
	water_efficiency = 70
	revenue = 0
	expenses = 0
	water_used = 0.0
	harvest_total = 0
	organic_waste = 0
	compost_progress = 0.0
	current_season = GrowWiseSimulation.season_index(day)
	current_weather = GrowWiseSimulation.weather_for_day(day, current_season)
	quest_index = 0
	quest_step = 0
	tutorial_step = 0
	overlay = ""
	mode = "menu"
	rng.seed = 20260731
	build_buttons()
	notify(translate("quest.1.title"), "success")

func build_buttons() -> void:
	buttons = [
		button("hoe", Rect2(205, 640, 60, 62), "hoe", "tool.hoe"),
		button("water", Rect2(269, 640, 60, 62), "water", "tool.water"),
		button("seed", Rect2(333, 640, 60, 62), "seed", "tool.seed"),
		button("fertilize", Rect2(397, 640, 60, 62), "fertilize", "tool.fertilize"),
		button("inspect", Rect2(461, 640, 60, 62), "inspect", "tool.inspect"),
		button("harvest", Rect2(525, 640, 60, 62), "harvest", "tool.harvest"),
		button("weed", Rect2(589, 640, 60, 62), "weed", "tool.weed"),
		button("bio", Rect2(653, 640, 60, 62), "bio", "tool.bio"),
		button("compost", Rect2(717, 640, 60, 62), "compost", "tool.compost"),
		button("remove", Rect2(781, 640, 60, 62), "remove", "tool.remove"),
		button("shop", Rect2(875, 635, 56, 56), "shop", "ui.shop"),
		button("market", Rect2(935, 635, 56, 56), "market", "ui.market"),
		button("lab", Rect2(995, 635, 56, 56), "lab", "ui.lab"),
		button("notebook", Rect2(1055, 635, 56, 56), "notebook", "ui.notebook"),
		button("settings", Rect2(1115, 635, 56, 56), "settings", "ui.settings"),
		button("save", Rect2(1175, 635, 42, 42), "save", "ui.save"),
		button("load", Rect2(1221, 635, 42, 42), "load", "ui.load")
	]

func button(id_value: String, rect_value: Rect2, icon_id: String, label_key: String) -> Dictionary:
	return {"id":id_value,"rect":rect_value,"icon":icons.get(icon_id),"label":label_key}

func _process(delta: float) -> void:
	if mode == "game":
		move_player(delta)
		if message_time > 0.0:
			message_time -= delta
		var panel_stops_time: bool = not bool(settings.get("time_in_panels", false)) and not overlay.is_empty()
		if not paused and not panel_stops_time:
			minutes += delta * float(speed) * 15.0
			while minutes >= 1440.0:
				minutes -= 1440.0
				advance_day()
			autosave_timer += delta
			if autosave_timer >= 60.0:
				autosave_timer = 0.0
				save_game(save_slot, true)
	queue_redraw()

func move_player(delta: float) -> void:
	if not overlay.is_empty():
		return
	var direction: Vector2 = Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_key_pressed(KEY_A): direction.x -= 1.0
	if Input.is_key_pressed(KEY_D): direction.x += 1.0
	if Input.is_key_pressed(KEY_W): direction.y -= 1.0
	if Input.is_key_pressed(KEY_S): direction.y += 1.0
	if direction.length() > 0.1:
		player_position += direction.normalized() * delta * 2.4
		player_position.x = clampf(player_position.x, 0.0, float(W - 1))
		player_position.y = clampf(player_position.y, 0.0, float(H - 1))
		if not bool(settings.get("reduced_motion", false)):
			animation_timer += delta
			if animation_timer >= 0.14:
				animation_timer = 0.0
				player_frame = 2 + ((player_frame - 1) % 4)
	else:
		player_frame = 0

func advance_day() -> void:
	var old_season: int = current_season
	day += 1
	current_season = GrowWiseSimulation.season_index(day)
	current_weather = GrowWiseSimulation.weather_for_day(day, current_season)
	var weather_data: Dictionary = dictionary_value(data, "weather")
	var weather_def: Dictionary = dictionary_value(weather_data, current_weather)
	var crops: Dictionary = dictionary_value(data, "crops")
	var keys: Array = tiles.keys()
	for key_value: Variant in keys:
		var key_string: String = String(key_value)
		var tile: Dictionary = dictionary_value(tiles, key_string)
		var crop_id: String = string_value(tile, "crop")
		var crop_def: Dictionary = dictionary_value(crops, crop_id)
		var previous_stage: int = int_value(tile, "stage")
		var previous_pest: float = float_value(tile, "pest")
		var previous_beneficial: float = float_value(tile, "beneficial")
		tile = GrowWiseSimulation.simulate_day(tile, crop_def, weather_def, current_season, rng)
		tiles[key_string] = tile
		if crop_id != "" and previous_stage == 0 and int_value(tile, "stage") >= 1:
			record_event("event.sprout")
		if previous_pest < 38.0 and float_value(tile, "pest") >= 38.0:
			record_event("event.find_pest")
		if previous_beneficial < 35.0 and float_value(tile, "beneficial") >= 35.0:
			record_event("event.find_beneficial")
	if organic_waste > 0:
		compost_progress += minf(25.0, float(organic_waste) * 5.0)
		if compost_progress >= 100.0:
			compost_progress -= 100.0
			organic_waste = maxi(0, organic_waste - 3)
			inventory["compost"] = int(inventory.get("compost", 0)) + 1
			record_event("event.make_compost")
	update_spacing_penalties()
	update_scores()
	if current_season != old_season:
		record_event("event.check_season")
		build_season_report()
		overlay = "season_report"

func update_spacing_penalties() -> void:
	for y: int in range(H):
		for x: int in range(W):
			var cell: Vector2i = Vector2i(x, y)
			var tile: Dictionary = dictionary_value(tiles, tile_key(cell))
			if string_value(tile, "crop").is_empty():
				tile["spacing_penalty"] = 0.0
				tiles[tile_key(cell)] = tile
				continue
			var neighbours: int = 0
			for oy: int in range(-1, 2):
				for ox: int in range(-1, 2):
					if ox == 0 and oy == 0:
						continue
					var other: Vector2i = cell + Vector2i(ox, oy)
					if valid_cell(other):
						var other_tile: Dictionary = dictionary_value(tiles, tile_key(other))
						if not string_value(other_tile, "crop").is_empty():
							neighbours += 1
			tile["spacing_penalty"] = float(neighbours * 11)
			tiles[tile_key(cell)] = tile

func update_scores() -> void:
	var farm_count: int = 0
	var soil_total: float = 0.0
	var biodiversity_total: float = 0.0
	var healthy_count: int = 0
	var crop_count: int = 0
	var keys: Array = tiles.keys()
	for key_value: Variant in keys:
		var tile: Dictionary = dictionary_value(tiles, String(key_value))
		if bool(tile.get("farm", false)):
			farm_count += 1
			soil_total += float_value(tile, "fertility")
			biodiversity_total += float_value(tile, "beneficial")
			if not string_value(tile, "crop").is_empty():
				crop_count += 1
				if float_value(tile, "health") >= 70.0:
					healthy_count += 1
	soil_score = int(round(soil_total / maxf(1.0, float(farm_count))))
	biodiversity_score = clampi(int(round(biodiversity_total / maxf(1.0, float(farm_count)))) + 25, 0, 100)
	var expected_water: float = maxf(1.0, float(day * maxi(crop_count, 1)) * 22.0)
	water_efficiency = clampi(int(round(100.0 - maxf(0.0, water_used - expected_water) / expected_water * 100.0)), 0, 100)
	eco_score = clampi(int(round((float(soil_score) + float(biodiversity_score) + float(water_efficiency)) / 3.0)), 0, 100)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE:
			if not overlay.is_empty():
				overlay = ""
			elif mode == "game":
				mode = "menu"
			else:
				mode = "game"
		elif mode == "game":
			if event.keycode == KEY_SPACE:
				paused = not paused
			elif event.keycode == KEY_F1:
				save_slot = 1
			elif event.keycode == KEY_F2:
				save_slot = 2
			elif event.keycode == KEY_F3:
				save_slot = 3
			elif event.keycode == KEY_MINUS:
				speed = maxi(1, int(speed / 2))
			elif event.keycode == KEY_EQUAL:
				speed = mini(4, speed * 2)
			elif event.keycode >= KEY_1 and event.keycode <= KEY_5:
				selected_seed = CROP_IDS[int(event.keycode - KEY_1)]
				selected_tool = "seed"
	if event is InputEventMouseMotion and mode == "game" and overlay.is_empty():
		var cell: Vector2i = pick_cell(event.position)
		if valid_cell(cell):
			selected = cell
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_RIGHT and mode == "game" and overlay.is_empty():
			var right_cell: Vector2i = pick_cell(event.position)
			if valid_cell(right_cell):
				selected = right_cell
				inspect_tile(right_cell)
		elif event.button_index == MOUSE_BUTTON_LEFT:
			if mode == "menu":
				menu_click(event.position)
			elif not overlay.is_empty():
				overlay_click(event.position)
			else:
				game_click(event.position)

func menu_click(position: Vector2) -> void:
	if Rect2(490, 360, 300, 58).has_point(position):
		new_game()
		mode = "game"
	elif Rect2(490, 430, 300, 58).has_point(position):
		if not load_game(save_slot):
			new_game()
		mode = "game"
	elif Rect2(490, 500, 300, 58).has_point(position):
		get_tree().quit()

func game_click(position: Vector2) -> void:
	for button_data: Dictionary in buttons:
		var rect_value: Rect2 = button_data.get("rect", Rect2()) as Rect2
		if rect_value.has_point(position):
			handle_button(String(button_data.get("id", "")))
			return
	if Rect2(12, 112, 226, 215).has_point(position):
		overlay = "quest"
		return
	if Rect2(1010, 112, 258, 370).has_point(position):
		inspect_tile(selected)
		return
	var cell: Vector2i = pick_cell(position)
	if valid_cell(cell):
		selected = cell
		apply_tool(cell)

func handle_button(button_id: String) -> void:
	if button_id in ["hoe","water","seed","fertilize","inspect","harvest","weed","bio","compost","remove"]:
		if button_id == "seed" and selected_tool == "seed":
			cycle_seed(1)
		else:
			selected_tool = button_id
	elif button_id in ["shop","market","lab","notebook","settings"]:
		overlay = button_id
	elif button_id == "save":
		save_game(save_slot, false)
	elif button_id == "load":
		load_game(save_slot)

func cycle_seed(direction: int) -> void:
	var index: int = CROP_IDS.find(selected_seed)
	selected_seed = CROP_IDS[posmod(index + direction, CROP_IDS.size())]
	notify(crop_name(selected_seed), "plant")

func apply_tool(cell: Vector2i) -> void:
	var key_string: String = tile_key(cell)
	var tile: Dictionary = dictionary_value(tiles, key_string)
	if not bool(tile.get("farm", false)):
		notify(translate("msg.tile_not_farm"), "error")
		return
	match selected_tool:
		"hoe":
			if not string_value(tile, "crop").is_empty():
				notify(translate("msg.crop_exists"), "error")
				return
			tile["tilled"] = true
			tile["moisture"] = minf(float_value(tile, "moisture"), 35.0)
			tile["weed"] = maxf(0.0, float_value(tile, "weed") - 20.0)
			record_event("event.till")
			play_sound("hoe")
		"water":
			if not bool(tile.get("tilled", false)):
				notify(translate("msg.till_first"), "error")
				return
			tile["moisture"] = minf(100.0, float_value(tile, "moisture") + 34.0)
			tile["water_total"] = float_value(tile, "water_total") + 34.0
			tile["last_watered"] = day
			water_used += 34.0
			expenses += 1
			record_event("event.water")
			play_sound("water")
		"seed":
			if not bool(tile.get("tilled", false)) or not string_value(tile, "crop").is_empty():
				notify(translate("msg.till_first"), "error")
				return
			var item_id: String = "seed_" + selected_seed
			if int(inventory.get(item_id, 0)) <= 0:
				notify(translate("msg.seed_empty"), "error")
				return
			inventory[item_id] = int(inventory.get(item_id, 0)) - 1
			tile["crop"] = selected_seed
			tile["growth"] = 0.0
			tile["stage"] = 0
			tile["health"] = 100.0
			tile["quality"] = 70.0
			tile["dead"] = false
			tile["pest"] = 0.0
			tile["disease"] = 0.0
			tile["cost_total"] = float_value(tile, "cost_total") + 4.0
			expenses += 4
			record_event("event.plant")
			if current_season in array_value(dictionary_value(dictionary_value(data, "crops"), selected_seed), "season_bonus"):
				record_event("event.plant_season")
			play_sound("plant")
		"fertilize":
			if int(inventory.get("organic_fertilizer", 0)) <= 0:
				notify(translate("msg.seed_empty"), "error")
				return
			inventory["organic_fertilizer"] = int(inventory.get("organic_fertilizer", 0)) - 1
			tile["fertility"] = minf(100.0, float_value(tile, "fertility") + 24.0)
			tile["nitrogen"] = minf(100.0, float_value(tile, "nitrogen") + 20.0)
			tile["phosphorus"] = minf(100.0, float_value(tile, "phosphorus") + 15.0)
			tile["potassium"] = minf(100.0, float_value(tile, "potassium") + 18.0)
			tile["last_fertilized"] = day
			expenses += 12
		"inspect":
			tiles[key_string] = tile
			inspect_tile(cell)
			return
		"harvest":
			harvest_tile(tile, key_string)
			return
		"weed":
			tile["weed"] = 0.0
			knowledge += 1
		"bio":
			if int(inventory.get("bio_spray", 0)) <= 0:
				notify(translate("msg.seed_empty"), "error")
				return
			inventory["bio_spray"] = int(inventory.get("bio_spray", 0)) - 1
			tile["pest"] = maxf(0.0, float_value(tile, "pest") - 55.0)
			tile["disease"] = maxf(0.0, float_value(tile, "disease") - 20.0)
			eco_score = mini(100, eco_score + 2)
			record_event("event.bio_control")
		"compost":
			if int(inventory.get("compost", 0)) <= 0:
				notify(translate("msg.seed_empty"), "error")
				return
			inventory["compost"] = int(inventory.get("compost", 0)) - 1
			tile["fertility"] = minf(100.0, float_value(tile, "fertility") + 30.0)
			tile["beneficial"] = minf(100.0, float_value(tile, "beneficial") + 15.0)
			tile["ph"] = clampf(float_value(tile, "ph") + (6.5 - float_value(tile, "ph")) * 0.35, 4.0, 9.0)
			record_event("event.use_compost")
			record_event("event.restore_soil")
		"remove":
			if string_value(tile, "crop").is_empty():
				return
			tile["crop"] = ""
			tile["growth"] = 0.0
			tile["stage"] = 0
			tile["dead"] = false
			organic_waste += 1
	tiles[key_string] = tile
	update_spacing_penalties()

func harvest_tile(tile: Dictionary, key_string: String) -> void:
	var crop_id: String = string_value(tile, "crop")
	if crop_id.is_empty() or int_value(tile, "stage") < 5 or bool(tile.get("dead", false)):
		notify(translate("msg.not_ready"), "error")
		return
	var crops: Dictionary = dictionary_value(data, "crops")
	var crop_def: Dictionary = dictionary_value(crops, crop_id)
	var minimum: int = int_value(crop_def, "harvest_min", 1)
	var maximum: int = int_value(crop_def, "harvest_max", minimum)
	var quality: int = GrowWiseSimulation.harvest_quality(tile)
	var amount: int = rng.randi_range(minimum, maximum)
	amount = maxi(1, int(round(float(amount) * float_value(tile, "health", 100.0) / 100.0)))
	var produce_id: String = "produce_" + crop_id
	inventory[produce_id] = int(inventory.get(produce_id, 0)) + amount
	quality_bank[crop_id] = float(quality_bank.get(crop_id, 0.0)) + float(quality * amount)
	harvest_total += amount
	organic_waste += 1
	knowledge += 5 + int(quality / 20)
	if current_season in array_value(crop_def, "season_bonus"):
		record_event("event.season_harvest")
	record_event("event.harvest")
	tile["crop"] = ""
	tile["growth"] = 0.0
	tile["stage"] = 0
	tile["health"] = 100.0
	tile["quality"] = 70.0
	tile["pest"] = 0.0
	tile["disease"] = 0.0
	tile["dead"] = false
	tile["moisture"] = maxf(12.0, float_value(tile, "moisture") - 18.0)
	tiles[key_string] = tile
	notify("%s: %d | %s %d" % [translate("msg.harvested"), amount, translate("lab.quality"), quality], "harvest")
	play_sound("harvest")

func inspect_tile(cell: Vector2i) -> void:
	selected = cell
	var tile: Dictionary = dictionary_value(tiles, tile_key(cell))
	diagnosis_actual = GrowWiseSimulation.primary_symptom(tile)
	diagnosis_choice = ""
	if diagnosis_actual == "dry": record_event("event.inspect_dry")
	if diagnosis_actual == "overwater": record_event("event.inspect_wet")
	if float_value(tile, "moisture") >= 35.0 and float_value(tile, "moisture") <= 75.0: record_event("event.inspect_moist")
	if float_value(tile, "spacing_penalty") >= 22.0: record_event("event.inspect_spacing")
	if int(inventory.get("ph_meter", 0)) > 0:
		record_event("event.measure_ph")
		record_event("event.measure_npk")
	overlay = "diagnosis"

func record_event(event_id: String) -> void:
	unlock_lessons(event_id)
	var quests: Array = array_value(data, "quests")
	if quest_index >= quests.size():
		return
	var quest: Dictionary = quests[quest_index] as Dictionary
	var steps: Array = array_value(quest, "steps")
	if quest_step < steps.size() and String(steps[quest_step]) == event_id:
		quest_step += 1
		knowledge += 3
		if tutorial_step < 5:
			tutorial_step += 1
		if quest_step >= steps.size():
			money += int_value(quest, "reward_money")
			knowledge += int_value(quest, "reward_knowledge")
			quest_index += 1
			quest_step = 0
			notify(translate("msg.quest_done"), "success")
			play_sound("success")

func unlock_lessons(event_id: String) -> void:
	var lessons: Array = array_value(data, "lessons")
	for lesson_value: Variant in lessons:
		var lesson: Dictionary = lesson_value as Dictionary
		if string_value(lesson, "unlock") == event_id:
			unlocked_lessons[string_value(lesson, "id")] = true

func notify(text: String, sound_id: String = "") -> void:
	message = text
	message_time = 5.0
	if not sound_id.is_empty():
		play_sound(sound_id)

func play_sound(sound_id: String) -> void:
	if not bool(settings.get("sound", true)) or audio_player == null:
		return
	audio_player.stream = GrowWiseAudioFactory.action_sound(sound_id)
	audio_player.play()

func overlay_click(position: Vector2) -> void:
	if Rect2(895, 115, 42, 36).has_point(position):
		overlay = ""
		return
	match overlay:
		"diagnosis": diagnosis_click(position)
		"shop": shop_click(position)
		"market": market_click(position)
		"lab": lab_click(position)
		"settings": settings_click(position)
		"quest", "notebook", "season_report": pass

func diagnosis_click(position: Vector2) -> void:
	for index: int in range(SYMPTOMS.size()):
		var rect_value: Rect2 = Rect2(345 + (index % 2) * 235, 325 + int(index / 2) * 48, 220, 38)
		if rect_value.has_point(position):
			diagnosis_choice = SYMPTOMS[index]
			if diagnosis_choice == diagnosis_actual:
				knowledge += 5
				notify(translate("msg.correct"), "success")
			else:
				notify(translate("msg.wrong"), "error")
			return

func shop_click(position: Vector2) -> void:
	var shop_items: Array = array_value(data, "shop")
	for index: int in range(shop_items.size()):
		var row: Rect2 = Rect2(315, 205 + index * 34, 540, 29)
		if row.has_point(position):
			buy_item(shop_items[index] as Dictionary)
			return

func buy_item(item: Dictionary) -> void:
	var price: int = int_value(item, "price")
	if money < price:
		notify(translate("msg.no_money"), "error")
		return
	var item_id: String = string_value(item, "id")
	var amount: int = int_value(item, "amount", 1)
	money -= price
	expenses += price
	inventory[item_id] = int(inventory.get(item_id, 0)) + amount
	notify(translate("msg.bought"), "shop")

func market_click(position: Vector2) -> void:
	for index: int in range(CROP_IDS.size()):
		var row: Rect2 = Rect2(340, 240 + index * 58, 500, 45)
		if row.has_point(position):
			sell_crop(CROP_IDS[index])
			return

func sell_crop(crop_id: String) -> void:
	var produce_id: String = "produce_" + crop_id
	var amount: int = int(inventory.get(produce_id, 0))
	if amount <= 0:
		notify(translate("msg.no_produce"), "error")
		return
	var crops: Dictionary = dictionary_value(data, "crops")
	var crop_def: Dictionary = dictionary_value(crops, crop_id)
	var base_price: int = int_value(crop_def, "sell_price")
	var average_quality: float = float(quality_bank.get(crop_id, 0.0)) / float(maxi(amount, 1))
	var multiplier: float = 0.65 + average_quality / 140.0
	var total: int = maxi(1, int(round(float(base_price * amount) * multiplier)))
	inventory[produce_id] = 0
	quality_bank[crop_id] = 0.0
	money += total
	revenue += total
	record_event("event.sell")
	notify("%s +%d" % [translate("msg.sold"), total], "shop")

func lab_click(position: Vector2) -> void:
	if Rect2(360, 195, 50, 36).has_point(position):
		cycle_lab_crop(-1)
	elif Rect2(765, 195, 50, 36).has_point(position):
		cycle_lab_crop(1)
	elif Rect2(495, 520, 220, 48).has_point(position):
		var crops: Dictionary = dictionary_value(data, "crops")
		var crop_def: Dictionary = dictionary_value(crops, selected_lab_crop)
		experiment_results = GrowWiseSimulation.run_experiment(crop_def)
		record_event("event.run_experiment")
		notify(translate("msg.experiment_done"), "success")

func cycle_lab_crop(direction: int) -> void:
	var index: int = CROP_IDS.find(selected_lab_crop)
	selected_lab_crop = CROP_IDS[posmod(index + direction, CROP_IDS.size())]

func settings_click(position: Vector2) -> void:
	var setting_ids: Array[String] = ["high_contrast","reduced_motion","large_text","sound","time_in_panels"]
	for index: int in range(setting_ids.size()):
		var rect_value: Rect2 = Rect2(380, 245 + index * 55, 420, 42)
		if rect_value.has_point(position):
			var setting_id: String = setting_ids[index]
			settings[setting_id] = not bool(settings.get(setting_id, false))
			return
	if Rect2(480, 535, 220, 46).has_point(position):
		switch_language()

func build_season_report() -> void:
	season_report = {
		"yield":harvest_total,"water":int(round(water_used)),"cost":expenses,"revenue":revenue,
		"profit":revenue-expenses,"soil":soil_score,"eco":eco_score,"biodiversity":biodiversity_score,"knowledge":knowledge
	}

func save_game(slot_number: int, automatic: bool) -> bool:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SAVE_DIR))
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var backup: String = path + ".bak"
	if FileAccess.file_exists(path):
		DirAccess.copy_absolute(ProjectSettings.globalize_path(path), ProjectSettings.globalize_path(backup))
	var payload: Dictionary = {
		"save_version":SAVE_VERSION,"language":language,"day":day,"minutes":minutes,"weather":current_weather,"season":current_season,
		"tiles":tiles,"inventory":inventory,"quality_bank":quality_bank,"player":[player_position.x,player_position.y],"selected_seed":selected_seed,
		"money":money,"knowledge":knowledge,"eco":eco_score,"soil_score":soil_score,"biodiversity":biodiversity_score,"water_efficiency":water_efficiency,
		"revenue":revenue,"expenses":expenses,"water_used":water_used,"harvest_total":harvest_total,"organic_waste":organic_waste,"compost_progress":compost_progress,
		"quest_index":quest_index,"quest_step":quest_step,"tutorial_step":tutorial_step,"unlocked_lessons":unlocked_lessons,"experiment_results":experiment_results,"settings":settings
	}
	var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(JSON.stringify(payload))
	file.close()
	if not automatic:
		notify(translate("msg.saved"), "success")
	return true

func load_game(slot_number: int) -> bool:
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty():
		payload = read_save(path + ".bak")
	if payload.is_empty():
		notify(translate("msg.no_save"), "error")
		return false
	language = string_value(payload, "language", "th")
	locale_data = load_json("res://localization/%s.json" % language)
	day = int_value(payload, "day", 1)
	minutes = float_value(payload, "minutes", 480.0)
	current_weather = string_value(payload, "weather", "clear")
	current_season = int_value(payload, "season")
	tiles = dictionary_value(payload, "tiles", tiles)
	inventory = dictionary_value(payload, "inventory", inventory)
	quality_bank = dictionary_value(payload, "quality_bank", quality_bank)
	var player_array: Array = array_value(payload, "player", [4.5, 6.5])
	player_position = Vector2(float(player_array[0]), float(player_array[1]))
	selected_seed = string_value(payload, "selected_seed", "water_spinach")
	money = int_value(payload, "money", 600)
	knowledge = int_value(payload, "knowledge")
	eco_score = int_value(payload, "eco", 50)
	soil_score = int_value(payload, "soil_score", 68)
	biodiversity_score = int_value(payload, "biodiversity", 40)
	water_efficiency = int_value(payload, "water_efficiency", 70)
	revenue = int_value(payload, "revenue")
	expenses = int_value(payload, "expenses")
	water_used = float_value(payload, "water_used")
	harvest_total = int_value(payload, "harvest_total")
	organic_waste = int_value(payload, "organic_waste")
	compost_progress = float_value(payload, "compost_progress")
	quest_index = int_value(payload, "quest_index")
	quest_step = int_value(payload, "quest_step")
	tutorial_step = int_value(payload, "tutorial_step")
	unlocked_lessons = dictionary_value(payload, "unlocked_lessons")
	experiment_results = dictionary_value(payload, "experiment_results")
	settings = dictionary_value(payload, "settings", settings)
	build_buttons()
	notify(translate("msg.loaded"), "success")
	return true

func read_save(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var file: FileAccess = FileAccess.open(path, FileAccess.READ)
	if file == null:
		return {}
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	file.close()
	if parsed is Dictionary:
		var payload: Dictionary = parsed as Dictionary
		if int_value(payload, "save_version", 0) <= SAVE_VERSION:
			return payload
	return {}

func tile_key(cell: Vector2i) -> String:
	return "%d,%d" % [cell.x, cell.y]

func valid_cell(cell: Vector2i) -> bool:
	return cell.x >= 0 and cell.y >= 0 and cell.x < W and cell.y < H

func iso(cell: Vector2) -> Vector2:
	return ORIGIN + Vector2((cell.x - cell.y) * TW * 0.5, (cell.x + cell.y) * TH * 0.5)

func pick_cell(position: Vector2) -> Vector2i:
	var relative: Vector2 = position - ORIGIN
	var dx: float = relative.x / (TW * 0.5)
	var dy: float = relative.y / (TH * 0.5)
	return Vector2i(roundi((dx + dy) * 0.5), roundi((dy - dx) * 0.5))

func crop_visual_state(tile: Dictionary) -> int:
	if bool(tile.get("dead", false)):
		return 11
	var symptom: String = GrowWiseSimulation.primary_symptom(tile)
	match symptom:
		"dry": return 6
		"poor_soil": return 7
		"overwater": return 8
		"pest": return 9
		"disease": return 10
		_: return clampi(int_value(tile, "stage"), 0, 5)

func font_size(base: int) -> int:
	return int(round(float(base) * (1.18 if bool(settings.get("large_text", false)) else 1.0)))

func panel(rect_value: Rect2, fill: Color) -> void:
	var border: Color = Color.WHITE if bool(settings.get("high_contrast", false)) else WOOD
	draw_rect(rect_value, border)
	draw_rect(Rect2(rect_value.position + Vector2(3, 3), rect_value.size - Vector2(6, 6)), fill)

func draw_text(text: String, position: Vector2, size: int = 17, color: Color = INK, width: float = -1.0, alignment: HorizontalAlignment = HORIZONTAL_ALIGNMENT_LEFT) -> void:
	draw_string(ThemeDB.fallback_font, position, text, alignment, width, font_size(size), color)

func draw_bar(rect_value: Rect2, value: float, color: Color, label: String) -> void:
	draw_rect(rect_value, Color("536057"))
	draw_rect(Rect2(rect_value.position, Vector2(rect_value.size.x * clampf(value / 100.0, 0.0, 1.0), rect_value.size.y)), color)
	draw_text(label + " %d" % int(round(value)), rect_value.position + Vector2(5, rect_value.size.y - 4), 13, Color.WHITE)

func _draw() -> void:
	var background: Color = Color("1c251f") if bool(settings.get("high_contrast", false)) else Color("87b86b")
	draw_rect(Rect2(0, 0, 1280, 720), background)
	draw_rect(Rect2(0, 0, 1280, 102), MIST if not bool(settings.get("high_contrast", false)) else Color("f5f5f5"))
	draw_rect(Rect2(0, 620, 1280, 100), WOOD)
	draw_world()
	draw_hud()
	if mode == "menu":
		draw_menu()
	elif not overlay.is_empty():
		draw_overlay()

func draw_world() -> void:
	for diagonal: int in range(W + H - 1):
		for y: int in range(H):
			var x: int = diagonal - y
			if x < 0 or x >= W:
				continue
			var cell: Vector2i = Vector2i(x, y)
			var tile: Dictionary = dictionary_value(tiles, tile_key(cell))
			var position: Vector2 = iso(Vector2(cell))
			var texture_index: int = 0
			if bool(tile.get("farm", false)):
				texture_index = 1
				if bool(tile.get("tilled", false)):
					var moisture: float = float_value(tile, "moisture")
					texture_index = 4 if moisture >= 86.0 else (3 if moisture >= 38.0 else 2)
					if float_value(tile, "fertility") < 30.0:
						texture_index = 8
			draw_texture(terrain[texture_index], position - Vector2(64, 32))
			var crop_id: String = string_value(tile, "crop")
			if not crop_id.is_empty():
				var frames_value: Variant = crop_textures.get(crop_id, [])
				if frames_value is Array:
					var frames: Array = frames_value as Array
					var state: int = crop_visual_state(tile)
					var bob: float = 0.0 if bool(settings.get("reduced_motion", false)) else sin(Time.get_ticks_msec() * 0.003 + x + y) * 1.5
					draw_texture(frames[state] as Texture2D, position - Vector2(32, 58 + bob))
			if float_value(tile, "pest") >= 38.0:
				draw_texture_rect(creature_textures[(x + y) % 4], Rect2(position + Vector2(18, -38), Vector2(24, 24)), false)
			elif float_value(tile, "beneficial") >= 35.0:
				draw_texture_rect(creature_textures[4 + (x + y) % 6], Rect2(position + Vector2(18, -38), Vector2(24, 24)), false)
			if cell == selected and mode == "game":
				draw_texture(selector_texture, position - Vector2(64, 32))
	# Original buildings and NPCs anchor the educational spaces.
	draw_texture(building_textures[0], iso(Vector2(8.2, 0.4)) - Vector2(64, 105))
	draw_texture(building_textures[1], iso(Vector2(9.0, 2.1)) - Vector2(64, 105))
	draw_texture(building_textures[2], iso(Vector2(9.0, 4.1)) - Vector2(64, 105))
	draw_texture(building_textures[5], iso(Vector2(8.4, 6.1)) - Vector2(64, 105))
	draw_texture(teacher_frames[int(Time.get_ticks_msec() / 450) % teacher_frames.size()], iso(Vector2(7.8, 2.6)) - Vector2(32, 58))
	var player_bob: float = 0.0 if bool(settings.get("reduced_motion", false)) else sin(Time.get_ticks_msec() * 0.008) * 1.0
	draw_texture(player_frames[clampi(player_frame, 0, player_frames.size() - 1)], iso(player_position) - Vector2(32, 58 + player_bob))
	draw_weather_effect()

func draw_weather_effect() -> void:
	if current_weather in ["light_rain", "heavy_rain", "storm"]:
		var count: int = 20 if current_weather == "light_rain" else 42
		for index: int in range(count):
			var x: float = float((index * 89 + int(Time.get_ticks_msec() / 8)) % 980) + 245.0
			var y: float = float((index * 47 + int(Time.get_ticks_msec() / 5)) % 500) + 105.0
			draw_line(Vector2(x, y), Vector2(x - 5, y + 12), BLUE, 2.0)
	elif current_weather == "fog":
		draw_rect(Rect2(240, 105, 760, 500), Color(0.9, 0.95, 0.94, 0.16))

func draw_hud() -> void:
	var hour: int = int(minutes / 60.0)
	var minute: int = int(minutes) % 60
	draw_text("%s %d   %02d:%02d   %s: %s   %s: %s" % [translate("ui.day"), day, hour, minute, translate("ui.season"), season_name(current_season), translate("ui.weather"), weather_name(current_weather)], Vector2(22, 34), 21)
	draw_text("%s %d   %s %d   %s %d   %s %d" % [translate("ui.money"), money, translate("ui.knowledge"), knowledge, translate("ui.eco"), eco_score, translate("lab.water"), int(round(water_used))], Vector2(22, 68), 18, GREEN)
	var weather_index: int = ["clear","cloudy","light_rain","heavy_rain","windy","hot","cool","storm","fog"].find(current_weather)
	if weather_index >= 0:
		draw_texture_rect(weather_textures[weather_index], Rect2(915, 16, 48, 48), false)
	draw_text("x%d%s  | F1-F3: %d" % [speed, " ⏸" if paused else "", save_slot], Vector2(980, 36), 18)
	draw_text("1-5: %s" % crop_name(selected_seed), Vector2(980, 69), 15)
	panel(Rect2(12, 112, 226, 215), CREAM)
	draw_text(translate("ui.quest"), Vector2(28, 142), 19, INK)
	var quests: Array = array_value(data, "quests")
	if quest_index < quests.size():
		var quest: Dictionary = quests[quest_index] as Dictionary
		draw_text(translate(string_value(quest, "title_key")), Vector2(28, 172), 16, GREEN, 194.0)
		var steps: Array = array_value(quest, "steps")
		for index: int in range(steps.size()):
			var marker: String = "✓" if index < quest_step else ("▶" if index == quest_step else "○")
			draw_text("%s %d/%d" % [marker, index + 1, steps.size()], Vector2(34, 207 + index * 25), 14, TEAL if index < quest_step else INK)
	else:
		draw_text("✓ %s" % translate("msg.quest_done"), Vector2(28, 182), 17, TEAL)
	draw_text("🧪 %d%%  ♻ %d" % [int(round(compost_progress)), organic_waste], Vector2(28, 307), 14, INK)
	panel(Rect2(1010, 112, 258, 370), CREAM)
	draw_inspector()
	for button_data: Dictionary in buttons:
		var id_value: String = String(button_data.get("id", ""))
		var rect_value: Rect2 = button_data.get("rect", Rect2()) as Rect2
		var active: bool = id_value == selected_tool
		panel(rect_value, GOLD if active else CREAM)
		var texture_value: Variant = button_data.get("icon")
		if texture_value is Texture2D:
			draw_texture_rect(texture_value as Texture2D, Rect2(rect_value.position + Vector2(10, 3), Vector2(rect_value.size.x - 20, rect_value.size.y - 23)), false)
		if rect_value.size.x >= 56.0:
			draw_text(translate(String(button_data.get("label", ""))), rect_value.position + Vector2(3, rect_value.size.y - 4), 10, INK, rect_value.size.x - 6, HORIZONTAL_ALIGNMENT_CENTER)
	if message_time > 0.0:
		panel(Rect2(275, 570, 690, 44), MIST)
		draw_text(message, Vector2(292, 599), 17, INK, 655.0, HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("%s: %s | %s %d | %s %d" % [translate("ui.inventory"), crop_name(selected_seed), translate("tool.compost"), int(inventory.get("compost", 0)), translate("tool.bio"), int(inventory.get("bio_spray", 0))], Vector2(12, 612), 14, CREAM)

func draw_inspector() -> void:
	var tile: Dictionary = dictionary_value(tiles, tile_key(selected))
	draw_text("(%d,%d)" % [selected.x, selected.y], Vector2(1028, 142), 18)
	draw_bar(Rect2(1028, 158, 218, 22), float_value(tile, "moisture"), BLUE, translate("tool.water"))
	draw_bar(Rect2(1028, 188, 218, 22), float_value(tile, "light"), GOLD, translate("ui.weather"))
	draw_bar(Rect2(1028, 218, 218, 22), float_value(tile, "fertility"), GREEN, translate("status.poor_soil"))
	draw_bar(Rect2(1028, 248, 218, 22), float_value(tile, "health"), TEAL, translate("status.healthy"))
	var crop_id: String = string_value(tile, "crop")
	draw_text(crop_name(crop_id) if not crop_id.is_empty() else translate("status.empty"), Vector2(1028, 298), 18, INK)
	draw_text("Stage %d/5  Q %d" % [int_value(tile, "stage"), int(round(float_value(tile, "quality")))], Vector2(1028, 326), 15)
	var symptom: String = GrowWiseSimulation.primary_symptom(tile)
	draw_text(translate("status." + symptom), Vector2(1028, 354), 16, RED if symptom != "healthy" else TEAL)
	if int(inventory.get("ph_meter", 0)) > 0:
		draw_text("pH %.1f  N %.0f P %.0f K %.0f" % [float_value(tile,"ph"),float_value(tile,"nitrogen"),float_value(tile,"phosphorus"),float_value(tile,"potassium")], Vector2(1028, 384), 14)
	draw_text("Pest %d  Disease %d  Weed %d" % [int(round(float_value(tile,"pest"))),int(round(float_value(tile,"disease"))),int(round(float_value(tile,"weed")))], Vector2(1028, 416), 13)
	draw_text("Helpful %d  Spacing -%d" % [int(round(float_value(tile,"beneficial"))),int(round(float_value(tile,"spacing_penalty")))], Vector2(1028, 444), 13)

func draw_menu() -> void:
	draw_rect(Rect2(0, 0, 1280, 720), Color(0.04, 0.08, 0.05, 0.78))
	panel(Rect2(385, 150, 510, 455), CREAM)
	draw_text(translate("game.title"), Vector2(420, 235), 36, GREEN, 440.0, HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("2D Isometric Learning Farm", Vector2(420, 285), 18, INK, 440.0, HORIZONTAL_ALIGNMENT_CENTER)
	var menu_items: Array[Dictionary] = [
		{"rect":Rect2(490,360,300,58),"label":"menu.new"},
		{"rect":Rect2(490,430,300,58),"label":"menu.continue"},
		{"rect":Rect2(490,500,300,58),"label":"menu.quit"}
	]
	for menu_item: Dictionary in menu_items:
		var rect_value: Rect2 = menu_item["rect"] as Rect2
		panel(rect_value, WOOD_LIGHT)
		draw_text(translate(String(menu_item["label"])), rect_value.position + Vector2(10, 38), 20, Color.WHITE, 280.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_overlay() -> void:
	draw_rect(Rect2(0, 0, 1280, 720), Color(0.03, 0.05, 0.04, 0.60))
	panel(Rect2(290, 95, 650, 530), CREAM)
	panel(Rect2(895, 115, 42, 36), RED)
	draw_text("×", Vector2(905, 143), 23, Color.WHITE, 22.0, HORIZONTAL_ALIGNMENT_CENTER)
	match overlay:
		"diagnosis": draw_diagnosis()
		"shop": draw_shop()
		"market": draw_market()
		"lab": draw_lab()
		"notebook": draw_notebook()
		"settings": draw_settings()
		"quest": draw_quest_book()
		"season_report": draw_season_report()

func draw_diagnosis() -> void:
	var tile: Dictionary = dictionary_value(tiles, tile_key(selected))
	draw_text(translate("tool.inspect"), Vector2(330, 145), 27, GREEN)
	draw_text("%s | %s" % [crop_name(string_value(tile,"crop")), translate("status." + diagnosis_actual)], Vector2(330, 185), 20)
	draw_text("Water %d  Soil %d  Light %d  Pest %d  Disease %d  Weed %d" % [int(round(float_value(tile,"moisture"))),int(round(float_value(tile,"fertility"))),int(round(float_value(tile,"light"))),int(round(float_value(tile,"pest"))),int(round(float_value(tile,"disease"))),int(round(float_value(tile,"weed")))], Vector2(330, 225), 14, INK, 560.0)
	draw_text("เลือกสาเหตุ / Choose a cause", Vector2(330, 275), 17, BLUE)
	for index: int in range(SYMPTOMS.size()):
		var rect_value: Rect2 = Rect2(345 + (index % 2) * 235, 325 + int(index / 2) * 48, 220, 38)
		panel(rect_value, GOLD if diagnosis_choice == SYMPTOMS[index] else MIST)
		draw_text(translate("status." + SYMPTOMS[index]), rect_value.position + Vector2(8, 26), 14, INK, 204.0, HORIZONTAL_ALIGNMENT_CENTER)
	if diagnosis_choice == diagnosis_actual:
		draw_text("✓ %s" % translate("msg.correct"), Vector2(350, 545), 18, TEAL, 520.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_shop() -> void:
	draw_text(translate("ui.shop"), Vector2(330, 145), 27, GREEN)
	draw_text("%s: %d" % [translate("ui.money"), money], Vector2(700, 145), 20, GOLD)
	var shop_items: Array = array_value(data, "shop")
	for index: int in range(shop_items.size()):
		var item: Dictionary = shop_items[index] as Dictionary
		var rect_value: Rect2 = Rect2(315, 205 + index * 34, 540, 29)
		panel(rect_value, MIST)
		draw_text(translate(string_value(item,"name_key")), rect_value.position + Vector2(8, 21), 13)
		draw_text("%d ×%d" % [int_value(item,"price"),int_value(item,"amount")], rect_value.position + Vector2(390, 21), 13, GOLD)
		draw_text(translate("ui.buy"), rect_value.position + Vector2(470, 21), 13, TEAL)

func draw_market() -> void:
	draw_text(translate("ui.market"), Vector2(330, 145), 27, GREEN)
	draw_text("%s: %d" % [translate("ui.money"), money], Vector2(700, 145), 20, GOLD)
	var crops: Dictionary = dictionary_value(data, "crops")
	for index: int in range(CROP_IDS.size()):
		var crop_id: String = CROP_IDS[index]
		var crop_def: Dictionary = dictionary_value(crops, crop_id)
		var amount: int = int(inventory.get("produce_" + crop_id, 0))
		var average_quality: int = int(round(float(quality_bank.get(crop_id, 0.0)) / float(maxi(amount, 1))))
		var rect_value: Rect2 = Rect2(340, 240 + index * 58, 500, 45)
		panel(rect_value, MIST)
		draw_text("%s ×%d" % [crop_name(crop_id), amount], rect_value.position + Vector2(10, 30), 16)
		draw_text("Q%d  @%d" % [average_quality,int_value(crop_def,"sell_price")], rect_value.position + Vector2(260, 30), 14, BLUE)
		draw_text(translate("ui.sell"), rect_value.position + Vector2(420, 30), 15, TEAL)

func draw_lab() -> void:
	draw_text(translate("ui.lab"), Vector2(330, 145), 27, GREEN)
	panel(Rect2(360, 195, 50, 36), MIST); draw_text("‹", Vector2(370, 222), 24, INK, 30.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(420, 195, 335, 36), GOLD); draw_text(crop_name(selected_lab_crop), Vector2(430, 221), 17, INK, 315.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(765, 195, 50, 36), MIST); draw_text("›", Vector2(775, 222), 24, INK, 30.0, HORIZONTAL_ALIGNMENT_CENTER)
	var strategies: Array[String] = ["daily","when_dry","twice_daily"]
	var labels: Array[String] = ["lab.a","lab.b","lab.c"]
	for index: int in range(3):
		var x: float = 330.0 + index * 195.0
		draw_text(translate(labels[index]), Vector2(x, 270), 14, INK, 180.0, HORIZONTAL_ALIGNMENT_CENTER)
		if experiment_results.has(strategies[index]):
			var result: Dictionary = dictionary_value(experiment_results, strategies[index])
			draw_bar(Rect2(x, 290, 175, 22), float_value(result,"growth"), GREEN, translate("lab.growth"))
			draw_bar(Rect2(x, 320, 175, 22), float_value(result,"yield"), GOLD, translate("lab.yield"))
			draw_bar(Rect2(x, 350, 175, 22), minf(100.0,float_value(result,"water")), BLUE, translate("lab.water"))
			draw_bar(Rect2(x, 380, 175, 22), float_value(result,"quality"), TEAL, translate("lab.quality"))
			draw_text("Cost %.1f" % float_value(result,"cost"), Vector2(x, 430), 14, RED, 175.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(495, 520, 220, 48), TEAL)
	draw_text(translate("ui.run"), Vector2(505, 553), 18, Color.WHITE, 200.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_notebook() -> void:
	draw_text(translate("ui.notebook"), Vector2(330, 145), 27, GREEN)
	var lessons: Array = array_value(data, "lessons")
	var line_y: float = 195.0
	for lesson_value: Variant in lessons:
		var lesson: Dictionary = lesson_value as Dictionary
		var id_value: String = string_value(lesson, "id")
		if bool(unlocked_lessons.get(id_value, false)):
			draw_text("• " + translate(string_value(lesson,"title_key")), Vector2(330, line_y), 15, INK, 560.0)
			line_y += 43.0
	if line_y <= 200.0:
		draw_text("เล่นและทดลองเพื่อปลดล็อกความรู้", Vector2(330, 205), 18, BLUE)

func draw_settings() -> void:
	draw_text(translate("ui.settings"), Vector2(330, 145), 27, GREEN)
	var setting_ids: Array[String] = ["high_contrast","reduced_motion","large_text","sound","time_in_panels"]
	var setting_labels: Array[String] = ["access.high_contrast","access.reduced_motion","access.large_text","access.sound","เวลาเดินระหว่างเปิดหน้าต่าง"]
	for index: int in range(setting_ids.size()):
		var rect_value: Rect2 = Rect2(380, 245 + index * 55, 420, 42)
		panel(rect_value, TEAL if bool(settings.get(setting_ids[index], false)) else MIST)
		var label: String = translate(setting_labels[index]) if setting_labels[index].begins_with("access.") else setting_labels[index]
		draw_text("%s: %s" % [label, "ON" if bool(settings.get(setting_ids[index], false)) else "OFF"], rect_value.position + Vector2(10, 29), 16, Color.WHITE if bool(settings.get(setting_ids[index], false)) else INK, 400.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(480, 535, 220, 46), GOLD)
	draw_text("%s: %s" % [translate("access.language"),language.to_upper()], Vector2(490, 567), 17, INK, 200.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_quest_book() -> void:
	draw_text(translate("ui.quest"), Vector2(330, 145), 27, GREEN)
	var quests: Array = array_value(data, "quests")
	for index: int in range(quests.size()):
		var quest: Dictionary = quests[index] as Dictionary
		var status: String = "✓" if index < quest_index else ("▶" if index == quest_index else "○")
		draw_text("%s %s" % [status,translate(string_value(quest,"title_key"))], Vector2(340, 195 + index * 52), 17, TEAL if index < quest_index else INK, 540.0)

func draw_season_report() -> void:
	draw_text(translate("msg.season_report"), Vector2(330, 145), 27, GREEN)
	var rows: Array[Array] = [
		[translate("lab.yield"),int_value(season_report,"yield")],[translate("lab.water"),int_value(season_report,"water")],[translate("lab.cost"),int_value(season_report,"cost")],
		[translate("ui.money"),int_value(season_report,"revenue")],["Profit",int_value(season_report,"profit")],["Soil",int_value(season_report,"soil")],
		[translate("ui.eco"),int_value(season_report,"eco")],["Biodiversity",int_value(season_report,"biodiversity")],[translate("ui.knowledge"),int_value(season_report,"knowledge")]
	]
	for index: int in range(rows.size()):
		draw_text("%s: %s" % [String(rows[index][0]),String(rows[index][1])], Vector2(370 + (index % 2) * 270, 205 + int(index / 2) * 65), 19, INK)
