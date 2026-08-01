extends Node
class_name GrowWiseTimeOfDayController

signal preset_changed(preset_name: String)

const PRESETS: Array[Dictionary] = [
	{"name":"Morning", "sun_rotation":Vector3(-24,-54,0), "sun_color":Color("ffd2a1"), "sun_energy":0.72, "ambient":Color("90a6ae"), "ambient_energy":0.38, "background":Color("7295a7"), "fog":Color("aeb8b4")},
	{"name":"Day", "sun_rotation":Vector3(-52,-28,0), "sun_color":Color("fff0cf"), "sun_energy":1.05, "ambient":Color("9aabb3"), "ambient_energy":0.46, "background":Color("5d8db2"), "fog":Color("b4c1c3")},
	{"name":"Evening", "sun_rotation":Vector3(-18,58,0), "sun_color":Color("ff9a62"), "sun_energy":0.66, "ambient":Color("766f82"), "ambient_energy":0.34, "background":Color("6d657c"), "fog":Color("9a7c74")},
	{"name":"Night", "sun_rotation":Vector3(-35,142,0), "sun_color":Color("9ab8df"), "sun_energy":0.18, "ambient":Color("33475f"), "ambient_energy":0.25, "background":Color("101b2c"), "fog":Color("263447")},
]

@export_node_path("DirectionalLight3D") var sun_path := NodePath("../../World3D/Environment/Sun")
@export_node_path("WorldEnvironment") var environment_path := NodePath("../../World3D/Environment/WorldEnvironment")

var preset_index: int = 0


func _ready() -> void:
	apply_preset(preset_index)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("time_cycle"):
		cycle_preset()


func cycle_preset() -> void:
	apply_preset((preset_index + 1) % PRESETS.size())


func apply_preset(index: int) -> void:
	preset_index = clampi(index, 0, PRESETS.size() - 1)
	var profile := PRESETS[preset_index]
	var sun := get_node_or_null(sun_path) as DirectionalLight3D
	var world_environment := get_node_or_null(environment_path) as WorldEnvironment
	if sun != null:
		sun.rotation_degrees = profile["sun_rotation"]
		sun.light_color = profile["sun_color"]
		sun.light_energy = profile["sun_energy"]
	if world_environment != null and world_environment.environment != null:
		var environment := world_environment.environment
		environment.ambient_light_color = profile["ambient"]
		environment.ambient_light_energy = profile["ambient_energy"]
		environment.background_color = profile["background"]
		environment.fog_light_color = profile["fog"]
	preset_changed.emit(get_preset_name())


func get_preset_name() -> String:
	return str(PRESETS[preset_index]["name"])


func serialize() -> Dictionary:
	return {"preset": get_preset_name(), "preset_index": preset_index}


func deserialize(data: Dictionary) -> void:
	var requested_name := str(data.get("preset", "Morning"))
	for index in range(PRESETS.size()):
		if PRESETS[index]["name"] == requested_name:
			apply_preset(index)
			return
	apply_preset(clampi(int(data.get("preset_index", 0)), 0, PRESETS.size() - 1))
