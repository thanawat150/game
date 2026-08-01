extends SceneTree

const MANAGER_PATH := "res://scripts/interaction/interaction_manager.gd"
const PLOT_SCENE := preload("res://scenes/farming/FarmPlot.tscn")


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not ResourceLoader.exists(MANAGER_PATH, "Script"):
		push_error("INTERACTION_LOS: missing interaction_manager.gd")
		quit(1)
		return
	var manager := Node.new()
	manager.set_script(load(MANAGER_PATH))
	get_root().add_child(manager)
	if not manager.has_method("has_line_of_sight"):
		push_error("INTERACTION_LOS: missing has_line_of_sight")
		quit(1)
		return
	var actor := Node3D.new()
	actor.position = Vector3(0, 0.8, 0)
	get_root().add_child(actor)
	var plot := PLOT_SCENE.instantiate()
	plot.position = Vector3(0, 0, -4)
	get_root().add_child(plot)
	await physics_frame
	if not manager.has_line_of_sight(actor, plot):
		push_error("INTERACTION_LOS: clear target must be visible")
		quit(1)
		return
	var wall := StaticBody3D.new()
	wall.collision_layer = 1
	var collision := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(2.0, 2.5, 0.35)
	collision.shape = shape
	wall.add_child(collision)
	wall.position = Vector3(0, 1.0, -2.0)
	get_root().add_child(wall)
	await physics_frame
	if manager.has_line_of_sight(actor, plot):
		push_error("INTERACTION_LOS: world-static wall must block target")
		quit(1)
		return
	print("GROWWISE3D_INTERACTION_LOS_OK")
	quit(0)
