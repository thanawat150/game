extends "res://scripts/openworld_play_fix.gd"

const FARM_SPACING: float = 1.16
const FARM_CENTER_X: float = 4.0
const FARM_CENTER_Y: float = 3.5

var realistic_viewport: SubViewport
var realistic_root: Node3D
var realistic_camera: Camera3D
var realistic_sun: DirectionalLight3D
var realistic_environment: Environment
var realistic_beds: Dictionary = {}
var realistic_crops: Dictionary = {}
var realistic_selected: MeshInstance3D
var realistic_last_signature: String = ""
var realistic_sync_timer: float = 0.0

func _ready() -> void:
	super._ready()
	build_realistic_3d_farm()
	sync_realistic_3d(true)
	print("GROWWISE_REALISTIC_3D_OK")

func _process(delta: float) -> void:
	super._process(delta)
	realistic_sync_timer += delta
	if realistic_sync_timer >= 0.25:
		realistic_sync_timer = 0.0
		sync_realistic_3d(false)
	update_realistic_lighting()

func build_realistic_3d_farm() -> void:
	realistic_viewport = SubViewport.new()
	realistic_viewport.name = "RealisticFarmViewport"
	realistic_viewport.size = Vector2i(1280, 720)
	realistic_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	realistic_viewport.msaa_3d = Viewport.MSAA_4X
	realistic_viewport.screen_space_aa = Viewport.SCREEN_SPACE_AA_FXAA
	realistic_viewport.own_world_3d = true
	add_child(realistic_viewport)

	realistic_root = Node3D.new()
	realistic_root.name = "RealisticFarmRoot"
	realistic_viewport.add_child(realistic_root)

	var world_environment: WorldEnvironment = WorldEnvironment.new()
	realistic_environment = Environment.new()
	realistic_environment.background_mode = Environment.BG_COLOR
	realistic_environment.background_color = Color("8bb276")
	realistic_environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	realistic_environment.ambient_light_color = Color("fff1d4")
	realistic_environment.ambient_light_energy = 0.72
	realistic_environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	realistic_environment.glow_enabled = true
	realistic_environment.glow_intensity = 0.18
	realistic_environment.fog_enabled = true
	realistic_environment.fog_light_color = Color("d9e3c7")
	realistic_environment.fog_density = 0.003
	world_environment.environment = realistic_environment
	realistic_root.add_child(world_environment)

	realistic_sun = DirectionalLight3D.new()
	realistic_sun.name = "WarmSun"
	realistic_sun.rotation_degrees = Vector3(-52.0, -34.0, 0.0)
	realistic_sun.light_color = Color("ffe4b0")
	realistic_sun.light_energy = 1.35
	realistic_sun.shadow_enabled = true
	realistic_sun.directional_shadow_max_distance = 38.0
	realistic_root.add_child(realistic_sun)

	var fill_light: DirectionalLight3D = DirectionalLight3D.new()
	fill_light.rotation_degrees = Vector3(-35.0, 145.0, 0.0)
	fill_light.light_color = Color("aacfe0")
	fill_light.light_energy = 0.28
	realistic_root.add_child(fill_light)

	realistic_camera = Camera3D.new()
	realistic_camera.name = "IsometricCamera"
	realistic_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	realistic_camera.size = 12.2
	realistic_camera.near = 0.1
	realistic_camera.far = 80.0
	realistic_camera.position = Vector3(10.6, 12.8, 14.6)
	realistic_camera.look_at(Vector3(0.0, 0.25, 0.0), Vector3.UP)
	realistic_camera.current = true
	realistic_root.add_child(realistic_camera)

	build_realistic_ground()
	build_realistic_farm_beds()
	build_realistic_fence()
	build_realistic_decor()
	build_realistic_characters()
	build_realistic_selected_marker()

func material_3d(color_value: Color, roughness_value: float = 0.82, metallic_value: float = 0.0) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = color_value
	material.roughness = roughness_value
	material.metallic = metallic_value
	return material

func transparent_material_3d(color_value: Color, emission_value: Color = Color.BLACK) -> StandardMaterial3D:
	var material: StandardMaterial3D = StandardMaterial3D.new()
	material.albedo_color = color_value
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.roughness = 0.65
	if emission_value != Color.BLACK:
		material.emission_enabled = true
		material.emission = emission_value
		material.emission_energy_multiplier = 1.35
	return material

