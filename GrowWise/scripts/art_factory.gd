extends RefCounted
class_name GrowWiseArtFactory

const ATLAS_WIDTH: int = 2048
const ATLAS_HEIGHT: int = 1152
const INK: Color = Color("29302a")
const SOIL: Color = Color("855239")
const SOIL_DARK: Color = Color("5e3b30")
const LEAF: Color = Color("4f8748")
const LEAF_LIGHT: Color = Color("a7d46f")
const WATER: Color = Color("4e9bb3")
const WATER_LIGHT: Color = Color("8bc8d1")
const GOLD: Color = Color("e9b84d")
const CREAM: Color = Color("f3e5c2")
const WOOD: Color = Color("714831")
const WOOD_LIGHT: Color = Color("b97a4d")

static func build_atlas() -> Image:
	var image: Image = Image.create_empty(ATLAS_WIDTH, ATLAS_HEIGHT, false, Image.FORMAT_RGBA8)
	image.fill(Color.TRANSPARENT)
	var terrain: Array[Dictionary] = [
		{"top":Color("78b85a"),"side":Color("315a3a"),"pattern":0},
		{"top":Color("a76d43"),"side":SOIL_DARK,"pattern":1},
		{"top":Color("a76d43"),"side":SOIL_DARK,"pattern":2},
		{"top":Color("654b43"),"side":Color("493934"),"pattern":2},
		{"top":Color("42504f"),"side":Color("293b40"),"pattern":3},
		{"top":Color("c18b5e"),"side":WOOD,"pattern":1},
		{"top":Color("778286"),"side":Color("4c5559"),"pattern":4},
		{"top":Color("6aa9bd"),"side":Color("315f75"),"pattern":5},
		{"top":Color("74604a"),"side":Color("49372c"),"pattern":6},
		{"top":Color("927a52"),"side":Color("5c4934"),"pattern":7}
	]
	for index: int in range(terrain.size()):
		var definition: Dictionary = terrain[index]
		_draw_iso_tile(image, index * 128, 0, definition["top"] as Color, definition["side"] as Color, int(definition["pattern"]))
	_draw_iso_outline(image, 1280, 0, Color("fff4d6"))
	var icon_kinds: Array[String] = ["hoe","water","seed","inspect","harvest","save","load","fertilize","weed","bio","compost","remove","shop","market","lab","notebook","quest","settings","money","knowledge","eco","ph","npk","language"]
	for index: int in range(icon_kinds.size()):
		_draw_icon(image, (index % 24) * 64, 96 + int(index / 24) * 64, icon_kinds[index])
	var crop_ids: Array[String] = ["water_spinach","kale","chili","tomato","cucumber"]
	for crop_index: int in range(crop_ids.size()):
		for state: int in range(12):
			_draw_crop(image, state * 64, 192 + crop_index * 64, crop_index, state)
	for frame: int in range(12):
		_draw_character(image, frame * 64, 544, 0, frame)
	for frame: int in range(4):
		_draw_character(image, frame * 64, 608, 1, frame)
	for npc: int in range(6):
		_draw_character(image, npc * 64, 672, npc + 2, 0)
	for insect: int in range(10):
		_draw_creature(image, insect * 64, 736, insect)
	for weather_index: int in range(9):
		_draw_weather(image, weather_index * 64, 800, weather_index)
	for building: int in range(8):
		_draw_building(image, building * 128, 864, building)
	return image

static func _rect(image: Image, rect: Rect2i, color: Color) -> void:
	image.fill_rect(rect, color)

static func _line(image: Image, a: Vector2i, b: Vector2i, color: Color, width: int = 1) -> void:
	var x0: int = a.x
	var y0: int = a.y
	var x1: int = b.x
	var y1: int = b.y
	var dx: int = absi(x1 - x0)
	var sx: int = 1 if x0 < x1 else -1
	var dy: int = -absi(y1 - y0)
	var sy: int = 1 if y0 < y1 else -1
	var error_value: int = dx + dy
	while true:
		_rect(image, Rect2i(x0 - int(width / 2), y0 - int(width / 2), width, width), color)
		if x0 == x1 and y0 == y1:
			break
		var doubled: int = 2 * error_value
		if doubled >= dy:
			error_value += dy
			x0 += sx
		if doubled <= dx:
			error_value += dx
			y0 += sy

