extends Node3D
class_name GrowWiseWorldBootstrap

const FARM_PLOT_SCENE := preload("res://scenes/farming/FarmPlot.tscn")
const NPC_SCENE := preload("res://scenes/npc/NPCBase.tscn")

func _ready() -> void:
	build_farm_grid()
	spawn_placeholder_npcs()
	print("GROWWISE3D_WORLD_SCAFFOLD_OK")

func build_farm_grid() -> void:
	var plots_root := Node3D.new()
	plots_root.name = "FarmPlots"
	add_child(plots_root)
	for y in range(4):
		for x in range(6):
			var plot := FARM_PLOT_SCENE.instantiate() as GrowWiseFarmPlot
			plot.plot_id = "farm_%02d_%02d" % [x, y]
			plot.position = Vector3((x - 2.5) * 2.15, 0.15, (y - 1.5) * 2.15)
			plots_root.add_child(plot)

func spawn_placeholder_npcs() -> void:
	var npc_data := [
		{"name":"ครูเมล็ดพันธุ์", "position":Vector3(-7, 0, 3), "route":[Vector3(-7,0,3), Vector3(-5,0,-3), Vector3(-2,0,-4)]},
		{"name":"นักวิจัยต้น", "position":Vector3(7, 0, 2), "route":[Vector3(7,0,2), Vector3(5,0,-4), Vector3(3,0,4)]},
		{"name":"ช่างน้ำวิน", "position":Vector3(0, 0, 7), "route":[Vector3(0,0,7), Vector3(-4,0,5), Vector3(4,0,5)]}
	]
	for entry in npc_data:
		var npc := NPC_SCENE.instantiate() as GrowWiseNPCController
		npc.display_name = str(entry["name"])
		npc.position = entry["position"]
		npc.patrol_points.assign(entry["route"])
		add_child(npc)
