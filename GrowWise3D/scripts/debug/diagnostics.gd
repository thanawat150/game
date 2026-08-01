extends Node
class_name GrowWiseDiagnostics

@export_node_path("CharacterBody3D") var player_path := NodePath("../../World3D/Player")
@export_node_path("Node") var interaction_manager_path := NodePath("../InteractionManager")
@export_node_path("NavigationRegion3D") var navigation_path := NodePath("../../World3D/Navigation")


func get_snapshot() -> Dictionary:
	var player := get_node_or_null(player_path) as CharacterBody3D
	var interaction := get_node_or_null(interaction_manager_path)
	var navigation := get_node_or_null(navigation_path)
	var npc_states: Array[String] = []
	for npc in get_tree().get_nodes_in_group("growwise_npc"):
		if npc.has_method("get_state_name"):
			npc_states.append("%s:%s" % [npc.name, npc.get_state_name()])
	return {
		"player_position": Vector3.ZERO if player == null else player.global_position,
		"player_velocity": Vector3.ZERO if player == null else player.velocity,
		"player_state": "MISSING" if player == null else player.get_state_name(),
		"interaction_target": "" if interaction == null else interaction.get_current_target_name(),
		"navigation": {} if navigation == null else navigation.get_navigation_diagnostics(),
		"npc_states": npc_states,
		"fps": Engine.get_frames_per_second(),
		"active_scene": get_tree().current_scene.scene_file_path if get_tree().current_scene != null else "Main3D.tscn",
		"save_version": 2,
	}