static func _circle(image: Image, cx: int, cy: int, radius: int, color: Color) -> void:
	for y: int in range(-radius, radius + 1):
		for x: int in range(-radius, radius + 1):
			if x * x + y * y <= radius * radius:
				var px: int = cx + x
				var py: int = cy + y
				if px >= 0 and py >= 0 and px < image.get_width() and py < image.get_height():
					image.set_pixel(px, py, color)

static func _diamond(image: Image, ox: int, oy: int, color: Color) -> void:
	for y: int in range(32):
		var half_width: int = mini(y * 2, (31 - y) * 2)
		for x: int in range(64 - half_width, 65 + half_width):
			image.set_pixel(ox + x, oy + y, color)

static func _draw_iso_tile(image: Image, ox: int, oy: int, top: Color, side: Color, pattern: int) -> void:
	_diamond(image, ox, oy, top)
	for y: int in range(32, 48):
		var cut: int = (y - 32) * 2
		for x: int in range(cut, 64):
			image.set_pixel(ox + x, oy + y, side.darkened(0.08))
		for x: int in range(64, 128 - cut):
			image.set_pixel(ox + x, oy + y, side)
	_line(image, Vector2i(ox, oy + 31), Vector2i(ox + 64, oy + 63), INK, 2)
	_line(image, Vector2i(ox + 127, oy + 31), Vector2i(ox + 64, oy + 63), INK, 2)
	for index: int in range(16):
		var x: int = ox + 18 + (index * 37) % 92
		var y: int = oy + 9 + (index * 17) % 21
		match pattern:
			0: _rect(image, Rect2i(x, y, 3, 2), LEAF_LIGHT)
			1: _rect(image, Rect2i(x, y, 4, 2), side.lightened(0.25))
			2: _line(image, Vector2i(x - 6, y), Vector2i(x + 7, y + 2), side.darkened(0.12), 2)
			3: _rect(image, Rect2i(x, y, 5, 2), WATER_LIGHT)
			4: _circle(image, x, y, 2, Color("cbd2d3"))
			5: _line(image, Vector2i(x - 2, y - 3), Vector2i(x + 3, y + 3), Color("d8f1f4"), 1)
			6: _circle(image, x, y, 2, Color("5b3c2f"))
			_: _rect(image, Rect2i(x, y, 3, 3), Color("5e452d"))

static func _draw_iso_outline(image: Image, ox: int, oy: int, color: Color) -> void:
	_line(image, Vector2i(ox + 64, oy), Vector2i(ox + 127, oy + 31), color, 3)
	_line(image, Vector2i(ox + 127, oy + 31), Vector2i(ox + 64, oy + 63), color, 3)
	_line(image, Vector2i(ox + 64, oy + 63), Vector2i(ox, oy + 31), color, 3)
	_line(image, Vector2i(ox, oy + 31), Vector2i(ox + 64, oy), color, 3)