func add_box(parent: Node3D, size_value: Vector3, position_value: Vector3, color_value: Color, name_value: String = "Box") -> MeshInstance3D:
	var mesh_instance: MeshInstance3D = MeshInstance3D.new()
	mesh_instance.name = name_value
	var box_mesh: BoxMesh = BoxMesh.new()
	box_mesh.size = size_value
	mesh_instance.mesh = box_mesh
	mesh_instance.position = position_value
	mesh_instance.material_override = material_3d(color_value)
	mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	parent.add_child(mesh_instance)
	return mesh_instance

func add_cylinder(parent: Node3D, radius_value: float, height_value: float, position_value: Vector3, color_value: Color, name_value: String = "Cylinder") -> MeshInstance3D:
	var mesh_instance: MeshInstance3D = MeshInstance3D.new()
	mesh_instance.name = name_value
	var cylinder_mesh: CylinderMesh = CylinderMesh.new()
	cylinder_mesh.top_radius = radius_value
	cylinder_mesh.bottom_radius = radius_value * 1.06
	cylinder_mesh.height = height_value
	cylinder_mesh.radial_segments = 10
	mesh_instance.mesh = cylinder_mesh
	mesh_instance.position = position_value
	mesh_instance.material_override = material_3d(color_value)
	mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	parent.add_child(mesh_instance)
	return mesh_instance

func add_sphere(parent: Node3D, radius_value: float, position_value: Vector3, color_value: Color, scale_value: Vector3 = Vector3.ONE, name_value: String = "Sphere") -> MeshInstance3D:
	var mesh_instance: MeshInstance3D = MeshInstance3D.new()
	mesh_instance.name = name_value
	var sphere_mesh: SphereMesh = SphereMesh.new()
	sphere_mesh.radius = radius_value
	sphere_mesh.height = radius_value * 2.0
	sphere_mesh.radial_segments = 10
	sphere_mesh.rings = 6
	mesh_instance.mesh = sphere_mesh
	mesh_instance.position = position_value
	mesh_instance.scale = scale_value
	mesh_instance.material_override = material_3d(color_value)
	mesh_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	parent.add_child(mesh_instance)
	return mesh_instance

func farm_world_position(cell: Vector2i, height_value: float = 0.0) -> Vector3:
	return Vector3((float(cell.x) - FARM_CENTER_X) * FARM_SPACING, height_value, (float(cell.y) - FARM_CENTER_Y) * FARM_SPACING)

func build_realistic_ground() -> void:
	add_box(realistic_root, Vector3(18.0, 0.28, 14.0), Vector3(0.0, -0.22, 0.0), Color("688f4d"), "Ground")
	add_box(realistic_root, Vector3(11.6, 0.08, 9.1), Vector3(0.0, -0.035, 0.15), Color("7cab5f"), "FarmClearing")
	var path_material: Color = Color("b99c6c")
	add_box(realistic_root, Vector3(1.1, 0.045, 13.5), Vector3(-5.15, 0.025, 0.0), path_material, "WestPath")
	add_box(realistic_root, Vector3(17.0, 0.045, 1.0), Vector3(0.0, 0.025, 5.55), path_material, "SouthPath")
	for index: int in range(18):
		var angle_value: float = float(index) * 0.73
		var x_value: float = -7.8 + float((index * 37) % 150) / 10.0
		var z_value: float = -6.0 + float((index * 53) % 112) / 10.0
		var stone: MeshInstance3D = add_sphere(realistic_root, 0.12 + float(index % 3) * 0.025, Vector3(x_value, 0.02, z_value), Color("b8aa8a"), Vector3(1.2, 0.28, 0.85), "PathStone")
		stone.rotation.y = angle_value

func build_realistic_farm_beds() -> void:
	for y_value: int in range(1, 7):
		for x_value: int in range(1, 8):
			var cell: Vector2i = Vector2i(x_value, y_value)
			var cell_root: Node3D = Node3D.new()
			cell_root.name = "Bed_%d_%d" % [x_value, y_value]
			cell_root.position = farm_world_position(cell)
			realistic_root.add_child(cell_root)
			add_box(cell_root, Vector3(1.05, 0.16, 1.05), Vector3(0.0, 0.08, 0.0), Color("5f3b27"), "BedBorder")
			var soil: MeshInstance3D = add_box(cell_root, Vector3(0.91, 0.13, 0.91), Vector3(0.0, 0.18, 0.0), Color("70452e"), "Soil")
			realistic_beds[tile_key(cell)] = soil
			for groove_index: int in range(3):
				var groove: MeshInstance3D = add_box(cell_root, Vector3(0.72, 0.012, 0.035), Vector3(0.0, 0.252, -0.22 + float(groove_index) * 0.22), Color("4b2e20"), "SoilGroove")
				groove.rotation.y = 0.0

