extends SceneTree

const MAIN_SCENE := preload("res://Main3D.tscn")
const PRESETS := ["Morning", "Day", "Evening", "Night"]


func _initialize() -> void:
	var root := MAIN_SCENE.instantiate()
	get_root().add_child(root)
	var controller := root.get_node_or_null("Systems/TimeOfDayController")
	if controller == null:
		push_error("TIME_CONTRACT: missing Systems/TimeOfDayController")
		quit(1)
		return
	for method in ["cycle_preset", "get_preset_name", "serialize", "deserialize"]:
		if not controller.has_method(method):
			push_error("TIME_CONTRACT: missing %s" % method)
			quit(1)
			return
	var observed: Array[String] = []
	for index in range(4):
		observed.append(controller.get_preset_name())
		controller.cycle_preset()
	if observed != PRESETS:
		push_error("TIME_CONTRACT: expected %s, got %s" % [PRESETS, observed])
		quit(1)
		return
	print("GROWWISE3D_TIME_CONTRACT_OK")
	quit(0)
