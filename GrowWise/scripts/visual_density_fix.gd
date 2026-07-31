extends "res://scripts/ui_layout_fix.gd"

# Adds structured scenery around the playable grid without changing farm cells,
# picking coordinates, save data or gameplay simulation.

func _ready() -> void:
	super._ready()
	print("GROWWISE_VISUAL_DENSITY_OK")

func draw_world() -> void:
	draw_world_backdrop()
	super.draw_world()
	draw_ambient_details()

func draw_world_backdrop() -> void:
	# A layered landscape replaces the large flat green field behind the map.
	var far_hills: PackedVector2Array = PackedVector2Array([
		Vector2(238, 190), Vector2(320, 128), Vector2(405, 176), Vector2(500, 116),
		Vector2(610, 172), Vector2(710, 122), Vector2(820, 172), Vector2(930, 126),
		Vector2(1010, 180), Vector2(1010, 250), Vector2(238, 250)
	])
	draw_colored_polygon(far_hills, Color("6fa25d"))

	var near_hills: PackedVector2Array = PackedVector2Array([
		Vector2(238, 232), Vector2(330, 168), Vector2(430, 218), Vector2(548, 158),
		Vector2(670, 222), Vector2(792, 160), Vector2(900, 220), Vector2(1010, 176),
		Vector2(1010, 300), Vector2(238, 300)
	])
	draw_colored_polygon(near_hills, Color("78ad62"))

	# Main garden clearing and a darker foreground bank create depth.
	var clearing: PackedVector2Array = PackedVector2Array([
		Vector2(250, 205), Vector2(470, 118), Vector2(780, 130), Vector2(995, 250),
		Vector2(985, 545), Vector2(730, 604), Vector2(420, 575), Vector2(245, 430)
	])
	draw_colored_polygon(clearing, Color("8dbc70"))

	var foreground_bank: PackedVector2Array = PackedVector2Array([
		Vector2(238, 510), Vector2(390, 548), Vector2(610, 560), Vector2(790, 535),
		Vector2(1010, 500), Vector2(1010, 584), Vector2(238, 584)
	])
	draw_colored_polygon(foreground_bank, Color("6f9d58"))

	# Distant treeline frames the garden but stays behind all interactive tiles.
	for index: int in range(13):
		var x: float = 262.0 + float(index) * 58.0
		var y: float = 190.0 + float(posmod(index * 19, 22))
		draw_distant_tree(Vector2(x, y), 0.72 + float(posmod(index, 3)) * 0.08)

	# Soft path leading from the lower entrance to the farm buildings.
	var path_points: PackedVector2Array = PackedVector2Array([
		Vector2(520, 584), Vector2(585, 530), Vector2(665, 475), Vector2(746, 420),
		Vector2(830, 360), Vector2(920, 300)
	])
	draw_polyline(path_points, Color("d8c28f"), 28.0, true)
	draw_polyline(path_points, Color("b99a68"), 2.0, true)

func draw_garden_decorations() -> void:
	draw_zone_paths()
	super.draw_garden_decorations()
	draw_border_scenery()
	draw_farm_props()
	draw_npc_activity()

func draw_zone_paths() -> void:
	# Stone walkway through the building side of the map.
	for step: int in range(7):
		var p: Vector2 = iso(Vector2(8.05, 0.85 + float(step)))
		draw_stone(p + Vector2(0, 7), 0.9 if step % 2 == 0 else 0.72)
	# A short path across the lower border links the field to crafting props.
	for step: int in range(7):
		var p: Vector2 = iso(Vector2(1.1 + float(step), 7.0))
		draw_stone(p + Vector2(0, 4), 0.74)

