extends Node3D
class_name GrowWiseWorldBootstrap

const PLACEMENT_PATH := "res://data/m1_world_placements.json"
const FARM_PLOT_SCENE := preload("res://scenes/farming/FarmPlot.tscn")
const SCENES: Dictionary[String, String] = {
	"player_house": "res://scenes/world/PlayerHouse.tscn",
	"storage_shed": "res://scenes/world/StorageShed.tscn",
	"well": "res://scenes/world/Well.tscn",
	"fence": "res://scenes/world/FenceSection.tscn",
	"tree": "res://scenes/world/Tree.tscn",
	"rock": "res://scenes/world/Rock.tscn",
}


func _ready() -> void:
	_place_world_instances()
	_place_farm_grid()
	print("GROWWISE3D_WORLD_SCAFFOLD_OK")


func _place_world_instances() -> void:
	var file := FileAccess.open(PLACEMENT_PATH, FileAccess.READ)
	if file == null:
		push_error("GrowWise3D placement data missing: %s" % PLACEMENT_PATH)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		push_error("GrowWise3D placement data must be a JSON object")
		return
	for entry: Dictionary in parsed.get("instances", []):
		var scene_id := str(entry.get("scene", ""))
		if not SCENES.has(scene_id):
			push_warning("Unknown M1 placement scene: %s" % scene_id)
			continue
		var packed := load(SCENES[scene_id]) as PackedScene
		if packed == null:
			push_error("Unable to load placement scene: %s" % SCENES[scene_id])
			continue
		var instance := packed.instantiate() as Node3D
		instance.name = str(entry.get("name", scene_id)).validate_node_name()
		instance.position = _array_to_vector3(entry.get("position", [0.0, 0.0, 0.0]))
		instance.rotation_degrees.y = float(entry.get("rotation_y", 0.0))
		var parent := get_node_or_null(str(entry.get("parent", "Props")))
		if parent == null:
			push_warning("Placement parent missing for %s" % instance.name)
			instance.queue_free()
			continue
		parent.add_child(instance)


func _place_farm_grid() -> void:
	var farm_root := get_node("Farm")
	for row in range(4):
		for column in range(6):
			var plot := FARM_PLOT_SCENE.instantiate() as GrowWiseFarmPlot
			plot.plot_id = "farm_%02d_%02d" % [column, row]
			plot.position = Vector3((column - 2.5) * 2.35, 0.14, (row - 1.5) * 2.35)
			farm_root.add_child(plot)


func _array_to_vector3(value: Variant) -> Vector3:
	if not value is Array or value.size() < 3:
		return Vector3.ZERO
	return Vector3(float(value[0]), float(value[1]), float(value[2]))
