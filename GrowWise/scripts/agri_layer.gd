extends "res://scripts/infrastructure_layer.gd"

const GrowWiseAgriExpansion = preload("res://scripts/agri_expansion_system.gd")

var agri_data: Dictionary = {}
var survey_state: Dictionary = {}
var animal_state: Dictionary = {}
var selected_survey_area: int = 0
var selected_lab_test: int = 0
var selected_lab_report: int = -1
var selected_animal_id: String = "chicken"
var selected_animal_building: String = "coop"

func _ready() -> void:
	agri_data = load_json("res://data/agri_expansion.json")
	super._ready()
	var test_result: Dictionary = GrowWiseAgriExpansion.self_test(agri_data)
	if bool(test_result.get("ok",false)):
		print("GROWWISE_SURVEY_LAB_ANIMALS_PROCESSING_OK")
	else:
		push_error("Agriculture expansion self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var custom: Dictionary = {
		"ui.survey":{"th":"สำรวจพื้นที่","en":"Survey"},
		"ui.soil_lab":{"th":"แล็บดิน","en":"Soil Lab"},
		"ui.animals":{"th":"เลี้ยงสัตว์","en":"Livestock"},
		"ui.processing":{"th":"แปรรูป","en":"Processing"},
		"ui.suitability":{"th":"ความเหมาะสมพืช","en":"Crop Suitability"}
	}
	if custom.has(key_name):
		var value: Dictionary = custom[key_name] as Dictionary
		return String(value.get(language,value.get("th",key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	survey_state = GrowWiseAgriExpansion.default_survey_state()
	animal_state = GrowWiseAgriExpansion.default_animal_state(agri_data)
	selected_survey_area = 0
	selected_lab_test = 0
	selected_lab_report = -1
	selected_animal_id = "chicken"
	selected_animal_building = "coop"
	var additions: Dictionary = {
		"grain_feed":4,"roughage_feed":0,"mixed_feed":0,"flower_feed":0,
		"egg":0,"duck_egg":0,"goat_milk":0,"milk":0,"honey":0,"manure":0,
		"clay":0,"wild_seed":0,"salt":0,
		"meal_boiled_egg":0,"processed_salted_egg":0,"processed_milk":0,
		"processed_goat_cheese":0,"processed_honey":0,"manure_compost":0,
		"processed_dried_fish":0,"processed_vegetable_sauce":0,"meal_farm_set":0
	}
	for item_id: String in additions:
		inventory[item_id] = additions[item_id]

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game":
		match event.keycode:
			KEY_Q:
				overlay = "survey"
				return
			KEY_L:
				overlay = "soil_lab"
				return
			KEY_A:
				overlay = "animals"
				return
			KEY_G:
				overlay = "processing"
				return
	super._unhandled_input(event)

func advance_day() -> void:
	super.advance_day()
	reset_survey_attempts()
	process_lab_results()
	simulate_livestock_day()

func reset_survey_attempts() -> void:
	if int(survey_state.get("attempt_day",day)) != day:
		survey_state["attempt_day"] = day
		survey_state["attempts"] = 3 + int(building_levels.get("research_center",0))

func survey_area(index: int) -> void:
	var areas: Array = array_value(agri_data,"survey_areas")
	if index < 0 or index >= areas.size(): return
	if int(survey_state.get("attempts",0)) <= 0:
		notify("วันนี้สำรวจพื้นที่ครบแล้ว","error"); return
	var area: Dictionary = areas[index] as Dictionary
	if farm_level < int_value(area,"level",1):
		notify("ต้องมีระดับสวน %d" % int_value(area,"level",1),"error"); return
	var cost: int = int_value(area,"cost")
	if money < cost:
		notify(tx("msg.no_money"),"error"); return
	money -= cost
	expenses += cost
	var sample_index: int = int(survey_state.get("total_surveys",0))+1
	var sample: Dictionary = GrowWiseAgriExpansion.generate_sample(agri_data,string_value(area,"id"),day,sample_index)
	var samples: Array = array_value(survey_state,"samples")
	samples.push_front(sample)
	if samples.size()>20: samples.resize(20)
	survey_state["samples"] = samples
	survey_state["attempts"] = int(survey_state.get("attempts",0))-1
	survey_state["total_surveys"] = sample_index
	var finds: Array = array_value(area,"finds")
	if not finds.is_empty():
		var found_id: String = String(finds[posmod(day+sample_index,finds.size())])
		inventory[found_id] = int(inventory.get(found_id,0))+1
	add_farm_xp(10,"สำรวจและเก็บตัวอย่าง",Vector2(640,150))
	notify("เก็บตัวอย่างจาก%sแล้ว" % string_value(area,"name"),"success")

func queue_selected_lab_test() -> void:
	var samples: Array = array_value(survey_state,"samples")
	var tests: Array = array_value(agri_data,"lab_tests")
	if samples.is_empty():
		notify("ต้องสำรวจและเก็บตัวอย่างก่อน","error"); return
	if selected_lab_test < 0 or selected_lab_test >= tests.size(): return
	var test_definition: Dictionary = tests[selected_lab_test] as Dictionary
	if string_value(test_definition,"id") == "complete_soil" and int(building_levels.get("research_center",0)) <= 0:
		notify("สร้างศูนย์วิจัยก่อนใช้การตรวจฉบับสมบูรณ์","error"); return
	var cost: int = int_value(test_definition,"cost")
	if money < cost:
		notify(tx("msg.no_money"),"error"); return
	money -= cost
	expenses += cost
	var job: Dictionary = GrowWiseAgriExpansion.queue_lab_test(samples[0] as Dictionary,test_definition,day)
	var queue: Array = array_value(survey_state,"lab_queue")
	queue.append(job)
	survey_state["lab_queue"] = queue
	add_farm_xp(8,"ส่งตัวอย่างเข้าแล็บ",Vector2(640,150))
	notify("ส่งตรวจแล้ว • ผลพร้อมวัน %d" % int(job.get("ready_day",day+1)),"success")

func process_lab_results() -> void:
	var result: Dictionary = GrowWiseAgriExpansion.process_lab_queue(agri_data,array_value(survey_state,"lab_queue"),day)
	survey_state["lab_queue"] = array_value(result,"remaining")
	var new_reports: Array = array_value(result,"reports")
	if new_reports.is_empty(): return
	var reports: Array = array_value(survey_state,"reports")
	for report_value: Variant in new_reports:
		reports.push_front(report_value)
		survey_state["reports"] = reports
	survey_state["total_tests"] = int(survey_state.get("total_tests",0))+new_reports.size()
	selected_lab_report = 0
	knowledge += new_reports.size()*5
	add_farm_xp(new_reports.size()*15,"รับผลวิเคราะห์ดิน",Vector2(640,150))
	notify("ผลแล็บดินพร้อมแล้ว %d รายงาน" % new_reports.size(),"success")

func active_recommendation_crop() -> String:
	var reports: Array = array_value(survey_state,"reports")
	if reports.is_empty(): return ""
	var suitability: Array = array_value(reports[0] as Dictionary,"suitability")
	if suitability.is_empty(): return ""
	return string_value(suitability[0] as Dictionary,"crop_id")

func apply_tool(cell: Vector2i) -> void:
	var before_crop: String = string_value(dictionary_value(tiles,tile_key(cell)),"crop")
	var tool_before: String = selected_tool
	var seed_before: String = selected_seed
	super.apply_tool(cell)
	if tool_before == "seed" and before_crop.is_empty():
		var tile: Dictionary = dictionary_value(tiles,tile_key(cell))
		if string_value(tile,"crop") == seed_before and seed_before == active_recommendation_crop():
			tile["quality"] = minf(100.0,float_value(tile,"quality",70.0)+6.0)
			tile["health"] = minf(100.0,float_value(tile,"health",100.0)+3.0)
			tiles[tile_key(cell)] = tile
			knowledge += 1
			add_feedback("ตรงตามคำแนะนำแล็บ • Q+6",iso(Vector2(cell))+Vector2(0,-70),TEAL)

func build_animal_building(building_id: String) -> void:
	var buildings: Dictionary = dictionary_value(animal_state,"buildings")
	var level: int = int(buildings.get(building_id,0))
	var definition: Dictionary = GrowWiseAgriExpansion.animal_building_definition(agri_data,building_id)
	if definition.is_empty(): return
	var levels: int = array_value(definition,"capacity").size()
	if level >= levels:
		notify("โรงเรือนเต็มระดับแล้ว","success"); return
	var zone_id: String = string_value(definition,"zone")
	if not bool(zone_unlocked.get(zone_id,false)):
		notify("ต้องซื้อพื้นที่ %s ก่อน" % zone_id,"error"); return
	var cost: Dictionary = GrowWiseAgriExpansion.animal_building_cost(agri_data,building_id,level)
	if not pay_infrastructure_cost(cost): return
	buildings[building_id] = level+1
	animal_state["buildings"] = buildings
	add_farm_xp(25+level*10,"สร้างโรงเรือนสัตว์",Vector2(640,150))
	notify("สร้าง%s ระดับ %d" % [string_value(definition,"name"),level+1],"success")

func buy_animal(animal_id: String) -> void:
	var definition: Dictionary = GrowWiseAgriExpansion.animal_definition(agri_data,animal_id)
	if definition.is_empty(): return
	var building_id: String = string_value(definition,"building")
	var capacity: int = GrowWiseAgriExpansion.animal_capacity(agri_data,dictionary_value(animal_state,"buildings"),building_id)
	var occupied: int = GrowWiseAgriExpansion.occupied_capacity(agri_data,animal_state,building_id)
	if occupied >= capacity:
		notify("ความจุโรงเรือนไม่พอ","error"); return
	var price: int = int_value(definition,"price")
	if money < price:
		notify(tx("msg.no_money"),"error"); return
	money -= price
	expenses += price
	var animals: Dictionary = dictionary_value(animal_state,"animals")
	var group: Dictionary = dictionary_value(animals,animal_id)
	group["count"] = int(group.get("count",0))+1
	animals[animal_id] = group
	animal_state["animals"] = animals
	animal_state["total_animals_bought"] = int(animal_state.get("total_animals_bought",0))+1
	add_farm_xp(12,"รับสัตว์เข้าฟาร์ม",Vector2(640,150))
	notify("ซื้อ%sแล้ว" % string_value(definition,"name"),"success")

func craft_feed(index: int) -> void:
	var feeds: Array = array_value(agri_data,"feeds")
	if index < 0 or index >= feeds.size(): return
	var feed: Dictionary = feeds[index] as Dictionary
	var test_inventory: Dictionary = inventory.duplicate(true)
	if not GrowWiseAgriExpansion.consume_requirements(dictionary_value(feed,"requires"),test_inventory):
		notify("วัตถุดิบอาหารสัตว์ไม่พอ","error"); return
	inventory = test_inventory
	var output_id: String = string_value(feed,"id")
	inventory[output_id] = int(inventory.get(output_id,0))+int_value(feed,"output",1)
	add_farm_xp(6,"ผสมอาหารสัตว์",Vector2(640,150))
	notify("ผลิต%s %d" % [string_value(feed,"name"),int_value(feed,"output",1)],"success")

func simulate_livestock_day() -> void:
	var result: Dictionary = GrowWiseAgriExpansion.simulate_animals(agri_data,animal_state,inventory)
	animal_state = dictionary_value(result,"state")
	inventory = dictionary_value(result,"inventory")
	var messages: Array = array_value(result,"messages")
	if not messages.is_empty():
		add_feedback(String(messages[0]),Vector2(900,165),GOLD)
	var animals: Dictionary = dictionary_value(animal_state,"animals")
	var happiness_total: float = 0.0
	var groups: int = 0
	for animal_id: String in animals:
		var group: Dictionary = dictionary_value(animals,animal_id)
		if int(group.get("count",0)) > 0:
			happiness_total += float(group.get("happiness",0.0)); groups += 1
	if groups > 0 and happiness_total/float(groups) >= 80.0:
		base_town_reputation += 1

func collect_animal_products() -> void:
	var pending: Dictionary = dictionary_value(animal_state,"pending_products")
	var total: int = 0
	for item_id: String in pending:
		var amount: int = int(pending[item_id])
		inventory[item_id] = int(inventory.get(item_id,0))+amount
		total += amount
	animal_state["pending_products"] = {}
	var manure_amount: int = int(animal_state.get("manure",0))
	inventory["manure"] = int(inventory.get("manure",0))+manure_amount
	animal_state["manure"] = 0
	if total+manure_amount <= 0:
		notify("ยังไม่มีผลผลิตสัตว์ให้เก็บ","error"); return
	add_farm_xp(8,"เก็บผลผลิตสัตว์",Vector2(800,150))
	notify("เก็บผลผลิต %d และปุ๋ยคอก %d" % [total,manure_amount],"success")

func process_agri_recipe(index: int) -> void:
	var recipes: Array = array_value(agri_data,"processing_recipes")
	if index < 0 or index >= recipes.size(): return
	var recipe: Dictionary = recipes[index] as Dictionary
	var building_id: String = string_value(recipe,"building")
	var available: bool = int(building_levels.get(building_id,0)) > 0 or int(dictionary_value(animal_state,"buildings").get(building_id,0)) > 0
	if not available:
		notify("ต้องสร้าง%sก่อน" % building_id,"error"); return
	var test_inventory: Dictionary = inventory.duplicate(true)
	if not GrowWiseAgriExpansion.consume_requirements(dictionary_value(recipe,"requires"),test_inventory):
		notify("วัตถุดิบแปรรูปไม่พอ","error"); return
	inventory = test_inventory
	var output_id: String = string_value(recipe,"output")
	var amount: int = int_value(recipe,"amount",1)
	inventory[output_id] = int(inventory.get(output_id,0))+amount
	add_farm_xp(10,"แปรรูปสินค้า",Vector2(720,150))
	notify("ผลิต%s %d" % [string_value(recipe,"name"),amount],"success")

func save_game(slot_number: int,automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number,automatic)
	if not result: return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR,slot_number]
	var payload: Dictionary = read_save(path)
	payload["survey_state"] = survey_state
	payload["animal_state"] = animal_state
	var file: FileAccess = FileAccess.open(path,FileAccess.WRITE)
	if file != null: file.store_string(JSON.stringify(payload)); file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result: return false
	var payload: Dictionary = read_save("%s/slot_%d.json" % [SAVE_DIR,slot_number])
	survey_state = dictionary_value(payload,"survey_state")
	if survey_state.is_empty(): survey_state = GrowWiseAgriExpansion.default_survey_state()
	animal_state = dictionary_value(payload,"animal_state")
	if animal_state.is_empty(): animal_state = GrowWiseAgriExpansion.default_animal_state(agri_data)
	return true

func overlay_click(position: Vector2) -> void:
	if overlay in ["survey","soil_lab","animals","processing"]:
		if Rect2(1012,76,42,36).has_point(position): overlay=""; return
		if overlay == "survey": handle_survey_click(position); return
		if overlay == "soil_lab": handle_lab_click(position); return
		if overlay == "animals": handle_animals_click(position); return
		if overlay == "processing": handle_processing_click(position); return
	super.overlay_click(position)

func handle_survey_click(position: Vector2) -> void:
	var areas: Array = array_value(agri_data,"survey_areas")
	for index: int in range(areas.size()):
		if Rect2(255,145+index*76,760,62).has_point(position): selected_survey_area=index; survey_area(index); return

func handle_lab_click(position: Vector2) -> void:
	var tests: Array = array_value(agri_data,"lab_tests")
	for index: int in range(tests.size()):
		if Rect2(255,145+index*72,360,58).has_point(position): selected_lab_test=index; return
	if Rect2(255,390,360,48).has_point(position): queue_selected_lab_test(); return
	var reports: Array = array_value(survey_state,"reports")
	for index: int in range(mini(5,reports.size())):
		if Rect2(645,145+index*58,360,48).has_point(position): selected_lab_report=index; return

func handle_animals_click(position: Vector2) -> void:
	var building_defs: Array = array_value(agri_data,"animal_buildings")
	for index: int in range(building_defs.size()):
		if Rect2(250,135+index*58,350,48).has_point(position): build_animal_building(string_value(building_defs[index] as Dictionary,"id")); return
	var animals: Array = array_value(agri_data,"animals")
	for index: int in range(animals.size()):
		if Rect2(625,135+index*58,380,48).has_point(position): buy_animal(string_value(animals[index] as Dictionary,"id")); return
	var feeds: Array = array_value(agri_data,"feeds")
	for index: int in range(feeds.size()):
		if Rect2(250+index*190,425,180,46).has_point(position): craft_feed(index); return
	if Rect2(720,510,285,50).has_point(position): collect_animal_products(); return

func handle_processing_click(position: Vector2) -> void:
	var recipes: Array = array_value(agri_data,"processing_recipes")
	for index: int in range(recipes.size()):
		var column: int=index%2; var row: int=int(index/2)
		if Rect2(245+column*405,140+row*88,390,76).has_point(position): process_agri_recipe(index); return

func draw_overlay() -> void:
	match overlay:
		"survey": draw_survey_overlay()
		"soil_lab": draw_lab_overlay()
		"animals": draw_animals_overlay()
		"processing": draw_processing_overlay()
		_: super.draw_overlay()

func draw_survey_overlay() -> void:
	draw_expansion_shell(tx("ui.survey"))
	draw_text("เหลือ %d ครั้งวันนี้ • ตัวอย่าง %d • รายงาน %d" % [int(survey_state.get("attempts",0)),array_value(survey_state,"samples").size(),array_value(survey_state,"reports").size()],Vector2(260,120),16,GOLD)
	var areas: Array = array_value(agri_data,"survey_areas")
	for index: int in range(areas.size()):
		var area: Dictionary=areas[index] as Dictionary
		var rect_value: Rect2=Rect2(255,145+index*76,760,62)
		panel(rect_value,Color("dce8d5") if farm_level>=int_value(area,"level",1) else MIST)
		draw_text(string_value(area,"name"),rect_value.position+Vector2(12,25),18,GREEN)
		draw_text("Lv.%d • ค่าเดินทาง %d" % [int_value(area,"level",1),int_value(area,"cost")],rect_value.position+Vector2(480,25),14,GOLD,250.0,HORIZONTAL_ALIGNMENT_RIGHT)
		draw_text(string_value(area,"description"),rect_value.position+Vector2(12,50),12,INK,710.0)

func draw_lab_overlay() -> void:
	draw_expansion_shell(tx("ui.soil_lab"))
	var tests: Array=array_value(agri_data,"lab_tests")
	for index: int in range(tests.size()):
		var test: Dictionary=tests[index] as Dictionary
		var rect_value: Rect2=Rect2(255,145+index*72,360,58)
		panel(rect_value,GOLD if selected_lab_test==index else MIST)
		draw_text(string_value(test,"name"),rect_value.position+Vector2(10,24),16,GREEN)
		draw_text("%d เงิน • %d วัน" % [int_value(test,"cost"),int_value(test,"days")],rect_value.position+Vector2(10,49),13,INK)
	panel(Rect2(255,390,360,48),TEAL); draw_text("ส่งตัวอย่างล่าสุดเข้าตรวจ",Vector2(265,423),17,Color.WHITE,340.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("คิวตรวจ %d" % array_value(survey_state,"lab_queue").size(),Vector2(255,470),16,GOLD)
	var reports: Array=array_value(survey_state,"reports")
	for index: int in range(mini(5,reports.size())):
		var report: Dictionary=reports[index] as Dictionary
		var rect_value: Rect2=Rect2(645,145+index*58,360,48)
		panel(rect_value,GOLD if selected_lab_report==index else Color("dce8d5"))
		draw_text("%s • %s" % [string_value(report,"area_name"),string_value(report,"test_name")],rect_value.position+Vector2(8,30),14,INK,340.0)
	if selected_lab_report>=0 and selected_lab_report<reports.size(): draw_lab_report_detail(reports[selected_lab_report] as Dictionary)

func draw_lab_report_detail(report: Dictionary) -> void:
	var suitability: Array=array_value(report,"suitability")
	var y: float=455.0
	draw_text(tx("ui.suitability"),Vector2(645,y),17,GREEN); y+=27.0
	for index: int in range(mini(3,suitability.size())):
		var item: Dictionary=suitability[index] as Dictionary
		draw_text("%d. %s %d%% • %s" % [index+1,string_value(item,"name"),int_value(item,"score"),string_value(item,"grade")],Vector2(655,y),14,TEAL if index==0 else INK); y+=25.0

func draw_animals_overlay() -> void:
	draw_expansion_shell(tx("ui.animals"))
	var buildings: Dictionary=dictionary_value(animal_state,"buildings")
	var building_defs: Array=array_value(agri_data,"animal_buildings")
	for index: int in range(building_defs.size()):
		var definition: Dictionary=building_defs[index] as Dictionary
		var building_id: String=string_value(definition,"id")
		var level: int=int(buildings.get(building_id,0))
		var rect_value: Rect2=Rect2(250,135+index*58,350,48)
		panel(rect_value,Color("dce8d5"))
		draw_text("%s Lv.%d" % [string_value(definition,"name"),level],rect_value.position+Vector2(8,30),15,GREEN)
	var animals: Dictionary=dictionary_value(animal_state,"animals")
	var animal_defs: Array=array_value(agri_data,"animals")
	for index: int in range(animal_defs.size()):
		var definition: Dictionary=animal_defs[index] as Dictionary
		var animal_id: String=string_value(definition,"id")
		var group: Dictionary=dictionary_value(animals,animal_id)
		var rect_value: Rect2=Rect2(625,135+index*58,380,48)
		panel(rect_value,Color("dce8d5"))
		draw_text("%s x%d • สุขภาพ %d • สุข %d" % [string_value(definition,"name"),int(group.get("count",0)),int(round(float(group.get("health",0.0)))),int(round(float(group.get("happiness",0.0))))],rect_value.position+Vector2(8,30),14,INK)
	var feeds: Array=array_value(agri_data,"feeds")
	for index: int in range(feeds.size()):
		panel(Rect2(250+index*190,425,180,46),WOOD_LIGHT)
		draw_text(string_value(feeds[index] as Dictionary,"name"),Vector2(255+index*190,456),13,Color.WHITE,170.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(720,510,285,50),TEAL); draw_text("เก็บผลผลิตและปุ๋ยคอก",Vector2(730,544),17,Color.WHITE,265.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("ผลผลิตค้าง %d • ปุ๋ยคอก %d • อาหารใช้วันนี้ %d" % [pending_product_total(),int(animal_state.get("manure",0)),int(animal_state.get("daily_feed_used",0))],Vector2(250,565),15,GOLD)

func pending_product_total() -> int:
	var total: int=0
	for item_id: String in dictionary_value(animal_state,"pending_products"): total+=int(dictionary_value(animal_state,"pending_products")[item_id])
	return total

func draw_processing_overlay() -> void:
	draw_expansion_shell(tx("ui.processing"))
	var recipes: Array=array_value(agri_data,"processing_recipes")
	for index: int in range(recipes.size()):
		var recipe: Dictionary=recipes[index] as Dictionary
		var column: int=index%2; var row: int=int(index/2)
		var rect_value: Rect2=Rect2(245+column*405,140+row*88,390,76)
		panel(rect_value,Color("dce8d5"))
		draw_text(string_value(recipe,"name"),rect_value.position+Vector2(10,25),16,GREEN)
		draw_text("ต้องใช้ %s • ได้ %d • มูลค่า %d" % [requirements_text(dictionary_value(recipe,"requires")),int_value(recipe,"amount",1),int_value(recipe,"value")],rect_value.position+Vector2(10,54),12,INK,370.0)

func requirements_text(requirements: Dictionary) -> String:
	var parts: PackedStringArray=[]
	for item_id: String in requirements: parts.append("%s %d" % [item_id,int(requirements[item_id])])
	return ", ".join(parts)

func draw_world() -> void:
	super.draw_world()
	draw_livestock_world()

func draw_livestock_world() -> void:
	var animals: Dictionary=dictionary_value(animal_state,"animals")
	var index: int=0
	for animal_id: String in animals:
		var count: int=int(dictionary_value(animals,animal_id).get("count",0))
		for unit: int in range(mini(count,3)):
			var p: Vector2=Vector2(820+posmod(index*47+unit*24,155),430+posmod(index*31+unit*17,95))
			var body: Color=Color("f3e5c2")
			if animal_id=="cow": body=Color("d8e2d5")
			elif animal_id=="goat": body=Color("b97a4d")
			elif animal_id=="duck": body=Color("e9b84d")
			elif animal_id=="bee": body=Color("e9b84d")
			draw_circle(p,6.0,body); draw_circle(p+Vector2(6,-3),3.5,body.darkened(0.1))
		index+=1

func draw_hud() -> void:
	super.draw_hud()
	draw_text("Q สำรวจ • L แล็บ • A สัตว์ • G แปรรูป • U น้ำ • V รถ • O แต่งตัว",Vector2(270,113),11,CREAM,720.0,HORIZONTAL_ALIGNMENT_CENTER)
