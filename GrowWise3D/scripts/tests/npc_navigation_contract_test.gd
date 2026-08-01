extends SceneTree

const MAIN_SCENE := preload("res://Main3D.tscn")
const NPC_SCENE := preload("res://scenes/npc/NPCBase.tscn")

var failures: PackedStringArray = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var npc := NPC_SCENE.instantiate()
	get_root().add_child(npc)
	_expect(npc.get_node_or_null("NavigationAgent3D") is NavigationAgent3D, "missing NavigationAgent3D")
	_expect(npc.get_node_or_null("InteractionArea") is Area3D, "missing InteractionArea")
	_expect(npc.get_node_or_null("ModelRoot") is Node3D, "missing ModelRoot")
	for method in ["begin_talk", "end_talk", "request_path_with_retry", "get_navigation_diagnostics", "get_state_name", "serialize", "deserialize"]:
		_expect(npc.has_method(method), "missing %s" % method)
	_expect_numeric(npc, "max_path_retries", 3.0, true)
	_expect_numeric(npc, "path_retry_interval", 0.0, false)
	_expect_numeric(npc, "wait_timeout", 0.0, false)
	var source := FileAccess.get_file_as_string("res://scripts/npc/npc_controller.gd").to_lower()
	_expect(source.find("teleport") == -1, "NPC controller must not contain teleport fallback")
	_expect(source.find("map_get_iteration_id") != -1, "NPC must wait for navigation map synchronization")
	_expect(source.find("velocity_computed") != -1, "NPC must apply avoidance safe velocity")
	var root := MAIN_SCENE.instantiate()
	get_root().add_child(root)
	await process_frame
	await process_frame
	var navigation := root.get_node_or_null("World3D/Navigation")
	_expect(navigation is NavigationRegion3D, "World3D/Navigation must be NavigationRegion3D")
	if navigation != null:
		_expect(navigation.has_method("rebake"), "Navigation region must expose rebake")
		_expect(navigation.has_method("get_navigation_diagnostics"), "Navigation region must expose diagnostics")
	var npcs := root.get_node("World3D/NPCs").get_children()
	_expect(npcs.size() == 3, "expected 3 NPCs, got %d" % npcs.size())
	var ids: Dictionary[String, bool] = {}
	for item in npcs:
		var npc_id := str(item.get("npc_id"))
		_expect(not npc_id.is_empty(), "NPC has empty ID")
		_expect(not ids.has(npc_id), "duplicate NPC ID %s" % npc_id)
		ids[npc_id] = true
		if item.has_method("get_navigation_diagnostics"):
			var diagnostics: Dictionary = item.get_navigation_diagnostics()
			for key in ["state", "retry_count", "wait_elapsed", "target", "failure_reason"]:
				_expect(diagnostics.has(key), "diagnostics missing %s" % key)
	_finish()


func _expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _expect_numeric(node: Node, property_name: StringName, threshold: float, exact: bool) -> void:
	var value: Variant = node.get(property_name)
	if not (value is int or value is float):
		failures.append("%s must be numeric" % property_name)
		return
	if exact:
		_expect(is_equal_approx(value, threshold), "%s must be %.1f" % [property_name, threshold])
	else:
		_expect(value > threshold, "%s must be greater than %.1f" % [property_name, threshold])


func _finish() -> void:
	if failures.is_empty():
		print("GROWWISE3D_NPC_NAVIGATION_CONTRACT_OK")
		quit(0)
		return
	for failure in failures:
		push_error("NPC_NAVIGATION_CONTRACT: %s" % failure)
	quit(1)
