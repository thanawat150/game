extends SceneTree

const MAIN_SCENE := preload("res://Main3D.tscn")
const REQUIRED_WORLD_NODES: Dictionary[String, String] = {
	"World3D": "Node3D",
	"World3D/Environment": "Node3D",
	"World3D/Terrain": "Node3D",
	"World3D/Navigation": "Node3D",
	"World3D/Buildings": "Node3D",
	"World3D/Props": "Node3D",
	"World3D/Farm": "Node3D",
	"World3D/NPCs": "Node3D",
	"World3D/Player": "CharacterBody3D",
}
const REQUIRED_SCENES := [
	"res://scenes/world/Terrain.tscn",
	"res://scenes/world/PlayerHouse.tscn",
	"res://scenes/world/StorageShed.tscn",
	"res://scenes/world/Well.tscn",
	"res://scenes/world/FenceSection.tscn",
	"res://scenes/world/Tree.tscn",
	"res://scenes/world/Rock.tscn",
]
const BOOTSTRAP_FORBIDDEN := ["Mesh.new", "Shape3D.new", "StandardMaterial3D.new", "change_state", "apply_water"]

var failures: PackedStringArray = []


func _initialize() -> void:
	var root := MAIN_SCENE.instantiate()
	get_root().add_child(root)
	for path in REQUIRED_WORLD_NODES:
		var node := root.get_node_or_null(path)
		_expect(node != null, "missing %s" % path)
		if node != null:
			_expect(node.is_class(REQUIRED_WORLD_NODES[path]), "%s must be %s" % [path, REQUIRED_WORLD_NODES[path]])
	for scene_path in REQUIRED_SCENES:
		_expect(ResourceLoader.exists(scene_path, "PackedScene"), "missing reusable scene %s" % scene_path)
	var terrain := (load("res://scenes/world/Terrain.tscn") as PackedScene).instantiate()
	var ground_mesh := terrain.get_node("Ground/Mesh") as MeshInstance3D
	_expect(ground_mesh.mesh.get_aabb().size.x >= 72.0, "terrain must fill the widest isometric framing")
	terrain.free()
	var bootstrap := FileAccess.get_file_as_string("res://scripts/world/world_bootstrap.gd")
	for token in BOOTSTRAP_FORBIDDEN:
		_expect(bootstrap.find(token) == -1, "world_bootstrap contains forbidden token %s" % token)
	_expect(FileAccess.file_exists("res://data/m1_world_placements.json"), "missing placement data")
	var placement_data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://data/m1_world_placements.json"))
	_expect(placement_data.get("farm_grid", null) is Dictionary, "farm grid placement must be data-driven")
	_finish()


func _expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func _finish() -> void:
	if failures.is_empty():
		print("GROWWISE3D_WORLD_CONTRACT_OK")
		quit(0)
		return
	for failure in failures:
		push_error("WORLD_CONTRACT: %s" % failure)
	quit(1)
