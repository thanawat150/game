extends Node
class_name GrowWiseInteractionManager

signal target_changed(target: Node3D, prompt: String)
signal interaction_started(target: Node3D)

@export_node_path("Node3D") var player_path := NodePath("../../World3D/Player")
@export_node_path("Area3D") var interaction_area_path := NodePath("../../World3D/Player/InteractionArea")
@export_flags_3d_physics var line_of_sight_mask: int = 1

var input_locked: bool = false
var current_target: Node3D
var candidates: Array[Node3D] = []

@onready var player: Node3D = get_node_or_null(player_path) as Node3D
@onready var interaction_area: Area3D = get_node_or_null(interaction_area_path) as Area3D


func _ready() -> void:
	if interaction_area != null:
		interaction_area.area_entered.connect(_on_area_entered)
		interaction_area.area_exited.connect(_on_area_exited)


func _physics_process(_delta: float) -> void:
	_refresh_target()


func _unhandled_input(event: InputEvent) -> void:
	if input_locked or current_target == null:
		return
	if event.is_action_pressed("interact") and current_target.has_method("interact"):
		interaction_started.emit(current_target)
		current_target.interact(player)
		get_viewport().set_input_as_handled()


func set_input_locked(locked: bool) -> void:
	input_locked = locked
	if locked:
		_set_target(null)


func get_current_target_name() -> String:
	return "" if current_target == null else current_target.name


func has_line_of_sight(actor: Node3D, target: Node3D) -> bool:
	if actor == null or target == null or not actor.is_inside_tree() or not target.is_inside_tree():
		return false
	var target_point := target.global_position
	if target.has_method("get_interaction_point"):
		target_point = target.get_interaction_point()
	var query := PhysicsRayQueryParameters3D.create(
		actor.global_position + Vector3.UP * 0.75,
		target_point,
		line_of_sight_mask
	)
	query.collide_with_areas = false
	query.collide_with_bodies = true
	if actor is CollisionObject3D:
		query.exclude = [actor.get_rid()]
	var hit := actor.get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return true
	var collider := hit.get("collider") as Node
	return collider == target or (collider != null and target.is_ancestor_of(collider))


func _refresh_target() -> void:
	if input_locked or player == null:
		_set_target(null)
		return
	var best: Node3D
	var best_score := INF
	for candidate in candidates.duplicate():
		if not is_instance_valid(candidate) or not candidate.is_inside_tree():
			candidates.erase(candidate)
			continue
		if not candidate.has_method("can_interact") or not candidate.can_interact(player):
			continue
		if not has_line_of_sight(player, candidate):
			continue
		var priority := int(candidate.get_interaction_priority()) if candidate.has_method("get_interaction_priority") else 0
		var score := player.global_position.distance_squared_to(candidate.global_position) - float(priority) * 0.01
		if score < best_score:
			best_score = score
			best = candidate
	_set_target(best)


func _set_target(next_target: Node3D) -> void:
	if current_target == next_target:
		return
	current_target = next_target
	var prompt := ""
	if current_target != null and current_target.has_method("get_interaction_prompt"):
		prompt = current_target.get_interaction_prompt()
	target_changed.emit(current_target, prompt)


func _on_area_entered(area: Area3D) -> void:
	var candidate := area.get_parent() as Node3D
	if candidate != null and candidate.has_method("interact") and not candidates.has(candidate):
		candidates.append(candidate)


func _on_area_exited(area: Area3D) -> void:
	var candidate := area.get_parent() as Node3D
	candidates.erase(candidate)
	if current_target == candidate:
		_set_target(null)
