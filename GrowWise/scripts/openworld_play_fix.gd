extends "res://scripts/openworld_layer_v2.gd"

# The Open World layer is active before the title menu is dismissed.
# Its inherited mouse handler therefore must not consume menu clicks.

func _ready() -> void:
	super._ready()
	var menu_safe: bool = not should_capture_openworld_mouse("menu", true)
	var game_capture: bool = should_capture_openworld_mouse("game", true)
	if menu_safe and game_capture:
		print("GROWWISE_MENU_PLAY_OK")
	else:
		push_error("Open World menu input routing self-test failed")

func should_capture_openworld_mouse(current_mode: String, active: bool) -> bool:
	return current_mode == "game" and active

func _unhandled_input(event: InputEvent) -> void:
	# Route title-menu clicks directly to the original menu before the
	# Open World mouse handler can consume them.
	if mode == "menu":
		if event is InputEventMouseButton:
			var mouse_event: InputEventMouseButton = event as InputEventMouseButton
			if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_LEFT:
				menu_click(mouse_event.position)
				return
		if event is InputEventKey:
			var key_event: InputEventKey = event as InputEventKey
			if key_event.pressed and not key_event.echo and key_event.keycode in [KEY_ENTER, KEY_KP_ENTER, KEY_SPACE]:
				new_game()
				mode = "game"
				openworld_active = true
				overlay = ""
				return
	super._unhandled_input(event)
