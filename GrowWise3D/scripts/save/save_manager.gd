extends Node
class_name GrowWiseSaveManager

signal save_completed(path: String)
signal load_completed()
signal save_warning(message: String)

const SAVE_VERSION := 2
const DEFAULT_SAVE_PATH := "user://growwise3d_save_v2.json"

@export var save_path: String = DEFAULT_SAVE_PATH

var save_version: int = SAVE_VERSION
var selected_plot_id: String = ""
var _validation_error: String = ""


func _ready() -> void:
	var hud := get_node_or_null("../../CanvasLayer/UI")
	if hud != null:
		save_warning.connect(hud.show_message)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("save_game"):
		save_game()
	elif event.is_action_pressed("load_game"):
		load_game()


func new_game() -> void:
	_apply_snapshot(_default_snapshot())


func save_game() -> Error:
	var interrupted_error := _recover_interrupted_primary()
	if interrupted_error != OK and interrupted_error != ERR_DOES_NOT_EXIST:
		return interrupted_error
	var snapshot := _collect_snapshot()
	var temporary_path := save_path + ".tmp"
	var write_error := _write_text(temporary_path, JSON.stringify(snapshot, "  "))
	if write_error != OK:
		_warn("เขียนไฟล์ชั่วคราวไม่สำเร็จ: %s" % error_string(write_error))
		return write_error
	var validated := _read_validated(temporary_path)
	if validated.is_empty():
		_warn("ไฟล์ชั่วคราวไม่ผ่าน JSON/schema validation")
		return ERR_INVALID_DATA
	var backup_path := save_path + ".bak"
	if FileAccess.file_exists(save_path) and not _read_validated(save_path).is_empty():
		var backup_error := _copy_validated(save_path, backup_path)
		if backup_error != OK:
			_warn("ไม่สามารถรักษา valid backup ได้ จึงยกเลิกการบันทึก")
			return backup_error
	var promotion_error := _promote_temporary(temporary_path, save_path)
	if promotion_error != OK:
		_warn("promote save ชั่วคราวไม่สำเร็จ: %s" % error_string(promotion_error))
		return promotion_error
	save_completed.emit(save_path)
	return OK


func load_game() -> Error:
	if not FileAccess.file_exists(save_path) and _recover_interrupted_primary() != OK:
		_warn("ยังไม่มีไฟล์บันทึก GrowWise3D")
		return ERR_FILE_NOT_FOUND
	var snapshot := _read_validated(save_path, true)
	if snapshot.is_empty() and _validation_error == "future_version":
		_warn("Save version ใหม่กว่าโปรแกรม ไฟล์จะไม่ถูกเขียนทับ")
		return ERR_FILE_UNRECOGNIZED
	if snapshot.is_empty():
		var corrupt_path := "%s.corrupt.%d.bak" % [save_path, int(Time.get_unix_time_from_system())]
		var preserve_error := DirAccess.rename_absolute(
			ProjectSettings.globalize_path(save_path),
			ProjectSettings.globalize_path(corrupt_path)
		)
		if preserve_error != OK:
			_warn("ไม่สามารถเก็บสำเนา Save ที่เสียได้")
			return preserve_error
		var backup_path := save_path + ".bak"
		var backup := _read_validated(backup_path)
		if backup.is_empty():
			_warn("Save เสียและไม่มี valid backup สำหรับ recovery")
			return ERR_INVALID_DATA
		var recovery_error := _copy_validated(backup_path, save_path)
		if recovery_error != OK:
			_warn("recovery จาก valid backup ไม่สำเร็จ")
			return recovery_error
		snapshot = _read_validated(save_path)
		if snapshot.is_empty():
			return ERR_INVALID_DATA
		_warn("กู้คืน Save จาก valid backup แล้ว")
	_apply_snapshot(snapshot)
	load_completed.emit()
	return OK


func _collect_snapshot() -> Dictionary:
	var snapshot := _default_snapshot()
	var player := get_node_or_null("../../World3D/Player") as Node3D
	var camera := get_node_or_null("../../CameraRig")
	var time_controller := get_node_or_null("../TimeOfDayController")
	if player != null:
		snapshot["player"] = {
			"position": _vector3_to_array(player.global_position),
			"rotation_y": player.rotation.y,
		}
	if camera != null and camera.has_method("get_zoom"):
		snapshot["camera"] = {"zoom": camera.get_zoom()}
	var npc_data: Array[Dictionary] = []
	for npc in get_tree().get_nodes_in_group("growwise_npc"):
		npc_data.append(npc.serialize())
	snapshot["world"] = {"selected_plot": selected_plot_id, "npcs": npc_data}
	if time_controller != null:
		snapshot["systems"] = {"time_of_day": time_controller.serialize()}
	return snapshot


