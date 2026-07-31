extends "res://scripts/machinery_layer.gd"

const GrowWiseOpenWorldSystem = preload("res://scripts/openworld_system.gd")
const WORLD_CENTER: Vector2 = Vector2(640.0, 360.0)
const WORLD_MAP_RECT: Rect2 = Rect2(190.0, 120.0, 900.0, 470.0)
const WORLD_INTERACT_DISTANCE: float = 92.0
const WORLD_RESOURCE_DISTANCE: float = 64.0

var openworld_data: Dictionary = {}
var openworld_active: bool = true
var world_position: Vector2 = Vector2.ZERO
var world_camera: Vector2 = Vector2.ZERO
var world_region_id: String = "greenfield"
var world_discovered_regions: Dictionary = {}
var world_discovered_points: Dictionary = {}
var world_collected_days: Dictionary = {}
var world_npc_last_talk: Dictionary = {}
var world_stamina: float = 100.0
var world_mount: String = "none"
var world_prompt: String = ""
var world_event: Dictionary = {}
var world_stats: Dictionary = {}
var world_quest_index: int = 0
var world_quest_completed: Dictionary = {}
var world_last_position: Vector2 = Vector2.ZERO
var world_travel_distance: float = 0.0
var world_resource_total: int = 0

func _ready() -> void:
	openworld_data = load_json("res://data/openworld.json")
	super._ready()
	var test_result: Dictionary = GrowWiseOpenWorldSystem.self_test(openworld_data)
	if bool(test_result.get("ok", false)):
		print("GROWWISE_OPENWORLD_OK")
	else:
		push_error("Open-world self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var labels: Dictionary = {
		"ui.openworld":{"th":"โลกกว้าง","en":"Open World"},
		"ui.world_map":{"th":"แผนที่โลก","en":"World Map"},
		"ui.enter_farm":{"th":"เข้าสวน","en":"Enter Farm"},
		"ui.fast_travel":{"th":"เดินทางด่วน","en":"Fast Travel"},
		"ui.interact":{"th":"โต้ตอบ","en":"Interact"},
		"ui.stamina":{"th":"พลังเดินทาง","en":"Travel Energy"}
	}
	if labels.has(key_name):
		var value: Dictionary = labels[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	world_position = GrowWiseOpenWorldSystem.start_position(openworld_data)
	world_camera = world_position
	world_last_position = world_position
	world_region_id = "greenfield"
	world_discovered_regions = {"greenfield":true}
	world_discovered_points = {"farm_gate":true}
	world_collected_days = {}
	world_npc_last_talk = {}
	world_stamina = 100.0
	world_mount = "none"
	world_prompt = ""
	world_event = GrowWiseOpenWorldSystem.active_event(openworld_data, day)
	world_stats = {"discover":1, "collect":0, "visit_lab":0, "fish":0}
	world_quest_index = 0
	world_quest_completed = {}
	world_travel_distance = 0.0
	world_resource_total = 0
	openworld_active = true
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	var kept: Array[Dictionary] = []
	for button_data: Dictionary in buttons:
		if String(button_data.get("id", "")) != "openworld_toggle":
			kept.append(button_data)
	buttons = kept
	buttons.append(button("openworld_toggle", Rect2(946, 492, 58, 52), "eco", "ui.openworld"))

func handle_button(button_id: String) -> void:
	if button_id == "openworld_toggle":
		enter_open_world()
		return
	super.handle_button(button_id)

func _process(delta: float) -> void:
	super._process(delta)
	if mode != "game" or not openworld_active:
		return
	world_camera = world_camera.lerp(world_position, clampf(delta * 5.5, 0.0, 1.0))
	update_world_discovery()
	update_world_prompt()
	update_world_quest()

func move_player(delta: float) -> void:
	if not openworld_active:
		super.move_player(delta)
		return
	if not overlay.is_empty():
		return
	var direction: Vector2 = Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_key_pressed(KEY_A): direction.x -= 1.0
	if Input.is_key_pressed(KEY_D): direction.x += 1.0
	if Input.is_key_pressed(KEY_W): direction.y -= 1.0
	if Input.is_key_pressed(KEY_S): direction.y += 1.0
	var moving: bool = direction.length() > 0.1
	if not moving:
		world_stamina = minf(100.0, world_stamina + delta * 9.0)
		return
	var sprinting: bool = Input.is_key_pressed(KEY_SHIFT) and world_stamina > 0.0
	var speed_value: float = 175.0
	if sprinting:
		speed_value = 285.0
		world_stamina = maxf(0.0, world_stamina - delta * 15.0)
	else:
		world_stamina = minf(100.0, world_stamina + delta * 3.5)
	match world_mount:
		"bicycle": speed_value *= 1.42
		"utility_cart": speed_value *= 1.78
	var old_position: Vector2 = world_position
	world_position += direction.normalized() * delta * speed_value
	var bounds: Rect2 = GrowWiseOpenWorldSystem.world_bounds(openworld_data)
	world_position.x = clampf(world_position.x, bounds.position.x + 24.0, bounds.end.x - 24.0)
	world_position.y = clampf(world_position.y, bounds.position.y + 24.0, bounds.end.y - 24.0)
	world_travel_distance += old_position.distance_to(world_position)
	animation_timer += delta
	if animation_timer >= 0.14:
		animation_timer = 0.0
		player_frame = posmod(player_frame + 1, 12)

func _unhandled_input(event: InputEvent) -> void:
	if mode == "game" and event is InputEventKey and event.pressed and not event.echo:
		if openworld_active:
			if event.keycode == KEY_TAB:
				overlay = "" if overlay == "world_map" else "world_map"
				return
			if event.keycode == KEY_E and overlay.is_empty():
				interact_open_world()
				return
			if event.keycode == KEY_T and overlay.is_empty():
				toggle_world_mount()
				return
			if event.keycode == KEY_ESCAPE and overlay == "world_map":
				overlay = ""
				return
		else:
			if event.keycode == KEY_TAB:
				enter_open_world()
				return
	if openworld_active and event is InputEventMouseButton and event.pressed:
		var mouse_event: InputEventMouseButton = event as InputEventMouseButton
		if overlay == "world_map":
			handle_world_map_click(mouse_event.position)
			return
		if overlay.is_empty():
			handle_world_hud_click(mouse_event.position)
			return
	super._unhandled_input(event)

func enter_open_world() -> void:
	openworld_active = true
	overlay = ""
	if world_position == Vector2.ZERO:
		world_position = GrowWiseOpenWorldSystem.start_position(openworld_data)
	world_camera = world_position
	notify("ออกสำรวจโลกกว้าง • กด E เพื่อโต้ตอบ", "success")

func enter_farm_view() -> void:
	openworld_active = false
	overlay = ""
	player_position = Vector2(4.5, 6.5)
	notify("เข้าสู่แปลงปลูก • กด TAB เพื่อกลับโลกกว้าง", "success")

func toggle_world_mount() -> void:
	if world_mount == "none":
		if farm_level < 2:
			notify("จักรยานปลดล็อกเมื่อสวนระดับ 2", "error")
			return
		world_mount = "bicycle"
		notify("ขึ้นจักรยาน • เดินทางเร็วขึ้น", "success")
	elif world_mount == "bicycle":
		if farm_level >= 5:
			world_mount = "utility_cart"
			notify("ใช้รถอเนกประสงค์", "success")
		else:
			world_mount = "none"
			notify("ลงจากจักรยาน", "success")
	else:
		world_mount = "none"
		notify("ลงจากรถ", "success")

func _draw() -> void:
	if mode == "game" and openworld_active:
		draw_open_world_scene()
		draw_open_world_hud()
		if overlay == "world_map":
			draw_world_map_overlay()
		elif not overlay.is_empty():
			draw_overlay()
		return
	super._draw()

func world_to_screen(position_value: Vector2) -> Vector2:
	return position_value - world_camera + WORLD_CENTER

func screen_rect_from_world(rect_value: Rect2) -> Rect2:
	return Rect2(world_to_screen(rect_value.position), rect_value.size)

func color_from_hex(value: String, fallback: Color) -> Color:
	var text_value: String = value
	if not text_value.begins_with("#"):
		text_value = "#" + text_value
	return Color.from_string(text_value, fallback)

func draw_open_world_scene() -> void:
	draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color("9acb76"))
	draw_world_regions()
	draw_world_waterways()
	draw_world_roads()
	draw_world_resources()
	draw_world_points()
	draw_world_npcs()
	draw_world_wildlife()
	draw_world_player()
	draw_world_weather()
	draw_world_light_tint()

