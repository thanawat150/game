extends CharacterBody3D
class_name GrowWiseNPCController

signal state_changed(npc_id: String, state_name: String)
signal dialogue_requested(npc_id: String, display_name: String, text: String)

enum NPCState { IDLE, WALK, WAIT, TALK, WORK, RETURN }

@export var npc_id: String = "npc"
@export var display_name: String = "NPC"
@export_multiline var dialogue_text: String = "สวัสดี"
@export var move_speed: float = 2.2
@export var rotation_speed: float = 7.0
@export var patrol_points: Array[Vector3] = []
@export var start_delay: float = 0.0
@export var stop_duration: float = 2.0
@export var max_path_retries: int = 3
@export var path_retry_interval: float = 1.0
@export var wait_timeout: float = 6.0

@onready var navigation_agent: NavigationAgent3D = $NavigationAgent3D

var current_state: NPCState = NPCState.WAIT
var patrol_index: int = 0
var retry_count: int = 0
var wait_elapsed: float = 0.0
var current_wait_duration: float = 0.0
var path_elapsed: float = 0.0
var current_target: Vector3 = Vector3.ZERO
var failure_reason: String = ""
var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity", 9.8)
var talk_target: Node3D
var navigation_retry_exhausted: bool = false


func _ready() -> void:
	add_to_group("growwise_npc")
	navigation_agent.path_desired_distance = 0.35
	navigation_agent.target_desired_distance = 0.45
	navigation_agent.avoidance_enabled = true
	navigation_agent.radius = 0.42
	navigation_agent.neighbor_distance = 2.2
	navigation_agent.max_neighbors = 8
	navigation_agent.velocity_computed.connect(_on_safe_velocity_computed)
	var navigation_region := get_node_or_null("../../Navigation")
	if navigation_region != null and navigation_region.has_signal("navigation_ready"):
		navigation_region.navigation_ready.connect(_on_navigation_ready)
	current_wait_duration = maxf(start_delay, 0.1)
	_set_state(NPCState.WAIT)


func _physics_process(delta: float) -> void:
	_apply_gravity(delta)
	match current_state:
		NPCState.TALK:
			_stop_planar(delta)
			_face_talk_target(delta)
		NPCState.WALK, NPCState.RETURN:
			_update_navigation(delta)
		NPCState.WAIT, NPCState.IDLE, NPCState.WORK:
			_stop_planar(delta)
			_update_wait(delta)
	move_and_slide()


func request_path_with_retry(target: Vector3) -> void:
	if navigation_retry_exhausted:
		return
	current_target = target
	path_elapsed = 0.0
	failure_reason = ""
	if NavigationServer3D.map_get_iteration_id(navigation_agent.get_navigation_map()) <= 0:
		_handle_path_failure("navigation_map_not_ready")
		return
	navigation_agent.target_position = target
	_set_state(NPCState.WALK)


func begin_talk(player: Node3D) -> void:
	talk_target = player
	velocity.x = 0.0
	velocity.z = 0.0
	_set_state(NPCState.TALK)
	dialogue_requested.emit(npc_id, display_name, dialogue_text)


func end_talk() -> void:
	talk_target = null
	retry_count = 0
	current_wait_duration = 0.35
	wait_elapsed = 0.0
	_set_state(NPCState.WAIT)


func get_interaction_prompt() -> String:
	return "[E] พูดคุยกับ%s" % display_name


func get_interaction_priority() -> int:
	return 30


func get_interaction_point() -> Vector3:
	return global_position + Vector3.UP * 1.25


func can_interact(_actor: Node3D) -> bool:
	return current_state != NPCState.WORK


func interact(actor: Node3D) -> void:
	begin_talk(actor)


func get_state_name() -> String:
	return NPCState.keys()[current_state]


func get_navigation_diagnostics() -> Dictionary:
	return {
		"state": get_state_name(),
		"retry_count": retry_count,
		"wait_elapsed": wait_elapsed,
		"target": [current_target.x, current_target.y, current_target.z],
		"failure_reason": failure_reason,
	}


func serialize() -> Dictionary:
	return {
		"npc_id": npc_id,
		"position": [global_position.x, global_position.y, global_position.z],
		"rotation_y": rotation.y,
		"state": get_state_name(),
		"patrol_index": patrol_index,
		"retry_count": retry_count,
	}


