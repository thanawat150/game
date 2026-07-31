extends Node2D

const GrowWiseArtFactory = preload("res://scripts/art_factory.gd")

const W:=8
const H:=8
const TW:=128.0
const TH:=64.0
const ORIGIN:=Vector2(520,145)
const SAVE_VERSION:=1
const SAVE_DIR:="user://saves"
const CREAM:=Color("f3e5c2")
const INK:=Color("29302a")
const WOOD:=Color("714831")
const WOOD_L:=Color("b97a4d")
const GREEN:=Color("4f8748")
const TEAL:=Color("4c927e")
const GOLD:=Color("e9b84d")
const MIST:=Color("d8e2d5")

var atlas:Texture2D
var grass:AtlasTexture
var dirt:AtlasTexture
var dry:AtlasTexture
var moist:AtlasTexture
var wet:AtlasTexture
var selector:AtlasTexture
var icons:Dictionary={}
var crops:Dictionary={"water_spinach":[],"kale":[]}
var players:Array[Texture2D]=[]
var defs:Dictionary={}
var tiles:Dictionary={}
var inv:Dictionary={}
var player:=Vector2(3.5,6.2)
var selected:=Vector2i(3,3)
var tool:="hoe"
var seed:="water_spinach"
var day:=1
var minutes:=480.0
var speed:=1
var paused:=false
var slot:=1
var autosave:=0.0
var anim:=0.0
var frame:=0
var mode:="menu"
var quest:=0
var knowledge:=0
var water_used:=0
var harvest_total:=0
var msg:="เลือกจอบแล้วพรวนดิน"
var msg_time:=8.0
var buttons:Array[Dictionary]=[]

func _ready()->void:
	load_atlas()
	build_regions()
	load_defs()
	new_game()
	build_buttons()
	print("GROWWISE_SMOKE_OK")
	queue_redraw()

func load_atlas()->void:
	var image:=GrowWiseArtFactory.build_atlas()
	atlas=ImageTexture.create_from_image(image)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("user://generated_assets"))
	image.save_png("user://generated_assets/growwise_phase1_atlas.png")

func region(x:int,y:int,w:int,h:int)->AtlasTexture:
	var t:=AtlasTexture.new()
	t.atlas=atlas
	t.region=Rect2(x,y,w,h)
	t.filter_clip=true
	return t

func build_regions()->void:
	grass=region(0,0,128,64);dirt=region(128,0,128,64)
	dry=region(256,0,128,64);moist=region(384,0,128,64)
	wet=region(512,0,128,64);selector=region(768,0,128,64)
	var names:=["hoe","water","seed","inspect","harvest","save","load"]
	for i in range(names.size()):icons[names[i]]=region(i*64,96,64,64)
	for i in range(5):
		crops.water_spinach.append(region(i*64,160,64,64))
		crops.kale.append(region(i*64,224,64,64))
	for i in range(12):players.append(region((i%8)*64,288+int(i/8)*64,64,64))

func load_defs()->void:
	var f:=FileAccess.open("res://data/crops.json",FileAccess.READ)
	if f:
		var p=JSON.parse_string(f.get_as_text())
		if typeof(p)==TYPE_DICTIONARY:defs=p

func new_game()->void:
	tiles.clear()
	for y in range(H):
		for x in range(W):
			tiles[key(Vector2i(x,y))]={"farm":x>=2 and x<=5 and y>=2 and y<=5,"tilled":false,"moisture":25.0,"fertility":70.0,"light":80.0,"crop":"","growth":0.0,"stage":0}
	inv={"seed_water_spinach":6,"seed_kale":6,"produce_water_spinach":0,"produce_kale":0}
	player=Vector2(3.5,6.2);selected=Vector2i(3,3);tool="hoe";seed="water_spinach"
	day=1;minutes=480.0;quest=0;knowledge=0;water_used=0;harvest_total=0
	notify("ภารกิจแรก: พรวนดิน")

func build_buttons()->void:
	buttons=[
		{"id":"hoe","r":Rect2(180,635,76,70),"i":icons.hoe,"t":"พรวน"},
		{"id":"water","r":Rect2(264,635,76,70),"i":icons.water,"t":"รดน้ำ"},
		{"id":"ws","r":Rect2(348,635,76,70),"i":icons.seed,"t":"ผักบุ้ง"},
		{"id":"kale","r":Rect2(432,635,76,70),"i":icons.seed,"t":"คะน้า"},
		{"id":"inspect","r":Rect2(516,635,76,70),"i":icons.inspect,"t":"ตรวจ"},
		{"id":"harvest","r":Rect2(600,635,76,70),"i":icons.harvest,"t":"เก็บ"},
		{"id":"save","r":Rect2(920,635,76,70),"i":icons.save,"t":"บันทึก"},
		{"id":"load","r":Rect2(1004,635,76,70),"i":icons.load,"t":"โหลด"}
	]