func draw_world_regions() -> void:
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "regions"):
		var region: Dictionary = value as Dictionary
		var rect_value: Rect2 = GrowWiseOpenWorldSystem.rect_from(GrowWiseOpenWorldSystem.dictionary_value(region, "rect"))
		var screen_rect: Rect2 = screen_rect_from_world(rect_value)
		if not screen_rect.intersects(Rect2(-200.0, -200.0, 1680.0, 1120.0)):
			continue
		var base_color: Color = color_from_hex(String(region.get("color", "7fbf66")), Color("7fbf66"))
		draw_rect(screen_rect, base_color)
		draw_rect(screen_rect.grow(-12.0), base_color.lightened(0.06), false, 3.0)
		var label_position: Vector2 = screen_rect.position + Vector2(28.0, 42.0)
		if screen_rect.has_point(label_position):
			draw_text(world_localized(region, "name"), label_position, 18, Color(1.0, 1.0, 1.0, 0.55), 360.0)

func draw_world_roads() -> void:
	for route_value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "roads"):
		var route: Array = route_value as Array
		var points: PackedVector2Array = PackedVector2Array()
		for point_value: Variant in route:
			var pair: Array = point_value as Array
			if pair.size() >= 2:
				points.append(world_to_screen(Vector2(float(pair[0]), float(pair[1]))))
		if points.size() >= 2:
			draw_polyline(points, Color("806c52"), 26.0, true)
			draw_polyline(points, Color("c4a873"), 18.0, true)
			draw_polyline(points, Color(1.0, 0.95, 0.75, 0.35), 2.0, true)

