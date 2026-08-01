extends SceneTree

const MAIN_SCENE_PATH := "res://Main3D.tscn"
const EXPECTED_SAVE_PATH := "user://growwise3d_save_v2.json"
const REQUIRED_NODES: Dictionary[String, String] = {
	"World3D": "Node3D",
	"World3D/Environment": "Node3D",
	"World3D/Terrain": "Node3D",
	"World3D/Navigation": "Node3D",
	"World3D/Buildings": "Node3D",
	"World3D/Props": "Node3D",
	"World3D/Farm": "Node3D",
	"World3D/NPCs": "Node3D",
	"World3D/Player": "CharacterBody3D",
	"CameraRig": "Node3D",
	"CameraRig/Pivot": "Node3D",
	"CameraRig/Pivot/SpringArm3D": "SpringArm3D",
	"CameraRig/Pivot/SpringArm3D/Camera3D": "Camera3D",
	"Systems": "Node",
	"CanvasLayer": "CanvasLayer",
	"CanvasLayer/UI": "Control",
}

var _failures: PackedStringArray = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var packed := load(MAIN_SCENE_PATH) as PackedScene
	_expect(packed != null, "Main3D.tscn must load as PackedScene")
	if packed == null:
		_finish()
		return
	var root := packed.instantiate()
	root.name = "GameRoot"
	get_root().add_child(root)
	for frame in range(10):
		await process_frame
	_check_required_nodes(root)
	_check_counts_and_ids(root)
	_check_important_properties(root)
	_check_save_path(root)
	_finish()


func _check_required_nodes(root: Node) -> void:
	for path in REQUIRED_NODES:
		var node := root.get_node_or_null(NodePath(path))
		_expect(node != null, "missing node: %s" % path)
		if node != null:
			_expect(node.is_class(REQUIRED_NODES[path]), "%s must be %s, got %s" % [path, REQUIRED_NODES[path], node.get_class()])


func _check_counts_and_ids(root: Node) -> void:
	var npcs := root.find_children("*", "CharacterBody3D", true, false).filter(
		func(node: Node) -> bool: return node.name != "Player"
	)
	_expect(npcs.size() == 3, "expected 3 NPC CharacterBody3D nodes, got %d" % npcs.size())
	var npc_ids: Dictionary[String, bool] = {}
	for npc in npcs:
		var npc_id := str(npc.get("npc_id"))
		_expect(not npc_id.is_empty(), "NPC %s has empty npc_id" % npc.name)
		_expect(not npc_ids.has(npc_id), "duplicate npc_id: %s" % npc_id)
		npc_ids[npc_id] = true
	var plots := root.find_children("*", "GrowWiseFarmPlot", true, false)
	_expect(plots.size() == 24, "expected 24 GrowWiseFarmPlot nodes, got %d" % plots.size())
	var plot_ids: Dictionary[String, bool] = {}
	for plot in plots:
		var plot_id := str(plot.get("plot_id"))
		_expect(not plot_id.is_empty(), "plot has empty plot_id")
		_expect(not plot_ids.has(plot_id), "duplicate plot_id: %s" % plot_id)
		plot_ids[plot_id] = true


func _check_important_properties(root: Node) -> void:
	var player := root.get_node_or_null("World3D/Player")
	if player != null:
		_expect_number_property(player, "walk_speed", 3.5)
		_expect_number_property(player, "run_speed", 6.0)
		_expect_number_property(player, "acceleration", 12.0)
		_expect_number_property(player, "deceleration", 16.0)
	var camera := root.get_node_or_null("CameraRig")
	if camera != null:
		_expect_number_property(camera, "min_zoom", 8.0)
		_expect_number_property(camera, "max_zoom", 22.0)
		_expect_number_property(camera, "default_zoom", 14.0)
	var navigation := root.get_node_or_null("World3D/Navigation") as NavigationRegion3D
	_expect(navigation != null, "World3D/Navigation must be NavigationRegion3D")
	if navigation != null:
		_expect(navigation.navigation_mesh != null, "navigation_mesh must be assigned")
		_expect(navigation.has_method("get_navigation_diagnostics"), "navigation diagnostics must exist")
		if navigation.has_method("get_navigation_diagnostics"):
			_expect(str(navigation.get_navigation_diagnostics().get("status", "")) == "ready", "navigation must finish baking before M1 marker")
	for npc in root.get_tree().get_nodes_in_group("growwise_npc"):
		_expect(npc.get_node_or_null("NavigationAgent3D") is NavigationAgent3D, "%s needs NavigationAgent3D" % npc.name)
		_expect(int(npc.get("max_path_retries")) > 0, "%s needs bounded path retry" % npc.name)
	for plot in root.find_children("*", "GrowWiseFarmPlot", true, false):
		_expect(plot.get_node_or_null("WorkPoints") != null and plot.get_node("WorkPoints").get_child_count() == 4, "%s needs 4 work points" % plot.name)


func _check_save_path(root: Node) -> void:
	var save_manager := root.get_node_or_null("Systems/SaveManager")
	_expect(save_manager != null, "missing Systems/SaveManager")
	if save_manager != null:
		_expect(str(save_manager.get("save_path")) == EXPECTED_SAVE_PATH, "save path must be %s" % EXPECTED_SAVE_PATH)


func _expect(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _expect_number_property(node: Node, property_name: StringName, expected: float) -> void:
	var value: Variant = node.get(property_name)
	if not (value is float or value is int):
		_failures.append("%s.%s must exist as a number" % [node.name, property_name])
		return
	_expect(is_equal_approx(value, expected), "%s.%s must be %.1f" % [node.name, property_name, expected])


func _finish() -> void:
	if _failures.is_empty():
		print("GROWWISE3D_M1_TESTS_OK")
		quit(0)
		return
	for failure in _failures:
		push_error("M1_SMOKE: %s" % failure)
	print("GROWWISE3D_M1_TESTS_FAILED count=%d" % _failures.size())
	quit(1)