func _process(delta:float)->void:
	if mode=="game":
		move_player(delta)
		if msg_time>0:msg_time-=delta
		if not paused:
			var d:=delta*speed
			minutes+=d*4.0
			if minutes>=1440:minutes-=1440;day+=1
			grow(d)
			autosave+=delta
			if autosave>=60:autosave=0;save_game(slot,true)
	queue_redraw()

func move_player(delta:float)->void:
	var d:=Input.get_vector("ui_left","ui_right","ui_up","ui_down")
	if Input.is_key_pressed(KEY_A):d.x-=1
	if Input.is_key_pressed(KEY_D):d.x+=1
	if Input.is_key_pressed(KEY_W):d.y-=1
	if Input.is_key_pressed(KEY_S):d.y+=1
	if d.length()>0.1:
		player+=d.normalized()*delta*2.2
		player.x=clamp(player.x,0.0,W-1.0);player.y=clamp(player.y,0.0,H-1.0)
		anim+=delta
		if anim>.16:anim=0;frame=2+((frame+1)%4)
	else:frame=0

func grow(delta:float)->void:
	for k in tiles:
		var t:Dictionary=tiles[k]
		if t.tilled:t.moisture=max(0.0,float(t.moisture)-delta*.42)
		if String(t.crop)!="":
			if t.moisture>12:t.growth=float(t.growth)+delta
			var a:Array=defs.get(String(t.crop),{}).get("growth_seconds",[0,10,20,30,45])
			var s:=0
			for i in range(a.size()):
				if t.growth>=a[i]:s=i
			t.stage=min(s,4)
		tiles[k]=t

func _unhandled_input(e:InputEvent)->void:
	if e is InputEventKey and e.pressed and not e.echo:
		if e.keycode==KEY_ESCAPE:mode="menu" if mode=="game" else "game"
		elif mode=="game":
			if e.keycode==KEY_SPACE:paused=not paused
			elif e.keycode==KEY_F1:slot=1;notify("ช่องบันทึก 1")
			elif e.keycode==KEY_F2:slot=2;notify("ช่องบันทึก 2")
			elif e.keycode==KEY_F3:slot=3;notify("ช่องบันทึก 3")
			elif e.keycode==KEY_MINUS:speed=max(1,int(speed/2))
			elif e.keycode==KEY_EQUAL:speed=min(4,speed*2)
	if e is InputEventMouseMotion and mode=="game":
		var c:=pick(e.position)
		if valid(c):selected=c
	if e is InputEventMouseButton and e.pressed and e.button_index==MOUSE_BUTTON_LEFT:
		if mode=="menu":menu_click(e.position)
		else:game_click(e.position)

func menu_click(p:Vector2)->void:
	if Rect2(490,365,300,58).has_point(p):new_game();mode="game"
	elif Rect2(490,435,300,58).has_point(p):
		if not load_game(slot):new_game()
		mode="game"
	elif Rect2(490,505,300,58).has_point(p):get_tree().quit()

func game_click(p:Vector2)->void:
	for b in buttons:
		if b.r.has_point(p):
			var id:=String(b.id)
			if id=="save":save_game(slot,false)
			elif id=="load":load_game(slot)
			elif id=="ws":tool="seed";seed="water_spinach"
			elif id=="kale":tool="seed";seed="kale"
			else:tool=id
			return
	var c:=pick(p)
	if valid(c):selected=c;act(c)

func act(c:Vector2i)->void:
	var k:=key(c);var t:Dictionary=tiles[k]
	if not t.farm:notify("พื้นที่นี้ไม่ใช่แปลง");return
	if tool=="hoe":
		if String(t.crop)!="":notify("มีพืชอยู่แล้ว");return
		t.tilled=true;t.moisture=min(float(t.moisture),30.0);notify("พรวนดินแล้ว");advance(0)
	elif tool=="water":
		if not t.tilled:notify("ต้องพรวนก่อน");return
		t.moisture=min(100.0,float(t.moisture)+42);water_used+=1;notify("รดน้ำแล้ว");advance(2)
	elif tool=="seed":
		if not t.tilled or String(t.crop)!="":notify("ปลูกไม่ได้");return
		var item:="seed_"+seed
		if inv[item]<=0:notify("เมล็ดหมด");return
		inv[item]-=1;t.crop=seed;t.growth=0.0;t.stage=0;notify("ปลูก"+crop_name(seed)+"แล้ว");advance(1)
	elif tool=="inspect":inspect(t)
	elif tool=="harvest":
		if String(t.crop)=="" or t.stage<4:notify("ยังไม่พร้อมเก็บ");return
		var id:=String(t.crop);var amount:=3 if id=="water_spinach" else 2
		inv["produce_"+id]+=amount;harvest_total+=amount;knowledge+=10
		t.crop="";t.growth=0.0;t.stage=0;t.moisture=max(10.0,float(t.moisture)-20)
		notify("เก็บเกี่ยวได้ %d หน่วย"%amount);advance(3)
	tiles[k]=t

