extends RefCounted
class_name GrowWiseAudioFactory

static func tone(frequency: float, duration: float, volume: float = 0.22) -> AudioStreamWAV:
	var mix_rate: int = 22050
	var sample_count: int = maxi(1, int(duration * float(mix_rate)))
	var bytes: PackedByteArray = PackedByteArray()
	bytes.resize(sample_count * 2)
	for index: int in range(sample_count):
		var time: float = float(index) / float(mix_rate)
		var envelope: float = minf(1.0, float(index) / 180.0) * minf(1.0, float(sample_count - index) / 500.0)
		var sample: float = sin(TAU * frequency * time) * volume * envelope
		bytes.encode_s16(index * 2, clampi(int(sample * 32767.0), -32768, 32767))
	var stream: AudioStreamWAV = AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = mix_rate
	stream.stereo = false
	stream.data = bytes
	return stream

static func action_sound(action: String) -> AudioStreamWAV:
	match action:
		"hoe": return tone(180.0, 0.10, 0.16)
		"water": return tone(420.0, 0.13, 0.12)
		"plant": return tone(520.0, 0.09, 0.14)
		"harvest": return tone(760.0, 0.16, 0.18)
		"success": return tone(880.0, 0.22, 0.18)
		"error": return tone(120.0, 0.18, 0.14)
		"shop": return tone(640.0, 0.11, 0.15)
		_: return tone(300.0, 0.08, 0.10)
