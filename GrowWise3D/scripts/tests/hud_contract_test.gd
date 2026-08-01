extends SceneTree

const HUD_PATH := "res://scenes/ui/HUD.tscn"


func _initialize() -> void:
	if not ResourceLoader.exists(HUD_PATH, "PackedScene"):
		push_error("HUD_CONTRACT: missing HUD.tscn")
		quit(1)
		return
	var hud := (load(HUD_PATH) as PackedScene).instantiate()
	get_root().add_child(hud)
	for path in ["SafeMargin", "SafeMargin/Layout", "SafeMargin/Layout/TopRow", "SafeMargin/Layout/BottomRow", "SafeMargin/Layout/BottomRow/ContextPanel"]:
		if hud.get_node_or_null(path) == null:
			push_error("HUD_CONTRACT: missing %s" % path)
			quit(1)
			return
	if not hud.has_method("set_context_prompt") or not hud.has_method("show_plot") or not hud.has_method("set_debug_visible"):
		push_error("HUD_CONTRACT: missing controller interface")
		quit(1)
		return
	print("GROWWISE3D_HUD_CONTRACT_OK")
	quit(0)