static func _draw_icon(image: Image, ox: int, oy: int, kind: String) -> void:
	var metal: Color = Color("d8e2d5")
	match kind:
		"hoe":
			_line(image, Vector2i(ox + 18, oy + 50), Vector2i(ox + 43, oy + 15), WOOD_LIGHT, 5)
			_line(image, Vector2i(ox + 34, oy + 18), Vector2i(ox + 54, oy + 22), metal, 6)
		"water":
			_rect(image, Rect2i(ox + 18, oy + 25, 28, 25), WATER)
			_circle(image, ox + 46, oy + 31, 10, WATER)
			_line(image, Vector2i(ox + 18, oy + 29), Vector2i(ox + 7, oy + 20), metal, 5)
		"seed":
			_rect(image, Rect2i(ox + 14, oy + 16, 36, 38), CREAM)
			_circle(image, ox + 32, oy + 35, 9, LEAF_LIGHT)
			_line(image, Vector2i(ox + 32, oy + 35), Vector2i(ox + 40, oy + 24), LEAF, 3)
		"inspect":
			_circle(image, ox + 28, oy + 27, 15, WATER)
			_circle(image, ox + 28, oy + 27, 10, Color("d8e2d5"))
			_line(image, Vector2i(ox + 39, oy + 39), Vector2i(ox + 53, oy + 53), WOOD_LIGHT, 6)
		"harvest":
			_rect(image, Rect2i(ox + 12, oy + 30, 40, 23), WOOD_LIGHT)
			_line(image, Vector2i(ox + 16, oy + 31), Vector2i(ox + 25, oy + 17), INK, 3)
			_line(image, Vector2i(ox + 48, oy + 31), Vector2i(ox + 39, oy + 17), INK, 3)
			_circle(image, ox + 28, oy + 29, 8, LEAF_LIGHT)
		"save":
			_rect(image, Rect2i(ox + 12, oy + 10, 40, 44), WATER)
			_rect(image, Rect2i(ox + 20, oy + 14, 24, 15), metal)
			_rect(image, Rect2i(ox + 22, oy + 38, 20, 12), INK)
		"load":
			_rect(image, Rect2i(ox + 10, oy + 19, 44, 34), GOLD)
			_rect(image, Rect2i(ox + 15, oy + 14, 20, 10), CREAM)
			_line(image, Vector2i(ox + 32, oy + 27), Vector2i(ox + 32, oy + 45), INK, 4)
			_line(image, Vector2i(ox + 24, oy + 38), Vector2i(ox + 32, oy + 46), INK, 4)
			_line(image, Vector2i(ox + 40, oy + 38), Vector2i(ox + 32, oy + 46), INK, 4)
		"fertilize", "compost":
			_rect(image, Rect2i(ox + 13, oy + 18, 38, 35), Color("9d7142"))
			_circle(image, ox + 31, oy + 30, 10, Color("5c3b29"))
			_circle(image, ox + 26, oy + 27, 3, LEAF_LIGHT)
			_circle(image, ox + 36, oy + 34, 3, LEAF)
		"weed":
			for index: int in range(5):
				_line(image, Vector2i(ox + 32, oy + 52), Vector2i(ox + 15 + index * 8, oy + 18 + (index % 2) * 8), LEAF, 3)
			_line(image, Vector2i(ox + 14, oy + 52), Vector2i(ox + 50, oy + 52), Color("c65a4b"), 5)
		"bio":
			_rect(image, Rect2i(ox + 22, oy + 21, 24, 34), Color("4c927e"))
			_rect(image, Rect2i(ox + 27, oy + 11, 14, 12), metal)
			_circle(image, ox + 34, oy + 39, 7, LEAF_LIGHT)
		"remove":
			_line(image, Vector2i(ox + 14, oy + 15), Vector2i(ox + 50, oy + 51), Color("c65a4b"), 6)
			_line(image, Vector2i(ox + 50, oy + 15), Vector2i(ox + 14, oy + 51), Color("c65a4b"), 6)
		"shop":
			_rect(image, Rect2i(ox + 10, oy + 24, 44, 30), WOOD_LIGHT)
			_rect(image, Rect2i(ox + 8, oy + 15, 48, 12), Color("d77a45"))
			_rect(image, Rect2i(ox + 27, oy + 36, 12, 18), CREAM)
		"market":
			_rect(image, Rect2i(ox + 8, oy + 21, 48, 34), Color("b99060"))
			_line(image, Vector2i(ox + 12, oy + 21), Vector2i(ox + 20, oy + 10), Color("c65a4b"), 5)
			_line(image, Vector2i(ox + 52, oy + 21), Vector2i(ox + 44, oy + 10), Color("c65a4b"), 5)
		"lab":
			_line(image, Vector2i(ox + 25, oy + 10), Vector2i(ox + 25, oy + 32), metal, 5)
			_line(image, Vector2i(ox + 39, oy + 10), Vector2i(ox + 39, oy + 32), metal, 5)
			_circle(image, ox + 32, oy + 42, 14, WATER)
		"notebook":
			_rect(image, Rect2i(ox + 13, oy + 12, 38, 44), CREAM)
			_line(image, Vector2i(ox + 21, oy + 12), Vector2i(ox + 21, oy + 56), Color("c65a4b"), 3)
			for line_index: int in range(4): _line(image, Vector2i(ox + 26, oy + 24 + line_index * 7), Vector2i(ox + 45, oy + 24 + line_index * 7), WATER, 2)
		"quest":
			_rect(image, Rect2i(ox + 17, oy + 10, 32, 46), CREAM)
			for line_index: int in range(3):
				_circle(image, ox + 23, oy + 24 + line_index * 10, 3, GOLD)
				_line(image, Vector2i(ox + 29, oy + 24 + line_index * 10), Vector2i(ox + 43, oy + 24 + line_index * 10), INK, 2)
		"settings":
			_circle(image, ox + 32, oy + 32, 18, metal)
			_circle(image, ox + 32, oy + 32, 8, INK)
			for index: int in range(8):
				var angle: float = TAU * float(index) / 8.0
				_circle(image, ox + 32 + int(cos(angle) * 22.0), oy + 32 + int(sin(angle) * 22.0), 4, metal)
		"money":
			_circle(image, ox + 32, oy + 32, 18, GOLD)
			_circle(image, ox + 32, oy + 32, 11, GOLD.lightened(0.25))
		"knowledge":
			_circle(image, ox + 32, oy + 27, 15, WATER_LIGHT)
			_rect(image, Rect2i(ox + 27, oy + 42, 10, 10), GOLD)
		"eco":
			_circle(image, ox + 32, oy + 33, 18, LEAF_LIGHT)
			_line(image, Vector2i(ox + 32, oy + 47), Vector2i(ox + 32, oy + 20), LEAF, 3)
		"ph":
			_circle(image, ox + 32, oy + 32, 18, Color("9333ea"))
			_line(image, Vector2i(ox + 22, oy + 38), Vector2i(ox + 42, oy + 26), CREAM, 4)
		"npk":
			for index: int in range(3): _circle(image, ox + 20 + index * 12, oy + 32, 8, [Color("4f8748"),Color("9333ea"),Color("f97316")][index])
		"language":
			_circle(image, ox + 32, oy + 32, 20, WATER)
			_line(image, Vector2i(ox + 13, oy + 32), Vector2i(ox + 51, oy + 32), CREAM, 2)
			_line(image, Vector2i(ox + 32, oy + 13), Vector2i(ox + 32, oy + 51), CREAM, 2)
		_:
			_circle(image, ox + 32, oy + 32, 16, LEAF_LIGHT)