func inspect(t:Dictionary)->void:
	if not t.tilled:notify("ดินยังไม่พรวน | น้ำ %d%%"%int(t.moisture))
	elif String(t.crop)=="":notify("ดินว่าง | น้ำ %d%% | แสง %d%% | ดิน %d%%"%[t.moisture,t.light,t.fertility])
	else:
		var s:="เหมาะสม"
		if t.moisture<20:s="ดินแห้ง"
		elif t.moisture>85:s="น้ำมากเกินไป"
		notify("%s ระยะ %d/5 | น้ำ %d%% | %s"%[crop_name(t.crop),t.stage+1,t.moisture,s])

func advance(n:int)->void:
	if quest!=n:return
	quest+=1;knowledge+=5
	var a:=["ต่อไป: ปลูกเมล็ด","ต่อไป: รดน้ำ","รอให้โตเต็มที่","บทเรียนแรกสำเร็จ"]
	notify(a[quest-1])

func notify(s:String)->void:msg=s;msg_time=6
func key(c:Vector2i)->String:return "%d,%d"%[c.x,c.y]
func valid(c:Vector2i)->bool:return c.x>=0 and c.y>=0 and c.x<W and c.y<H
func iso(c:Vector2)->Vector2:return ORIGIN+Vector2((c.x-c.y)*TW*.5,(c.x+c.y)*TH*.5)
func pick(p:Vector2)->Vector2i:
	var q:=p-ORIGIN;var dx:=q.x/(TW*.5);var dy:=q.y/(TH*.5)
	return Vector2i(round((dx+dy)*.5),round((dy-dx)*.5))
func crop_name(id:String)->String:return "ผักบุ้ง" if id=="water_spinach" else "คะน้า"

func save_game(n:int,auto:bool)->bool:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SAVE_DIR))
	var p:="%s/slot_%d.json"%[SAVE_DIR,n];var bak:=p+".bak"
	if FileAccess.file_exists(p):DirAccess.copy_absolute(ProjectSettings.globalize_path(p),ProjectSettings.globalize_path(bak))
	var data={"save_version":SAVE_VERSION,"day":day,"minutes":minutes,"tiles":tiles,"inv":inv,"player":[player.x,player.y],"quest":quest,"knowledge":knowledge,"water":water_used,"harvest":harvest_total}
	var f:=FileAccess.open(p,FileAccess.WRITE)
	if not f:notify("บันทึกไม่สำเร็จ");return false
	f.store_string(JSON.stringify(data));f.close()
	if not auto:notify("บันทึกช่อง %d แล้ว"%n)
	return true

func load_game(n:int)->bool:
	var p:="%s/slot_%d.json"%[SAVE_DIR,n];var data:=read_save(p)
	if data.is_empty():data=read_save(p+".bak")
	if data.is_empty():notify("ไม่พบไฟล์บันทึก");return false
	day=data.get("day",1);minutes=data.get("minutes",480.0);tiles=data.get("tiles",tiles);inv=data.get("inv",inv)
	var a:Array=data.get("player",[3.5,6.2]);player=Vector2(a[0],a[1])
	quest=data.get("quest",0);knowledge=data.get("knowledge",0);water_used=data.get("water",0);harvest_total=data.get("harvest",0)
	notify("โหลดช่อง %d แล้ว"%n);return true

func read_save(p:String)->Dictionary:
	if not FileAccess.file_exists(p):return {}
	var f:=FileAccess.open(p,FileAccess.READ)
	if not f:return {}
	var d=JSON.parse_string(f.get_as_text());return d if typeof(d)==TYPE_DICTIONARY else {}

func _draw()->void:
	draw_rect(Rect2(0,0,1280,720),Color("87b86b"));draw_rect(Rect2(0,0,1280,110),MIST);draw_rect(Rect2(0,610,1280,110),WOOD)
	draw_world();draw_hud()
	if mode=="menu":draw_menu()

