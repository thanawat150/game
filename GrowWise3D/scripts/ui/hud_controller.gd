extends Control
class_name GrowWiseHUDController

@onready var date_label: Label = $SafeMargin/Layout/TopRow/InfoPanel/Info/Date
@onready var time_label: Label = $SafeMargin/Layout/TopRow/InfoPanel/Info/Time
@onready var weather_label: Label = $SafeMargin/Layout/TopRow/InfoPanel/Info/Weather
@onready var debug_panel: PanelContainer = $SafeMargin/Layout/TopRow/DebugPanel
@onready var debug_label: Label = $SafeMargin/Layout/TopRow/DebugPanel/Debug
@onready var plot_panel: PanelContainer = $SafeMargin/Layout/TopRow/PlotPanel
@onready var plot_label: Label = $SafeMargin/Layout/TopRow/PlotPanel/Plot
@onready var context_panel: PanelContainer = $SafeMargin/Layout/BottomRow/ContextPanel
@onready var context_label: Label = $SafeMargin/Layout/BottomRow/ContextPanel/Context

var debug_visible: bool = OS.is_debug_build()
var diagnostics: Node


func _ready() -> void:
	diagnostics = get_node_or_null("../../Systems/Diagnostics")
	var interaction := get_node_or_null("../../Systems/InteractionManager")
	if interaction != null:
		interaction.target_changed.connect(_on_target_changed)
	var time_controller := get_node_or_null("../../Systems/TimeOfDayController")
	if time_controller != null:
		time_controller.preset_changed.connect(_on_time_preset_changed)
		_on_time_preset_changed(time_controller.get_preset_name())
	call_deferred("_connect_world_signals")
	set_context_prompt("")
	plot_panel.visible = false
	set_debug_visible(debug_visible)


func _process(_delta: float) -> void:
	if not debug_visible or diagnostics == null:
		return
	var snapshot: Dictionary = diagnostics.get_snapshot()
	debug_label.text = "DEBUG • %d FPS\nPlayer: %s\nPos: %s\nTarget: %s\nNav: %s\nNPC: %s\nSave v%d" % [
		snapshot["fps"], snapshot["player_state"], str(snapshot["player_position"]),
		snapshot["interaction_target"], str(snapshot["navigation"].get("status", "pending")),
		", ".join(snapshot["npc_states"]), snapshot["save_version"]
	]


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("debug_toggle"):
		set_debug_visible(not debug_visible)


func set_context_prompt(text: String) -> void:
	context_label.text = text
	context_panel.visible = not text.is_empty()


func show_plot(plot: GrowWiseFarmPlot) -> void:
	plot_label.text = "แปลง %s\nความชื้น %.0f%%\nความอุดมสมบูรณ์ %.0f%%\nสุขภาพ %.0f%%" % [plot.plot_id, plot.moisture, plot.fertility, plot.health]
	plot_panel.visible = true


func show_message(text: String) -> void:
	set_context_prompt(text)


func set_debug_visible(value: bool) -> void:
	debug_visible = value
	debug_panel.visible = value


func _connect_world_signals() -> void:
	for plot in get_tree().get_nodes_in_group("growwise_plot"):
		plot.selected.connect(show_plot)
	for npc in get_tree().get_nodes_in_group("growwise_npc"):
		npc.dialogue_requested.connect(_on_dialogue_requested)


func _on_target_changed(_target: Node3D, prompt: String) -> void:
	set_context_prompt(prompt)


func _on_time_preset_changed(preset_name: String) -> void:
	date_label.text = "GrowWise 3D • วันที่ 1"
	time_label.text = "08:00"
	weather_label.text = "อากาศ: แจ่มใส • %s" % preset_name


func _on_dialogue_requested(_npc_id: String, display_name: String, text: String) -> void:
	show_message("%s: %s" % [display_name, text])
