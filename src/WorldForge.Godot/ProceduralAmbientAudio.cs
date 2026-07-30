using Godot;

namespace WorldForge.Presentation;

public enum ProceduralAmbientMode
{
    Silent,
    Forest,
    Market,
    Rain,
    Storm,
    Event,
}

/// <summary>
/// Generates lightweight looping ambience at runtime, avoiding external audio assets.
/// </summary>
public sealed partial class ProceduralAmbientAudio : Node
{
    private AudioStreamPlayer _player = null!;
    private AudioStreamGeneratorPlayback? _playback;
    private readonly Random _random = new(90210);
    private ProceduralAmbientMode _mode;
    private double _phase;
    private double _secondaryPhase;
    private float _filteredNoise;
    private double _eventRemaining;
    private int _sampleIndex;

    public override void _Ready()
    {
        var generator = new AudioStreamGenerator
        {
            MixRate = 22050,
            BufferLength = 0.35f,
        };
        _player = new AudioStreamPlayer
        {
            Stream = generator,
            VolumeDb = -22,
            Bus = "Master",
            Autoplay = false,
        };
        AddChild(_player);
        _player.Play();
        _playback = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    public override void _Process(double delta)
    {
        if (_playback is null)
            return;
        if (_eventRemaining > 0)
        {
            _eventRemaining = Math.Max(0, _eventRemaining - delta);
            if (_eventRemaining <= 0 && _mode == ProceduralAmbientMode.Event)
                _mode = ProceduralAmbientMode.Silent;
        }

        int available = _playback.GetFramesAvailable();
        for (int i = 0; i < available; i++)
        {
            float sample = GenerateSample();
            _playback.PushFrame(new Vector2(sample, sample));
            _sampleIndex++;
        }
    }

    public void SetMode(ProceduralAmbientMode mode, float volumeDb)
    {
        if (_mode != ProceduralAmbientMode.Event || _eventRemaining <= 0)
            _mode = mode;
        if (_player is not null)
            _player.VolumeDb = volumeDb;
    }

    public void TriggerEvent()
    {
        _mode = ProceduralAmbientMode.Event;
        _eventRemaining = 1.25;
        _sampleIndex = 0;
        if (_player is not null)
            _player.VolumeDb = -12;
    }

    private float GenerateSample()
    {
        const double sampleRate = 22050.0;
        double time = _sampleIndex / sampleRate;
        float white = (float)(_random.NextDouble() * 2 - 1);
        _filteredNoise = _filteredNoise * 0.82f + white * 0.18f;

        return _mode switch
        {
            ProceduralAmbientMode.Forest => ForestSample(time, white),
            ProceduralAmbientMode.Market => MarketSample(time, white),
            ProceduralAmbientMode.Rain => RainSample(white, false),
            ProceduralAmbientMode.Storm => RainSample(white, true),
            ProceduralAmbientMode.Event => EventSample(time),
            _ => 0f,
        };
    }

    private float ForestSample(double time, float white)
    {
        double windEnvelope = 0.6 + 0.4 * Math.Sin(time * 0.7);
        float wind = _filteredNoise * (float)(0.022 * windEnvelope);
        double cycle = time % 4.2;
        float bird = 0;
        if (cycle is > 0.7 and < 0.82)
        {
            double local = cycle - 0.7;
            double frequency = 1450 + local * 4400;
            bird = (float)(Math.Sin(Math.Tau * frequency * local) * (1 - local / 0.12) * 0.045);
        }
        return wind + bird;
    }

    private float MarketSample(double time, float white)
    {
        _phase += Math.Tau * 105 / 22050.0;
        _secondaryPhase += Math.Tau * 163 / 22050.0;
        float hum = (float)(Math.Sin(_phase) * 0.008 + Math.Sin(_secondaryPhase) * 0.006);
        float murmur = white * 0.014f;
        double cycle = time % 3.4;
        float bell = cycle < 0.2
            ? (float)(Math.Sin(Math.Tau * 620 * cycle) * Math.Exp(-12 * cycle) * 0.035)
            : 0;
        return hum + murmur + bell;
    }

    private float RainSample(float white, bool storm)
    {
        float baseRain = _filteredNoise * (storm ? 0.085f : 0.048f);
        float drops = _random.NextDouble() < (storm ? 0.003 : 0.0012) ? (storm ? 0.22f : 0.12f) : 0;
        return baseRain + drops + white * (storm ? 0.025f : 0.012f);
    }

    private float EventSample(double time)
    {
        double local = Math.Min(time, 1.25);
        double envelope = Math.Exp(-3.4 * local);
        double first = Math.Sin(Math.Tau * 523.25 * local) * 0.16 * envelope;
        double second = Math.Sin(Math.Tau * 659.25 * local) * 0.11 * Math.Exp(-4.1 * local);
        return (float)(first + second);
    }
}