func draw_world_waterways() -> void:
	for route_value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "waterways"):
		var route: Array = route_value as Array
		var points: PackedVector2Array = PackedVector2Array()
		for point_value: Variant in route:
			var pair: Array = point_value as Array
			if pair.size() >= 2:
				points.append(world_to_screen(Vector2(float(pair[0]), float(pair[1]))))
		if points.size() >= 2:
			draw_polyline(points, Color("3c7e98"), 58.0, true)
			draw_polyline(points, Color("69b6c5"), 46.0, true)
			draw_polyline(points, Color(1.0, 1.0, 1.0, 0.25), 5.0, true)

func draw_world_resources() -> void:
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "resources"):
		var resource: Dictionary = value as Dictionary
		var resource_id: String = String(resource.get("id", ""))
		if int(world_collected_days.get(resource_id, -999)) == day:
			continue
		var world_value: Vector2 = Vector2(float(resource.get("x", 0.0)), float(resource.get("y", 0.0)))
		var screen_value: Vector2 = world_to_screen(world_value)
		if not Rect2(-60.0, -60.0, 1400.0, 840.0).has_point(screen_value):
			continue
		var resource_type: String = String(resource.get("type", "fiber"))
		var resource_color: Color = Color("6f9a50")
		match resource_type:
			"wood": resource_color = Color("6d4d34")
			"stone": resource_color = Color("858783")
			"scrap": resource_color = Color("7c8588")
			"mineral": resource_color = Color("b78bd1")
			"herb": resource_color = Color("57a55a")
			"fiber": resource_color = Color("c0b06b")
			"clay": resource_color = Color("b66f4d")
			"seed": resource_color = Color("e0b847")
		draw_circle(screen_value + Vector2(3.0, 8.0), 17.0, Color(0.1, 0.15, 0.1, 0.28))
		draw_circle(screen_value, 14.0, resource_color)
		draw_circle(screen_value - Vector2(4.0, 4.0), 4.0, resource_color.lightened(0.35))

func building_texture_index(point_type: String) -> int:
	match point_type:
		"farm": return 0
		"machinery": return 1
		"animals": return 2
		"town": return 3
		"shop", "market": return 4
		"lab", "survey": return 5
		"dock", "water": return 6
		"processing", "mine", "forest": return 7
	return 0

func draw_world_points() -> void:
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "points"):
		var point: Dictionary = value as Dictionary
		var point_position: Vector2 = GrowWiseOpenWorldSystem.point_position(point)
		var screen_value: Vector2 = world_to_screen(point_position)
		if not Rect2(-150.0, -150.0, 1580.0, 1020.0).has_point(screen_value):
			continue
		var type_value: String = String(point.get("type", "farm"))
		var texture_index: int = building_texture_index(type_value)
		if texture_index >= 0 and texture_index < building_textures.size():
			draw_texture_rect(building_textures[texture_index], Rect2(screen_value - Vector2(48.0, 80.0), Vector2(96.0, 96.0)), false)
		else:
			draw_circle(screen_value, 28.0, GOLD)
		var point_id: String = String(point.get("id", ""))
		var discovered: bool = bool(world_discovered_points.get(point_id, false))
		if discovered or world_position.distance_to(point_position) < 190.0:
			draw_text(world_localized(point, "name"), screen_value + Vector2(-90.0, 32.0), 13, CREAM, 180.0, HORIZONTAL_ALIGNMENT_CENTER)
		if bool(point.get("fast_travel", false)) and discovered:
			draw_circle(screen_value + Vector2(34.0, -45.0), 7.0, GOLD)