func deserialize(data: Dictionary) -> void:
	var position_data: Variant = data.get("position", [])
	if position_data is Array and position_data.size() >= 3:
		global_position = Vector3(float(position_data[0]), float(position_data[1]), float(position_data[2]))
	rotation.y = float(data.get("rotation_y", rotation.y))
	patrol_index = clampi(int(data.get("patrol_index", 0)), 0, maxi(patrol_points.size() - 1, 0))
	retry_count = clampi(int(data.get("retry_count", 0)), 0, max_path_retries)
	current_wait_duration = 0.3
	wait_elapsed = 0.0
	_set_state(NPCState.WAIT)


func _update_navigation(delta: float) -> void:
	path_elapsed += delta
	if navigation_agent.is_navigation_finished():
		if global_position.distance_to(current_target) <= navigation_agent.target_desired_distance + 0.25:
			_arrive_at_target()
		elif path_elapsed >= path_retry_interval:
			_handle_path_failure("no_path_to_target")
		return
	var next_position := navigation_agent.get_next_path_position()
	var direction := global_position.direction_to(next_position)
	direction.y = 0.0
	if direction.is_zero_approx():
		_stop_planar(delta)
		return
	var desired_velocity := direction * move_speed
	if navigation_agent.avoidance_enabled:
		navigation_agent.velocity = desired_velocity
	else:
		velocity.x = desired_velocity.x
		velocity.z = desired_velocity.z
	rotation.y = lerp_angle(rotation.y, atan2(direction.x, direction.z), clampf(rotation_speed * delta, 0.0, 1.0))


func _handle_path_failure(reason: String) -> void:
	retry_count += 1
	failure_reason = reason
	wait_elapsed = 0.0
	current_wait_duration = path_retry_interval if retry_count < max_path_retries else wait_timeout
	_set_state(NPCState.WAIT)
	push_warning("NPC_NAVIGATION_WAIT npc=%s retry=%d/%d reason=%s" % [npc_id, retry_count, max_path_retries, reason])


func _update_wait(delta: float) -> void:
	wait_elapsed += delta
	if wait_elapsed < current_wait_duration:
		return
	wait_elapsed = 0.0
	if retry_count > 0 and retry_count < max_path_retries:
		request_path_with_retry(current_target)
		return
	if retry_count >= max_path_retries:
		if failure_reason == "navigation_map_not_ready":
			navigation_retry_exhausted = true
			failure_reason = "navigation_map_timeout"
			current_wait_duration = INF
			push_warning("NPC_NAVIGATION_TIMEOUT npc=%s retries=%d" % [npc_id, retry_count])
			return
		retry_count = 0
		failure_reason = "bounded_retry_exhausted"
		patrol_index = (patrol_index + 1) % maxi(patrol_points.size(), 1)
	if patrol_points.is_empty():
		current_wait_duration = stop_duration
		_set_state(NPCState.IDLE)
		return
	request_path_with_retry(patrol_points[patrol_index])


func _arrive_at_target() -> void:
	retry_count = 0
	failure_reason = ""
	patrol_index = (patrol_index + 1) % maxi(patrol_points.size(), 1)
	wait_elapsed = 0.0
	current_wait_duration = stop_duration
	_set_state(NPCState.WAIT)


func _stop_planar(delta: float) -> void:
	velocity.x = move_toward(velocity.x, 0.0, move_speed * delta * 5.0)
	velocity.z = move_toward(velocity.z, 0.0, move_speed * delta * 5.0)


func _face_talk_target(delta: float) -> void:
	if talk_target == null:
		return
	var direction := global_position.direction_to(talk_target.global_position)
	direction.y = 0.0
	if not direction.is_zero_approx():
		rotation.y = lerp_angle(rotation.y, atan2(direction.x, direction.z), clampf(rotation_speed * delta, 0.0, 1.0))


func _apply_gravity(delta: float) -> void:
	if not is_on_floor():
		velocity.y -= gravity * delta
	else:
		velocity.y = -0.1


func _on_safe_velocity_computed(safe_velocity: Vector3) -> void:
	velocity.x = safe_velocity.x
	velocity.z = safe_velocity.z


func _on_navigation_ready() -> void:
	if not navigation_retry_exhausted:
		return
	navigation_retry_exhausted = false
	retry_count = 0
	wait_elapsed = 0.0
	failure_reason = ""
	var resume_target := current_target
	if resume_target == Vector3.ZERO and not patrol_points.is_empty():
		resume_target = patrol_points[patrol_index]
	request_path_with_retry(resume_target)


func _set_state(next_state: NPCState) -> void:
	current_state = next_state
	state_changed.emit(npc_id, get_state_name())