func draw_world()->void:
	for d in range(W+H-1):
		for y in range(H):
			var x:=d-y
			if x<0 or x>=W:continue
			var c:=Vector2i(x,y);var t:Dictionary=tiles[key(c)];var p:=iso(c);var tx:Texture2D=grass
			if t.farm:
				tx=dirt
				if t.tilled:tx=wet if t.moisture>=80 else (moist if t.moisture>=40 else dry)
			draw_texture(tx,p-Vector2(64,32))
			if String(t.crop)!="":draw_texture(crops[t.crop][clamp(t.stage,0,4)],p-Vector2(32,58))
			if c==selected and mode=="game":draw_texture(selector,p-Vector2(64,32))
	draw_texture(players[frame],iso(player)-Vector2(32,58))

func panel(r:Rect2,c:Color)->void:draw_rect(r,WOOD);draw_rect(Rect2(r.position+Vector2(3,3),r.size-Vector2(6,6)),c)

func draw_hud()->void:
	var f:=ThemeDB.fallback_font;var hr:=int(minutes/60);var mn:=int(minutes)%60
	draw_string(f,Vector2(28,38),"วันที่ %d   %02d:%02d   อากาศแจ่มใส"%[day,hr,mn],0,-1,23,INK)
	draw_string(f,Vector2(28,72),"ความรู้ %d   น้ำที่ใช้ %d   ผลผลิต %d"%[knowledge,water_used,harvest_total],0,-1,19,GREEN)
	draw_string(f,Vector2(930,38),"เวลา x%d%s"%[speed," (หยุด)" if paused else ""],0,-1,20,INK)
	draw_string(f,Vector2(930,70),"ช่องบันทึก %d | F1-F3"%slot,0,-1,16,INK)
	panel(Rect2(18,125,230,205),CREAM);draw_string(f,Vector2(34,155),"เมล็ดแรกของฉัน",0,-1,18,INK)
	var qs:=["พรวนดิน","ปลูกเมล็ด","รดน้ำ","เก็บเกี่ยว"]
	for i in range(4):draw_string(f,Vector2(38,188+i*31),("✓ " if quest>i else "○ ")+qs[i],0,-1,17,TEAL if quest>i else INK)
	panel(Rect2(1010,125,252,250),CREAM);var t:Dictionary=tiles[key(selected)]
	draw_string(f,Vector2(1028,155),"ช่อง (%d,%d)"%[selected.x,selected.y],0,-1,18,INK)
	draw_string(f,Vector2(1028,190),"น้ำ %d%%"%int(t.moisture),0,-1,17,Color("4e9bb3"))
	draw_string(f,Vector2(1028,222),"แสง %d%%"%int(t.light),0,-1,17,GOLD)
	draw_string(f,Vector2(1028,254),"ดิน %d%%"%int(t.fertility),0,-1,17,GREEN)
	draw_string(f,Vector2(1028,290),"พืช: "+(crop_name(t.crop) if String(t.crop)!="" else "ว่าง"),0,-1,17,INK)
	for b in buttons:
		var active:=String(b.id)==tool or (b.id=="ws" and tool=="seed" and seed=="water_spinach") or (b.id=="kale" and tool=="seed" and seed=="kale")
		panel(b.r,GOLD if active else CREAM);draw_texture_rect(b.i,Rect2(b.r.position+Vector2(18,3),Vector2(40,40)),false);draw_string(f,b.r.position+Vector2(7,62),b.t,1,62,13,INK)
	draw_string(f,Vector2(18,602),"เมล็ด ผักบุ้ง %d | คะน้า %d    ผลผลิต ผักบุ้ง %d | คะน้า %d"%[inv.seed_water_spinach,inv.seed_kale,inv.produce_water_spinach,inv.produce_kale],0,-1,16,CREAM)
	if msg_time>0:panel(Rect2(300,548,680,48),MIST);draw_string(f,Vector2(320,580),msg,1,640,18,INK)

func draw_menu()->void:
	draw_rect(Rect2(0,0,1280,720),Color(0.05,0.08,0.05,.72));var f:=ThemeDB.fallback_font
	panel(Rect2(400,185,480,420),CREAM);draw_string(f,Vector2(430,250),"ปลูกเป็น",1,420,48,GREEN);draw_string(f,Vector2(430,292),"สวนทดลองของเรา",1,420,28,INK)
	var a:=[{"r":Rect2(490,365,300,58),"t":"เริ่มสวนทดลองใหม่"},{"r":Rect2(490,435,300,58),"t":"เล่นต่อจากช่องบันทึก"},{"r":Rect2(490,505,300,58),"t":"ออกจากเกม"}]
	for b in a:panel(b.r,WOOD_L);draw_string(f,b.r.position+Vector2(10,38),b.t,1,280,20,Color.WHITE)