func build_realistic_fence() -> void:
	var wood_color: Color = Color("795039")
	for index: int in range(13):
		var x_value: float = -6.2 + float(index) * 1.03
		add_cylinder(realistic_root, 0.07, 0.9, Vector3(x_value, 0.45, -5.0), wood_color, "FencePost")
		if index < 12:
			add_box(realistic_root, Vector3(0.96, 0.09, 0.09), Vector3(x_value + 0.51, 0.56, -5.0), wood_color, "FenceRail")
			add_box(realistic_root, Vector3(0.96, 0.075, 0.075), Vector3(x_value + 0.51, 0.30, -5.0), wood_color, "FenceRail")
	for index: int in range(11):
		var z_value: float = -4.6 + float(index) * 0.92
		add_cylinder(realistic_root, 0.07, 0.9, Vector3(5.85, 0.45, z_value), wood_color, "FencePost")
		if index < 10:
			add_box(realistic_root, Vector3(0.09, 0.09, 0.84), Vector3(5.85, 0.56, z_value + 0.46), wood_color, "FenceRail")
			add_box(realistic_root, Vector3(0.075, 0.075, 0.84), Vector3(5.85, 0.30, z_value + 0.46), wood_color, "FenceRail")

func build_realistic_decor() -> void:
	var tree_positions: Array[Vector3] = [
		Vector3(-7.2, 0.0, -4.8), Vector3(-5.9, 0.0, -5.7), Vector3(-3.9, 0.0, -5.8),
		Vector3(3.2, 0.0, -5.7), Vector3(5.6, 0.0, -5.4), Vector3(7.0, 0.0, -3.7),
		Vector3(-7.5, 0.0, 2.4), Vector3(7.1, 0.0, 1.8), Vector3(-6.9, 0.0, 5.4)
	]
	for index: int in range(tree_positions.size()):
		build_tree(tree_positions[index], 0.84 + float(index % 3) * 0.12)

	build_shed(Vector3(6.8, 0.0, 4.55))
	build_well(Vector3(-6.25, 0.0, 3.4))
	build_crates(Vector3(-5.7, 0.0, 5.05))
	build_water_tank(Vector3(5.9, 0.0, 3.55))

	for index: int in range(34):
		var x_value: float = -8.1 + float((index * 47) % 160) / 10.0
		var z_value: float = -6.2 + float((index * 71) % 125) / 10.0
		if absf(x_value) < 5.5 and absf(z_value) < 5.0:
			continue
		var flower_color: Color = [Color("f6d06f"), Color("f5a4bd"), Color("d6b1ff"), Color("f5f1d0")][index % 4]
		add_sphere(realistic_root, 0.055, Vector3(x_value, 0.13, z_value), flower_color, Vector3(1.0, 0.6, 1.0), "WildFlower")
		add_cylinder(realistic_root, 0.012, 0.18, Vector3(x_value, 0.055, z_value), Color("3f7d3e"), "FlowerStem")

func build_tree(position_value: Vector3, size_value: float) -> void:
	var tree_root: Node3D = Node3D.new()
	tree_root.position = position_value
	realistic_root.add_child(tree_root)
	add_cylinder(tree_root, 0.18 * size_value, 1.75 * size_value, Vector3(0.0, 0.87 * size_value, 0.0), Color("6f4930"), "TreeTrunk")
	add_sphere(tree_root, 0.72 * size_value, Vector3(0.0, 1.82 * size_value, 0.0), Color("3f743c"), Vector3(1.0, 0.9, 1.0), "TreeCrown")
	add_sphere(tree_root, 0.54 * size_value, Vector3(-0.42 * size_value, 1.67 * size_value, 0.12), Color("5a914b"), Vector3(1.0, 0.9, 1.0), "TreeCrown")
	add_sphere(tree_root, 0.50 * size_value, Vector3(0.43 * size_value, 1.70 * size_value, -0.10), Color("6aa653"), Vector3(1.0, 0.9, 1.0), "TreeCrown")