func draw_world_npcs() -> void:
	var npc_values: Array = GrowWiseOpenWorldSystem.array_value(openworld_data, "npcs")
	for index: int in range(npc_values.size()):
		var npc: Dictionary = npc_values[index] as Dictionary
		var npc_world: Vector2 = GrowWiseOpenWorldSystem.npc_position(npc, minutes)
		var npc_screen: Vector2 = world_to_screen(npc_world)
		if not Rect2(-80.0, -80.0, 1440.0, 880.0).has_point(npc_screen):
			continue
		var texture_index: int = int(npc.get("texture", index))
		if texture_index >= 0 and texture_index < npc_textures.size():
			draw_texture_rect(npc_textures[texture_index], Rect2(npc_screen - Vector2(24.0, 48.0), Vector2(48.0, 48.0)), false)
		draw_text(world_localized(npc, "name"), npc_screen + Vector2(-60.0, 18.0), 11, CREAM, 120.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_world_wildlife() -> void:
	if creature_textures.is_empty():
		return
	var wildlife_positions: Array[Vector2] = [
		Vector2(-280.0, -630.0), Vector2(420.0, -810.0), Vector2(-1130.0, 420.0),
		Vector2(-380.0, 820.0), Vector2(980.0, -610.0), Vector2(360.0, 260.0)
	]
	for index: int in range(wildlife_positions.size()):
		var offset_value: Vector2 = Vector2(sin(minutes * 0.012 + float(index)) * 28.0, cos(minutes * 0.009 + float(index)) * 14.0)
		var screen_value: Vector2 = world_to_screen(wildlife_positions[index] + offset_value)
		if Rect2(-50.0, -50.0, 1380.0, 820.0).has_point(screen_value):
			var texture_index: int = posmod(index + day, creature_textures.size())
			draw_texture_rect(creature_textures[texture_index], Rect2(screen_value - Vector2(18.0, 26.0), Vector2(36.0, 36.0)), false)

func draw_world_player() -> void:
	var shadow_size: float = 22.0 if world_mount == "none" else 32.0
	draw_ellipse(WORLD_CENTER + Vector2(0.0, 19.0), Vector2(shadow_size, 9.0), Color(0.08, 0.12, 0.08, 0.35))
	if world_mount == "bicycle":
		draw_circle(WORLD_CENTER + Vector2(-16.0, 14.0), 10.0, INK, false, 3.0)
		draw_circle(WORLD_CENTER + Vector2(16.0, 14.0), 10.0, INK, false, 3.0)
		draw_line(WORLD_CENTER + Vector2(-16.0, 14.0), WORLD_CENTER + Vector2(0.0, -3.0), TEAL, 3.0)
		draw_line(WORLD_CENTER + Vector2(0.0, -3.0), WORLD_CENTER + Vector2(16.0, 14.0), TEAL, 3.0)
	elif world_mount == "utility_cart":
		draw_rect(Rect2(WORLD_CENTER + Vector2(-31.0, 4.0), Vector2(62.0, 22.0)), WOOD_LIGHT)
		draw_circle(WORLD_CENTER + Vector2(-22.0, 26.0), 8.0, INK)
		draw_circle(WORLD_CENTER + Vector2(22.0, 26.0), 8.0, INK)
	if not player_frames.is_empty():
		var texture_index: int = posmod(player_frame, player_frames.size())
		draw_texture_rect(player_frames[texture_index], Rect2(WORLD_CENTER - Vector2(28.0, 50.0), Vector2(56.0, 56.0)), false)

func draw_ellipse(center_value: Vector2, radii: Vector2, color_value: Color) -> void:
	var points: PackedVector2Array = PackedVector2Array()
	for index: int in range(24):
		var angle_value: float = TAU * float(index) / 24.0
		points.append(center_value + Vector2(cos(angle_value) * radii.x, sin(angle_value) * radii.y))
	draw_colored_polygon(points, color_value)

func draw_world_weather() -> void:
	if current_weather in ["light_rain", "heavy_rain", "storm"]:
		var count_value: int = 24 if current_weather == "light_rain" else 48
		for index: int in range(count_value):
			var x_value: float = float(posmod(index * 83 + day * 37, 1320)) - 20.0
			var y_value: float = float(posmod(index * 47 + int(minutes) * 3, 760)) - 20.0
			draw_line(Vector2(x_value, y_value), Vector2(x_value - 8.0, y_value + 18.0), Color(0.75, 0.9, 1.0, 0.55), 2.0)
	elif current_weather == "fog":
		draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.88, 0.92, 0.88, 0.28))

func draw_world_light_tint() -> void:
	var hour_value: float = minutes / 60.0
	if hour_value < 6.0 or hour_value >= 20.0:
		draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.04, 0.08, 0.18, 0.48))
	elif hour_value < 8.0:
		draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.95, 0.63, 0.36, 0.13))
	elif hour_value >= 17.0:
		draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.95, 0.48, 0.28, 0.16))

