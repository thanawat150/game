extends "res://scripts/full_game_main.gd"

# Runtime UX fix for autoplay:
# - one visible press starts Full Auto immediately
# - autoplay is available from a new game
# - closes blocking overlays and resumes time
# - performs the first action immediately
# - avoids the default seed reserve preventing all planting
# - exposes a large on-screen start/stop button and settings button

const AutoPlayRuntime = preload("res://scripts/autoplay_manager.gd")

func _ready() -> void:
	super._ready()
	var test_tiles: Dictionary = {
		"0,0": {
			"farm": true,
			"tilled": false,
			"crop": "",
			"stage": 0,
			"moisture": 20.0,
			"fertility": 60.0,
			"pest": 0.0,
			"disease": 0.0,
			"weed": 0.0,
			"dead": false
		}
	}
	var test_inventory: Dictionary = {
		"seed_water_spinach": 5,
		"produce_water_spinach": 0
	}
	var test_action: Dictionary = AutoPlayRuntime.choose_action(
		test_tiles,
		test_inventory,
		["water_spinach"],
		AutoPlayRuntime.MODE_FULL,
		1,
		100
	)
	if String(test_action.get("action", "")) == "hoe":
		print("GROWWISE_AUTOPLAY_RUNTIME_FIX_OK")
	else:
		push_error("Autoplay runtime fix self-test failed: %s" % JSON.stringify(test_action))

func tx(key_name: String) -> String:
	match key_name:
		"ui.autoplay_primary":
			if autoplay_mode == AutoPlayRuntime.MODE_OFF:
				return "เริ่มฟาร์มอัตโนมัติ" if language == "th" else "Start Auto Farm"
			return "หยุดฟาร์มอัตโนมัติ" if language == "th" else "Stop Auto Farm"
		"ui.auto_settings_visible":
			return "ตั้งค่า" if language == "th" else "Rules"
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	# Full Auto must be able to establish a new farm instead of preserving
	# every starting seed and appearing inactive.
	auto_rules["keep_seed"] = 0
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	# Replace the small, easy-to-miss autoplay control with a clear block
	# below the crop inspector. Inventory and crafting remain available.
	var updated_buttons: Array[Dictionary] = []
	for button_data: Dictionary in buttons:
		var button_id: String = String(button_data.get("id", ""))
		if button_id not in ["inventory_full", "crafting", "autoplay"]:
			updated_buttons.append(button_data)
	buttons = updated_buttons
	buttons.append(button("inventory_full", Rect2(1010, 492, 80, 52), "market", "ui.inventory_full"))
	buttons.append(button("crafting", Rect2(1095, 492, 80, 52), "compost", "ui.crafting"))
	buttons.append(button("auto_rules_visible", Rect2(1180, 492, 80, 52), "settings", "ui.auto_settings_visible"))
	buttons.append(button("autoplay_primary", Rect2(1010, 550, 250, 62), "", "ui.autoplay_primary"))

func handle_button(button_id: String) -> void:
	match button_id:
		"autoplay_primary":
			toggle_autoplay_primary()
		"auto_rules_visible":
			overlay = "auto_rules"
		_:
			super.handle_button(button_id)

func draw_hud() -> void:
	super.draw_hud()
	# The large button is already drawn by the inherited button renderer.
	# Add a live status line inside its upper half.
	var status_text: String = autoplay_status_text()
	var status_color: Color = MIST if autoplay_mode == AutoPlayRuntime.MODE_OFF else Color("c9e7b7")
	draw_rect(Rect2(1014, 554, 242, 26), status_color)
	draw_text(status_text, Vector2(1020, 573), 11, INK, 230.0, HORIZONTAL_ALIGNMENT_CENTER)

