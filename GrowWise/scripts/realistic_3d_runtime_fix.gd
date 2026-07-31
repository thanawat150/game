extends "res://scripts/realistic_3d_layer.gd"

func build_realistic_3d_farm() -> void:
	realistic_viewport = SubViewport.new()
	realistic_viewport.name = "RealisticFarmViewport"
	realistic_viewport.size = Vector2i(1280, 720)
	realistic_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	realistic_viewport.msaa_3d = Viewport.MSAA_4X
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
	realistic_root.add_child(realistic_camera)
	realistic_camera.look_at_from_position(Vector3(10.6, 12.8, 14.6), Vector3(0.0, 0.25, 0.0), Vector3.UP)
	realistic_camera.current = true

	build_realistic_ground()
	build_realistic_farm_beds()
	build_realistic_fence()
	build_realistic_decor()
	build_realistic_characters()
	build_realistic_selected_marker()
