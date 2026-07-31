extends "res://scripts/town_layer.gd"

const GrowWiseInfrastructure = preload("res://scripts/infrastructure_system.gd")

var infrastructure_data: Dictionary = {}
var water_state: Dictionary = {}
var selected_water_tool: String = "drain_channel"
var water_direction: int = 0
var logistics_state: Dictionary = {}
var wardrobe_state: Dictionary = {}
var last_game_clock: float = 0.0

func _ready() -> void:
	infrastructure_data = load_json("res://data/infrastructure.json")
	super._ready()
	last_game_clock = float(day * 1440) + minutes
	var test_result: Dictionary = GrowWiseInfrastructure.self_test(infrastructure_data)
	if bool(test_result.get("ok", false)):
		print("GROWWISE_WATER_LOGISTICS_OUTFIT_OK")
	else:
		push_error("Infrastructure self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var custom: Dictionary = {
		"ui.water_management":{"th":"จัดการน้ำ","en":"Water"},
		"ui.logistics":{"th":"ขนส่ง","en":"Logistics"},
		"ui.wardrobe":{"th":"แต่งตัว","en":"Wardrobe"},
		"ui.pond":{"th":"บ่อรับน้ำ","en":"Retention Pond"},
		"ui.gate":{"th":"ประตูน้ำ","en":"Water Gate"},
		"ui.pump":{"th":"ปั๊มน้ำ","en":"Water Pump"}
	}
	if custom.has(key_name):
		var value: Dictionary = custom[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	water_state = GrowWiseInfrastructure.default_water_state()
	selected_water_tool = "drain_channel"
	water_direction = 0
	logistics_state = GrowWiseInfrastructure.default_logistics_state()
	wardrobe_state = GrowWiseInfrastructure.default_wardrobe_state()
	last_game_clock = float(day * 1440) + minutes
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game":
		match event.keycode:
			KEY_U:
				overlay = "water_management"
				return
			KEY_V:
				overlay = "logistics"
				return
			KEY_O:
				overlay = "wardrobe"
				return
	super._unhandled_input(event)

func _process(delta: float) -> void:
	super._process(delta)
	var current_clock: float = float(day * 1440) + minutes
	var elapsed_minutes: float = current_clock - last_game_clock
	if elapsed_minutes > 0.0 and elapsed_minutes < 2000.0:
		advance_logistics(elapsed_minutes / 60.0)
	last_game_clock = current_clock

func advance_day() -> void:
	super.advance_day()
	apply_daily_water_network()

func weather_rain_amount() -> float:
	match current_weather:
		"light_rain": return 12.0
		"heavy_rain": return 28.0
		"storm": return 42.0
		_: return 0.0

func apply_daily_water_network() -> void:
	var pump_cost_available: bool = money >= 8
	if bool(water_state.get("pump_built", false)) and bool(water_state.get("pump_on", false)) and pump_cost_available:
		money -= 8
		expenses += 8
	var any_wet: bool = false
	var any_dry: bool = false
	for key_value: Variant in tiles.keys():
		var moisture: float = float_value(dictionary_value(tiles,String(key_value)),"moisture")
		if moisture > 85.0: any_wet = true
		if moisture < 28.0: any_dry = true
	if autoplay_mode != GrowWiseAutoPlay.MODE_OFF and bool(water_state.get("gate_built", false)):
		water_state["gate_open"] = any_wet or any_dry
	if autoplay_mode != GrowWiseAutoPlay.MODE_OFF and bool(water_state.get("pump_built", false)):
		water_state["pump_on"] = any_dry and float(water_state.get("pond_level",0.0)) > 20.0
	var result: Dictionary = GrowWiseInfrastructure.apply_water_network(water_state,tiles,W,H,weather_rain_amount(),pump_cost_available)
	water_state = dictionary_value(result,"water_state")
	tiles = dictionary_value(result,"tiles")
	var drained: int = int(round(float(water_state.get("daily_drained",0.0))))
	var irrigated: int = int(round(float(water_state.get("daily_irrigated",0.0))))
	if drained > 0 or irrigated > 0:
		add_feedback("ระบบน้ำ: ระบาย %d • ส่งน้ำ %d" % [drained,irrigated],Vector2(710,135),BLUE)

func pay_infrastructure_cost(cost: Dictionary) -> bool:
	if not GrowWiseInfrastructure.can_pay(cost,inventory,money):
		notify("ทรัพยากรหรือเงินไม่พอ","error")
		return false
	for item_id: String in cost:
		var amount: int = int(cost[item_id])
		if item_id == "money":
			money -= amount
			expenses += amount
		else:
			inventory[item_id] = int(inventory.get(item_id,0))-amount
	return true

func place_water_structure(tool_id: String) -> void:
	var definition: Dictionary = GrowWiseInfrastructure.water_tool_definition(infrastructure_data,tool_id)
	if definition.is_empty(): return
	if tool_id == "retention_pond" and bool(water_state.get("pond_built",false)):
		notify("สร้างบ่อรับน้ำแล้ว","success"); return
	if tool_id == "water_gate" and bool(water_state.get("gate_built",false)):
		notify("สร้างประตูน้ำแล้ว","success"); return
	if tool_id == "water_pump" and bool(water_state.get("pump_built",false)):
		notify("ติดตั้งปั๊มน้ำแล้ว","success"); return
	if not pay_infrastructure_cost(dictionary_value(definition,"cost")): return
	match tool_id:
		"drain_channel": water_state = GrowWiseInfrastructure.add_channel(water_state,selected,"drain",water_direction)
		"irrigation_channel": water_state = GrowWiseInfrastructure.add_channel(water_state,selected,"irrigate",water_direction)
		"levee": water_state = GrowWiseInfrastructure.add_levee(water_state,selected,water_direction)
		"water_gate": water_state["gate_built"] = true; water_state["gate_open"] = true
		"water_pump": water_state["pump_built"] = true; water_state["pump_on"] = false
		"retention_pond": water_state["pond_built"] = true; water_state["pond_level"] = 100.0
	add_farm_xp(12,"สร้างระบบน้ำ",iso(Vector2(selected))+Vector2(0,-55))
	notify("ติดตั้ง%sแล้ว" % string_value(definition,"name"),"success")

func remove_selected_water_structure() -> void:
	water_state = GrowWiseInfrastructure.remove_water_structure(water_state,selected)
	notify("รื้อระบบน้ำในช่องที่เลือกแล้ว","success")

func buy_vehicle(vehicle_id: String) -> void:
	var definition: Dictionary = GrowWiseInfrastructure.vehicle_definition(infrastructure_data,vehicle_id)
	if definition.is_empty(): return
	var owned: Dictionary = dictionary_value(logistics_state,"owned_vehicles")
	if bool(owned.get(vehicle_id,false)):
		logistics_state["selected_vehicle"] = vehicle_id
		notify("เลือกรถ %s" % string_value(definition,"name"),"success")
		return
	if not pay_infrastructure_cost(dictionary_value(definition,"cost")): return
	owned[vehicle_id] = true
	logistics_state["owned_vehicles"] = owned
	logistics_state["selected_vehicle"] = vehicle_id
	add_farm_xp(20,"ซื้อรถขนส่ง",Vector2(720,150))
	notify("ซื้อ%sแล้ว" % string_value(definition,"name"),"success")

func start_city_delivery() -> void:
	if not dictionary_value(logistics_state,"active_trip").is_empty():
		notify("รถกำลังเดินทางอยู่","error"); return
	var selected_outfit: String = String(wardrobe_state.get("selected","garden"))
	var trip: Dictionary = GrowWiseInfrastructure.create_trip(infrastructure_data,logistics_state,inventory,selected_outfit)
	if trip.is_empty():
		notify("ยังไม่มีรถหรือสินค้าให้ขน","error"); return
	var transport_cost: int = int(trip.get("transport_cost",0))
	if money < transport_cost:
		notify("เงินค่าขนส่งไม่พอ","error"); return
	var cargo: Dictionary = dictionary_value(trip,"cargo")
	for item_id: String in cargo:
		inventory[item_id] = int(inventory.get(item_id,0))-int(cargo[item_id])
	money -= transport_cost
	expenses += transport_cost
	logistics_state["active_trip"] = trip
	notify("รถออกเดินทาง • %.1f ชั่วโมง" % float(trip.get("hours_total",0.0)),"success")

func advance_logistics(game_hours: float) -> void:
	if dictionary_value(logistics_state,"active_trip").is_empty(): return
	var result: Dictionary = GrowWiseInfrastructure.advance_trip(logistics_state,game_hours)
	logistics_state = dictionary_value(result,"state")
	var completed: Dictionary = dictionary_value(result,"completed")
	if completed.is_empty(): return
	var gross: int = int(completed.get("gross",0))
	money += gross
	revenue += gross
	if String(completed.get("vehicle","")) == "electric_truck": eco_score = mini(100,eco_score+3)
	add_farm_xp(18,"ส่งสินค้าเข้าเมือง",Vector2(820,150))
	add_feedback("รถกลับแล้ว • รายรับ %d • สุทธิ %d" % [gross,int(completed.get("net",0))],Vector2(820,150),GOLD)

func buy_or_select_outfit(outfit_id: String) -> void:
	var definition: Dictionary = GrowWiseInfrastructure.outfit_definition(infrastructure_data,outfit_id)
	if definition.is_empty(): return
	var owned: Dictionary = dictionary_value(wardrobe_state,"owned")
	if bool(owned.get(outfit_id,false)):
		wardrobe_state["selected"] = outfit_id
		notify("สวม%s" % string_value(definition,"name"),"success")
		return
	if farm_level < int_value(definition,"level",1):
		notify("ต้องมีระดับสวน %d" % int_value(definition,"level",1),"error"); return
	var price: int = int_value(definition,"cost")
	if money < price:
		notify(tx("msg.no_money"),"error"); return
	money -= price
	expenses += price
	owned[outfit_id] = true
	wardrobe_state["owned"] = owned
	wardrobe_state["selected"] = outfit_id
	notify("ซื้อและสวม%sแล้ว" % string_value(definition,"name"),"success")

func save_game(slot_number: int,automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number,automatic)
	if not result: return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR,slot_number]
	var payload: Dictionary = read_save(path)
	payload["water_state"] = water_state
	payload["selected_water_tool"] = selected_water_tool
	payload["water_direction"] = water_direction
	payload["logistics_state"] = logistics_state
	payload["wardrobe_state"] = wardrobe_state
	var file: FileAccess = FileAccess.open(path,FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(payload)); file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result: return false
	var payload: Dictionary = read_save("%s/slot_%d.json" % [SAVE_DIR,slot_number])
	water_state = dictionary_value(payload,"water_state")
	if water_state.is_empty(): water_state = GrowWiseInfrastructure.default_water_state()
	selected_water_tool = string_value(payload,"selected_water_tool","drain_channel")
	water_direction = int_value(payload,"water_direction",0)
	logistics_state = dictionary_value(payload,"logistics_state")
	if logistics_state.is_empty(): logistics_state = GrowWiseInfrastructure.default_logistics_state()
	wardrobe_state = dictionary_value(payload,"wardrobe_state")
	if wardrobe_state.is_empty(): wardrobe_state = GrowWiseInfrastructure.default_wardrobe_state()
	last_game_clock = float(day*1440)+minutes
	return true

