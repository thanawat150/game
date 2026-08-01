extends SceneTree

const REQUIRED_FILES := [
	"res://export_presets.cfg",
	"res://scripts/tests/capture_m1_screenshots.gd",
]
const SCREENSHOT_NAMES := ["morning.png", "day.png", "evening.png", "night.png", "selected_plot.png", "npc_overview.png"]


func _initialize() -> void:
	for path in REQUIRED_FILES:
		if not FileAccess.file_exists(path):
			push_error("RELEASE_CONTRACT: missing %s" % path)
			quit(1)
			return
	var capture := FileAccess.get_file_as_string("res://scripts/tests/capture_m1_screenshots.gd")
	for file_name in SCREENSHOT_NAMES:
		if capture.find(file_name) == -1:
			push_error("RELEASE_CONTRACT: capture script missing %s" % file_name)
			quit(1)
			return
	if capture.find("AUTOMATED_SCREENSHOT_ARTIFACT_NOT_MANUAL_VISUAL_TEST") == -1:
		push_error("RELEASE_CONTRACT: missing automated screenshot disclaimer")
		quit(1)
		return
	print("GROWWISE3D_RELEASE_PIPELINE_CONTRACT_OK")
	quit(0)
