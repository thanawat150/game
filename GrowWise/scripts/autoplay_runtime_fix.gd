extends "res://scripts/full_game_main.gd"

# Runtime UX fix for autoplay:
# - one press starts Full Auto immediately
# - autoplay is available from a new game
# - closes blocking overlays and resumes time
# - performs the first action immediately
# - avoids the default seed reserve preventing all planting

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

func new_game() -> void:
	super.new_game()
	# Full Auto must be able to establish a new farm instead of preserving
	# every starting seed and appearing inactive.
	auto_rules["keep_seed"] = 0

func cycle_autoplay() -> void:
	# Put the mode users expect first. The previous order started with
	# Care Assist, which intentionally does not till or plant empty plots.
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
		notify("ฟาร์มอัตโนมัติ: ปิด", "success")
		queue_redraw()
		return

	# Autoplay is a core convenience feature and is now usable from level 1.
	# Full/Learning mode may use available seeds and restock them as needed.
	if autoplay_mode in [AutoPlayRuntime.MODE_FULL, AutoPlayRuntime.MODE_LEARNING]:
		auto_rules["keep_seed"] = 0

	paused = false
	overlay = ""
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
			notify("ออโต้กำลังรอ • กฎปัจจุบันไม่อนุญาตงานที่พบ กด R เพื่อตั้งค่า", "error")
		else:
			notify("ออโต้เปิดอยู่ • กำลังรอเงื่อนไขของแปลง", "success")
	else:
		notify("ออโต้ทำงาน: %s" % auto_action_text(action_id), "success")
	queue_redraw()
