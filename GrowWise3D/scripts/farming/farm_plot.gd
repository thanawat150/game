extends StaticBody3D
class_name GrowWiseFarmPlot

signal selected(plot: GrowWiseFarmPlot)
signal plot_changed(plot_id: String, snapshot: Dictionary)

@export var plot_id: String = "plot_0_0"
@export var tilled: bool = false
@export_range(0.0, 100.0) var moisture: float = 40.0
@export_range(0.0, 100.0) var fertility: float = 65.0
@export_range(0.0, 100.0) var health: float = 100.0
@export var crop_id: String = ""
@export_range(0, 5) var growth_stage: int = 0
@export_range(0.0, 100.0) var water_level: float = 0.0

@onready var soil_mesh: MeshInstance3D = $SoilMesh
@onready var selection_highlight: MeshInstance3D = $SelectionHighlight


func _ready() -> void:
	add_to_group("growwise_plot")
	refresh_visual()
	set_selected(false)


func get_interaction_prompt() -> String:
	return "[E] ตรวจแปลง"


func get_interaction_priority() -> int:
	return 20


func get_interaction_point() -> Vector3:
	return global_position + Vector3.UP * 0.45


func can_interact(_actor: Node3D) -> bool:
	return is_inside_tree()


func interact(_actor: Node3D) -> void:
	set_selected(true)
	selected.emit(self)
	plot_changed.emit(plot_id, serialize())


func set_selected(value: bool) -> void:
	if selection_highlight != null:
		selection_highlight.visible = value


func refresh_visual() -> void:
	if soil_mesh == null:
		return
	var material := StandardMaterial3D.new()
	var dry := Color("765039")
	var wet := Color("302722")
	material.albedo_color = dry.lerp(wet, clampf(moisture / 100.0, 0.0, 1.0))
	material.roughness = lerpf(0.94, 0.72, clampf(moisture / 100.0, 0.0, 1.0))
	soil_mesh.material_override = material


func apply_water(amount: float) -> void:
	moisture = clampf(moisture + amount, 0.0, 100.0)
	water_level = clampf(water_level + maxf(amount - 10.0, 0.0) * 0.2, 0.0, 100.0)
	refresh_visual()
	plot_changed.emit(plot_id, serialize())


func serialize() -> Dictionary:
	return {
		"plot_id": plot_id,
		"tilled": tilled,
		"moisture": moisture,
		"fertility": fertility,
		"health": health,
		"crop_id": crop_id,
		"growth_stage": growth_stage,
		"water_level": water_level,
	}


func deserialize(data: Dictionary) -> void:
	tilled = bool(data.get("tilled", false))
	moisture = clampf(float(data.get("moisture", 40.0)), 0.0, 100.0)
	fertility = clampf(float(data.get("fertility", 65.0)), 0.0, 100.0)
	health = clampf(float(data.get("health", 100.0)), 0.0, 100.0)
	crop_id = str(data.get("crop_id", ""))
	growth_stage = clampi(int(data.get("growth_stage", 0)), 0, 5)
	water_level = clampf(float(data.get("water_level", 0.0)), 0.0, 100.0)
	refresh_visual()
