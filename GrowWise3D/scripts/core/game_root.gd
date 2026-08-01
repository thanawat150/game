extends Node3D
class_name GrowWise3DGameRoot

func _ready() -> void:
	print("GROWWISE3D_SCAFFOLD_OK")

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_tree().quit()