func overlay_click(position: Vector2) -> void:
	if overlay in ["water_management","logistics","wardrobe"]:
		if Rect2(1012,76,42,36).has_point(position): overlay=""; return
		if overlay == "water_management": handle_water_click(position); return
		if overlay == "logistics": handle_logistics_click(position); return
		if overlay == "wardrobe": handle_wardrobe_click(position); return
	super.overlay_click(position)

func handle_water_click(position: Vector2) -> void:
	var tools: Array = array_value(infrastructure_data,"water_tools")
	for index: int in range(tools.size()):
		var column: int = index%2
		var row: int = int(index/2)
		if Rect2(255+column*390,155+row*72,370,60).has_point(position):
			selected_water_tool = string_value(tools[index] as Dictionary,"id")
			return
	if Rect2(255,405,170,44).has_point(position): water_direction=posmod(water_direction-1,4); return
	if Rect2(435,405,170,44).has_point(position): water_direction=posmod(water_direction+1,4); return
	if Rect2(625,405,190,44).has_point(position): place_water_structure(selected_water_tool); return
	if Rect2(825,405,170,44).has_point(position): remove_selected_water_structure(); return
	if Rect2(255,485,240,44).has_point(position) and bool(water_state.get("gate_built",false)):
		water_state["gate_open"] = not bool(water_state.get("gate_open",true)); return
	if Rect2(510,485,240,44).has_point(position) and bool(water_state.get("pump_built",false)):
		water_state["pump_on"] = not bool(water_state.get("pump_on",false)); return