func build_shed(position_value: Vector3) -> void:
	var shed: Node3D = Node3D.new()
	shed.position = position_value
	realistic_root.add_child(shed)
	add_box(shed, Vector3(2.1, 1.65, 1.55), Vector3(0.0, 0.83, 0.0), Color("9b6847"), "ShedBody")
	var roof_left: MeshInstance3D = add_box(shed, Vector3(1.55, 0.18, 1.9), Vector3(-0.48, 1.75, 0.0), Color("5c3827"), "Roof")
	roof_left.rotation.z = deg_to_rad(28.0)
	var roof_right: MeshInstance3D = add_box(shed, Vector3(1.55, 0.18, 1.9), Vector3(0.48, 1.75, 0.0), Color("5c3827"), "Roof")
	roof_right.rotation.z = deg_to_rad(-28.0)
	add_box(shed, Vector3(0.62, 1.05, 0.08), Vector3(0.0, 0.55, -0.82), Color("4a3025"), "ShedDoor")
	add_box(shed, Vector3(0.38, 0.38, 0.07), Vector3(0.62, 1.05, -0.83), Color("9ed0d2"), "ShedWindow")

func build_well(position_value: Vector3) -> void:
	var well: Node3D = Node3D.new()
	well.position = position_value
	realistic_root.add_child(well)
	add_cylinder(well, 0.58, 0.48, Vector3(0.0, 0.24, 0.0), Color("8a8171"), "WellStone")
	add_cylinder(well, 0.41, 0.51, Vector3(0.0, 0.27, 0.0), Color("263b3c"), "WellWater")
	add_cylinder(well, 0.06, 1.65, Vector3(-0.62, 0.83, 0.0), Color("6d4b31"), "WellPost")
	add_cylinder(well, 0.06, 1.65, Vector3(0.62, 0.83, 0.0), Color("6d4b31"), "WellPost")
	add_box(well, Vector3(1.45, 0.10, 0.10), Vector3(0.0, 1.5, 0.0), Color("6d4b31"), "WellBeam")

func build_crates(position_value: Vector3) -> void:
	for index: int in range(4):
		var offset: Vector3 = Vector3(float(index % 2) * 0.55, float(index / 2) * 0.48 + 0.22, float(index % 2) * 0.12)
		add_box(realistic_root, Vector3(0.5, 0.45, 0.5), position_value + offset, Color("a46f42"), "ProduceCrate")

func build_water_tank(position_value: Vector3) -> void:
	add_cylinder(realistic_root, 0.52, 1.25, position_value + Vector3(0.0, 0.65, 0.0), Color("6f9e9e"), "WaterTank")
	add_cylinder(realistic_root, 0.18, 0.16, position_value + Vector3(0.0, 1.35, 0.0), Color("4e7777"), "WaterTankCap")

func build_realistic_characters() -> void:
	build_person(Vector3(-4.9, 0.0, 2.55), Color("4a8c54"), Color("d6a06d"), "FarmGuide")
	build_person(Vector3(4.85, 0.0, 2.9), Color("3d7f86"), Color("b77b4b"), "Researcher")
	build_person(Vector3(5.15, 0.0, -2.8), Color("b75d45"), Color("d4a06e"), "Merchant")
	build_person(Vector3(-1.6, 0.0, 4.6), Color("5684a6"), Color("efc37b"), "PlayerPreview")

func build_person(position_value: Vector3, shirt_color: Color, skin_color: Color, name_value: String) -> void:
	var person: Node3D = Node3D.new()
	person.name = name_value
	person.position = position_value
	realistic_root.add_child(person)
	add_cylinder(person, 0.22, 0.68, Vector3(0.0, 0.68, 0.0), shirt_color, "Body")
	add_sphere(person, 0.24, Vector3(0.0, 1.18, 0.0), skin_color, Vector3(1.0, 1.05, 1.0), "Head")
	add_cylinder(person, 0.075, 0.52, Vector3(-0.16, 0.28, 0.0), Color("2e4d5b"), "Leg")
	add_cylinder(person, 0.075, 0.52, Vector3(0.16, 0.28, 0.0), Color("2e4d5b"), "Leg")
	add_cylinder(person, 0.055, 0.5, Vector3(-0.29, 0.72, 0.0), skin_color, "Arm")
	add_cylinder(person, 0.055, 0.5, Vector3(0.29, 0.72, 0.0), skin_color, "Arm")
	var hat: MeshInstance3D = add_cylinder(person, 0.34, 0.08, Vector3(0.0, 1.42, 0.0), Color("d6a65c"), "HatBrim")
	hat.scale.z = 0.78
	add_cylinder(person, 0.23, 0.18, Vector3(0.0, 1.52, 0.0), Color("c9954e"), "HatTop")