func draw_open_world_hud() -> void:
	var region: Dictionary = GrowWiseOpenWorldSystem.region_definition(openworld_data, world_region_id)
	panel(Rect2(18.0, 18.0, 390.0, 98.0), Color(0.10, 0.18, 0.12, 0.92))
	draw_text(world_localized(region, "name"), Vector2(34.0, 47.0), 20, CREAM, 350.0)
	draw_text("วัน %d • %02d:%02d • %s" % [day, int(minutes / 60.0), int(minutes) % 60, weather_name(current_weather)], Vector2(34.0, 72.0), 13, MIST, 350.0)
	var mount_name: String = "เดินเท้า"
	if world_mount == "bicycle": mount_name = "จักรยาน"
	if world_mount == "utility_cart": mount_name = "รถอเนกประสงค์"
	draw_text("%s %.0f/100 • %s" % [tx("ui.stamina"), world_stamina, mount_name], Vector2(34.0, 98.0), 13, GOLD, 350.0)
	draw_world_minimap(Rect2(1040.0, 20.0, 220.0, 142.0))
	panel(Rect2(18.0, 612.0, 800.0, 90.0), Color(0.09, 0.15, 0.10, 0.94))
	draw_text(current_world_quest_text(), Vector2(34.0, 640.0), 14, GOLD, 760.0)
	draw_text(world_prompt, Vector2(34.0, 669.0), 15, CREAM, 760.0)
	draw_text("WASD เดิน • SHIFT วิ่ง • E โต้ตอบ • TAB แผนที่ • T พาหนะ", Vector2(34.0, 692.0), 12, MIST, 760.0)
	panel(Rect2(840.0, 630.0, 122.0, 56.0), TEAL)
	draw_text("แผนที่\nTAB", Vector2(846.0, 651.0), 13, Color.WHITE, 110.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(970.0, 630.0, 122.0, 56.0), WOOD_LIGHT)
	draw_text("คลัง\nI", Vector2(976.0, 651.0), 13, Color.WHITE, 110.0, HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(1100.0, 630.0, 160.0, 56.0), BLUE)
	draw_text("เครื่องจักร\nM", Vector2(1106.0, 651.0), 13, Color.WHITE, 148.0, HORIZONTAL_ALIGNMENT_CENTER)
	if not world_event.is_empty():
		panel(Rect2(430.0, 20.0, 440.0, 42.0), GOLD)
		draw_text("เหตุการณ์วันนี้: %s" % world_localized(world_event, "name"), Vector2(440.0, 48.0), 14, INK, 420.0, HORIZONTAL_ALIGNMENT_CENTER)

func draw_world_minimap(rect_value: Rect2) -> void:
	panel(rect_value, Color(0.07, 0.12, 0.09, 0.92))
	var inner: Rect2 = rect_value.grow(-10.0)
	var bounds: Rect2 = GrowWiseOpenWorldSystem.world_bounds(openworld_data)
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "regions"):
		var region: Dictionary = value as Dictionary
		var region_id: String = String(region.get("id", ""))
		if not bool(world_discovered_regions.get(region_id, false)):
			continue
		var region_rect: Rect2 = GrowWiseOpenWorldSystem.rect_from(GrowWiseOpenWorldSystem.dictionary_value(region, "rect"))
		var top_left: Vector2 = GrowWiseOpenWorldSystem.map_position(region_rect.position, bounds, inner)
		var bottom_right: Vector2 = GrowWiseOpenWorldSystem.map_position(region_rect.end, bounds, inner)
		draw_rect(Rect2(top_left, bottom_right - top_left), color_from_hex(String(region.get("color", "7fbf66")), GREEN))
	var player_map: Vector2 = GrowWiseOpenWorldSystem.map_position(world_position, bounds, inner)
	draw_circle(player_map, 4.5, GOLD)
	draw_rect(inner, Color(1.0, 1.0, 1.0, 0.28), false, 1.0)

func draw_world_map_overlay() -> void:
	draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.02, 0.04, 0.03, 0.78))
	panel(Rect2(150.0, 74.0, 980.0, 560.0), Color("e8dfbf"))
	draw_text(tx("ui.world_map"), Vector2(180.0, 108.0), 24, GREEN, 880.0)
	draw_text("คลิกจุดสีทองที่ค้นพบแล้วเพื่อเดินทางด่วน", Vector2(180.0, 132.0), 13, INK, 880.0)
	var bounds: Rect2 = GrowWiseOpenWorldSystem.world_bounds(openworld_data)
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "regions"):
		var region: Dictionary = value as Dictionary
		var region_id: String = String(region.get("id", ""))
		var region_rect: Rect2 = GrowWiseOpenWorldSystem.rect_from(GrowWiseOpenWorldSystem.dictionary_value(region, "rect"))
		var top_left: Vector2 = GrowWiseOpenWorldSystem.map_position(region_rect.position, bounds, WORLD_MAP_RECT)
		var bottom_right: Vector2 = GrowWiseOpenWorldSystem.map_position(region_rect.end, bounds, WORLD_MAP_RECT)
		var fill_color: Color = Color("525a4f")
		if bool(world_discovered_regions.get(region_id, false)):
			fill_color = color_from_hex(String(region.get("color", "7fbf66")), GREEN)
		draw_rect(Rect2(top_left, bottom_right - top_left), fill_color)
		draw_rect(Rect2(top_left, bottom_right - top_left), Color(1.0, 1.0, 1.0, 0.35), false, 1.0)
		if bool(world_discovered_regions.get(region_id, false)):
			draw_text(world_localized(region, "name"), top_left + Vector2(6.0, 17.0), 11, Color.WHITE, maxf(80.0, bottom_right.x - top_left.x - 12.0))
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "points"):
		var point: Dictionary = value as Dictionary
		var point_id: String = String(point.get("id", ""))
		if not bool(point.get("fast_travel", false)) or not bool(world_discovered_points.get(point_id, false)):
			continue
		var map_position_value: Vector2 = GrowWiseOpenWorldSystem.map_position(GrowWiseOpenWorldSystem.point_position(point), bounds, WORLD_MAP_RECT)
		draw_circle(map_position_value, 9.0, GOLD)
		draw_circle(map_position_value, 12.0, INK, false, 2.0)
		draw_text(world_localized(point, "name"), map_position_value + Vector2(-65.0, 25.0), 10, INK, 130.0, HORIZONTAL_ALIGNMENT_CENTER)
	var player_map: Vector2 = GrowWiseOpenWorldSystem.map_position(world_position, bounds, WORLD_MAP_RECT)
	draw_circle(player_map, 6.0, RED)
	panel(Rect2(1040.0, 82.0, 54.0, 38.0), RED)
	draw_text("X", Vector2(1048.0, 108.0), 16, Color.WHITE, 38.0, HORIZONTAL_ALIGNMENT_CENTER)

