extends SceneTree

const PLAYER_SCENE := preload("res://scenes/player/Player.tscn")
const REQUIRED_CHILDREN: Dictionary[String, String] = {
	"CollisionShape3D": "CollisionShape3D",
	"ModelRoot": "Node3D",
	"AnimationPlayer": "AnimationPlayer",
	"InteractionArea": "Area3D",
	"ToolSocket": "Marker3D",
	"GroundCheck": "RayCast3D",
}

var failures: PackedStringArray = []


func _initialize() -> void:
	var player := PLAYER_SCENE.instantiate()
	get_root().add_child(player)
	for path in REQUIRED_CHILDREN:
		var child := player.get_node_or_null(path)
		_expect(child != null, "missing %s" % path)
		if child != null:
			_expect(child.is_class(REQUIRED_CHILDREN[path]), "%s must be %s" % [path, REQUIRED_CHILDREN[path]])
	_expect_number(player, "walk_speed", 3.5)
	_expect_number(player, "run_speed", 6.0)
	_expect_number(player, "acceleration", 12.0)
	_expect_number(player, "deceleration", 16.0)
	_expect_number(player, "rotation_speed", 10.0)
	_expect(player.has_method("set_input_locked"), "missing set_input_locked")
	_expect(player.has_method("get_state_name"), "missing get_state_name")
	_expect(player.has_method("get_camera_relative_direction"), "missing get_camera_relative_direction")
	if player.has_method("set_input_locked"):
		player.set_input_locked(true)
		_expect(bool(player.get("input_locked")), "set_input_locked(true) must lock input")
	_finish()


func _expect_number(node: Node, property_name: StringName, expected: float) -> void:
	var value: Variant = node.get(property_name)
	_expect(value is float or value is int, "%s must be numeric" % property_name)
	if value is float or value is int:
		_expect(is_equal_approx(value, expected), "%s must be %.1f" % [property_name, expected])


func _expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("GROWWISE3D_PLAYER_CONTRACT_OK")
		quit(0)
		return
	for failure in failures:
		push_error("PLAYER_CONTRACT: %s" % failure)
	quit(1)
