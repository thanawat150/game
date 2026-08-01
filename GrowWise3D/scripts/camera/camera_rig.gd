extends Node3D
class_name GrowWiseCameraRig

@export_node_path("Node3D") var target_path: NodePath
@export var follow_smoothing: float = 8.0
@export var zoom_smoothing: float = 10.0
@export var min_zoom: float = 8.0
@export var max_zoom: float = 22.0
@export var default_zoom: float = 14.0
@export var focus_height: float = 1.0
@export var initial_pitch_degrees: float = -42.0
@export var initial_yaw_degrees: float = 45.0

@onready var pivot: Node3D = $Pivot
@onready var spring_arm: SpringArm3D = $Pivot/SpringArm3D
@onready var camera: Camera3D = $Pivot/SpringArm3D/Camera3D

var target: Node3D
var target_zoom: float


func _ready() -> void:
	target = get_node_or_null(target_path) as Node3D
	target_zoom = clampf(default_zoom, min_zoom, max_zoom)
	pivot.rotation_degrees = Vector3(initial_pitch_degrees, initial_yaw_degrees, 0.0)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = target_zoom
	if target != null:
		global_position = target.global_position + Vector3.UP * focus_height


func _process(delta: float) -> void:
	if target != null:
		var desired := target.global_position + Vector3.UP * focus_height
		var follow_weight := 1.0 - exp(-follow_smoothing * delta)
		global_position = global_position.lerp(desired, follow_weight)
	var zoom_weight := 1.0 - exp(-zoom_smoothing * delta)
	camera.size = lerpf(camera.size, target_zoom, zoom_weight)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			set_zoom(target_zoom - 1.0)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			set_zoom(target_zoom + 1.0)
	elif event.is_action_pressed("camera_reset"):
		reset_view()


func get_planar_basis() -> Basis:
	var right := camera.global_basis.x
	var forward := -camera.global_basis.z
	right.y = 0.0
	forward.y = 0.0
	right = right.normalized()
	forward = forward.normalized()
	return Basis(right, Vector3.UP, -forward).orthonormalized()


func get_zoom() -> float:
	return target_zoom


func set_zoom(value: float) -> void:
	target_zoom = clampf(value, min_zoom, max_zoom)


func reset_view() -> void:
	pivot.rotation_degrees = Vector3(initial_pitch_degrees, initial_yaw_degrees, 0.0)
	set_zoom(default_zoom)
