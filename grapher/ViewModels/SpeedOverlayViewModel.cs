using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using grapher.Speed;

namespace grapher.ViewModels;

public sealed partial class SpeedOverlayViewModel : ObservableObject
{
    public const double GraphWindowMs = 5000;
    public const double CurrentSpeedWindowMs = 100;

    private const string NoValue = "-";
    private const double MinimumScale = 1;
    private static readonly double[] ScaleSteps = { 1, 1.5, 2, 3, 4, 5, 6, 8 };

    private readonly SpeedRecorder recorder;
    private readonly DispatcherTimer frameTimer;
    private readonly double[] currentSpeed = new double[1];

    public SpeedOverlayViewModel(SpeedRecorder recorder)
    {
        this.recorder = recorder;
        frameTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Refresh());
    }

    public event EventHandler? Refreshed;

    public string Title => ShowInput ? "Input speed (counts/ms)" : "Output speed (counts/ms)";

    public bool ShowOutput
    {
        get => !ShowInput;
        set => ShowInput = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(ShowOutput))]
    private bool showInput = true;

    [ObservableProperty]
    private string now = Format(0);

    [ObservableProperty]
    private string max = NoValue;

    [ObservableProperty]
    private string min = NoValue;

    [ObservableProperty]
    private string average = NoValue;

    [ObservableProperty]
    private string median = NoValue;

    [ObservableProperty]
    private string percentile95 = NoValue;

    public void Start()
    {
        recorder.Reset();
        recorder.Enabled = true;
        frameTimer.Start();
        Refresh();
    }

    public void Stop()
    {
        frameTimer.Stop();
        recorder.Enabled = false;
    }

    public void Refresh()
    {
        var statistics = ShowInput ? recorder.InputStatistics : recorder.OutputStatistics;
        recorder.History.FillSpeeds(recorder.NowMs - CurrentSpeedWindowMs, CurrentSpeedWindowMs, ShowInput, currentSpeed);

        Now = Format(currentSpeed[0]);
        Max = FormatStatistic(statistics, statistics.Max);
        Min = FormatStatistic(statistics, statistics.Min);
        Average = FormatStatistic(statistics, statistics.Average);
        Median = FormatStatistic(statistics, statistics.Median);
        Percentile95 = FormatStatistic(statistics, statistics.Percentile95);

        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    public double FillGraph(Span<double> bins)
    {
        double binMs = GraphWindowMs / bins.Length;
        double firstBin = Math.Floor(recorder.NowMs / binMs) - bins.Length;
        recorder.History.FillSpeeds(firstBin * binMs, binMs, ShowInput, bins);

        double peak = MinimumScale;
        foreach (var speed in bins)
        {
            peak = Math.Max(peak, speed);
        }

        return NiceCeiling(peak);
    }

    public static string Format(double speed) =>
        speed < 10 ? speed.ToString("0.00") : speed < 100 ? speed.ToString("0.0") : speed.ToString("0");

    partial void OnShowInputChanged(bool value) => Refresh();

    [RelayCommand]
    private void ResetStats()
    {
        recorder.Reset();
        Refresh();
    }

    private static string FormatStatistic(SpeedStatistics statistics, double speed) =>
        statistics.Count > 0 ? Format(speed) : NoValue;

    private static double NiceCeiling(double value)
    {
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));

        foreach (var step in ScaleSteps)
        {
            if (step * magnitude >= value)
            {
                return step * magnitude;
            }
        }

        return 10 * magnitude;
    }
}
