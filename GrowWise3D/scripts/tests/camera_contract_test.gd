extends SceneTree

const MAIN_SCENE := preload("res://Main3D.tscn")

var failures: PackedStringArray = []


func _initialize() -> void:
	var root := MAIN_SCENE.instantiate()
	get_root().add_child(root)
	var rig := root.get_node_or_null("CameraRig")
	_expect(rig != null, "missing CameraRig")
	if rig != null:
		_expect(rig.get_node_or_null("Pivot") is Node3D, "missing Pivot Node3D")
		_expect(rig.get_node_or_null("Pivot/SpringArm3D") is SpringArm3D, "missing SpringArm3D")
		_expect(rig.get_node_or_null("Pivot/SpringArm3D/Camera3D") is Camera3D, "missing nested Camera3D")
		_expect_number(rig, "follow_smoothing", 8.0)
		_expect_number(rig, "min_zoom", 8.0)
		_expect_number(rig, "max_zoom", 22.0)
		_expect_number(rig, "default_zoom", 14.0)
		_expect(rig.has_method("get_planar_basis"), "missing get_planar_basis")
		_expect(rig.has_method("get_zoom"), "missing get_zoom")
		_expect(rig.has_method("set_zoom"), "missing set_zoom")
		_expect(rig.has_method("reset_view"), "missing reset_view")
		if rig.has_method("set_zoom") and rig.has_method("get_zoom"):
			rig.set_zoom(100.0)
			_expect(is_equal_approx(rig.get_zoom(), 22.0), "set_zoom must clamp maximum")
			rig.set_zoom(-100.0)
			_expect(is_equal_approx(rig.get_zoom(), 8.0), "set_zoom must clamp minimum")
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
		print("GROWWISE3D_CAMERA_CONTRACT_OK")
		quit(0)
		return
	for failure in failures:
		push_error("CAMERA_CONTRACT: %s" % failure)
	quit(1)
