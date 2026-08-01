extends SceneTree

const MAIN_SCENE := preload("res://Main3D.tscn")
const REQUIRED_MARKERS := [
	"GROWWISE3D_SCAFFOLD_OK",
	"GROWWISE3D_WORLD_SCAFFOLD_OK",
	"GROWWISE3D_PLAYER_OK",
	"GROWWISE3D_CAMERA_OK",
	"GROWWISE3D_NAVIGATION_OK",
	"GROWWISE3D_NPC_OK",
	"GROWWISE3D_INTERACTION_OK",
	"GROWWISE3D_M1_FOUNDATION_OK",
]


func _initialize() -> void:
	var root := MAIN_SCENE.instantiate()
	get_root().add_child(root)
	await process_frame
	if not root.has_method("validate_foundation") or not root.has_method("get_runtime_markers"):
		push_error("MARKER_CONTRACT: missing validation interface")
		quit(1)
		return
	if not root.validate_foundation():
		push_error("MARKER_CONTRACT: foundation validation failed")
		quit(1)
		return
	var markers: Array[String] = root.get_runtime_markers()
	for marker in REQUIRED_MARKERS:
		if not markers.has(marker):
			push_error("MARKER_CONTRACT: missing %s" % marker)
			quit(1)
			return
	print("GROWWISE3D_MARKER_CONTRACT_OK")
	quit(0)