static func _draw_crop(image: Image, ox: int, oy: int, species: int, state: int) -> void:
	var base_colors: Array[Color] = [Color("78b85a"),Color("4f8748"),Color("669a43"),Color("5c984e"),Color("69a44d")]
	var leaf_color: Color = base_colors[clampi(species, 0, base_colors.size() - 1)]
	var pale: Color = Color("d4c86a")
	var stem: Color = Color("315a3a")
	_circle(image, ox + 32, oy + 55, 12, SOIL_DARK)
	if state == 0:
		_circle(image, ox + 32, oy + 49, 3, Color("714831"))
		return
	if state == 11:
		_line(image, Vector2i(ox + 32, oy + 53), Vector2i(ox + 28, oy + 29), Color("76513e"), 3)
		_line(image, Vector2i(ox + 28, oy + 35), Vector2i(ox + 17, oy + 40), Color("8b6b4e"), 3)
		_line(image, Vector2i(ox + 29, oy + 39), Vector2i(ox + 43, oy + 44), Color("8b6b4e"), 3)
		return
	var visual_stage: int = clampi(state, 1, 5)
	var height: int = 8 + visual_stage * 6
	_line(image, Vector2i(ox + 32, oy + 52), Vector2i(ox + 32, oy + 52 - height), stem, 3)
	var leaf_count: int = visual_stage * 2 + (2 if species == 1 else 0)
	for index: int in range(leaf_count):
		var y: int = oy + 47 - int(index / 2) * 6
		var direction: int = -1 if index % 2 == 0 else 1
		var radius: int = 3 + visual_stage + (2 if species == 1 else 0)
		var color: Color = pale if state == 7 else (leaf_color.darkened(0.25) if state == 6 or state == 8 else leaf_color)
		_circle(image, ox + 32 + direction * (5 + visual_stage * 2), y, radius, color)
	if species == 0 and visual_stage >= 4:
		_line(image, Vector2i(ox + 30, oy + 44), Vector2i(ox + 16, oy + 27), stem, 3)
		_line(image, Vector2i(ox + 34, oy + 44), Vector2i(ox + 49, oy + 25), stem, 3)
	elif species == 1 and visual_stage >= 3:
		for index: int in range(5): _circle(image, ox + 21 + index * 6, oy + 35 - (index % 2) * 5, 7, leaf_color)
	elif species == 2 and visual_stage >= 4:
		_circle(image, ox + 22, oy + 34, 4, Color("d77a45"))
		_circle(image, ox + 43, oy + 40, 4, Color("c65a4b"))
	elif species == 3 and visual_stage >= 4:
		_circle(image, ox + 20, oy + 38, 6, Color("c65a4b"))
		_circle(image, ox + 44, oy + 35, 6, Color("d75a45"))
	elif species == 4 and visual_stage >= 4:
		_line(image, Vector2i(ox + 18, oy + 42), Vector2i(ox + 49, oy + 31), stem, 3)
		_rect(image, Rect2i(ox + 39, oy + 29, 13, 6), Color("78a64e"))
	if state == 6:
		_line(image, Vector2i(ox + 16, oy + 31), Vector2i(ox + 11, oy + 48), Color("8b6b4e"), 2)
	if state == 8:
		for index: int in range(3): _circle(image, ox + 19 + index * 13, oy + 56, 2, WATER_LIGHT)
	if state == 9:
		for index: int in range(5): _circle(image, ox + 18 + (index * 11) % 31, oy + 27 + (index * 7) % 20, 2, INK)
	if state == 10:
		for index: int in range(6): _circle(image, ox + 17 + (index * 13) % 34, oy + 26 + (index * 9) % 22, 2, Color("9333ea"))

