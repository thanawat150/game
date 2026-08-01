extends Node3D
class_name GrowWise3DGameRoot

const RUNTIME_MARKERS: Array[String] = [
	"GROWWISE3D_SCAFFOLD_OK",
	"GROWWISE3D_WORLD_SCAFFOLD_OK",
	"GROWWISE3D_PLAYER_OK",
	"GROWWISE3D_CAMERA_OK",
	"GROWWISE3D_NAVIGATION_OK",
	"GROWWISE3D_NPC_OK",
	"GROWWISE3D_INTERACTION_OK",
	"GROWWISE3D_M1_FOUNDATION_OK",
]

var _emitted_markers: Array[String] = []


func _ready() -> void:
	var navigation := get_node_or_null("World3D/Navigation")
	if navigation != null and navigation.has_signal("navigation_ready"):
		navigation.navigation_ready.connect(_announce_validated_systems)
	call_deferred("_announce_validated_systems")


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_tree().quit()


func get_runtime_markers() -> Array[String]:
	return _emitted_markers.duplicate()


func validate_foundation() -> bool:
	if not _has_player_contract() or not _has_camera_contract():
		return false
	var navigation := get_node_or_null("World3D/Navigation") as NavigationRegion3D
	if navigation == null or navigation.navigation_mesh == null:
		return false
	if not navigation.has_method("get_navigation_diagnostics") \
		or str(navigation.get_navigation_diagnostics().get("status", "")) != "ready":
		return false
	if get_node_or_null("Systems/InteractionManager") == null or get_node_or_null("Systems/SaveManager") == null:
		return false
	if get_node_or_null("CanvasLayer/UI") == null:
		return false
	var plots := get_node("World3D/Farm").get_children()
	var npcs := get_node("World3D/NPCs").get_children()
	if plots.size() != 24 or npcs.size() != 3:
		return false
	return _ids_are_unique(plots, "plot_id") and _ids_are_unique(npcs, "npc_id")


func _announce_validated_systems() -> void:
	_emit_marker("GROWWISE3D_SCAFFOLD_OK")
	if get_node_or_null("World3D") is Node3D:
		_emit_marker("GROWWISE3D_WORLD_SCAFFOLD_OK")
	if _has_player_contract():
		_emit_marker("GROWWISE3D_PLAYER_OK")
	if _has_camera_contract():
		_emit_marker("GROWWISE3D_CAMERA_OK")
	var navigation := get_node_or_null("World3D/Navigation")
	if navigation != null and navigation.has_method("get_navigation_diagnostics") \
		and str(navigation.get_navigation_diagnostics().get("status", "")) == "ready":
		_emit_marker("GROWWISE3D_NAVIGATION_OK")
	var npcs := get_node("World3D/NPCs").get_children()
	if npcs.size() == 3 and _ids_are_unique(npcs, "npc_id"):
		_emit_marker("GROWWISE3D_NPC_OK")
	if get_node_or_null("Systems/InteractionManager") != null:
		_emit_marker("GROWWISE3D_INTERACTION_OK")
	if validate_foundation():
		_emit_marker("GROWWISE3D_M1_FOUNDATION_OK")


func _emit_marker(marker: String) -> void:
	if _emitted_markers.has(marker):
		return
	_emitted_markers.append(marker)
	print(marker)


func _has_player_contract() -> bool:
	var player := get_node_or_null("World3D/Player")
	return player is CharacterBody3D \
		and player.get_node_or_null("CollisionShape3D") is CollisionShape3D \
		and player.get_node_or_null("ModelRoot") is Node3D \
		and player.get_node_or_null("InteractionArea") is Area3D


func _has_camera_contract() -> bool:
	return get_node_or_null("CameraRig/Pivot/SpringArm3D/Camera3D") is Camera3D


func _ids_are_unique(nodes: Array[Node], property_name: StringName) -> bool:
	var observed: Dictionary[String, bool] = {}
	for node in nodes:
		var identifier := str(node.get(property_name))
		if identifier.is_empty() or observed.has(identifier):
			return false
		observed[identifier] = true
	return true
