using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using grapher.Settings;
using grapher.Theming;
using grapher.ViewModels;

namespace grapher.Views;

public partial class MainWindow : Window
{
    private const string GuideUrl = "https://github.com/a1xd/rawaccel/blob/master/doc/Guide.md";
    private const string FaqUrl = "https://github.com/a1xd/rawaccel/blob/master/doc/FAQ.md";
    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 860;

    private MainWindowViewModel? viewModel;
    private bool restoreMaximized;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.DeviceMenuRequested += async (_, _) => await ShowDeviceMenu();
        viewModel.AboutRequested += async (_, _) => await new AboutWindow { DataContext = viewModel }.ShowDialog(this);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        BuildThemeMenu();
        RestorePlacement(viewModel.Gui.Window);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (restoreMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (viewModel is not null)
        {
            var placement = viewModel.Gui.Window ?? new WindowPlacement();
            placement.Maximized = WindowState == WindowState.Maximized;

            if (WindowState == WindowState.Normal)
            {
                placement.X = Position.X;
                placement.Y = Position.Y;
                placement.Width = Width;
                placement.Height = Height;
            }

            placement.OptionsWidth = MainGrid.ColumnDefinitions[0].ActualWidth;
            viewModel.Gui.Window = placement;
            viewModel.SaveGuiSettings();
        }

        base.OnClosing(e);
    }

    private void RestorePlacement(WindowPlacement? placement)
    {
        var workingArea = (Screens.ScreenFromWindow(this) ?? Screens.Primary)?.WorkingArea;
        double scaling = (Screens.ScreenFromWindow(this) ?? Screens.Primary)?.Scaling ?? 1;

        if (placement is not null && placement.Width > 0 && placement.Height > 0 && IsOnScreen(placement))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(placement.X, placement.Y);
            Width = placement.Width;
            Height = placement.Height;
            restoreMaximized = placement.Maximized;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            if (workingArea is PixelRect area)
            {
                Width = Math.Min(DefaultWidth, area.Width / scaling * 0.95);
                Height = Math.Min(DefaultHeight, area.Height / scaling * 0.95);
            }
        }

        if (placement is { OptionsWidth: > 300 })
        {
            MainGrid.ColumnDefinitions[0].Width = new GridLength(placement.OptionsWidth);
        }
    }

    private bool IsOnScreen(WindowPlacement placement)
    {
        const int Margin = 80;
        var titleBar = new PixelRect(placement.X + Margin, placement.Y, Math.Max(1, (int)placement.Width - Margin * 2), Margin);
        return Screens.All.Any(s => s.WorkingArea.Intersects(titleBar));
    }

    private void BuildThemeMenu()
    {
        if (viewModel is null)
        {
            return;
        }

        ThemeMenu.Items.Clear();

        foreach (var name in viewModel.ThemeNames)
        {
            var item = new MenuItem
            {
                Header = name == ThemeService.SystemName ? "Follow Windows" : name,
                ToggleType = MenuItemToggleType.Radio,
                GroupName = "Theme",
                IsChecked = name == viewModel.SelectedTheme,
                Tag = name,
            };
            item.Click += (_, _) => viewModel.SelectedTheme = name;
            ThemeMenu.Items.Add(item);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTheme) && viewModel is not null)
        {
            foreach (var item in ThemeMenu.Items.OfType<MenuItem>())
            {
                item.IsChecked = (string?)item.Tag == viewModel.SelectedTheme;
            }
        }
    }

    private async System.Threading.Tasks.Task ShowDeviceMenu()
    {
        if (viewModel is null)
        {
            return;
        }

        var menu = new DeviceMenuViewModel(viewModel.Session);
        EventHandler rebuild = (_, _) => menu.Rebuild();
        viewModel.DevicesChanged += rebuild;

        try
        {
            var dialog = new DeviceMenuWindow { DataContext = menu };
            bool apply = await dialog.ShowDialog<bool>(this);

            if (apply)
            {
                var (defaults, overrides) = menu.Collect();
                await viewModel.ApplyDevices(defaults, overrides);
            }
        }
        finally
        {
            viewModel.DevicesChanged -= rebuild;
        }
    }

    private async void OnGuideClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(GuideUrl));

    private async void OnFaqClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(FaqUrl));
}
