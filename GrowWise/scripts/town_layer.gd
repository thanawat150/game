extends "res://scripts/progression_layer.gd"

const GrowWiseWorldExpansion = preload("res://scripts/world_expansion_system.gd")

var world_data: Dictionary = {}
var zone_unlocked: Dictionary = {}
var building_levels: Dictionary = {}
var selected_zone_index: int = 1
var town_metrics: Dictionary = {}
var base_town_reputation: int = 0
var town_daily_income: int = 0
var town_daily_expense: int = 0
var current_festival: Dictionary = {}

var fishing_active: bool = false
var fishing_meter: float = 0.0
var fishing_direction: float = 1.0
var fishing_target: float = 0.55
var fishing_width: float = 0.16
var hooked_fish: Dictionary = {}
var fishing_attempts: int = 5
var fishing_day: int = 1
var fish_collection: Dictionary = {}
var fish_quality: Dictionary = {}

var forest_attempts: int = 3
var mine_attempts: int = 3
var activity_day: int = 1

func _ready() -> void:
	world_data = load_json("res://data/world_expansion.json")
	super._ready()
	var test_result: Dictionary = GrowWiseWorldExpansion.self_test(world_data)
	if bool(test_result.get("ok", false)):
		print("GROWWISE_TOWN_FISHING_OK")
	else:
		push_error("Town/fishing self-test failed: %s" % JSON.stringify(test_result))

func tx(key_name: String) -> String:
	var custom: Dictionary = {
		"ui.town":{"th":"เมือง","en":"Town"},
		"ui.fishing":{"th":"ตกปลา","en":"Fishing"},
		"ui.cooking":{"th":"ทำอาหาร","en":"Cooking"},
		"ui.explore":{"th":"สำรวจ","en":"Explore"},
		"ui.population":{"th":"ประชากร","en":"Population"},
		"ui.happiness":{"th":"ความสุข","en":"Happiness"},
		"ui.reputation":{"th":"ชื่อเสียง","en":"Reputation"},
		"ui.town_income":{"th":"รายได้เมือง","en":"Town Income"},
		"ui.storage":{"th":"ความจุคลัง","en":"Storage"}
	}
	if custom.has(key_name):
		var value: Dictionary = custom[key_name] as Dictionary
		return String(value.get(language, value.get("th", key_name)))
	return super.tx(key_name)

func new_game() -> void:
	super.new_game()
	zone_unlocked = GrowWiseWorldExpansion.default_zone_state(world_data)
	building_levels = GrowWiseWorldExpansion.default_building_state(world_data)
	selected_zone_index = 1
	base_town_reputation = 0
	town_daily_income = 0
	town_daily_expense = 0
	current_festival = {}
	town_metrics = GrowWiseWorldExpansion.calculate_town_metrics(world_data, building_levels, base_town_reputation)
	fishing_active = false
	fishing_meter = 0.0
	fishing_attempts = 5
	fishing_day = day
	fish_collection = {}
	fish_quality = {}
	forest_attempts = 3
	mine_attempts = 3
	activity_day = day
	inventory["bait"] = 5
	inventory["meal_grilled_fish"] = 0
	inventory["meal_vegetable_soup"] = 0
	inventory["meal_fish_curry"] = 0
	inventory["processed_pickles"] = 0
	inventory["processed_seed_pack"] = 0
	build_buttons()

func build_buttons() -> void:
	super.build_buttons()
	buttons.append(button("town", Rect2(846, 500, 76, 54), "shop", "ui.town"))
	buttons.append(button("fishing", Rect2(928, 500, 76, 54), "water", "ui.fishing"))

func handle_button(button_id: String) -> void:
	match button_id:
		"town": overlay = "town"
		"fishing": open_fishing()
		_: super.handle_button(button_id)

