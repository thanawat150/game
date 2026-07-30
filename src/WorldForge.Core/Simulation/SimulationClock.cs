namespace WorldForge.Core.Simulation;

public sealed class SimulationClock
{
    public const double FixedStepSeconds = 0.1;
    public const int DefaultMaxStepsPerFrame = 128;

    private double _accumulator;

    public long TickCount { get; private set; }
    public double TimeScale { get; private set; } = 1.0;
    public bool IsPaused { get; private set; }
    public int MaxStepsPerFrame { get; init; } = DefaultMaxStepsPerFrame;

    public void SetPaused(bool paused) => IsPaused = paused;

    public void TogglePaused() => IsPaused = !IsPaused;

    public void SetTimeScale(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0 || scale > 64)
            throw new ArgumentOutOfRangeException(nameof(scale), "Time scale must be greater than zero and no more than 64.");
        TimeScale = scale;
    }

    public int Advance(double realDeltaSeconds, Action<long>? onTick = null)
    {
        if (realDeltaSeconds < 0 || double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds))
            throw new ArgumentOutOfRangeException(nameof(realDeltaSeconds));
        if (IsPaused)
            return 0;

        _accumulator += realDeltaSeconds * TimeScale;
        int steps = 0;
        while (_accumulator >= FixedStepSeconds && steps < MaxStepsPerFrame)
        {
            _accumulator -= FixedStepSeconds;
            TickCount++;
            steps++;
            onTick?.Invoke(TickCount);
        }

        // Protect the game from an unbounded spiral of death while keeping at most one frame of backlog.
        if (steps == MaxStepsPerFrame && _accumulator > FixedStepSeconds)
            _accumulator = FixedStepSeconds;

        return steps;
    }

    public void Restore(long tickCount, double timeScale, bool paused = false)
    {
        if (tickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(tickCount));
        TickCount = tickCount;
        SetTimeScale(timeScale);
        IsPaused = paused;
        _accumulator = 0;
    }
}
