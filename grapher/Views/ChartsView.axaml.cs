using System;
using System.Collections.Generic;
using Avalonia.Controls;
using grapher.Charts;
using grapher.Theming;
using grapher.ViewModels;
using ScottPlot.Avalonia;
using Alignment = ScottPlot.Alignment;
using LinePattern = ScottPlot.LinePattern;
using Plot = ScottPlot.Plot;
using Scatter = ScottPlot.Plottables.Scatter;

namespace grapher.Views;

public partial class ChartsView : UserControl
{
    private const string InputAxisLabel = "Input speed (counts/ms)";

    private readonly ChartPanel[,] panels = new ChartPanel[3, 2];
    private MainWindowViewModel? viewModel;
    private ChartLayout shownLayout;
    private int shownRows;
    private int shownColumns;

    public ChartsView()
    {
        InitializeComponent();

        foreach (ChartKind kind in Enum.GetValues<ChartKind>())
        {
            for (int column = 0; column < 2; column++)
            {
                panels[(int)kind, column] = new ChartPanel(kind, column == 1);
            }
        }

        DataContextChanged += (_, _) => Attach(DataContext as MainWindowViewModel);
    }

    private enum ChartKind
    {
        Sensitivity,
        Velocity,
        Gain,
    }

    private void Attach(MainWindowViewModel? next)
    {
        if (viewModel is not null)
        {
            viewModel.ChartsChanged -= OnChartsChanged;
            viewModel.DotsChanged -= OnDotsChanged;
            viewModel.Themes.Changed -= OnChartsChanged;
        }

        viewModel = next;

        if (viewModel is not null)
        {
            viewModel.ChartsChanged += OnChartsChanged;
            viewModel.DotsChanged += OnDotsChanged;
            viewModel.Themes.Changed += OnChartsChanged;
            Redraw();
        }
    }

    private void OnChartsChanged(object? sender, EventArgs e) => Redraw();

    private void OnDotsChanged(object? sender, EventArgs e) => UpdateDots();

    private void Redraw()
    {
        if (viewModel is null)
        {
            return;
        }

        var preview = viewModel.PreviewCurves;
        var applied = viewModel.AppliedCurves;
        var shown = preview ?? applied;

        if (shown is null)
        {
            return;
        }

        bool overlay = preview is not null && applied is not null &&
            viewModel.HasUnappliedChanges && !preview.SameShapeAs(applied);
        var overlayCurves = overlay ? applied : null;

        shownLayout = shown.Layout;
        shownColumns = shownLayout == ChartLayout.ByComponent ? 2 : 1;
        shownRows = viewModel.ShowVelocityAndGain ? 3 : 1;
        Arrange();

        var scheme = viewModel.Themes.Current;

        for (int row = 0; row < shownRows; row++)
        {
            for (int column = 0; column < shownColumns; column++)
            {
                Draw(panels[row, column], shown, overlayCurves, scheme, viewModel.ChartMaxSpeed);
            }
        }

        UpdateDots();
    }