func _process(delta: float) -> void:
	super._process(delta)
	if fishing_active and overlay == "fishing":
		fishing_meter += fishing_direction * delta * 0.72
		if fishing_meter >= 1.0:
			fishing_meter = 1.0
			fishing_direction = -1.0
		elif fishing_meter <= 0.0:
			fishing_meter = 0.0
			fishing_direction = 1.0
	if day != activity_day:
		activity_day = day
		forest_attempts = 3 + GrowWiseWorldExpansion.indexed_bonus(GrowWiseWorldExpansion.building_definition(world_data,"forest_camp"),"forage_bonus",int(building_levels.get("forest_camp",0)))
		mine_attempts = 3 + GrowWiseWorldExpansion.indexed_bonus(GrowWiseWorldExpansion.building_definition(world_data,"mine"),"mining_bonus",int(building_levels.get("mine",0)))
	if day != fishing_day:
		fishing_day = day
		fishing_attempts = 5 + int(building_levels.get("dock",0))

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo and mode == "game":
		if overlay == "fishing" and event.keycode == KEY_SPACE:
			if fishing_active: resolve_fishing()
			else: start_fishing()
			return
		match event.keycode:
			KEY_B:
				overlay = "town"
				return
			KEY_F:
				open_fishing()
				return
			KEY_K:
				overlay = "cooking"
				return
			KEY_X:
				overlay = "explore"
				return
	super._unhandled_input(event)

func advance_day() -> void:
	super.advance_day()
	apply_town_day()
	check_festival()

func apply_town_day() -> void:
	town_metrics = GrowWiseWorldExpansion.calculate_town_metrics(world_data, building_levels, base_town_reputation)
	town_daily_income = int(town_metrics.get("income",0))
	town_daily_expense = maxi(0,int(town_metrics.get("population",1))/3)
	var net: int = town_daily_income-town_daily_expense
	money += net
	revenue += maxi(0,town_daily_income)
	expenses += town_daily_expense
	knowledge += int(town_metrics.get("knowledge",0))
	if net != 0:
		add_feedback("เมืองสุทธิ %+d" % net,Vector2(850,130),GOLD if net>=0 else RED)

func check_festival() -> void:
	current_festival = {}
	for value: Variant in array_value(world_data,"festivals"):
		var festival: Dictionary = value as Dictionary
		if int_value(festival,"day") == day:
			current_festival = festival.duplicate(true)
			base_town_reputation += 5
			money += 100
			add_feedback("เทศกาล: %s" % string_value(festival,"name"),Vector2(640,150),GOLD)
			break

func buy_zone(index: int) -> void:
	var zones: Array = array_value(world_data,"zones")
	if index < 0 or index >= zones.size(): return
	var zone: Dictionary = zones[index] as Dictionary
	var zone_id: String = string_value(zone,"id")
	if bool(zone_unlocked.get(zone_id,false)): return
	if farm_level < int_value(zone,"level",1):
		notify("ต้องมีระดับสวน %d" % int_value(zone,"level",1),"error")
		return
	var cost: int = int_value(zone,"cost")
	if money < cost:
		notify(tx("msg.no_money"),"error")
		return
	money -= cost
	expenses += cost
	zone_unlocked[zone_id] = true
	base_town_reputation += 3
	add_farm_xp(25,"ขยายพื้นที่",Vector2(640,150))
	notify("เปิดพื้นที่ %s แล้ว" % string_value(zone,"name"),"success")

func build_or_upgrade(building_id: String) -> void:
	var definition: Dictionary = GrowWiseWorldExpansion.building_definition(world_data,building_id)
	if definition.is_empty(): return
	var zone_id: String = string_value(definition,"zone")
	if not bool(zone_unlocked.get(zone_id,false)):
		notify("ต้องซื้อพื้นที่ก่อน","error")
		return
	var level: int = int(building_levels.get(building_id,0))
	var max_level: int = int_value(definition,"levels",1)
	if level >= max_level:
		notify("อาคารเต็มระดับแล้ว","success")
		return
	var cost: Dictionary = GrowWiseWorldExpansion.next_building_cost(world_data,building_id,level)
	if not GrowWiseWorldExpansion.can_pay_cost(cost,inventory,money):
		notify("ทรัพยากรก่อสร้างไม่พอ","error")
		return
	for item_id: String in cost:
		var amount: int = int(cost[item_id])
		if item_id == "money":
			money -= amount
			expenses += amount
		else:
			inventory[item_id] = int(inventory.get(item_id,0))-amount
	building_levels[building_id] = level+1
	collect_entry("buildings",building_id)
	base_town_reputation += 2+level
	town_metrics = GrowWiseWorldExpansion.calculate_town_metrics(world_data,building_levels,base_town_reputation)
	add_farm_xp(30+level*15,"สร้าง%s" % string_value(definition,"name"),Vector2(640,150))
	notify("ก่อสร้างสำเร็จ: %s ระดับ %d" % [string_value(definition,"name"),level+1],"success")

