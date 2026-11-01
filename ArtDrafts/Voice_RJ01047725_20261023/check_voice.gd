extends SceneTree
func _initialize():
	for clip in ["79", "94", "102", "104", "106", "114", "116", "120", "121", "122"]:
		var stream = load("res://Resources/Audio/Voice/RJ01047725/" + clip + ".wav")
		assert(stream is AudioStreamWAV)
		assert(stream.get_length() > 0.1)
		print("VOICE_OK ", clip)
	quit()