    private void Arrange()
    {
        ChartGrid.Children.Clear();
        ChartGrid.RowDefinitions.Clear();
        ChartGrid.ColumnDefinitions.Clear();

        for (int row = 0; row < shownRows; row++)
        {
            ChartGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        for (int column = 0; column < shownColumns; column++)
        {
            ChartGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int row = 0; row < shownRows; row++)
        {
            for (int column = 0; column < shownColumns; column++)
            {
                var control = panels[row, column].Control;
                Grid.SetRow(control, row);
                Grid.SetColumn(control, column);
                ChartGrid.Children.Add(control);
            }
        }
    }

    private void Draw(ChartPanel panel, CurveSet curves, CurveSet? applied, ColorScheme scheme, double maxSpeed)
    {
        var plot = panel.Control.Plot;
        plot.Clear();

        var background = ScottPlot.Color.FromSDColor(scheme.ChartBackground);
        var foreground = ScottPlot.Color.FromSDColor(scheme.ChartForeground);
        var primary = ScottPlot.Color.FromSDColor(scheme.Primary);
        var secondary = ScottPlot.Color.FromSDColor(scheme.Secondary);
        var dotColor = ScottPlot.Color.FromSDColor(scheme.MouseMovement);

        plot.FigureBackground.Color = background;
        plot.DataBackground.Color = background;
        plot.Axes.Color(foreground);
        plot.Grid.MajorLineColor = foreground.WithAlpha(0.12);
        plot.Grid.MinorLineColor = foreground.WithAlpha(0.05);
        plot.Legend.BackgroundColor = background.WithAlpha(0.85);
        plot.Legend.FontColor = foreground;
        plot.Legend.OutlineColor = foreground.WithAlpha(0.25);

        plot.Title(Title(panel), 16);
        plot.XLabel(InputAxisLabel, 13);
        plot.YLabel(YLabel(panel.Kind), 13);

        var series = new List<(Curve Curve, ScottPlot.Color Color, string Name)>();

        switch (curves.Layout)
        {
            case ChartLayout.Directional:
                series.Add((curves.First, primary, "Horizontal"));
                if (curves.Second is not null)
                {
                    series.Add((curves.Second, secondary, "Vertical"));
                }

                break;
            case ChartLayout.ByComponent:
                var componentCurve = panel.IsSecond && curves.Second is not null ? curves.Second : curves.First;
                series.Add((componentCurve, panel.IsSecond ? secondary : primary, string.Empty));
                break;
            default:
                series.Add((curves.First, primary, string.Empty));
                break;
        }

        bool legend = curves.Layout == ChartLayout.Directional;

        if (applied is not null && applied.Layout == curves.Layout)
        {
            var appliedCurves = curves.Layout switch
            {
                ChartLayout.Directional => new[] { applied.First, applied.Second },
                ChartLayout.ByComponent => new[] { panel.IsSecond ? applied.Second : applied.First },
                _ => new[] { applied.First },
            };

            for (int i = 0; i < appliedCurves.Length; i++)
            {
                if (appliedCurves[i] is not Curve old)
                {
                    continue;
                }

                var line = plot.Add.ScatterLine(old.Input, Values(old, panel.Kind), series[Math.Min(i, series.Count - 1)].Color.WithAlpha(0.45));
                line.LineWidth = 2;
                line.LinePattern = LinePattern.Dashed;
                line.LegendText = i == 0 ? "Applied" : string.Empty;
            }

            legend = true;
        }

        foreach (var (curve, color, name) in series)
        {
            var line = plot.Add.ScatterLine(curve.Input, Values(curve, panel.Kind), color);
            line.LineWidth = 3;
            line.LegendText = applied is not null && curves.Layout != ChartLayout.Directional ? "Preview" : name;
        }

        panel.DotA = AddDot(plot, dotColor);
        panel.DotB = curves.Layout == ChartLayout.Directional ? AddDot(plot, dotColor) : null;

        if (legend)
        {
            plot.ShowLegend(Alignment.UpperLeft);
        }
        else
        {
            plot.HideLegend();
        }

        SetLimits(plot, panel, curves, applied, maxSpeed);
        panel.Control.Refresh();
    }

    private static void SetLimits(Plot plot, ChartPanel panel, CurveSet curves, CurveSet? applied, double maxSpeed)
    {
        if (panel.Kind == ChartKind.Velocity)
        {
            plot.Axes.AutoScale();
            plot.Axes.SetLimitsX(0, maxSpeed);
            return;
        }

        var range = panel.Kind == ChartKind.Sensitivity ? curves.SensitivityRange(panel.IsSecond) : curves.GainRange(panel.IsSecond);

        if (applied is not null && applied.Layout == curves.Layout)
        {
            range = range.Union(panel.Kind == ChartKind.Sensitivity ? applied.SensitivityRange(panel.IsSecond) : applied.GainRange(panel.IsSecond));
        }

        plot.Axes.SetLimits(0, maxSpeed, range.Min, range.Max);
    }

    private static DotMarker AddDot(Plot plot, ScottPlot.Color color)
    {
        var xs = new double[1];
        var ys = new double[1];
        var scatter = plot.Add.ScatterPoints(xs, ys, color);
        scatter.MarkerSize = 10;
        scatter.IsVisible = false;
        return new DotMarker(scatter, xs, ys);
    }

    private void UpdateDots()
    {
        if (viewModel is null)
        {
            return;
        }

        var dots = viewModel.ShowLastMouseMove ? viewModel.LastDots : null;
        bool matches = viewModel.AppliedCurves?.Layout == shownLayout;

        for (int row = 0; row < shownRows; row++)
        {
            for (int column = 0; column < shownColumns; column++)
            {
                var panel = panels[row, column];

                if (shownLayout == ChartLayout.ByComponent)
                {
                    SetDot(panel.DotA, matches ? (column == 0 ? dots?.First : dots?.Second) : null, panel.Kind);
                }
                else
                {
                    SetDot(panel.DotA, matches ? dots?.First : null, panel.Kind);
                    SetDot(panel.DotB, matches ? dots?.Second : null, panel.Kind);
                }

                panel.Control.Refresh();
            }
        }
    }

    private static void SetDot(DotMarker? marker, Dot? dot, ChartKind kind)
    {
        if (marker is null)
        {
            return;
        }

        if (dot is not Dot value)
        {
            marker.Scatter.IsVisible = false;
            return;
        }

        marker.Xs[0] = value.Input;
        marker.Ys[0] = kind switch
        {
            ChartKind.Velocity => value.Velocity,
            ChartKind.Gain => value.Gain,
            _ => value.Sensitivity,
        };
        marker.Scatter.IsVisible = true;
    }

    private string Title(ChartPanel panel)
    {
        string name = panel.Kind.ToString();

        if (shownLayout == ChartLayout.ByComponent)
        {
            return panel.IsSecond ? $"{name}: vertical" : $"{name}: horizontal";
        }

        return name;
    }

    private static string YLabel(ChartKind kind) => kind switch
    {
        ChartKind.Velocity => "Output speed (counts/ms)",
        ChartKind.Gain => "Slope of velocity",
        _ => "Output / input ratio",
    };

    private static double[] Values(Curve curve, ChartKind kind) => kind switch
    {
        ChartKind.Velocity => curve.Velocity,
        ChartKind.Gain => curve.Gain,
        _ => curve.Sensitivity,
    };

    private sealed class ChartPanel
    {
        public ChartPanel(ChartKind kind, bool isSecond)
        {
            Kind = kind;
            IsSecond = isSecond;
            Control = new AvaPlot();
            Control.UserInputProcessor.DoubleLeftClickBenchmark(false);
        }

        public ChartKind Kind { get; }

        public bool IsSecond { get; }

        public AvaPlot Control { get; }

        public DotMarker? DotA { get; set; }

        public DotMarker? DotB { get; set; }
    }

    private sealed record DotMarker(Scatter Scatter, double[] Xs, double[] Ys);
}
