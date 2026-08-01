extends SceneTree

const MANAGER_PATH := "res://scripts/save/save_manager.gd"
const TEST_PATH := "user://growwise3d_save_v2_contract.json"


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if not ResourceLoader.exists(MANAGER_PATH, "Script"):
		push_error("SAVE_CONTRACT: missing save_manager.gd")
		quit(1)
		return
	_cleanup_test_files()
	var manager := Node.new()
	manager.set_script(load(MANAGER_PATH))
	manager.set("save_path", TEST_PATH)
	get_root().add_child(manager)
	if int(manager.get("save_version")) != 2:
		_fail("SAVE_VERSION must be 2")
		return
	if manager.save_game() != OK or not FileAccess.file_exists(TEST_PATH):
		_fail("first save must create primary")
		return
	if not _valid_v2(TEST_PATH):
		_fail("primary save must validate")
		return
	if manager.save_game() != OK or not FileAccess.file_exists(TEST_PATH + ".bak"):
		_fail("second save must retain valid backup")
		return
	if not _valid_v2(TEST_PATH + ".bak"):
		_fail("backup must validate")
		return
	var partial := FileAccess.open(TEST_PATH, FileAccess.WRITE)
	partial.store_string(JSON.stringify({"save_version":2, "player":{"position":[1,0,2]}}))
	partial.close()
	if manager.load_game() != OK:
		_fail("missing optional sections must load with defaults")
		return
	if manager.save_game() != OK:
		_fail("save after missing-section defaults must succeed")
		return
	var corrupt := FileAccess.open(TEST_PATH, FileAccess.WRITE)
	corrupt.store_string("{broken json")
	corrupt.close()
	if manager.load_game() != OK:
		_fail("corrupt primary must recover from valid backup")
		return
	if not _valid_v2(TEST_PATH) or not _valid_v2(TEST_PATH + ".bak"):
		_fail("recovery must preserve valid primary and backup")
		return
	if not _has_corrupt_copy():
		_fail("corrupt primary must be preserved")
		return
	var future := FileAccess.open(TEST_PATH, FileAccess.WRITE)
	future.store_string(JSON.stringify({"save_version": 99, "player": {}, "camera": {}, "world": {}, "systems": {}}))
	future.close()
	if manager.load_game() != ERR_FILE_UNRECOGNIZED:
		_fail("future version must be rejected read-only")
		return
	var future_data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(TEST_PATH))
	if int(future_data.get("save_version", 0)) != 99:
		_fail("future save must not be overwritten")
		return
	print("GROWWISE3D_SAVE_CONTRACT_OK")
	quit(0)


func _valid_v2(path: String) -> bool:
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed is Dictionary and int(parsed.get("save_version", 0)) == 2


func _has_corrupt_copy() -> bool:
	var directory := DirAccess.open("user://")
	if directory == null:
		return false
	for file_name in directory.get_files():
		if file_name.begins_with("growwise3d_save_v2_contract.json.corrupt."):
			return true
	return false


func _cleanup_test_files() -> void:
	var directory := DirAccess.open("user://")
	if directory == null:
		return
	for file_name in directory.get_files():
		if file_name.begins_with("growwise3d_save_v2_contract.json"):
			directory.remove(file_name)


func _fail(message: String) -> void:
	push_error("SAVE_CONTRACT: %s" % message)
	quit(1)