func draw_border_scenery() -> void:
	# Top hedge and flower border.
	for index: int in range(8):
		var hedge_pos: Vector2 = iso(Vector2(0.9 + float(index), 0.05)) + Vector2(0, -12)
		draw_bush(hedge_pos, 0.72 + float(posmod(index, 2)) * 0.08)
		if index % 2 == 0:
			draw_flower_patch(hedge_pos + Vector2(16, 10), index)

	# Bottom garden edge contains shrubs rather than an empty green strip.
	for index: int in range(7):
		var lower_pos: Vector2 = iso(Vector2(0.8 + float(index), 7.15)) + Vector2(0, 15)
		if index % 2 == 0:
			draw_bush(lower_pos, 0.78)
		else:
			draw_flower_patch(lower_pos, index)

	# Additional orchard trees complete the left-side frame.
	for index: int in range(3):
		var tree_pos: Vector2 = iso(Vector2(-0.05, 5.2 + float(index) * 0.85)) + Vector2(-44, -46)
		draw_tree(tree_pos, 0.82 + float(index) * 0.04, index)

	# Reeds, stones and water flowers make the pond feel inhabited.
	var pond: Vector2 = iso(Vector2(8.4, 0.7))
	for index: int in range(7):
		var reed_pos: Vector2 = pond + Vector2(-50 + float(index) * 16.0, 12 + float(posmod(index * 7, 12)))
		draw_reed(reed_pos, 0.8 + float(posmod(index, 3)) * 0.12)
	for index: int in range(4):
		var lily: Vector2 = pond + Vector2(-26 + float(index) * 18.0, -6 + float(posmod(index * 5, 12)))
		draw_circle(lily, 5.5, Color("315a3a"))
		draw_circle(lily + Vector2(2, -1), 2.0, Color("f3e5c2"))

func draw_farm_props() -> void:
	# Compost corner.
	var compost_pos: Vector2 = iso(Vector2(0.55, 6.55)) + Vector2(-8, 10)
	draw_circle(compost_pos + Vector2(-12, 6), 13.0, Color("5e3b30"))
	draw_circle(compost_pos + Vector2(2, 2), 16.0, Color("6f6c3f"))
	draw_circle(compost_pos + Vector2(14, 8), 11.0, Color("4f8748"))
	draw_line(compost_pos + Vector2(-22, 18), compost_pos + Vector2(24, 18), Color("714831"), 4.0)

	# Hay and crates fill the lower service edge.
	draw_hay_bale(iso(Vector2(1.55, 7.0)) + Vector2(-18, -4), 0.9)
	draw_hay_bale(iso(Vector2(2.0, 7.18)) + Vector2(8, 5), 0.72)
	draw_crate_stack(iso(Vector2(6.65, 7.0)) + Vector2(-8, -5))
	draw_barrel(iso(Vector2(5.75, 7.12)) + Vector2(0, 0))

	# Entry sign and simple arch make the bottom area a clear destination.
	var gate: Vector2 = iso(Vector2(4.25, 7.25)) + Vector2(0, 8)
	draw_rect(Rect2(gate + Vector2(-46, -32), Vector2(7, 42)), Color("714831"))
	draw_rect(Rect2(gate + Vector2(39, -32), Vector2(7, 42)), Color("714831"))
	draw_rect(Rect2(gate + Vector2(-46, -35), Vector2(92, 8)), Color("b97a4d"))
	draw_rect(Rect2(gate + Vector2(-26, -28), Vector2(52, 18)), Color("f3e5c2"))
	draw_line(gate + Vector2(-20, -17), gate + Vector2(20, -17), Color("4f8748"), 3.0)

	# Water tank and tool rack near the farm buildings.
	var tank: Vector2 = iso(Vector2(7.7, 0.25)) + Vector2(15, -28)
	draw_rect(Rect2(tank + Vector2(-16, -4), Vector2(32, 30)), Color("4e9bb3"))
	draw_rect(Rect2(tank + Vector2(-18, -8), Vector2(36, 8)), Color("8bc8d1"))
	draw_line(tank + Vector2(-12, 26), tank + Vector2(-15, 38), Color("714831"), 4.0)
	draw_line(tank + Vector2(12, 26), tank + Vector2(15, 38), Color("714831"), 4.0)

	var rack: Vector2 = iso(Vector2(7.15, 0.15)) + Vector2(-8, -12)
	draw_line(rack + Vector2(-18, 0), rack + Vector2(18, 0), Color("714831"), 5.0)
	draw_line(rack + Vector2(-14, 0), rack + Vector2(-14, 30), Color("714831"), 4.0)
	draw_line(rack + Vector2(14, 0), rack + Vector2(14, 30), Color("714831"), 4.0)
	draw_line(rack + Vector2(-4, 2), rack + Vector2(-8, 24), Color("29302a"), 3.0)
	draw_line(rack + Vector2(5, 2), rack + Vector2(10, 23), Color("4e9bb3"), 3.0)

	# A small scarecrow on the left non-farm border.
	var scarecrow: Vector2 = iso(Vector2(0.2, 4.7)) + Vector2(-18, -24)
	draw_line(scarecrow, scarecrow + Vector2(0, 52), Color("714831"), 5.0)
	draw_line(scarecrow + Vector2(-22, 18), scarecrow + Vector2(22, 18), Color("714831"), 4.0)
	draw_circle(scarecrow + Vector2(0, -3), 10.0, Color("e9b84d"))
	draw_colored_polygon(PackedVector2Array([
		scarecrow + Vector2(-17, 8), scarecrow + Vector2(17, 8),
		scarecrow + Vector2(11, 30), scarecrow + Vector2(-11, 30)
	]), Color("d77a45"))