func open_fishing() -> void:
	if not bool(zone_unlocked.get("riverside",false)) or int(building_levels.get("dock",0)) <= 0:
		notify("ซื้อพื้นที่ริมแม่น้ำและสร้างท่าเรือก่อน","error")
		return
	overlay = "fishing"
	fishing_active = false

func start_fishing() -> void:
	if fishing_attempts <= 0:
		notify("วันนี้ตกปลาครบแล้ว","error")
		return
	if int(inventory.get("bait",0)) <= 0:
		notify("เหยื่อตกปลาไม่พอ","error")
		return
	inventory["bait"] = int(inventory.get("bait",0))-1
	fishing_attempts -= 1
	var dock_level: int = int(building_levels.get("dock",0))
	hooked_fish = GrowWiseWorldExpansion.choose_fish(world_data,current_season,current_weather,dock_level,day*101+fishing_attempts*17)
	fishing_target = 0.18+float(posmod(day*23+fishing_attempts*11,63))/100.0
	fishing_width = GrowWiseWorldExpansion.fishing_target_width(hooked_fish,dock_level)
	fishing_meter = 0.0
	fishing_direction = 1.0
	fishing_active = true

func resolve_fishing() -> void:
	if not fishing_active or hooked_fish.is_empty(): return
	fishing_active = false
	var distance: float = absf(fishing_meter-fishing_target)
	if distance > fishing_width:
		notify("ปลาหลุดไปแล้ว","error")
		return
	var quality_value: int = GrowWiseWorldExpansion.fishing_quality(distance,fishing_width,int(building_levels.get("dock",0)))
	var fish_id: String = string_value(hooked_fish,"id")
	inventory["fish_"+fish_id] = int(inventory.get("fish_"+fish_id,0))+1
	fish_collection[fish_id] = int(fish_collection.get(fish_id,0))+1
	fish_quality[fish_id] = maxi(int(fish_quality.get(fish_id,0)),quality_value)
	collect_entry("fish",fish_id)
	add_farm_xp(8+quality_value/10,"ตกปลา",Vector2(640,145))
	add_feedback("ได้%s • Q%d" % [string_value(hooked_fish,"name"),quality_value],Vector2(640,180),BLUE)

func cook_recipe(index: int) -> void:
	if int(building_levels.get("kitchen",0)) <= 0:
		notify("สร้างครัวชุมชนก่อน","error")
		return
	var recipes: Array = array_value(world_data,"recipes")
	if index < 0 or index >= recipes.size(): return
	var recipe: Dictionary = recipes[index] as Dictionary
	if not consume_flexible_requirements(dictionary_value(recipe,"requires")):
		notify("วัตถุดิบทำอาหารไม่พอ","error")
		return
	var output_id: String = string_value(recipe,"output")
	inventory[output_id] = int(inventory.get(output_id,0))+1
	base_town_reputation += int_value(recipe,"happiness")/2
	knowledge += int_value(recipe,"knowledge")
	add_farm_xp(10,"ทำอาหารและแปรรูป",Vector2(640,150))
	notify("ทำ %s สำเร็จ" % string_value(recipe,"name"),"success")

func consume_flexible_requirements(requirements: Dictionary) -> bool:
	var test_inventory: Dictionary = inventory.duplicate(true)
	for requirement_id: String in requirements:
		var amount: int = int(requirements[requirement_id])
		if requirement_id == "fish_any":
			if not consume_prefix(test_inventory,"fish_",amount): return false
		elif requirement_id == "produce_any":
			if not consume_prefix(test_inventory,"produce_",amount): return false
		elif int(test_inventory.get(requirement_id,0)) >= amount:
			test_inventory[requirement_id] = int(test_inventory.get(requirement_id,0))-amount
		else: return false
	inventory = test_inventory
	return true

func consume_prefix(target_inventory: Dictionary,prefix: String,amount: int) -> bool:
	var remaining: int = amount
	for item_id: String in target_inventory:
		if item_id.begins_with(prefix) and int(target_inventory.get(item_id,0)) > 0:
			var used: int = mini(remaining,int(target_inventory.get(item_id,0)))
			target_inventory[item_id] = int(target_inventory.get(item_id,0))-used
			remaining -= used
			if remaining <= 0: return true
	return false

