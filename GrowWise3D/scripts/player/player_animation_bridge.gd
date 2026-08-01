extends Node
class_name GrowWisePlayerAnimationBridge

@export var model_root_path: NodePath = NodePath("../ModelRoot")

@onready var model_root: Node3D = get_node(model_root_path) as Node3D

var state: int = GrowWisePlayerController.PlayerState.IDLE
var speed_ratio: float = 0.0
var elapsed: float = 0.0


func _ready() -> void:
	var player := get_parent() as GrowWisePlayerController
	player.state_changed.connect(_on_state_changed)


func _process(delta: float) -> void:
	elapsed += delta
	if model_root == null:
		return
	var frequency := 2.0
	var amplitude := 0.012
	if state == GrowWisePlayerController.PlayerState.WALK:
		frequency = lerpf(3.0, 6.0, speed_ratio)
		amplitude = 0.035
	elif state == GrowWisePlayerController.PlayerState.RUN:
		frequency = lerpf(6.0, 9.0, speed_ratio)
		amplitude = 0.06
	elif state == GrowWisePlayerController.PlayerState.INTERACT:
		frequency = 2.5
		amplitude = 0.025
	elif state == GrowWisePlayerController.PlayerState.WORK:
		frequency = 4.0
		amplitude = 0.045
	model_root.position.y = sin(elapsed * frequency) * amplitude
	var squash := absf(cos(elapsed * frequency)) * amplitude * 0.25
	model_root.scale = Vector3(1.0 + squash, 1.0 - squash, 1.0 + squash)


func _on_state_changed(next_state: int, next_speed_ratio: float) -> void:
	state = next_state
	speed_ratio = next_speed_ratio