func handle_world_hud_click(position_value: Vector2) -> void:
	if Rect2(840.0, 630.0, 122.0, 56.0).has_point(position_value):
		overlay = "world_map"
	elif Rect2(970.0, 630.0, 122.0, 56.0).has_point(position_value):
		overlay = "inventory_full"
	elif Rect2(1100.0, 630.0, 160.0, 56.0).has_point(position_value):
		overlay = "machinery"

func handle_world_map_click(position_value: Vector2) -> void:
	if Rect2(1040.0, 82.0, 54.0, 38.0).has_point(position_value):
		overlay = ""
		return
	var bounds: Rect2 = GrowWiseOpenWorldSystem.world_bounds(openworld_data)
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "points"):
		var point: Dictionary = value as Dictionary
		var point_id: String = String(point.get("id", ""))
		if not bool(point.get("fast_travel", false)) or not bool(world_discovered_points.get(point_id, false)):
			continue
		var map_position_value: Vector2 = GrowWiseOpenWorldSystem.map_position(GrowWiseOpenWorldSystem.point_position(point), bounds, WORLD_MAP_RECT)
		if position_value.distance_to(map_position_value) <= 18.0:
			var target_position: Vector2 = GrowWiseOpenWorldSystem.point_position(point)
			var distance_value: float = world_position.distance_to(target_position)
			world_position = target_position + Vector2(0.0, 72.0)
			world_camera = world_position
			minutes += maxf(5.0, distance_value / 120.0)
			while minutes >= 1440.0:
				minutes -= 1440.0
				advance_day()
			overlay = ""
			notify("เดินทางถึง %s" % world_localized(point, "name"), "success")
			return

func update_world_discovery() -> void:
	var region: Dictionary = GrowWiseOpenWorldSystem.region_at(openworld_data, world_position)
	if not region.is_empty():
		var region_id: String = String(region.get("id", ""))
		world_region_id = region_id
		if not bool(world_discovered_regions.get(region_id, false)):
			world_discovered_regions[region_id] = true
			world_stats["discover"] = world_discovered_regions.size()
			add_farm_xp(35, "ค้นพบพื้นที่ใหม่", WORLD_CENTER)
			notify("ค้นพบ: %s" % world_localized(region, "name"), "success")
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "points"):
		var point: Dictionary = value as Dictionary
		var point_id: String = String(point.get("id", ""))
		if bool(world_discovered_points.get(point_id, false)):
			continue
		if world_position.distance_to(GrowWiseOpenWorldSystem.point_position(point)) <= 150.0:
			world_discovered_points[point_id] = true
			knowledge += 1
			add_feedback("พบสถานที่: %s" % world_localized(point, "name"), WORLD_CENTER + Vector2(0.0, -80.0), GOLD)

func update_world_prompt() -> void:
	if not overlay.is_empty():
		world_prompt = ""
		return
	var point: Dictionary = GrowWiseOpenWorldSystem.nearest_point(openworld_data, world_position, WORLD_INTERACT_DISTANCE)
	if not point.is_empty():
		world_prompt = "[E] %s • %s" % [world_localized(point, "name"), String(point.get("description", ""))]
		return
	var resource: Dictionary = GrowWiseOpenWorldSystem.nearest_resource(openworld_data, world_position, WORLD_RESOURCE_DISTANCE, world_collected_days, day)
	if not resource.is_empty():
		world_prompt = "[E] เก็บทรัพยากรธรรมชาติ"
		return
	var npc: Dictionary = nearest_world_npc(82.0)
	if not npc.is_empty():
		world_prompt = "[E] พูดคุยกับ %s" % world_localized(npc, "name")
		return
	world_prompt = "เดินสำรวจเพื่อค้นพบสถานที่ ทรัพยากร และผู้คน"