func explore_forest() -> void:
	if not bool(zone_unlocked.get("highland",false)):
		notify("ต้องซื้อพื้นที่เนินป่าก่อน","error")
		return
	if forest_attempts <= 0:
		notify("วันนี้สำรวจป่าครบแล้ว","error")
		return
	forest_attempts -= 1
	var level: int = int(building_levels.get("forest_camp",0))
	var wood_gain: int = 2+level+posmod(day+forest_attempts,3)
	var herb_gain: int = 1+posmod(day,2)
	inventory["wood"] = int(inventory.get("wood",0))+wood_gain
	inventory["herb"] = int(inventory.get("herb",0))+herb_gain
	eco_score = mini(100,eco_score+1)
	add_farm_xp(7,"สำรวจป่าชุมชน",Vector2(640,150))
	notify("ได้ไม้ %d และสมุนไพร %d" % [wood_gain,herb_gain],"success")

func explore_mine() -> void:
	if not bool(zone_unlocked.get("highland",false)):
		notify("ต้องซื้อพื้นที่เนินป่าก่อน","error")
		return
	if int(building_levels.get("mine",0)) <= 0:
		notify("สร้างเหมืองชุมชนก่อน","error")
		return
	if mine_attempts <= 0:
		notify("วันนี้ทำเหมืองครบแล้ว","error")
		return
	mine_attempts -= 1
	var level: int = int(building_levels.get("mine",0))
	var stone_gain: int = 2+level+posmod(day+mine_attempts,3)
	var scrap_gain: int = 1+(1 if level>=2 else 0)
	inventory["stone"] = int(inventory.get("stone",0))+stone_gain
	inventory["scrap"] = int(inventory.get("scrap",0))+scrap_gain
	eco_score = maxi(0,eco_score-1)
	add_farm_xp(7,"สำรวจเหมือง",Vector2(640,150))
	notify("ได้หิน %d และเศษโลหะ %d" % [stone_gain,scrap_gain],"success")

func save_game(slot_number: int,automatic: bool) -> bool:
	var result: bool = super.save_game(slot_number,automatic)
	if not result: return false
	var path: String = "%s/slot_%d.json" % [SAVE_DIR,slot_number]
	var payload: Dictionary = read_save(path)
	payload["zone_unlocked"] = zone_unlocked
	payload["building_levels"] = building_levels
	payload["base_town_reputation"] = base_town_reputation
	payload["fish_collection"] = fish_collection
	payload["fish_quality"] = fish_quality
	var file: FileAccess = FileAccess.open(path,FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(payload)); file.close()
	return result

func load_game(slot_number: int) -> bool:
	var result: bool = super.load_game(slot_number)
	if not result: return false
	var payload: Dictionary = read_save("%s/slot_%d.json" % [SAVE_DIR,slot_number])
	zone_unlocked = dictionary_value(payload,"zone_unlocked")
	if zone_unlocked.is_empty(): zone_unlocked = GrowWiseWorldExpansion.default_zone_state(world_data)
	building_levels = dictionary_value(payload,"building_levels")
	if building_levels.is_empty(): building_levels = GrowWiseWorldExpansion.default_building_state(world_data)
	base_town_reputation = int_value(payload,"base_town_reputation",0)
	fish_collection = dictionary_value(payload,"fish_collection")
	fish_quality = dictionary_value(payload,"fish_quality")
	town_metrics = GrowWiseWorldExpansion.calculate_town_metrics(world_data,building_levels,base_town_reputation)
	return true

func overlay_click(position: Vector2) -> void:
	if overlay in ["town","fishing","cooking","explore"]:
		if Rect2(1012,76,42,36).has_point(position): overlay=""; fishing_active=false; return
		if overlay == "town": handle_town_click(position); return
		if overlay == "fishing":
			if Rect2(475,500,330,55).has_point(position):
				if fishing_active: resolve_fishing()
				else: start_fishing()
			return
		if overlay == "cooking":
			var recipes: Array = array_value(world_data,"recipes")
			for index: int in range(recipes.size()):
				if Rect2(255,150+index*76,760,62).has_point(position): cook_recipe(index); return
		if overlay == "explore":
			if Rect2(300,230,300,110).has_point(position): explore_forest(); return
			if Rect2(680,230,300,110).has_point(position): explore_mine(); return
	super.overlay_click(position)