func draw_npc_activity() -> void:
	if npc_textures.size() >= 4:
		draw_texture(npc_textures[0], iso(Vector2(8.05, 3.25)) - Vector2(32, 58))
		draw_texture(npc_textures[2], iso(Vector2(0.35, 7.05)) - Vector2(32, 58))
		draw_texture(npc_textures[3], iso(Vector2(7.55, 0.55)) - Vector2(32, 58))
	if creature_textures.size() >= 10:
		draw_texture_rect(creature_textures[5], Rect2(iso(Vector2(2.1, 0.25)) + Vector2(-12, -44), Vector2(24, 24)), false)
		draw_texture_rect(creature_textures[6], Rect2(iso(Vector2(5.9, 0.2)) + Vector2(-10, -40), Vector2(22, 22)), false)
		draw_texture_rect(creature_textures[8], Rect2(iso(Vector2(8.6, 1.05)) + Vector2(5, -28), Vector2(24, 24)), false)

func draw_ambient_details() -> void:
	# Small moving leaves and butterflies make the scene feel alive.
	var motion_time: float = 0.0 if bool(settings.get("reduced_motion", false)) else float(Time.get_ticks_msec()) * 0.001
	for index: int in range(8):
		var base_x: float = 280.0 + float(index) * 84.0
		var x: float = base_x + sin(motion_time * 0.7 + float(index)) * 12.0
		var y: float = 225.0 + float(posmod(index * 43, 270)) + cos(motion_time * 0.9 + float(index)) * 5.0
		var leaf_color: Color = Color("a7d46f") if index % 2 == 0 else Color("f3e5c2")
		draw_circle(Vector2(x, y), 2.2, leaf_color)
		draw_line(Vector2(x - 2, y), Vector2(x + 3, y + 2), leaf_color.darkened(0.18), 1.0)

func draw_distant_tree(position: Vector2, scale_value: float) -> void:
	draw_rect(Rect2(position + Vector2(-3, 12) * scale_value, Vector2(6, 22) * scale_value), Color("714831"))
	draw_circle(position, 17.0 * scale_value, Color("315a3a"))
	draw_circle(position + Vector2(-11, 5) * scale_value, 13.0 * scale_value, Color("4f8748"))
	draw_circle(position + Vector2(11, 4) * scale_value, 12.0 * scale_value, Color("78b85a"))

func draw_tree(position: Vector2, scale_value: float, variant: int) -> void:
	draw_rect(Rect2(position + Vector2(-5, 22) * scale_value, Vector2(10, 34) * scale_value), Color("714831"))
	draw_circle(position, 25.0 * scale_value, Color("315a3a"))
	draw_circle(position + Vector2(-15, 6) * scale_value, 19.0 * scale_value, Color("4f8748"))
	draw_circle(position + Vector2(15, 5) * scale_value, 18.0 * scale_value, Color("78b85a"))
	if variant % 2 == 0:
		for fruit_index: int in range(3):
			draw_circle(position + Vector2(-12 + fruit_index * 12, 5 + posmod(fruit_index * 7, 8)) * scale_value, 3.0 * scale_value, Color("d77a45"))

