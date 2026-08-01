extends Node3D
class_name GrowWiseCameraRig

@export_node_path("Node3D") var target_path: NodePath
@export var follow_smoothing: float = 7.0
@export var camera_offset: Vector3 = Vector3(10.0, 12.0, 10.0)
@export var min_zoom: float = 8.0
@export var max_zoom: float = 20.0

@onready var camera: Camera3D = $Camera3D
var target: Node3D

func _ready() -> void:
	target = get_node_or_null(target_path) as Node3D
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 13.0

func _process(delta: float) -> void:
	if target == null:
		return
	var desired := target.global_position + camera_offset
	global_position = global_position.lerp(desired, clampf(follow_smoothing * delta, 0.0, 1.0))
	look_at(target.global_position + Vector3(0.0, 1.0, 0.0), Vector3.UP)

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			camera.size = clampf(camera.size - 1.0, min_zoom, max_zoom)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			camera.size = clampf(camera.size + 1.0, min_zoom, max_zoom)