func nearest_world_npc(maximum_distance: float) -> Dictionary:
	var nearest: Dictionary = {}
	var nearest_distance: float = maximum_distance
	for value: Variant in GrowWiseOpenWorldSystem.array_value(openworld_data, "npcs"):
		var npc: Dictionary = value as Dictionary
		var npc_position: Vector2 = GrowWiseOpenWorldSystem.npc_position(npc, minutes)
		var distance_value: float = world_position.distance_to(npc_position)
		if distance_value <= nearest_distance:
			nearest_distance = distance_value
			nearest = npc
	return nearest

func interact_open_world() -> void:
	var point: Dictionary = GrowWiseOpenWorldSystem.nearest_point(openworld_data, world_position, WORLD_INTERACT_DISTANCE)
	if not point.is_empty():
		interact_world_point(point)
		return
	var resource: Dictionary = GrowWiseOpenWorldSystem.nearest_resource(openworld_data, world_position, WORLD_RESOURCE_DISTANCE, world_collected_days, day)
	if not resource.is_empty():
		collect_world_resource(resource)
		return
	var npc: Dictionary = nearest_world_npc(82.0)
	if not npc.is_empty():
		talk_world_npc(npc)
		return
	notify("ยังไม่มีสิ่งที่โต้ตอบได้ใกล้จุดนี้", "error")

func interact_world_point(point: Dictionary) -> void:
	var required_level: int = int(point.get("level", 1))
	if farm_level < required_level:
		notify("พื้นที่นี้ต้องมีระดับสวน %d" % required_level, "error")
		return
	var point_id: String = String(point.get("id", ""))
	world_discovered_points[point_id] = true
	var action_value: String = String(point.get("action", ""))
	match action_value:
		"enter_farm": enter_farm_view()
		"machinery": overlay = "machinery"
		"animals": overlay = "animals"
		"town": overlay = "town"
		"shop": overlay = "shop"
		"market": overlay = "market"
		"lab":
			overlay = "lab"
			world_stats["visit_lab"] = 1
		"fishing": open_fishing()
		"water": overlay = "water"
		"explore": overlay = "explore"
		"survey": overlay = "survey"
		"processing": overlay = "processing"
		_: notify("สถานที่นี้ยังอยู่ระหว่างพัฒนา", "error")

func collect_world_resource(resource: Dictionary) -> void:
	var resource_id: String = String(resource.get("id", ""))
	var resource_type: String = String(resource.get("type", "fiber"))
	var reward: Dictionary = GrowWiseOpenWorldSystem.resource_reward(resource_type, day, resource_id)
	var amount_value: int = int(reward.get("amount", 1))
	if String(world_event.get("id", "")) == "forest_bloom" and resource_type == "herb":
		amount_value += 2
	var item_id: String = String(reward.get("item", "fiber"))
	inventory[item_id] = int(inventory.get(item_id, 0)) + amount_value
	world_collected_days[resource_id] = day
	world_resource_total += amount_value
	world_stats["collect"] = int(world_stats.get("collect", 0)) + amount_value
	add_farm_xp(4 + amount_value, "สำรวจทรัพยากร", WORLD_CENTER)
	add_feedback("+%d %s" % [amount_value, String(reward.get("name", item_id))], WORLD_CENTER + Vector2(0.0, -70.0), GOLD)

func talk_world_npc(npc: Dictionary) -> void:
	var npc_id: String = String(npc.get("id", "npc"))
	var already_talked: bool = int(world_npc_last_talk.get(npc_id, -1)) == day
	var dialogue: String = ""
	match npc_id:
		"teacher": dialogue = "ลองสังเกตดิน น้ำ และสภาพอากาศก่อนเลือกพืชนะ"
		"merchant": dialogue = "ตลาดต้องการสินค้าคุณภาพสูงและของแปรรูปมากขึ้น"
		"researcher": dialogue = "เก็บตัวอย่างจากหลายพื้นที่ แล้วเปรียบเทียบผลแล็บ"
		"fisher": dialogue = "สภาพอากาศและฤดูกาลมีผลต่อปลาที่พบ"
		"ranger": dialogue = "เก็บทรัพยากรได้ แต่อย่าลืมรักษาความหลากหลายของป่า"
		"engineer": dialogue = "น้ำมากให้ระบายลงบ่อ น้ำขาดค่อยส่งกลับเข้าแปลง"
		_: dialogue = "ยินดีต้อนรับสู่ชุมชน GrowWise"
	if not already_talked:
		world_npc_last_talk[npc_id] = day
		knowledge += 1
		if npc_id == "fisher":
			inventory["bait"] = int(inventory.get("bait", 0)) + 1
			dialogue += " • รับเหยื่อตกปลา 1 ชิ้น"
		add_farm_xp(5, "พูดคุยกับชุมชน", WORLD_CENTER)
	notify("%s: %s" % [world_localized(npc, "name"), dialogue], "success")

func resolve_fishing() -> void:
	var before_value: int = total_fish_inventory()
	super.resolve_fishing()
	var after_value: int = total_fish_inventory()
	if after_value > before_value:
		world_stats["fish"] = int(world_stats.get("fish", 0)) + after_value - before_value