func build_realistic_selected_marker() -> void:
	realistic_selected = MeshInstance3D.new()
	var marker_mesh: BoxMesh = BoxMesh.new()
	marker_mesh.size = Vector3(1.13, 0.035, 1.13)
	realistic_selected.mesh = marker_mesh
	realistic_selected.material_override = transparent_material_3d(Color(0.95, 0.78, 0.2, 0.22), Color("f5ce55"))
	realistic_selected.position = farm_world_position(selected, 0.31)
	realistic_root.add_child(realistic_selected)

func realistic_signature() -> String:
	var parts: PackedStringArray = PackedStringArray()
	for y_value: int in range(1, 7):
		for x_value: int in range(1, 8):
			var cell: Vector2i = Vector2i(x_value, y_value)
			var tile: Dictionary = dictionary_value(tiles, tile_key(cell))
			parts.append("%d,%d:%s:%d:%d:%d:%d" % [
				x_value, y_value, string_value(tile, "crop"), int_value(tile, "stage"),
				int(round(float_value(tile, "moisture"))), int(round(float_value(tile, "health"))),
				1 if bool(tile.get("tilled", false)) else 0
			])
	return "|".join(parts)

func sync_realistic_3d(force_value: bool) -> void:
	if realistic_root == null:
		return
	var signature_value: String = realistic_signature()
	if not force_value and signature_value == realistic_last_signature:
		if realistic_selected != null:
			realistic_selected.position = farm_world_position(selected, 0.31)
		return
	realistic_last_signature = signature_value
	for y_value: int in range(1, 7):
		for x_value: int in range(1, 8):
			var cell: Vector2i = Vector2i(x_value, y_value)
			var key_value: String = tile_key(cell)
			var tile: Dictionary = dictionary_value(tiles, key_value)
			var bed_value: Variant = realistic_beds.get(key_value)
			if bed_value is MeshInstance3D:
				var bed: MeshInstance3D = bed_value as MeshInstance3D
				var moisture_value: float = float_value(tile, "moisture")
				var fertility_value: float = float_value(tile, "fertility")
				var soil_color: Color = Color("6e422b")
				if moisture_value >= 82.0:
					soil_color = Color("3e342e")
				elif moisture_value >= 45.0:
					soil_color = Color("59402e")
				elif moisture_value < 24.0:
					soil_color = Color("8a5b38")
				if fertility_value >= 75.0:
					soil_color = soil_color.lightened(0.08)
				bed.material_override = material_3d(soil_color)
			var existing_value: Variant = realistic_crops.get(key_value)
			if existing_value is Node3D:
				(existing_value as Node3D).queue_free()
			realistic_crops.erase(key_value)
			var crop_id: String = string_value(tile, "crop")
			if not crop_id.is_empty():
				var crop_root: Node3D = build_realistic_crop(cell, crop_id, int_value(tile, "stage"), tile)
				realistic_crops[key_value] = crop_root
	if realistic_selected != null:
		realistic_selected.position = farm_world_position(selected, 0.31)

