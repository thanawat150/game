extends CharacterBody3D
class_name GrowWiseNPCController

@export var display_name: String = "NPC"
@export var move_speed: float = 2.2
@export var patrol_points: Array[Vector3] = []

var patrol_index: int = 0
var gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity", 9.8)

func _physics_process(delta: float) -> void:
	if patrol_points.is_empty():
		velocity.x = move_toward(velocity.x, 0.0, move_speed * delta * 4.0)
		velocity.z = move_toward(velocity.z, 0.0, move_speed * delta * 4.0)
	else:
		var target := patrol_points[patrol_index]
		var flat_delta := Vector3(target.x - global_position.x, 0.0, target.z - global_position.z)
		if flat_delta.length() < 0.35:
			patrol_index = (patrol_index + 1) % patrol_points.size()
		else:
			var direction := flat_delta.normalized()
			velocity.x = direction.x * move_speed
			velocity.z = direction.z * move_speed
			rotation.y = lerp_angle(rotation.y, atan2(direction.x, direction.z), delta * 7.0)
	if not is_on_floor():
		velocity.y -= gravity * delta
	else:
		velocity.y = 0.0
	move_and_slide()
