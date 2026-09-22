using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using grapher.Platform;
using grapher.Settings;
using grapher.Theming;
using grapher.ViewModels;
using grapher.Views;

namespace grapher;

public partial class App : Application
{
    private RawInputWindow? rawInput;
    private MainWindowViewModel? mainViewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                desktop.MainWindow = CreateMainWindow();
                desktop.Exit += (_, _) => Shutdown();
            }
            catch (InteropException e)
            {
                NativeDialogs.Show(e.Message, "Raw Accel");
                desktop.Shutdown(1);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CreateMainWindow()
    {
        var driverVersion = VersionHelper.ValidOrThrow();
        var paths = AppPaths.Default;
        var gui = GuiSettings.Load(paths.GuiSettingsFile);

        var themes = new ThemeService(new ThemeCatalog(paths.ThemesDirectory).Load());
        themes.Select(gui.CurrentColorScheme);

        var session = new DriverSession(new DriverAccess(), paths);
        var startupMessage = session.Load(gui.AutoWriteToDriverOnStartup);

        mainViewModel = new MainWindowViewModel(session, gui, paths, themes, driverVersion);
        mainViewModel.ShowStartupMessage(startupMessage);
        mainViewModel.SaveGuiSettings();
        _ = mainViewModel.WatchActivation(session.Activation);

        var window = new MainWindow(mainViewModel);

        rawInput = new RawInputWindow();
        rawInput.MouseMoved += mainViewModel.OnMouseMoved;
        rawInput.DevicesChanged += mainViewModel.OnDevicesChanged;
        mainViewModel.OnDevicesChanged();

        return window;
    }

    private void Shutdown()
    {
        rawInput?.Dispose();
        mainViewModel?.Dispose();
    }
}