func handle_town_click(position: Vector2) -> void:
	var zones: Array = array_value(world_data,"zones")
	for index: int in range(zones.size()):
		if Rect2(235+index*163,115,150,42).has_point(position): selected_zone_index=index; return
	if selected_zone_index < 0 or selected_zone_index >= zones.size(): return
	var zone: Dictionary = zones[selected_zone_index] as Dictionary
	var zone_id: String = string_value(zone,"id")
	if not bool(zone_unlocked.get(zone_id,false)):
		if Rect2(450,520,380,50).has_point(position): buy_zone(selected_zone_index)
		return
	var row: int = 0
	for value: Variant in array_value(world_data,"buildings"):
		var building: Dictionary = value as Dictionary
		if string_value(building,"zone") != zone_id: continue
		if Rect2(250,205+row*63,760,52).has_point(position): build_or_upgrade(string_value(building,"id")); return
		row += 1

func draw_overlay() -> void:
	match overlay:
		"town": draw_town_overlay()
		"fishing": draw_fishing_overlay()
		"cooking": draw_cooking_overlay()
		"explore": draw_explore_overlay()
		_: super.draw_overlay()

func draw_town_overlay() -> void:
	draw_expansion_shell(tx("ui.town"))
	town_metrics = GrowWiseWorldExpansion.calculate_town_metrics(world_data,building_levels,base_town_reputation)
	draw_text("%s Lv.%d • %s %d • %s %d • %s %d" % [String(town_metrics.get("name","บ้านสวน")),int(town_metrics.get("level",1)),tx("ui.population"),int(town_metrics.get("population",1)),tx("ui.happiness"),int(town_metrics.get("happiness",0)),tx("ui.reputation"),int(town_metrics.get("reputation",0))],Vector2(245,105),15,GOLD,730.0,HORIZONTAL_ALIGNMENT_RIGHT)
	var zones: Array = array_value(world_data,"zones")
	for index: int in range(zones.size()):
		var zone: Dictionary = zones[index] as Dictionary
		var unlocked: bool = bool(zone_unlocked.get(string_value(zone,"id"),false))
		panel(Rect2(235+index*163,115,150,42),GOLD if selected_zone_index==index else (TEAL if unlocked else MIST))
		draw_text(("✓ " if unlocked else "🔒 ")+string_value(zone,"name"),Vector2(240+index*163,144),13,Color.WHITE if unlocked else INK,140.0,HORIZONTAL_ALIGNMENT_CENTER)
	if selected_zone_index < 0 or selected_zone_index >= zones.size(): return
	var selected_zone: Dictionary = zones[selected_zone_index] as Dictionary
	var zone_id: String = string_value(selected_zone,"id")
	if not bool(zone_unlocked.get(zone_id,false)):
		draw_text(string_value(selected_zone,"description"),Vector2(280,240),20,INK,700.0,HORIZONTAL_ALIGNMENT_CENTER)
		draw_text("ต้องการระดับสวน %d • ราคา %d" % [int_value(selected_zone,"level"),int_value(selected_zone,"cost")],Vector2(280,315),18,GOLD,700.0,HORIZONTAL_ALIGNMENT_CENTER)
		panel(Rect2(450,520,380,50),TEAL)
		draw_text("ซื้อพื้นที่",Vector2(460,554),19,Color.WHITE,360.0,HORIZONTAL_ALIGNMENT_CENTER)
		return
	var row: int = 0
	for value: Variant in array_value(world_data,"buildings"):
		var building: Dictionary = value as Dictionary
		if string_value(building,"zone") != zone_id: continue
		var level: int = int(building_levels.get(string_value(building,"id"),0))
		var max_level: int = int_value(building,"levels",1)
		var rect_value: Rect2 = Rect2(250,205+row*63,760,52)
		panel(rect_value,Color("dce8d5") if level<max_level else MIST)
		draw_text("%s • Lv.%d/%d" % [string_value(building,"name"),level,max_level],rect_value.position+Vector2(12,23),16,GREEN)
		if level < max_level:
			draw_text(GrowWiseWorldExpansion.cost_text(GrowWiseWorldExpansion.next_building_cost(world_data,string_value(building,"id"),level)),rect_value.position+Vector2(300,23),13,INK,430.0,HORIZONTAL_ALIGNMENT_RIGHT)
		else:
			draw_text("เต็มระดับ",rect_value.position+Vector2(610,23),13,TEAL)
		row += 1
		if row >= 6: break