func build_realistic_crop(cell: Vector2i, crop_id: String, stage_value: int, tile: Dictionary) -> Node3D:
	var crop_root: Node3D = Node3D.new()
	crop_root.name = "Crop_%s_%d_%d" % [crop_id, cell.x, cell.y]
	crop_root.position = farm_world_position(cell, 0.28)
	realistic_root.add_child(crop_root)
	var stage_scale: float = 0.18 + float(clampi(stage_value, 0, 5)) * 0.13
	var health_factor: float = clampf(float_value(tile, "health", 100.0) / 100.0, 0.45, 1.0)
	var leaf_color: Color = Color("4f9648")
	if health_factor < 0.72:
		leaf_color = Color("9a8a42")
	match crop_id:
		"water_spinach":
			for index: int in range(5):
				var angle_value: float = float(index) * TAU / 5.0
				var offset: Vector3 = Vector3(cos(angle_value) * 0.17, 0.0, sin(angle_value) * 0.17)
				add_cylinder(crop_root, 0.025, stage_scale * 1.9, offset + Vector3(0.0, stage_scale * 0.95, 0.0), Color("4f8f48"), "Stem")
				add_sphere(crop_root, 0.15, offset + Vector3(0.0, stage_scale * 1.8, 0.0), leaf_color, Vector3(0.55, 0.25, 1.15), "Leaf")
		"kale":
			for index: int in range(8):
				var angle_value: float = float(index) * TAU / 8.0
				var radius_value: float = 0.12 + stage_scale * 0.23
				var offset: Vector3 = Vector3(cos(angle_value) * radius_value, stage_scale * 0.35, sin(angle_value) * radius_value)
				var leaf: MeshInstance3D = add_sphere(crop_root, 0.19 + stage_scale * 0.08, offset, leaf_color.darkened(float(index % 2) * 0.06), Vector3(0.85, 0.24, 1.25), "KaleLeaf")
				leaf.rotation.y = angle_value
		"chili", "tomato":
			add_cylinder(crop_root, 0.035, stage_scale * 2.2, Vector3(0.0, stage_scale * 1.1, 0.0), Color("477b3e"), "Stem")
			for index: int in range(6):
				var angle_value: float = float(index) * TAU / 6.0
				var y_value: float = 0.22 + float(index % 3) * stage_scale * 0.48
				var offset: Vector3 = Vector3(cos(angle_value) * (0.16 + stage_scale * 0.22), y_value, sin(angle_value) * (0.16 + stage_scale * 0.22))
				add_sphere(crop_root, 0.15 + stage_scale * 0.04, offset, leaf_color, Vector3(0.8, 0.28, 1.0), "Leaf")
				if stage_value >= 4 and index % 2 == 0:
					var fruit_color: Color = Color("e14f3f") if crop_id == "tomato" else Color("df6442")
					var fruit_scale: Vector3 = Vector3.ONE if crop_id == "tomato" else Vector3(0.55, 1.3, 0.55)
					add_sphere(crop_root, 0.095, offset + Vector3(0.0, -0.11, 0.0), fruit_color, fruit_scale, "Fruit")
		"cucumber":
			add_cylinder(crop_root, 0.035, stage_scale * 1.5, Vector3(0.0, stage_scale * 0.75, 0.0), Color("4a7b3d"), "Vine")
			for index: int in range(7):
				var angle_value: float = float(index) * 1.17
				var offset: Vector3 = Vector3(cos(angle_value) * (0.16 + float(index) * 0.035), 0.15 + float(index) * stage_scale * 0.23, sin(angle_value) * (0.16 + float(index) * 0.035))
				var leaf: MeshInstance3D = add_sphere(crop_root, 0.17, offset, leaf_color, Vector3(0.85, 0.2, 1.0), "Leaf")
				leaf.rotation.y = angle_value
				if stage_value >= 4 and index in [3, 5]:
					add_sphere(crop_root, 0.10, offset + Vector3(0.0, -0.13, 0.0), Color("4f9b4c"), Vector3(0.55, 1.5, 0.55), "Cucumber")
		_:
			add_sphere(crop_root, stage_scale, Vector3(0.0, stage_scale, 0.0), leaf_color, Vector3(1.0, 0.75, 1.0), "Plant")
	crop_root.scale = Vector3.ONE * health_factor
	return crop_root

func update_realistic_lighting() -> void:
	if realistic_sun == null or realistic_environment == null:
		return
	var hour_value: float = minutes / 60.0
	var daylight: float = clampf(1.0 - absf(hour_value - 13.0) / 8.5, 0.12, 1.0)
	var warm_mix: float = clampf(absf(hour_value - 13.0) / 7.0, 0.0, 1.0)
	realistic_sun.light_energy = 0.35 + daylight * 1.2
	realistic_sun.light_color = Color("ffe8bc").lerp(Color("ef9d62"), warm_mix * 0.48)
	realistic_environment.ambient_light_energy = 0.28 + daylight * 0.55
	var background_day: Color = Color("8fb579")
	var background_evening: Color = Color("5d665f")
	realistic_environment.background_color = background_evening.lerp(background_day, daylight)
	if current_weather in ["light_rain", "heavy_rain", "storm"]:
		realistic_environment.background_color = realistic_environment.background_color.darkened(0.18)
		realistic_environment.ambient_light_energy *= 0.72

