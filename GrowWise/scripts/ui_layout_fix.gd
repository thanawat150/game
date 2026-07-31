extends "res://scripts/remaster_main.gd"

# Final HUD composition layer. It keeps transient notifications and persistent
# stock information in separate rows so neither can cover dialogs or tools.

func _ready() -> void:
	super._ready()
	print("GROWWISE_UI_LAYOUT_OK")

func draw_hud() -> void:
	super.draw_hud()

	# The inherited HUD contains an older inventory sentence and the remaster
	# stock strip in the same vertical area as transient notifications. Cover
	# that legacy area, then render one clean toolbar header.
	if message_time > 0.0:
		# Remove the inherited notification panel before drawing it in its own row.
		draw_rect(Rect2(268, 566, 704, 52), Color(0.12, 0.18, 0.12, 0.94))
		panel(Rect2(275, 518, 690, 44), MIST)
		draw_text(message, Vector2(292, 548), 16, INK, 655.0, HORIZONTAL_ALIGNMENT_CENTER)

	# Dedicated toolbar header: y=584..639. Tool buttons begin at y=640.
	draw_rect(Rect2(0, 584, 1280, 56), WOOD)
	draw_line(Vector2(0, 584), Vector2(1280, 584), WOOD_LIGHT, 2.0)
	draw_line(Vector2(0, 639), Vector2(1280, 639), WOOD.darkened(0.25), 2.0)

	var selected_index: int = maxi(0, CROP_IDS.find(selected_seed))
	var next_crop: String = CROP_IDS[(selected_index + 1) % CROP_IDS.size()]
	var stock_line: String = "คลังคงเหลือ: เมล็ด %s %d | %s %d | ปุ๋ยหมัก %d | ปุ๋ยอินทรีย์ %d | สเปรย์ %d | วัตถุดิบ %d" % [
		crop_name(selected_seed),
		int(inventory.get("seed_" + selected_seed, 0)),
		crop_name(next_crop),
		int(inventory.get("seed_" + next_crop, 0)),
		int(inventory.get("compost", 0)),
		int(inventory.get("organic_fertilizer", 0)),
		int(inventory.get("bio_spray", 0)),
		material_total()
	]
	draw_text(stock_line, Vector2(14, 606), 12, CREAM, 970.0)

	var auto_name: String = GrowWiseAutoPlay.mode_name(autoplay_mode, language)
	draw_text("I คลัง | C คราฟต์ | F4 ออโต้: %s" % auto_name, Vector2(985, 606), 11, CREAM, 275.0, HORIZONTAL_ALIGNMENT_RIGHT)

	var detail_line: String = "ของที่เลือก: เมล็ด%s %d | ผลผลิต%s %d | เก็บวัตถุดิบได้อีก %d ครั้งวันนี้" % [
		crop_name(selected_seed),
		int(inventory.get("seed_" + selected_seed, 0)),
		crop_name(selected_seed),
		int(inventory.get("produce_" + selected_seed, 0)),
		forage_left
	]
	draw_text(detail_line, Vector2(14, 629), 11, CREAM, 900.0)
	draw_text("ดูแลต่อเนื่อง %d วัน • ออโต้ทำงาน %d ครั้ง" % [care_streak, autoplay_actions_total], Vector2(930, 629), 11, CREAM, 330.0, HORIZONTAL_ALIGNMENT_RIGHT)