func draw_fishing_overlay() -> void:
	draw_expansion_shell(tx("ui.fishing"))
	draw_text("ท่าเรือ Lv.%d • เหยื่อ %d • เหลือ %d ครั้งวันนี้" % [int(building_levels.get("dock",0)),int(inventory.get("bait",0)),fishing_attempts],Vector2(260,150),18,GREEN)
	draw_rect(Rect2(300,280,680,55),Color("315a3a"))
	draw_rect(Rect2(300+680.0*(fishing_target-fishing_width),280,680.0*fishing_width*2.0,55),TEAL)
	draw_rect(Rect2(300+680.0*fishing_meter-4,270,8,75),GOLD)
	if hooked_fish.is_empty(): draw_text("กด Space หรือปุ่มด้านล่างเพื่อหย่อนเบ็ด",Vector2(330,390),20,INK,620.0,HORIZONTAL_ALIGNMENT_CENTER)
	else: draw_text("ปลากำลังกินเหยื่อ • กดเมื่อแท่งอยู่ในพื้นที่สีเขียว",Vector2(330,390),20,INK,620.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(475,500,330,55),TEAL)
	draw_text("ดึงเบ็ด" if fishing_active else "เริ่มตกปลา",Vector2(485,537),20,Color.WHITE,310.0,HORIZONTAL_ALIGNMENT_CENTER)

func draw_cooking_overlay() -> void:
	draw_expansion_shell(tx("ui.cooking"))
	var recipes: Array = array_value(world_data,"recipes")
	for index: int in range(recipes.size()):
		var recipe: Dictionary = recipes[index] as Dictionary
		var rect_value: Rect2 = Rect2(255,150+index*76,760,62)
		panel(rect_value,Color("dce8d5"))
		draw_text(string_value(recipe,"name"),rect_value.position+Vector2(12,26),17,GREEN)
		draw_text("มูลค่า %d • ความสุข +%d" % [int_value(recipe,"value"),int_value(recipe,"happiness")],rect_value.position+Vector2(470,26),14,GOLD,270.0,HORIZONTAL_ALIGNMENT_RIGHT)

func draw_explore_overlay() -> void:
	draw_expansion_shell(tx("ui.explore"))
	panel(Rect2(300,230,300,110),GREEN)
	draw_text("สำรวจป่าชุมชน",Vector2(315,270),22,Color.WHITE,270.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("เหลือ %d ครั้ง • ไม้/สมุนไพร" % forest_attempts,Vector2(315,310),15,Color.WHITE,270.0,HORIZONTAL_ALIGNMENT_CENTER)
	panel(Rect2(680,230,300,110),Color("5e3b30"))
	draw_text("สำรวจเหมือง",Vector2(695,270),22,Color.WHITE,270.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("เหลือ %d ครั้ง • หิน/โลหะ" % mine_attempts,Vector2(695,310),15,Color.WHITE,270.0,HORIZONTAL_ALIGNMENT_CENTER)
	draw_text("X เปิดหน้าสำรวจ • การใช้ทรัพยากรป่าที่พอดีเพิ่มคะแนนสิ่งแวดล้อม",Vector2(300,430),16,INK,680.0,HORIZONTAL_ALIGNMENT_CENTER)

func draw_world() -> void:
	super.draw_world()
	draw_town_growth()

func draw_town_growth() -> void:
	var population: int = int(town_metrics.get("population",1))
	var houses: int = mini(5,maxi(0,population/4))
	for index: int in range(houses):
		var p: Vector2 = Vector2(845+index*37,500-posmod(index*19,70))
		draw_rect(Rect2(p+Vector2(-12,-8),Vector2(24,20)),Color("f3e5c2"))
		draw_colored_polygon(PackedVector2Array([p+Vector2(-15,-8),p+Vector2(0,-22),p+Vector2(15,-8)]),Color("d77a45"))
		draw_rect(Rect2(p+Vector2(-3,2),Vector2(6,10)),Color("714831"))
	for index: int in range(mini(8,population)):
		var p: Vector2 = Vector2(820+posmod(index*43,150),520-posmod(index*29,95))
		draw_circle(p,4.0,Color("f3e5c2"))
		draw_line(p+Vector2(0,4),p+Vector2(0,13),Color("315a3a"),3.0)

func draw_hud() -> void:
	super.draw_hud()
	draw_text("%s Lv.%d • คน %d • สุข %d" % [String(town_metrics.get("name","บ้านสวน")),int(town_metrics.get("level",1)),int(town_metrics.get("population",1)),int(town_metrics.get("happiness",0))],Vector2(760,92),13,TEAL,240.0,HORIZONTAL_ALIGNMENT_RIGHT)