func pick_cell(position: Vector2) -> Vector2i:
	if mode == "game" and not openworld_active and realistic_camera != null:
		var ray_origin: Vector3 = realistic_camera.project_ray_origin(position)
		var ray_direction: Vector3 = realistic_camera.project_ray_normal(position)
		var hit_value: Variant = Plane(Vector3.UP, 0.28).intersects_ray(ray_origin, ray_direction)
		if hit_value is Vector3:
			var hit: Vector3 = hit_value as Vector3
			var cell_x: int = roundi(hit.x / FARM_SPACING + FARM_CENTER_X)
			var cell_y: int = roundi(hit.z / FARM_SPACING + FARM_CENTER_Y)
			return Vector2i(cell_x, cell_y)
	return super.pick_cell(position)

func panel(rect_value: Rect2, fill: Color) -> void:
	var shadow_box: StyleBoxFlat = StyleBoxFlat.new()
	shadow_box.bg_color = Color(0.10, 0.07, 0.04, 0.20)
	shadow_box.corner_radius_top_left = 12
	shadow_box.corner_radius_top_right = 12
	shadow_box.corner_radius_bottom_left = 12
	shadow_box.corner_radius_bottom_right = 12
	shadow_box.shadow_color = Color(0.03, 0.02, 0.01, 0.36)
	shadow_box.shadow_size = 9
	shadow_box.shadow_offset = Vector2(0.0, 4.0)
	draw_style_box(shadow_box, rect_value)
	var panel_box: StyleBoxFlat = StyleBoxFlat.new()
	panel_box.bg_color = Color(fill.r, fill.g, fill.b, 0.94)
	panel_box.border_color = Color("60442f")
	panel_box.set_border_width_all(2)
	panel_box.corner_radius_top_left = 10
	panel_box.corner_radius_top_right = 10
	panel_box.corner_radius_bottom_left = 10
	panel_box.corner_radius_bottom_right = 10
	draw_style_box(panel_box, rect_value)

func draw_bar(rect_value: Rect2, value: float, color: Color, label: String) -> void:
	var background_box: StyleBoxFlat = StyleBoxFlat.new()
	background_box.bg_color = Color(0.08, 0.08, 0.07, 0.78)
	background_box.corner_radius_top_left = 4
	background_box.corner_radius_top_right = 4
	background_box.corner_radius_bottom_left = 4
	background_box.corner_radius_bottom_right = 4
	draw_style_box(background_box, rect_value)
	var fill_rect: Rect2 = Rect2(rect_value.position + Vector2(2.0, 2.0), Vector2((rect_value.size.x - 4.0) * clampf(value / 100.0, 0.0, 1.0), rect_value.size.y - 4.0))
	var fill_box: StyleBoxFlat = StyleBoxFlat.new()
	fill_box.bg_color = color
	fill_box.corner_radius_top_left = 3
	fill_box.corner_radius_top_right = 3
	fill_box.corner_radius_bottom_left = 3
	fill_box.corner_radius_bottom_right = 3
	draw_style_box(fill_box, fill_rect)
	draw_text(label + " %d" % int(round(value)), rect_value.position + Vector2(6.0, rect_value.size.y - 5.0), 13, Color.WHITE)

func _draw() -> void:
	if realistic_viewport != null and (mode == "menu" or (mode == "game" and not openworld_active)):
		draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color("263126"))
		draw_texture_rect(realistic_viewport.get_texture(), Rect2(0.0, 0.0, 1280.0, 720.0), false)
		if mode == "menu":
			draw_rect(Rect2(0.0, 0.0, 1280.0, 720.0), Color(0.05, 0.04, 0.025, 0.42))
			draw_menu()
			return
		draw_rect(Rect2(0.0, 0.0, 1280.0, 102.0), Color(0.94, 0.93, 0.86, 0.90))
		draw_rect(Rect2(0.0, 620.0, 1280.0, 100.0), Color(0.24, 0.14, 0.08, 0.92))
		draw_hud()
		if not overlay.is_empty():
			draw_overlay()
		return
	super._draw()
