using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using grapher.Theming;
using grapher.ViewModels;

namespace grapher.Views;

public partial class SpeedOverlayWindow : Window
{
    private readonly SpeedOverlayViewModel? viewModel;
    private readonly ThemeService? themes;

    public SpeedOverlayWindow()
    {
        InitializeComponent();
    }

    public SpeedOverlayWindow(SpeedOverlayViewModel viewModel, ThemeService themes)
        : this()
    {
        this.viewModel = viewModel;
        this.themes = themes;
        DataContext = viewModel;
        Graph.Source = viewModel;
        viewModel.Refreshed += OnRefreshed;
        themes.Changed += OnThemeChanged;
        ApplyScheme(themes.Current);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        viewModel?.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.Stop();
            viewModel.Refreshed -= OnRefreshed;
        }

        if (themes is not null)
        {
            themes.Changed -= OnThemeChanged;
        }

        base.OnClosed(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnRefreshed(object? sender, EventArgs e) => Graph.InvalidateVisual();

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (themes is not null)
        {
            ApplyScheme(themes.Current);
        }
    }

    private void ApplyScheme(ColorScheme scheme)
    {
        var background = ThemeService.ToAvalonia(scheme.ChartBackground);
        var foreground = ThemeService.ToAvalonia(scheme.ChartForeground);

        Background = new SolidColorBrush(background);
        Foreground = new SolidColorBrush(foreground);
        Frame.BorderBrush = new SolidColorBrush(foreground, 0.2);
        Graph.SetColors(foreground, ThemeService.ToAvalonia(scheme.MouseMovement));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
