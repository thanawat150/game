extends "res://scripts/agri_layer.gd"

# Final integration layer for the complete GrowWise farm-to-town build.
# A is reserved for WASD movement, so livestock is opened with H.

func _ready() -> void:
	super._ready()
	print("GROWWISE_FARM_TOWN_COMPLETE_OK")

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey:
		# Never forward A to agri_layer, where the original livestock shortcut
		# would conflict with the player's move-left control.
		if event.keycode == KEY_A:
			return
		if event.pressed and not event.echo and mode == "game" and event.keycode == KEY_H:
			overlay = "animals"
			return
	super._unhandled_input(event)

func draw_hud() -> void:
	super.draw_hud()
	# Replace the inherited shortcut hint with the conflict-free final mapping.
	draw_rect(Rect2(255, 101, 755, 20), Color(0.12, 0.18, 0.12, 0.92))
	draw_text(
		"Q สำรวจ • L แล็บ • H สัตว์ • G แปรรูป • U น้ำ • V รถ • O แต่งตัว",
		Vector2(270, 116),
		11,
		CREAM,
		720.0,
		HORIZONTAL_ALIGNMENT_CENTER
	)