func autoplay_status_text() -> String:
	if autoplay_mode == AutoPlayRuntime.MODE_OFF:
		return "สถานะ: ปิด" if language == "th" else "Status: Off"
	var action_id: String = String(autoplay_last_action.get("action", "idle"))
	if action_id == "idle":
		if String(autoplay_last_action.get("reason", "")) == "rule":
			return "กำลังรอ: กฎไม่อนุญาต" if language == "th" else "Waiting: blocked by rules"
		return "กำลังรอเงื่อนไขของแปลง" if language == "th" else "Waiting for farm conditions"
	var short_names: Dictionary = {
		"hoe":"กำลังพรวนดิน", "seed":"กำลังปลูก", "water":"กำลังรดน้ำ",
		"harvest":"กำลังเก็บเกี่ยว", "weed":"กำลังถอนวัชพืช", "bio":"กำลังรักษาพืช",
		"fertilize":"กำลังใส่ปุ๋ย", "compost":"กำลังปรับดิน", "remove":"กำลังถอนต้นตาย",
		"sell":"กำลังขายผลผลิต", "restock":"กำลังซื้อเมล็ด"
	}
	return String(short_names.get(action_id, "ฟาร์มอัตโนมัติกำลังทำงาน"))

func toggle_autoplay_primary() -> void:
	if autoplay_mode == AutoPlayRuntime.MODE_OFF:
		start_full_autoplay()
	else:
		autoplay_mode = AutoPlayRuntime.MODE_OFF
		autoplay_timer = 0.0
		autoplay_last_action = {"action":"idle"}
		notify("ฟาร์มอัตโนมัติ: ปิด", "success")
		build_buttons()
		queue_redraw()

func start_full_autoplay() -> void:
	autoplay_mode = AutoPlayRuntime.MODE_FULL
	autoplay_timer = 0.0
	auto_rules["keep_seed"] = 0
	paused = false
	overlay = ""
	build_buttons()
	notify("ฟาร์มอัตโนมัติเต็มระบบ • เริ่มทำงานแล้ว", "success")
	queue_redraw()
	call_deferred("_run_first_autoplay_action")

func cycle_autoplay() -> void:
	# F4 remains an advanced mode switch. The visible button is a simple
	# start/stop toggle, while F4 cycles Full -> Learning -> Assist -> Off.
	match autoplay_mode:
		AutoPlayRuntime.MODE_OFF:
			autoplay_mode = AutoPlayRuntime.MODE_FULL
		AutoPlayRuntime.MODE_FULL:
			autoplay_mode = AutoPlayRuntime.MODE_LEARNING
		AutoPlayRuntime.MODE_LEARNING:
			autoplay_mode = AutoPlayRuntime.MODE_ASSIST
		_:
			autoplay_mode = AutoPlayRuntime.MODE_OFF

	autoplay_timer = 0.0
	if autoplay_mode == AutoPlayRuntime.MODE_OFF:
		autoplay_last_action = {"action":"idle"}
		notify("ฟาร์มอัตโนมัติ: ปิด", "success")
		build_buttons()
		queue_redraw()
		return

	if autoplay_mode in [AutoPlayRuntime.MODE_FULL, AutoPlayRuntime.MODE_LEARNING]:
		auto_rules["keep_seed"] = 0
	paused = false
	overlay = ""
	build_buttons()
	notify(
		"ฟาร์มอัตโนมัติ: %s • เริ่มทำงานแล้ว" % AutoPlayRuntime.mode_name(autoplay_mode, language),
		"success"
	)
	queue_redraw()
	call_deferred("_run_first_autoplay_action")

func _run_first_autoplay_action() -> void:
	if mode != "game" or autoplay_mode == AutoPlayRuntime.MODE_OFF:
		return
	run_autoplay_step()
	var action_id: String = String(autoplay_last_action.get("action", "idle"))
	if action_id == "idle":
		var reason: String = String(autoplay_last_action.get("reason", ""))
		if reason == "rule":
			notify("ออโต้กำลังรอ • กฎปัจจุบันไม่อนุญาตงานที่พบ กดปุ่มตั้งค่า", "error")
		else:
			notify("ออโต้เปิดอยู่ • กำลังรอเงื่อนไขของแปลง", "success")
	else:
		notify("ออโต้ทำงาน: %s" % auto_action_text(action_id), "success")
	queue_redraw()