func total_fish_inventory() -> int:
	var total_value: int = 0
	for key_value: Variant in inventory.keys():
		var item_id: String = String(key_value)
		if item_id.begins_with("fish_"):
			total_value += int(inventory.get(item_id, 0))
	return total_value

func update_world_quest() -> void:
	var quests: Array = GrowWiseOpenWorldSystem.array_value(openworld_data, "quests")
	if world_quest_index < 0 or world_quest_index >= quests.size():
		return
	var quest: Dictionary = quests[world_quest_index] as Dictionary
	var quest_id: String = String(quest.get("id", ""))
	if bool(world_quest_completed.get(quest_id, false)):
		return
	var kind_value: String = String(quest.get("kind", "discover"))
	var target_value: int = int(quest.get("target", 1))
	var progress_value: int = int(world_stats.get(kind_value, 0))
	if progress_value < target_value:
		return
	world_quest_completed[quest_id] = true
	var reward_money: int = int(quest.get("reward_money", 0))
	var reward_xp: int = int(quest.get("reward_xp", 0))
	money += reward_money
	add_farm_xp(reward_xp, "ภารกิจโลกกว้าง", WORLD_CENTER)
	notify("สำเร็จ: %s • +%d เงิน" % [world_localized(quest, "title"), reward_money], "success")
	world_quest_index += 1

func current_world_quest_text() -> String:
	var quests: Array = GrowWiseOpenWorldSystem.array_value(openworld_data, "quests")
	if world_quest_index < 0 or world_quest_index >= quests.size():
		return "ภารกิจโลกกว้างครบแล้ว • สำรวจและพัฒนาชุมชนต่อได้อย่างอิสระ"
	var quest: Dictionary = quests[world_quest_index] as Dictionary
	var kind_value: String = String(quest.get("kind", "discover"))
	var target_value: int = int(quest.get("target", 1))
	var progress_value: int = mini(target_value, int(world_stats.get(kind_value, 0)))
	return "ภารกิจ: %s • %d/%d" % [world_localized(quest, "title"), progress_value, target_value]

func world_localized(source: Dictionary, prefix: String) -> String:
	var key_name: String = prefix + ("_th" if language == "th" else "_en")
	var fallback_key: String = prefix + "_th"
	return String(source.get(key_name, source.get(fallback_key, source.get(prefix, ""))))

func advance_day() -> void:
	super.advance_day()
	world_stamina = 100.0
	world_event = GrowWiseOpenWorldSystem.active_event(openworld_data, day)
	if not world_event.is_empty():
		notify("เหตุการณ์โลก: %s" % world_localized(world_event, "name"), "success")

func save_game(slot_number: int, automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number, automatic)
	if not result:
		return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR, slot_number]
	var payload: Dictionary = read_save(path)
	if payload.is_empty():
		return result
	payload["openworld_active"] = openworld_active
	payload["world_position"] = {"x":world_position.x, "y":world_position.y}
	payload["world_region_id"] = world_region_id
	payload["world_discovered_regions"] = world_discovered_regions
	payload["world_discovered_points"] = world_discovered_points
	payload["world_collected_days"] = world_collected_days
	payload["world_npc_last_talk"] = world_npc_last_talk
	payload["world_stamina"] = world_stamina
	payload["world_mount"] = world_mount
	payload["world_stats"] = world_stats
	payload["world_quest_index"] = world_quest_index
	payload["world_quest_completed"] = world_quest_completed
	payload["world_travel_distance"] = world_travel_distance
	payload["world_resource_total"] = world_resource_total
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
	openworld_active = bool(payload.get("openworld_active", true))
	var position_data: Dictionary = dictionary_value(payload, "world_position", {})
	world_position = Vector2(float(position_data.get("x", -180.0)), float(position_data.get("y", 180.0)))
	world_camera = world_position
	world_region_id = string_value(payload, "world_region_id", "greenfield")
	world_discovered_regions = dictionary_value(payload, "world_discovered_regions", {"greenfield":true})
	world_discovered_points = dictionary_value(payload, "world_discovered_points", {"farm_gate":true})
	world_collected_days = dictionary_value(payload, "world_collected_days", {})
	world_npc_last_talk = dictionary_value(payload, "world_npc_last_talk", {})
	world_stamina = float_value(payload, "world_stamina", 100.0)
	world_mount = string_value(payload, "world_mount", "none")
	world_stats = dictionary_value(payload, "world_stats", {"discover":1, "collect":0, "visit_lab":0, "fish":0})
	world_quest_index = int_value(payload, "world_quest_index", 0)
	world_quest_completed = dictionary_value(payload, "world_quest_completed", {})
	world_travel_distance = float_value(payload, "world_travel_distance", 0.0)
	world_resource_total = int_value(payload, "world_resource_total", 0)
	world_event = GrowWiseOpenWorldSystem.active_event(openworld_data, day)
	build_buttons()
	return true
