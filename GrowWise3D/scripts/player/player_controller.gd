extends CharacterBody3D
class_name GrowWisePlayerController

signal state_changed(state: int, speed_ratio: float)

enum PlayerState { IDLE, WALK, RUN, INTERACT, WORK }

@export var walk_speed: float = 3.5
@export var run_speed: float = 6.0
@export var acceleration: float = 12.0
@export var deceleration: float = 16.0
@export var rotation_speed: float = 10.0

var input_locked: bool = false
var current_state: PlayerState = PlayerState.IDLE
var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity", 9.8)


func _physics_process(delta: float) -> void:
	var input_vector := Vector2.ZERO if input_locked else Input.get_vector(
		"move_left", "move_right", "move_forward", "move_back"
	)
	var direction := get_camera_relative_direction(input_vector)
	var wants_run := not input_locked and Input.is_action_pressed("sprint")
	var target_speed := run_speed if wants_run else walk_speed
	var target_velocity := direction * target_speed
	var change_rate := acceleration if not direction.is_zero_approx() else deceleration
	velocity.x = move_toward(velocity.x, target_velocity.x, change_rate * delta)
	velocity.z = move_toward(velocity.z, target_velocity.z, change_rate * delta)
	if not is_on_floor():
		velocity.y -= gravity * delta
	else:
		velocity.y = -0.1
	if not direction.is_zero_approx():
		var target_angle := atan2(direction.x, direction.z)
		rotation.y = lerp_angle(rotation.y, target_angle, clampf(rotation_speed * delta, 0.0, 1.0))
	move_and_slide()
	_update_locomotion_state(direction, wants_run)


func get_camera_relative_direction(input_vector: Vector2) -> Vector3:
	if input_vector.is_zero_approx():
		return Vector3.ZERO
	var camera := get_viewport().get_camera_3d()
	if camera == null:
		return Vector3(input_vector.x, 0.0, input_vector.y).normalized()
	var right := camera.global_basis.x
	var forward := -camera.global_basis.z
	right.y = 0.0
	forward.y = 0.0
	right = right.normalized()
	forward = forward.normalized()
	return (right * input_vector.x + forward * -input_vector.y).normalized()


func set_input_locked(locked: bool) -> void:
	input_locked = locked
	if locked:
		_set_state(PlayerState.IDLE, 0.0)


func set_action_state(state: PlayerState) -> void:
	if state == PlayerState.INTERACT or state == PlayerState.WORK:
		_set_state(state, 0.0)


func get_state_name() -> String:
	return PlayerState.keys()[current_state]


func _update_locomotion_state(direction: Vector3, wants_run: bool) -> void:
	if current_state == PlayerState.INTERACT or current_state == PlayerState.WORK:
		return
	var planar_speed := Vector2(velocity.x, velocity.z).length()
	if direction.is_zero_approx() and planar_speed < 0.08:
		_set_state(PlayerState.IDLE, 0.0)
	elif wants_run:
		_set_state(PlayerState.RUN, clampf(planar_speed / run_speed, 0.0, 1.0))
	else:
		_set_state(PlayerState.WALK, clampf(planar_speed / walk_speed, 0.0, 1.0))


func _set_state(next_state: PlayerState, speed_ratio: float) -> void:
	if current_state == next_state and not is_physics_processing():
		return
	current_state = next_state
	state_changed.emit(current_state, speed_ratio)
