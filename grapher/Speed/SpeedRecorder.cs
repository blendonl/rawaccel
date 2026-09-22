using System.Diagnostics;

namespace grapher.Speed;

public sealed class SpeedRecorder
{
    public const int HistoryCapacity = 1 << 16;

    private readonly Stopwatch clock = Stopwatch.StartNew();

    public bool Enabled { get; set; }

    public SpeedHistory History { get; } = new(HistoryCapacity);

    public SpeedStatistics InputStatistics { get; } = new();

    public SpeedStatistics OutputStatistics { get; } = new();

    public double NowMs => clock.Elapsed.TotalMilliseconds;

    public void Record(double inputSpeed, double outputSpeed, double timeMs, bool movedFromRest)
    {
        if (!Enabled)
        {
            return;
        }

        History.Add(NowMs, inputSpeed * timeMs, outputSpeed * timeMs);

        if (!movedFromRest)
        {
            InputStatistics.Add(inputSpeed);
            OutputStatistics.Add(outputSpeed);
        }
    }

    public void Reset()
    {
        History.Clear();
        InputStatistics.Reset();
        OutputStatistics.Reset();
    }
}
