extends RefCounted
class_name GrowWiseArtFactory

static func build_atlas() -> Image:
	var image := Image.create_empty(1024, 512, false, Image.FORMAT_RGBA8)
	image.fill(Color.TRANSPARENT)
	# 128x64 isometric terrain tiles.
	_draw_iso_tile(image, 0, 0, Color("78b85a"), Color("315a3a"), 0)
	_draw_iso_tile(image, 128, 0, Color("a76d43"), Color("5e3b30"), 1)
	_draw_iso_tile(image, 256, 0, Color("a76d43"), Color("5e3b30"), 2)
	_draw_iso_tile(image, 384, 0, Color("654b43"), Color("493934"), 2)
	_draw_iso_tile(image, 512, 0, Color("42504f"), Color("293b40"), 3)
	_draw_iso_tile(image, 640, 0, Color("b99060"), Color("714831"), 1)
	_draw_iso_outline(image, 768, 0, Color("fff4d6"))
	# 64x64 tool and UI icons.
	_draw_icon(image, 0, 96, "hoe")
	_draw_icon(image, 64, 96, "water")
	_draw_icon(image, 128, 96, "seed")
	_draw_icon(image, 192, 96, "inspect")
	_draw_icon(image, 256, 96, "harvest")
	_draw_icon(image, 320, 96, "save")
	_draw_icon(image, 384, 96, "load")
	# Five growth stages for water spinach and kale.
	for stage in range(5):
		_draw_crop(image, stage * 64, 160, stage, false)
		_draw_crop(image, stage * 64, 224, stage, true)
	# Original 64x64 player frames.
	for index in range(12):
		_draw_player(image, (index % 8) * 64, 288 + int(index / 8) * 64, index)
	return image

static func _rect(image: Image, rect: Rect2i, color: Color) -> void:
	image.fill_rect(rect, color)

static func _line(image: Image, a: Vector2i, b: Vector2i, color: Color, width: int = 1) -> void:
	var x0 := a.x
	var y0 := a.y
	var x1 := b.x
	var y1 := b.y
	var dx := abs(x1 - x0)
	var sx := 1 if x0 < x1 else -1
	var dy := -abs(y1 - y0)
	var sy := 1 if y0 < y1 else -1
	var error := dx + dy
	while true:
		_rect(image, Rect2i(x0 - int(width / 2), y0 - int(width / 2), width, width), color)
		if x0 == x1 and y0 == y1:
			break
		var doubled := 2 * error
		if doubled >= dy:
			error += dy
			x0 += sx
		if doubled <= dx:
			error += dx
			y0 += sy

static func _circle(image: Image, cx: int, cy: int, radius: int, color: Color) -> void:
	for y in range(-radius, radius + 1):
		for x in range(-radius, radius + 1):
			if x * x + y * y <= radius * radius:
				image.set_pixel(cx + x, cy + y, color)

static func _diamond(image: Image, ox: int, oy: int, color: Color) -> void:
	for y in range(32):
		var half_width := min(y * 2, (31 - y) * 2)
		for x in range(64 - half_width, 65 + half_width):
			image.set_pixel(ox + x, oy + y, color)

static func _draw_iso_tile(image: Image, ox: int, oy: int, top: Color, side: Color, pattern: int) -> void:
	_diamond(image, ox, oy, top)
	for y in range(32, 48):
		var cut := (y - 32) * 2
		for x in range(cut, 64):
			image.set_pixel(ox + x, oy + y, side.darkened(0.08))
		for x in range(64, 128 - cut):
			image.set_pixel(ox + x, oy + y, side)
	_line(image, Vector2i(ox, oy + 31), Vector2i(ox + 64, oy + 63), Color("29302a"), 2)
	_line(image, Vector2i(ox + 127, oy + 31), Vector2i(ox + 64, oy + 63), Color("29302a"), 2)
	for index in range(14):
		var x := ox + 18 + (index * 37) % 92
		var y := oy + 10 + (index * 17) % 20
		if pattern == 0:
			_rect(image, Rect2i(x, y, 3, 2), Color("a7d46f"))
		elif pattern == 1:
			_rect(image, Rect2i(x, y, 4, 2), side.lightened(0.25))
		elif pattern == 2:
			_line(image, Vector2i(x - 6, y), Vector2i(x + 7, y + 2), side.darkened(0.12), 2)
		else:
			_rect(image, Rect2i(x, y, 5, 2), Color("8bc8d1"))

static func _draw_iso_outline(image: Image, ox: int, oy: int, color: Color) -> void:
	_line(image, Vector2i(ox + 64, oy), Vector2i(ox + 127, oy + 31), color, 3)
	_line(image, Vector2i(ox + 127, oy + 31), Vector2i(ox + 64, oy + 63), color, 3)
	_line(image, Vector2i(ox + 64, oy + 63), Vector2i(ox, oy + 31), color, 3)
	_line(image, Vector2i(ox, oy + 31), Vector2i(ox + 64, oy), color, 3)