func draw_bush(position: Vector2, scale_value: float) -> void:
	draw_circle(position + Vector2(-10, 2) * scale_value, 12.0 * scale_value, Color("315a3a"))
	draw_circle(position + Vector2(2, -3) * scale_value, 15.0 * scale_value, Color("4f8748"))
	draw_circle(position + Vector2(14, 3) * scale_value, 11.0 * scale_value, Color("78b85a"))

func draw_flower_patch(position: Vector2, seed_value: int) -> void:
	var flower_colors: Array[Color] = [Color("f3e5c2"), Color("e9b84d"), Color("d77a45"), Color("8bc8d1")]
	for index: int in range(5):
		var offset: Vector2 = Vector2(float(posmod(seed_value * 13 + index * 17, 24) - 12), float(posmod(seed_value * 7 + index * 11, 14) - 7))
		draw_line(position + offset, position + offset + Vector2(0, 6), Color("315a3a"), 1.0)
		draw_circle(position + offset, 2.4, flower_colors[(seed_value + index) % flower_colors.size()])

func draw_stone(position: Vector2, scale_value: float) -> void:
	var points: PackedVector2Array = PackedVector2Array([
		position + Vector2(0, -8) * scale_value,
		position + Vector2(15, -1) * scale_value,
		position + Vector2(10, 7) * scale_value,
		position + Vector2(-9, 8) * scale_value,
		position + Vector2(-15, 0) * scale_value
	])
	draw_colored_polygon(points, Color("d8c28f"))
	draw_polyline(PackedVector2Array([points[0], points[1], points[2], points[3], points[4], points[0]]), Color("a98c61"), 1.0)

func draw_reed(position: Vector2, scale_value: float) -> void:
	draw_line(position, position + Vector2(0, -18) * scale_value, Color("315a3a"), 2.0)
	draw_line(position + Vector2(3, 0), position + Vector2(5, -14) * scale_value, Color("4f8748"), 2.0)
	draw_circle(position + Vector2(0, -19) * scale_value, 2.5 * scale_value, Color("714831"))

func draw_hay_bale(position: Vector2, scale_value: float) -> void:
	draw_rect(Rect2(position + Vector2(-18, -12) * scale_value, Vector2(36, 24) * scale_value), Color("e9b84d"))
	draw_line(position + Vector2(-18, -3) * scale_value, position + Vector2(18, -3) * scale_value, Color("c59639"), 2.0)
	draw_line(position + Vector2(-5, -12) * scale_value, position + Vector2(-5, 12) * scale_value, Color("b97a4d"), 2.0)
	draw_line(position + Vector2(8, -12) * scale_value, position + Vector2(8, 12) * scale_value, Color("b97a4d"), 2.0)

func draw_crate_stack(position: Vector2) -> void:
	for row: int in range(2):
		for column: int in range(2 - row):
			var rect_value: Rect2 = Rect2(position + Vector2(float(column * 24 - 12 + row * 12), float(-row * 21 - 18)), Vector2(22, 20))
			draw_rect(rect_value, Color("b97a4d"))
			draw_rect(Rect2(rect_value.position + Vector2(3, 3), rect_value.size - Vector2(6, 6)), Color("8a5a38"), false, 2.0)
			draw_line(rect_value.position + Vector2(3, 3), rect_value.end - Vector2(3, 3), Color("714831"), 1.0)

func draw_barrel(position: Vector2) -> void:
	draw_rect(Rect2(position + Vector2(-12, -22), Vector2(24, 34)), Color("8a5a38"))
	draw_line(position + Vector2(-12, -14), position + Vector2(12, -14), Color("29302a"), 2.0)
	draw_line(position + Vector2(-12, 3), position + Vector2(12, 3), Color("29302a"), 2.0)
	draw_circle(position + Vector2(0, -22), 12.0, Color("b97a4d"))
