extends NavigationRegion3D
class_name GrowWiseNavigationBaker

signal navigation_ready()

var status: String = "pending"
var last_error: String = ""
var bake_count: int = 0


func _ready() -> void:
	call_deferred("rebake")


func rebake() -> void:
	if navigation_mesh == null:
		status = "failed"
		last_error = "missing_navigation_mesh"
		push_error("GROWWISE3D_NAVIGATION_FAILED reason=%s" % last_error)
		return
	status = "parsing"
	last_error = ""
	var source_data := NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(
		navigation_mesh,
		source_data,
		get_parent(),
		_on_source_geometry_parsed.bind(source_data)
	)


func get_navigation_diagnostics() -> Dictionary:
	return {
		"status": status,
		"last_error": last_error,
		"bake_count": bake_count,
		"map_iteration": NavigationServer3D.map_get_iteration_id(get_navigation_map()),
	}


func _on_source_geometry_parsed(source_data: NavigationMeshSourceGeometryData3D) -> void:
	status = "baking"
	NavigationServer3D.bake_from_source_geometry_data(
		navigation_mesh,
		source_data,
		_on_navigation_baked
	)


func _on_navigation_baked() -> void:
	status = "ready"
	bake_count += 1
	navigation_ready.emit()
	print("GROWWISE3D_NAVIGATION_OK")