static func _draw_icon(image: Image, ox: int, oy: int, kind: String) -> void:
	var ink := Color("29302a")
	var wood := Color("b97a4d")
	var metal := Color("d8e2d5")
	match kind:
		"hoe":
			_line(image, Vector2i(ox + 18, oy + 49), Vector2i(ox + 43, oy + 15), wood, 5)
			_line(image, Vector2i(ox + 34, oy + 18), Vector2i(ox + 54, oy + 22), metal, 6)
		"water":
			_rect(image, Rect2i(ox + 18, oy + 25, 28, 25), Color("4e9bb3"))
			_circle(image, ox + 46, oy + 31, 10, Color("4e9bb3"))
			_line(image, Vector2i(ox + 18, oy + 29), Vector2i(ox + 7, oy + 20), metal, 5)
		"seed":
			_rect(image, Rect2i(ox + 14, oy + 16, 36, 38), Color("f3e5c2"))
			_circle(image, ox + 32, oy + 35, 9, Color("78b85a"))
			_line(image, Vector2i(ox + 32, oy + 35), Vector2i(ox + 40, oy + 24), Color("315a3a"), 3)
		"inspect":
			_circle(image, ox + 28, oy + 27, 15, Color("4e9bb3"))
			_circle(image, ox + 28, oy + 27, 10, Color("d8e2d5"))
			_line(image, Vector2i(ox + 39, oy + 39), Vector2i(ox + 53, oy + 53), wood, 6)
		"harvest":
			_rect(image, Rect2i(ox + 12, oy + 30, 40, 23), wood)
			_line(image, Vector2i(ox + 16, oy + 31), Vector2i(ox + 25, oy + 17), ink, 3)
			_line(image, Vector2i(ox + 48, oy + 31), Vector2i(ox + 39, oy + 17), ink, 3)
			_circle(image, ox + 28, oy + 29, 8, Color("78b85a"))
		"save":
			_rect(image, Rect2i(ox + 12, oy + 10, 40, 44), Color("4e9bb3"))
			_rect(image, Rect2i(ox + 20, oy + 14, 24, 15), metal)
			_rect(image, Rect2i(ox + 22, oy + 38, 20, 12), ink)
		"load":
			_rect(image, Rect2i(ox + 10, oy + 19, 44, 34), Color("e9b84d"))
			_rect(image, Rect2i(ox + 15, oy + 14, 20, 10), Color("f3e5c2"))
			_line(image, Vector2i(ox + 32, oy + 27), Vector2i(ox + 32, oy + 45), ink, 4)
			_line(image, Vector2i(ox + 24, oy + 38), Vector2i(ox + 32, oy + 46), ink, 4)
			_line(image, Vector2i(ox + 40, oy + 38), Vector2i(ox + 32, oy + 46), ink, 4)

static func _draw_crop(image: Image, ox: int, oy: int, stage: int, kale: bool) -> void:
	var leaf := Color("4f8748") if kale else Color("78b85a")
	_circle(image, ox + 32, oy + 54, 13, Color("5e3b30"))
	if stage == 0:
		_circle(image, ox + 32, oy + 48, 3, Color("714831"))
		return
	var stem_height := 8 + stage * 6
	_line(image, Vector2i(ox + 32, oy + 51), Vector2i(ox + 32, oy + 51 - stem_height), Color("315a3a"), 3)
	var leaf_count := stage * 2 + (2 if kale and stage > 2 else 0)
	for index in range(leaf_count):
		var y := oy + 46 - int(index / 2) * 6
		var direction := -1 if index % 2 == 0 else 1
		var radius := 4 + stage + (2 if kale else 0)
		_circle(image, ox + 32 + direction * (5 + stage * 2), y, radius, Color("a7d46f") if index % 3 == 0 else leaf)
	if stage == 4 and not kale:
		_line(image, Vector2i(ox + 30, oy + 44), Vector2i(ox + 19, oy + 28), Color("315a3a"), 3)
		_line(image, Vector2i(ox + 34, oy + 44), Vector2i(ox + 47, oy + 26), Color("315a3a"), 3)

static func _draw_player(image: Image, ox: int, oy: int, index: int) -> void:
	var bob := index % 2
	var skin := Color("e8ad76")
	var hat := Color("e9b84d")
	var denim := Color("365f8c")
	var ink := Color("29302a")
	_circle(image, ox + 32, oy + 20 + bob, 13, skin)
	_rect(image, Rect2i(ox + 18, oy + 7 + bob, 28, 8), hat)
	_rect(image, Rect2i(ox + 14, oy + 13 + bob, 36, 5), hat.darkened(0.15))
	_rect(image, Rect2i(ox + 18, oy + 28 + bob, 28, 10), Color("f3e5c2"))
	_rect(image, Rect2i(ox + 23, oy + 30 + bob, 18, 24), denim)
	_rect(image, Rect2i(ox + 23, oy + 54, 7, 7), ink)
	_rect(image, Rect2i(ox + 35, oy + 54, 7, 7), ink)
	_rect(image, Rect2i(ox + 26, oy + 18 + bob, 3, 4), ink)
	_rect(image, Rect2i(ox + 36, oy + 18 + bob, 3, 4), ink)