func _apply_snapshot(snapshot: Dictionary) -> void:
	var player := get_node_or_null("../../World3D/Player") as Node3D
	var player_data: Dictionary = snapshot.get("player", {})
	if player != null:
		player.global_position = _array_to_vector3(player_data.get("position", [0.0, 0.2, 10.0]), Vector3(0, 0.2, 10))
		player.rotation.y = float(player_data.get("rotation_y", 0.0))
	var camera := get_node_or_null("../../CameraRig")
	if camera != null:
		camera.set_zoom(float(snapshot.get("camera", {}).get("zoom", 14.0)))
	var world_data: Dictionary = snapshot.get("world", {})
	selected_plot_id = str(world_data.get("selected_plot", ""))
	var npc_by_id: Dictionary[String, Node] = {}
	for npc in get_tree().get_nodes_in_group("growwise_npc"):
		npc_by_id[npc.npc_id] = npc
	for npc_data: Dictionary in world_data.get("npcs", []):
		var npc_id := str(npc_data.get("npc_id", ""))
		if npc_by_id.has(npc_id):
			npc_by_id[npc_id].deserialize(npc_data)
	var time_controller := get_node_or_null("../TimeOfDayController")
	if time_controller != null:
		time_controller.deserialize(snapshot.get("systems", {}).get("time_of_day", {}))


func _default_snapshot() -> Dictionary:
	return {
		"save_version": SAVE_VERSION,
		"created_at": Time.get_datetime_string_from_system(true),
		"updated_at": Time.get_datetime_string_from_system(true),
		"player": {"position":[0.0,0.2,10.0], "rotation_y":0.0},
		"camera": {"zoom":14.0},
		"world": {"selected_plot":"", "npcs":[]},
		"systems": {"time_of_day":{"preset":"Morning", "preset_index":0}},
	}


func _read_validated(path: String, allow_future_check: bool = false) -> Dictionary:
	_validation_error = ""
	if not FileAccess.file_exists(path):
		_validation_error = "missing"
		return {}
	var json := JSON.new()
	if json.parse(FileAccess.get_file_as_string(path)) != OK:
		_validation_error = "invalid_json"
		return {}
	var parsed: Variant = json.data
	if not parsed is Dictionary:
		_validation_error = "invalid_json"
		return {}
	var version: Variant = parsed.get("save_version")
	if not version is int and not version is float:
		_validation_error = "missing_version"
		return {}
	if int(version) > SAVE_VERSION and allow_future_check:
		_validation_error = "future_version"
		return {}
	if int(version) != SAVE_VERSION:
		_validation_error = "unsupported_version"
		return {}
	for section in ["player", "camera", "world", "systems"]:
		if parsed.has(section) and not parsed[section] is Dictionary:
			_validation_error = "invalid_section_%s" % section
			return {}
	var defaults := _default_snapshot()
	for section in ["player", "camera", "world", "systems"]:
		if not parsed.has(section):
			parsed[section] = defaults[section].duplicate(true)
	return parsed


func _copy_validated(source: String, destination: String) -> Error:
	var source_data := _read_validated(source)
	if source_data.is_empty():
		return ERR_INVALID_DATA
	var temporary := destination + ".tmp"
	var error := _write_text(temporary, JSON.stringify(source_data, "  "))
	if error != OK or _read_validated(temporary).is_empty():
		return ERR_INVALID_DATA if error == OK else error
	return _promote_temporary(temporary, destination)


func _promote_temporary(temporary: String, destination: String) -> Error:
	var previous := destination + ".previous"
	if FileAccess.file_exists(previous) and not _read_validated(previous).is_empty():
		if FileAccess.file_exists(destination) and not _read_validated(destination).is_empty():
			DirAccess.remove_absolute(ProjectSettings.globalize_path(previous))
		else:
			var recover_error := DirAccess.rename_absolute(ProjectSettings.globalize_path(previous), ProjectSettings.globalize_path(destination))
			if recover_error != OK:
				return recover_error
	if FileAccess.file_exists(previous):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(previous))
	if FileAccess.file_exists(destination):
		var move_error := DirAccess.rename_absolute(ProjectSettings.globalize_path(destination), ProjectSettings.globalize_path(previous))
		if move_error != OK:
			return move_error
	var promote_error := DirAccess.rename_absolute(ProjectSettings.globalize_path(temporary), ProjectSettings.globalize_path(destination))
	if promote_error != OK:
		if FileAccess.file_exists(previous):
			DirAccess.rename_absolute(ProjectSettings.globalize_path(previous), ProjectSettings.globalize_path(destination))
		return promote_error
	if _read_validated(destination).is_empty():
		DirAccess.remove_absolute(ProjectSettings.globalize_path(destination))
		if FileAccess.file_exists(previous):
			DirAccess.rename_absolute(ProjectSettings.globalize_path(previous), ProjectSettings.globalize_path(destination))
		return ERR_INVALID_DATA
	if FileAccess.file_exists(previous):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(previous))
	return OK


func _recover_interrupted_primary() -> Error:
	if FileAccess.file_exists(save_path):
		return OK
	var previous := save_path + ".previous"
	if not FileAccess.file_exists(previous):
		return ERR_DOES_NOT_EXIST
	if _read_validated(previous).is_empty():
		return ERR_INVALID_DATA
	return DirAccess.rename_absolute(ProjectSettings.globalize_path(previous), ProjectSettings.globalize_path(save_path))


func _write_text(path: String, content: String) -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(content)
	file.flush()
	file.close()
	return OK


func _vector3_to_array(value: Vector3) -> Array[float]:
	return [value.x, value.y, value.z]


func _array_to_vector3(value: Variant, fallback: Vector3) -> Vector3:
	if not value is Array or value.size() < 3:
		return fallback
	return Vector3(float(value[0]), float(value[1]), float(value[2]))


func _warn(message: String) -> void:
	push_warning(message)
	save_warning.emit(message)
