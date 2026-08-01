extends StaticBody3D
class_name GrowWiseFarmPlot

@export var plot_id: String = "plot_0_0"
@export_range(0.0, 100.0) var moisture: float = 40.0
@export_range(0.0, 100.0) var fertility: float = 65.0
@export_range(0.0, 100.0) var water_level: float = 0.0
@export var crop_id: String = ""
@export_range(0, 5) var growth_stage: int = 0

@onready var soil_mesh: MeshInstance3D = $SoilMesh

func _ready() -> void:
	refresh_visual()

func refresh_visual() -> void:
	if soil_mesh == null:
		return
	var material := StandardMaterial3D.new()
	var dry := Color("8a5a3a")
	var wet := Color("49382f")
	material.albedo_color = dry.lerp(wet, clampf(moisture / 100.0, 0.0, 1.0))
	material.roughness = 0.9
	soil_mesh.material_override = material

func apply_water(amount: float) -> void:
	moisture = clampf(moisture + amount, 0.0, 100.0)
	water_level = clampf(water_level + maxf(amount - 10.0, 0.0) * 0.2, 0.0, 100.0)
	refresh_visual()

func serialize() -> Dictionary:
	return {
		"plot_id": plot_id,
		"moisture": moisture,
		"fertility": fertility,
		"water_level": water_level,
		"crop_id": crop_id,
		"growth_stage": growth_stage
	}