static func _draw_character(image: Image, ox: int, oy: int, kind: int, frame: int) -> void:
	var bob: int = frame % 2
	var skin: Color = [Color("e8ad76"),Color("d89a65"),Color("b97a58"),Color("edbd85")][kind % 4]
	var shirt: Color = [Color("365f8c"),Color("4c927e"),Color("9333ea"),Color("d77a45"),Color("4f8748"),Color("c65a4b"),Color("6a7fb0"),Color("9a7752")][kind % 8]
	_circle(image, ox + 32, oy + 20 + bob, 13, skin)
	if kind == 0:
		_rect(image, Rect2i(ox + 18, oy + 7 + bob, 28, 8), GOLD)
		_rect(image, Rect2i(ox + 14, oy + 13 + bob, 36, 5), GOLD.darkened(0.15))
	elif kind == 1:
		_rect(image, Rect2i(ox + 19, oy + 6 + bob, 26, 8), Color("8b5b39"))
		_circle(image, ox + 20, oy + 17 + bob, 5, Color("8b5b39"))
		_circle(image, ox + 44, oy + 17 + bob, 5, Color("8b5b39"))
	else:
		_rect(image, Rect2i(ox + 20, oy + 6 + bob, 24, 7), Color("49352e"))
	_rect(image, Rect2i(ox + 18, oy + 28 + bob, 28, 12), CREAM)
	_rect(image, Rect2i(ox + 22, oy + 31 + bob, 20, 23), shirt)
	_rect(image, Rect2i(ox + 22, oy + 54, 7, 7), INK)
	_rect(image, Rect2i(ox + 36, oy + 54, 7, 7), INK)
	_rect(image, Rect2i(ox + 26, oy + 18 + bob, 3, 4), INK)
	_rect(image, Rect2i(ox + 36, oy + 18 + bob, 3, 4), INK)
	if frame >= 6:
		_line(image, Vector2i(ox + 16, oy + 38), Vector2i(ox + 8, oy + 49), WOOD_LIGHT, 4)