func handle_logistics_click(position: Vector2) -> void:
	var vehicles: Array = array_value(infrastructure_data,"vehicles")
	for index: int in range(vehicles.size()):
		if Rect2(250,150+index*75,760,62).has_point(position): buy_vehicle(string_value(vehicles[index] as Dictionary,"id")); return
	if Rect2(430,505,420,50).has_point(position): start_city_delivery(); return

func handle_wardrobe_click(position: Vector2) -> void:
	var outfits: Array = array_value(infrastructure_data,"outfits")
	for index: int in range(outfits.size()):
		var column: int = index%2
		var row: int = int(index/2)
		if Rect2(250+column*390,150+row*85,370,72).has_point(position): buy_or_select_outfit(string_value(outfits[index] as Dictionary,"id")); return

func draw_overlay() -> void:
	match overlay:
		"water_management": draw_water_overlay()
		"logistics": draw_logistics_overlay()
		"wardrobe": draw_wardrobe_overlay()
		_: super.draw_overlay()

func draw_water_overlay() -> void:
	draw_expansion_shell(tx("ui.water_management"))
	var tile: Dictionary = dictionary_value(tiles,tile_key(selected))
	var advice: String = GrowWiseInfrastructure.overwater_advice(float_value(tile,"moisture"),dictionary_value(water_state,"channels").has(tile_key(selected)),bool(water_state.get("pond_built",false)))
	draw_text("ช่อง (%d,%d) • ความชื้น %d%% • ทิศ %s" % [selected.x,selected.y,int(round(float_value(tile,"moisture"))),GrowWiseInfrastructure.direction_name(water_direction)],Vector2(250,128),16,GREEN)
	if not advice.is_empty(): draw_text(advice,Vector2(250,151),14,RED,740.0)
	var tools: Array = array_value(infrastructure_data,"water_tools")
	for index: int in range(tools.size()):
		var definition: Dictionary = tools[index] as Dictionary
		var column: int=index%2; var row: int=int(index/2)
		var rect_value: Rect2=Rect2(255+column*390,175+row*72,370,60)
		panel(rect_value,GOLD if selected_water_tool==string_value(definition,"id") else MIST)
		draw_text(string_value(definition,"name"),rect_value.position+Vector2(10,24),16,GREEN)
		draw_text(GrowWiseInfrastructure.cost_text(dictionary_value(definition,"cost")),rect_value.position+Vector2(10,48),12,INK)
	panel(Rect2(255,405,170,44),WOOD_LIGHT); draw_text("หมุนซ้าย",Vector2(265,435),16,Color.WHITE,150.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(435,405,170,44),WOOD_LIGHT); draw_text("หมุนขวา",Vector2(445,435),16,Color.WHITE,150.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(625,405,190,44),TEAL); draw_text("สร้างที่ช่องเลือก",Vector2(635,435),16,Color.WHITE,170.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(825,405,170,44),RED); draw_text("รื้อ",Vector2(835,435),16,Color.WHITE,150.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(255,485,240,44),TEAL if bool(water_state.get("gate_open",false)) else MIST)
	draw_text("ประตูน้ำ: %s" % ["เปิด" if bool(water_state.get("gate_open",false)) else "ปิด"],Vector2(265,515),16,Color.WHITE if bool(water_state.get("gate_open",false)) else INK,220.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(510,485,240,44),TEAL if bool(water_state.get("pump_on",false)) else MIST)
	draw_text("ปั๊ม: %s" % ["ทำงาน" if bool(water_state.get("pump_on",false)) else "หยุด"],Vector2(520,515),16,Color.WHITE if bool(water_state.get("pump_on",false)) else INK,220.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("บ่อรับน้ำ %.0f/%.0f • ระบายวันนี้ %.0f • ส่งน้ำ %.0f" % [float(water_state.get("pond_level",0.0)),float(water_state.get("pond_capacity",900.0)),float(water_state.get("daily_drained",0.0)),float(water_state.get("daily_irrigated",0.0))],Vector2(255,575),16,BLUE)

func draw_logistics_overlay() -> void:
	draw_expansion_shell(tx("ui.logistics"))
	var owned: Dictionary = dictionary_value(logistics_state,"owned_vehicles")
	var selected_vehicle: String = String(logistics_state.get("selected_vehicle",""))
	var vehicles: Array = array_value(infrastructure_data,"vehicles")
	for index: int in range(vehicles.size()):
		var vehicle: Dictionary = vehicles[index] as Dictionary
		var vehicle_id: String = string_value(vehicle,"id")
		var rect_value: Rect2 = Rect2(250,150+index*75,760,62)
		panel(rect_value,GOLD if selected_vehicle==vehicle_id else (TEAL if bool(owned.get(vehicle_id,false)) else MIST))
		draw_text(string_value(vehicle,"name"),rect_value.position+Vector2(12,26),17,GREEN)
		draw_text("จุ %d • %.1f ชม. • ค่าขน %d • ราคา x%.2f" % [int_value(vehicle,"capacity"),float(vehicle.get("trip_hours",0.0)),int_value(vehicle,"transport_cost"),float(vehicle.get("price_multiplier",1.0))],rect_value.position+Vector2(245,26),14,INK,490.0,HORIZONTAL_ALIGNMENT_RIGHT)
	var trip: Dictionary = dictionary_value(logistics_state,"active_trip")
	if trip.is_empty():
		panel(Rect2(430,505,420,50),TEAL); draw_text("จัดของและส่งเข้าเมือง",Vector2(440,539),19,Color.WHITE,400.0,HORIZONTAL_ALIGNMENT_CENTER)
	else:
		draw_text("รถกำลังเดินทาง • เหลือ %.1f ชม. • สินค้า %d • สุทธิ %d" % [float(trip.get("hours_left",0.0)),GrowWiseInfrastructure.cargo_count(dictionary_value(trip,"cargo")),int(trip.get("net",0))],Vector2(300,540),18,GOLD,680.0,HORIZONTAL_ALIGNMENT_CENTER)

func draw_wardrobe_overlay() -> void:
	draw_expansion_shell(tx("ui.wardrobe"))
	var owned: Dictionary = dictionary_value(wardrobe_state,"owned")
	var selected_outfit: String = String(wardrobe_state.get("selected","garden"))
	var outfits: Array = array_value(infrastructure_data,"outfits")
	for index: int in range(outfits.size()):
		var outfit: Dictionary = outfits[index] as Dictionary
		var outfit_id: String = string_value(outfit,"id")
		var column: int=index%2; var row: int=int(index/2)
		var rect_value: Rect2=Rect2(250+column*390,150+row*85,370,72)
		panel(rect_value,GOLD if selected_outfit==outfit_id else (TEAL if bool(owned.get(outfit_id,false)) else MIST))
		draw_text(string_value(outfit,"name"),rect_value.position+Vector2(12,25),17,GREEN)
		draw_text("Lv.%d • ราคา %d" % [int_value(outfit,"level"),int_value(outfit,"cost")],rect_value.position+Vector2(230,25),13,GOLD,120.0,HORIZONTAL_ALIGNMENT_RIGHT)
		draw_text(string_value(outfit,"bonus"),rect_value.position+Vector2(12,52),12,INK,340.0)

func draw_world() -> void:
	super.draw_world()
	draw_water_network()
	draw_transport_vehicle()
	draw_player_outfit()

func draw_water_network() -> void:
	var channels: Dictionary = dictionary_value(water_state,"channels")
	for key_value: Variant in channels.keys():
		var key_string: String = String(key_value)
		var parts: PackedStringArray = key_string.split(",")
		if parts.size()!=2: continue
		var cell: Vector2i=Vector2i(int(parts[0]),int(parts[1]))
		var channel: Dictionary=dictionary_value(channels,key_string)
		var center: Vector2=iso(Vector2(cell))
		var direction_vector: Vector2i=GrowWiseInfrastructure.direction_vector(int(channel.get("direction",0)))
		var endpoint: Vector2=center+Vector2(direction_vector.x-direction_vector.y,direction_vector.x+direction_vector.y)*18.0
		var color_value: Color=BLUE if String(channel.get("mode",""))=="irrigate" else Color("654b43")
		draw_line(center,endpoint,color_value,5.0)
		draw_circle(endpoint,3.0,color_value)
	var levees: Dictionary=dictionary_value(water_state,"levees")
	for key_value: Variant in levees.keys():
		var key_string: String=String(key_value); var parts: PackedStringArray=key_string.split(",")
		if parts.size()!=2: continue
		var center: Vector2=iso(Vector2(int(parts[0]),int(parts[1])))
		draw_line(center+Vector2(-22,6),center+Vector2(22,6),Color("b97a4d"),5.0)
	if bool(water_state.get("pond_built",false)):
		draw_circle(Vector2(950,185),26.0,BLUE); draw_circle(Vector2(950,181),18.0,Color("8bc8d1"))

func draw_transport_vehicle() -> void:
	var trip: Dictionary=dictionary_value(logistics_state,"active_trip")
	if trip.is_empty(): return
	var progress: float=1.0-float(trip.get("hours_left",0.0))/maxf(0.1,float(trip.get("hours_total",1.0)))
	var p: Vector2=Vector2(285+progress*650.0,565-sin(progress*PI)*55.0)
	draw_rect(Rect2(p+Vector2(-16,-8),Vector2(32,14)),Color("c65a4b"))
	draw_rect(Rect2(p+Vector2(-7,-17),Vector2(16,10)),Color("f3e5c2"))
	draw_circle(p+Vector2(-10,8),5.0,Color("29302a")); draw_circle(p+Vector2(10,8),5.0,Color("29302a"))

func draw_player_outfit() -> void:
	var outfit: Dictionary=GrowWiseInfrastructure.outfit_definition(infrastructure_data,String(wardrobe_state.get("selected","garden")))
	if outfit.is_empty(): return
	var p: Vector2=iso(player_position)-Vector2(0,35)
	var shirt: Color=Color(String(outfit.get("shirt","4e9bb3")))
	draw_rect(Rect2(p+Vector2(-7,7),Vector2(14,13)),shirt)
	var hat_type: String=String(outfit.get("hat","straw"))
	if hat_type=="helmet":
		draw_circle(p+Vector2(0,-5),9.0,Color("e9b84d")); draw_rect(Rect2(p+Vector2(-11,-3),Vector2(22,4)),Color("d77a45"))
	elif hat_type=="hood":
		draw_circle(p+Vector2(0,-3),9.0,shirt.lightened(0.15))
	else:
		draw_rect(Rect2(p+Vector2(-10,-9),Vector2(20,5)),Color("e9b84d"))

func draw_inspector() -> void:
	super.draw_inspector()
	var tile: Dictionary=dictionary_value(tiles,tile_key(selected))
	var advice: String=GrowWiseInfrastructure.overwater_advice(float_value(tile,"moisture"),dictionary_value(water_state,"channels").has(tile_key(selected)),bool(water_state.get("pond_built",false)))
	if not advice.is_empty(): draw_text(advice,Vector2(1028,470),11,RED,218.0)
