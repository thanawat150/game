extends SceneTree

const PLOT_SCENE := preload("res://scenes/farming/FarmPlot.tscn")
const DATA_KEYS := ["plot_id", "tilled", "moisture", "fertility", "health", "crop_id", "growth_stage", "water_level"]

var failures: PackedStringArray = []


func _initialize() -> void:
	var plot := PLOT_SCENE.instantiate()
	get_root().add_child(plot)
	_expect(plot is StaticBody3D, "plot must be StaticBody3D")
	_expect(plot.get_node_or_null("CollisionShape3D") is CollisionShape3D, "missing collision")
	_expect(plot.get_node_or_null("InteractionArea") is Area3D, "missing InteractionArea")
	var highlight := plot.get_node_or_null("SelectionHighlight") as MeshInstance3D
	_expect(highlight != null, "missing SelectionHighlight")
	if highlight != null:
		_expect(not highlight.visible, "highlight must start hidden")
		_expect(highlight.position.y > 0.15, "highlight must be raised to avoid Z-fighting")
	for side in ["North", "East", "South", "West"]:
		_expect(plot.get_node_or_null("WorkPoints/%s" % side) is Marker3D, "missing %s work point" % side)
	for method in ["set_selected", "get_interaction_prompt", "interact", "serialize", "deserialize"]:
		_expect(plot.has_method(method), "missing %s" % method)
	var snapshot: Dictionary = plot.serialize()
	for key in DATA_KEYS:
		_expect(snapshot.has(key), "snapshot missing %s" % key)
	_finish()


func _expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("GROWWISE3D_FARM_PLOT_CONTRACT_OK")
		quit(0)
		return
	for failure in failures:
		push_error("FARM_PLOT_CONTRACT: %s" % failure)
	quit(1)