static func _draw_creature(image: Image, ox: int, oy: int, kind: int) -> void:
	var hostile: bool = kind < 4
	var body: Color = [Color("6f9d4f"),Color("8b6b3e"),Color("9a7e5c"),Color("f1eee3"),Color("d75a45"),Color("e9b84d"),Color("9333ea"),Color("855239"),Color("4f8748"),Color("5f7fa7")][kind]
	if kind == 2:
		_circle(image, ox + 31, oy + 38, 14, Color("9a7752"))
		_circle(image, ox + 31, oy + 38, 8, Color("6d513c"))
		_circle(image, ox + 46, oy + 45, 7, body)
	elif kind == 7:
		for index: int in range(5): _circle(image, ox + 18 + index * 7, oy + 35 + (index % 2) * 4, 5, body)
	elif kind == 8:
		_circle(image, ox + 32, oy + 38, 13, body)
		_circle(image, ox + 23, oy + 28, 7, body.lightened(0.15))
		_circle(image, ox + 41, oy + 28, 7, body.lightened(0.15))
	else:
		_circle(image, ox + 32, oy + 35, 10, body)
		_circle(image, ox + 23, oy + 29, 7, body.lightened(0.18))
		_circle(image, ox + 41, oy + 29, 7, body.lightened(0.18))
	if hostile:
		_line(image, Vector2i(ox + 17, oy + 52), Vector2i(ox + 47, oy + 52), Color("c65a4b"), 3)
	else:
		_circle(image, ox + 51, oy + 14, 4, GOLD)

static func _draw_weather(image: Image, ox: int, oy: int, kind: int) -> void:
	if kind in [0,5]:
		_circle(image, ox + 32, oy + 28, 14, GOLD if kind == 0 else Color("f97316"))
		for index: int in range(8):
			var angle: float = TAU * float(index) / 8.0
			_line(image, Vector2i(ox + 32 + int(cos(angle) * 18.0), oy + 28 + int(sin(angle) * 18.0)), Vector2i(ox + 32 + int(cos(angle) * 24.0), oy + 28 + int(sin(angle) * 24.0)), GOLD, 2)
	else:
		_circle(image, ox + 25, oy + 30, 12, Color("d8e2d5"))
		_circle(image, ox + 38, oy + 26, 14, Color("c4d0d1"))
		_rect(image, Rect2i(ox + 18, oy + 30, 34, 12), Color("c4d0d1"))
	if kind in [2,3,7]:
		var count: int = 3 if kind == 2 else 6
		for index: int in range(count): _line(image, Vector2i(ox + 18 + (index % 3) * 12, oy + 45 + int(index / 3) * 4), Vector2i(ox + 14 + (index % 3) * 12, oy + 56 + int(index / 3) * 4), WATER, 2)
	if kind == 4:
		for index: int in range(3): _line(image, Vector2i(ox + 10, oy + 44 + index * 6), Vector2i(ox + 54, oy + 41 + index * 6), WATER_LIGHT, 2)
	if kind == 6:
		_circle(image, ox + 32, oy + 52, 6, WATER_LIGHT)
	if kind == 8:
		for index: int in range(4): _line(image, Vector2i(ox + 10, oy + 45 + index * 4), Vector2i(ox + 54, oy + 45 + index * 4), Color("e5eeee"), 2)

static func _draw_building(image: Image, ox: int, oy: int, kind: int) -> void:
	var wall: Color = [Color("c18b5e"),Color("d6c18a"),Color("b99060"),Color("879c76"),Color("769aa0"),Color("aa8c68"),Color("9a7752"),Color("8f7baa")][kind]
	_rect(image, Rect2i(ox + 24, oy + 44, 80, 64), wall)
	_line(image, Vector2i(ox + 16, oy + 48), Vector2i(ox + 64, oy + 12), Color("8b4f36"), 5)
	_line(image, Vector2i(ox + 112, oy + 48), Vector2i(ox + 64, oy + 12), Color("8b4f36"), 5)
	_rect(image, Rect2i(ox + 48, oy + 70, 30, 38), WOOD)
	_rect(image, Rect2i(ox + 85, oy + 59, 14, 18), WATER_LIGHT)
	if kind == 3:
		for index: int in range(4): _rect(image, Rect2i(ox + 25 + index * 20, oy + 46, 14, 60), Color(0.55,0.8,0.82,0.7))
	if kind == 4:
		_circle(image, ox + 64, oy + 70, 22, WATER)
