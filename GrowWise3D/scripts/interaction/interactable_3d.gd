extends Area3D
class_name GrowWiseInteractable3D

@export var prompt: String = "[E] โต้ตอบ"
@export var priority: int = 0
@export var enabled: bool = true


func get_interaction_prompt() -> String:
	return prompt


func get_interaction_priority() -> int:
	return priority


func get_interaction_point() -> Vector3:
	return global_position


func can_interact(_actor: Node3D) -> bool:
	return enabled and is_inside_tree()


func interact(_actor: Node3D) -> void:
	pass
