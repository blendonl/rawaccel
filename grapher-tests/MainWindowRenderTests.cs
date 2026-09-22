using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using grapher;
using grapher.Charts;
using grapher.Parameters;
using grapher.Settings;
using grapher.Speed;
using grapher.Theming;
using grapher.ViewModels;
using grapher.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[TestClass]
public class MainWindowRenderTests
{
    private static HeadlessUnitTestSession session = null!;
    private static string? screenshotDirectory;

    [ClassInitialize]
    public static void StartSession(TestContext context)
    {
        session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
        screenshotDirectory = Environment.GetEnvironmentVariable("RAWACCEL_SCREENSHOTS");

        if (screenshotDirectory is not null)
        {
            Directory.CreateDirectory(screenshotDirectory);
        }
    }

    [ClassCleanup]
    public static void StopSession() => session.Dispose();

    [TestMethod]
    public void ShowsAppliedSynchronousCurve() => Run("synchronous-light", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        Assert.AreEqual(CurveType.Synchronous, vm.EditorX.SelectedCurve.Type);
        Assert.IsNull(vm.ValidationMessage);
        Assert.IsFalse(vm.HasUnappliedChanges);
        Assert.AreEqual(ChartLayout.Combined, vm.PreviewCurves!.Layout);
        CollectionAssert.AreEqual(
            new[] { "Gain", "Sync speed", "Motivity", "Gamma", "Smooth" },
            vm.EditorX.Rows.Select(r => r.Label).ToList());
    });

    [TestMethod]
    public void RendersDarkThemeWithVelocityAndGain() => Run("synchronous-dark-velocity-gain", BuiltInSchemes.DarkName, ActiveSynchronous(), (window, vm) =>
    {
        vm.ShowVelocityAndGain = true;
        Flush();
        Assert.IsTrue(vm.Themes.Current.IsDark);
    });

    [TestMethod]
    public void EditingPreviewsWithoutApplying() => Run("classic-preview", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.EditorX.SelectedCurve = ParameterCatalog.For(CurveType.Classic);
        Flush();
        var capType = vm.EditorX.Rows.OfType<ChoiceRowViewModel>().Single();
        capType.Selected = capType.Options.Single(o => o.Label == "Both");
        Flush();

        Assert.IsTrue(vm.HasUnappliedChanges);
        Assert.IsNull(vm.ValidationMessage);
        Assert.IsFalse(vm.EditorX.Rows.Single(r => r.Label == "Acceleration").IsVisible);
        Assert.IsTrue(vm.EditorX.Rows.Single(r => r.Label == "Cap input").IsVisible);
        Assert.IsNotNull(vm.PreviewCurves);
    });

    [TestMethod]
    public void InvalidInputBlocksApply() => Run("invalid-motivity", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        var motivity = vm.EditorX.Rows.OfType<NumberRowViewModel>().Single(r => r.Label == "Motivity");
        motivity.Text = "0.5";
        Flush();

        Assert.AreEqual("motivity must be greater than 1", vm.ValidationMessage);
        Assert.IsFalse(vm.ApplyCommand.CanExecute(null));
        Assert.IsNull(vm.PreviewCurves);

        motivity.Text = "abc";
        Flush();

        Assert.IsTrue(motivity.HasError);
        Assert.AreEqual("Some fields don't contain a valid number.", vm.ValidationMessage);
    });

    [TestMethod]
    public void ByComponentShowsTwoEditorsAndCharts() => Run("by-component", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.IsByComponent = true;
        vm.LockXY = false;
        Flush();
        vm.EditorY.SelectedCurve = ParameterCatalog.For(CurveType.Natural);
        Flush();

        Assert.IsTrue(vm.ShowEditorY);
        Assert.IsFalse(vm.LpNorm.IsEnabled);
        Assert.AreEqual(ChartLayout.ByComponent, vm.PreviewCurves!.Layout);
    });

    [TestMethod]
    public void AnisotropyUsesDirectionalCharts() => Run("anisotropy", BuiltInSchemes.DarkName, ActiveSynchronous(), (window, vm) =>
    {
        vm.Range.Y.Text = "0.5";
        Flush();

        Assert.AreEqual(ChartLayout.Directional, vm.PreviewCurves!.Layout);
        Assert.IsTrue(vm.Range.IsEdited);
    });

    [TestMethod]
    public void LookupTableShowsParseErrors() => Run("lookup-table", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.EditorX.SelectedCurve = ParameterCatalog.For(CurveType.LookupTable);
        Flush();
        var points = vm.EditorX.Rows.OfType<TextRowViewModel>().Single();
        points.Text = "1.505,0.855;4.375,3.31;13.51,15.17;140,354.7;";
        Flush();

        Assert.IsNull(vm.ValidationMessage);

        points.Text = "3,1;2,1";
        Flush();

        StringAssert.StartsWith(vm.ValidationMessage, "X-value for point at index 1");
    });

    [TestMethod]
    public void ApplyWritesDriverAndSettingsFile() => Run("after-apply", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.Sensitivity.Text = "1.5";
        Flush();
        Assert.IsTrue(vm.HasUnappliedChanges);

        vm.ApplyCommand.Execute(null);
        Flush();

        Assert.AreEqual(1500, vm.Session.ActiveProfile.outputDPI);
        Assert.IsFalse(vm.HasUnappliedChanges);
        Assert.AreEqual("1.5", vm.Sensitivity.ActiveText);
    });

    [TestMethod]
    public void EveryCurveRendersItsRows() => Run("every-curve", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        foreach (var curve in ParameterCatalog.Curves)
        {
            vm.EditorX.SelectedCurve = curve;
            Flush();

            Assert.AreEqual(curve.Rows.Count, vm.EditorX.Rows.Count, curve.Name);

            if (curve.Type != CurveType.LookupTable)
            {
                Assert.IsNull(vm.ValidationMessage, curve.Name);
                Assert.IsNotNull(vm.PreviewCurves, curve.Name);
            }

            Capture(window, "curve-" + curve.Type.ToString().ToLowerInvariant());
        }
    });

    [TestMethod]
    public void RendersDialogs() => Run("dialogs", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        var devices = new DeviceMenuWindow { DataContext = new DeviceMenuViewModel(vm.Session) };
        devices.Show();
        Flush();
        Capture(devices, "device-menu");
        devices.Close();

        var about = new AboutWindow { DataContext = vm };
        about.Show();
        Flush();
        Capture(about, "about");
        about.Close();
    });

    [TestMethod]
    public void OpensAnisotropyWhenItIsInUse()
    {
        var profile = ActiveSynchronous();
        profile.domainXY.y = 2;

        Run("anisotropy-open", BuiltInSchemes.LightName, profile, (window, vm) =>
        {
            Assert.IsTrue(vm.IsAnisotropyExpanded);
            Assert.AreEqual(ChartLayout.Directional, vm.PreviewCurves!.Layout);
        });
    }

    [TestMethod]
    public void SpeedOverlayShowsRecordedSpeeds() => Run("speed-overlay-main", BuiltInSchemes.DarkName, ActiveSynchronous(), (window, vm) =>
    {
        vm.ShowSpeedOverlay = true;
        Flush();

        var overlay = window.SpeedOverlay;
        Assert.IsNotNull(overlay);
        Assert.IsTrue(vm.SpeedRecorder.Enabled);

        var overlayViewModel = (SpeedOverlayViewModel)overlay.DataContext!;
        Assert.AreEqual("-", overlayViewModel.Max);

        FeedSpeeds(vm.SpeedRecorder);
        overlayViewModel.Refresh();
        Assert.AreEqual("24.0", overlayViewModel.Max);
        Assert.AreEqual("Input speed (counts/ms)", overlayViewModel.Title);
        Capture(overlay, "speed-overlay-dark");

        vm.SelectedTheme = BuiltInSchemes.LightName;
        Capture(overlay, "speed-overlay-light");

        overlayViewModel.ShowOutput = true;
        Assert.AreEqual("Output speed (counts/ms)", overlayViewModel.Title);
        Assert.AreEqual("33.6", overlayViewModel.Max);
        Capture(overlay, "speed-overlay-output");

        overlayViewModel.ResetStatsCommand.Execute(null);
        Assert.AreEqual("-", overlayViewModel.Max);

        overlay.Close();
        Flush();

        Assert.IsFalse(vm.ShowSpeedOverlay);
        Assert.IsNull(window.SpeedOverlay);
        Assert.IsFalse(vm.SpeedRecorder.Enabled);
    });

    [TestMethod]
    public void UncheckingTheMenuClosesTheSpeedOverlay() => Run("speed-overlay-toggle", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.ShowSpeedOverlay = true;
        Flush();
        var overlay = window.SpeedOverlay!;
        overlay.Position = new PixelPoint(40, 60);

        vm.ShowSpeedOverlay = false;
        Flush();

        Assert.IsNull(window.SpeedOverlay);
        Assert.IsFalse(overlay.IsVisible);

        vm.ShowSpeedOverlay = true;
        Flush();

        Assert.AreEqual(new PixelPoint(40, 60), window.SpeedOverlay!.Position);
    });

    [TestMethod]
    public void MenuTogglesUpdateTheViewModel() => Run("menu-toggles", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        ClickMenuItem(window, "Show speed overlay");
        Assert.IsTrue(vm.ShowSpeedOverlay);
        Assert.IsNotNull(window.SpeedOverlay);

        var overlay = window.SpeedOverlay;
        var overlayViewModel = (SpeedOverlayViewModel)overlay.DataContext!;
        var contextMenu = overlay.ContextMenu!;
        contextMenu.Open(overlay);
        Flush();
        ClickMenuItem(contextMenu, "Output speed");
        Assert.IsFalse(overlayViewModel.ShowInput);
        contextMenu.Open(overlay);
        Flush();
        ClickMenuItem(contextMenu, "Input speed");
        Assert.IsTrue(overlayViewModel.ShowInput);
        contextMenu.Close();

        ClickMenuItem(window, "Show speed overlay");
        Assert.IsFalse(vm.ShowSpeedOverlay);
        Assert.IsNull(window.SpeedOverlay);

        bool velocityAndGain = vm.ShowVelocityAndGain;
        ClickMenuItem(window, "Show velocity and gain");
        Assert.AreNotEqual(velocityAndGain, vm.ShowVelocityAndGain);

        bool lastMouseMove = vm.ShowLastMouseMove;
        ClickMenuItem(window, "Show last mouse move");
        Assert.AreNotEqual(lastMouseMove, vm.ShowLastMouseMove);

        bool autoApply = vm.AutoApplyOnStartup;
        ClickMenuItem(window, "Apply settings.json on startup");
        Assert.AreNotEqual(autoApply, vm.AutoApplyOnStartup);
    });

    private static void ClickMenuItem(ILogical root, string header)
    {
        var item = root.GetLogicalDescendants().OfType<MenuItem>().Single(m => Equals(m.Header, header));

        if (item.ToggleType == MenuItemToggleType.CheckBox)
        {
            item.SetCurrentValue(MenuItem.IsCheckedProperty, !item.IsChecked);
        }
        else if (item.ToggleType == MenuItemToggleType.Radio && !item.IsChecked)
        {
            item.SetCurrentValue(MenuItem.IsCheckedProperty, true);
        }

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Flush();
    }

    private static void FeedSpeeds(SpeedRecorder recorder)
    {
        double now = recorder.NowMs;

        for (double time = now - 5200; time < now - 20; time += 1)
        {
            double phase = (time - now + 350) / 700;
            double speed = Math.Max(0, 18 * Math.Sin(phase * Math.PI)) + Math.Max(0, 6 * Math.Sin(phase * 3.1));

            if (speed > 0.05)
            {
                recorder.History.Add(time, speed, speed * 1.4);
                recorder.InputStatistics.Add(speed);
                recorder.OutputStatistics.Add(speed * 1.4);
            }
        }
    }

    private static Profile ActiveSynchronous()
    {
        var profile = new Profile();
        profile.name = "default";
        profile.outputDPI = 1200;
        profile.argsX.mode = AccelMode.synchronous;
        profile.argsX.syncSpeed = 12;
        profile.argsX.motivity = 1.6;
        profile.argsY = ProfileCopy.Clone(profile.argsX);
        return profile;
    }

    private static void Run(string name, string theme, Profile active, Action<MainWindow, MainWindowViewModel> test)
    {
        session.Dispatch(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "rawaccel-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                var paths = new AppPaths(directory);
                var driver = new FixedDriver(active);
                var driverSession = new DriverSession(driver, paths);
                driverSession.Load(applyOnStartup: false);

                var themes = new ThemeService(new[] { BuiltInSchemes.Light, BuiltInSchemes.Dark });
                themes.Select(theme);

                var gui = new GuiSettings { CurrentColorScheme = theme };
                var vm = new MainWindowViewModel(driverSession, gui, paths, themes, new Version(1, 7, 0));
                var window = new MainWindow(vm) { Width = 1280, Height = 860 };
                window.Show();
                Flush();

                test(window, vm);
                Flush();
                Capture(window, name);

                window.Close();
                vm.Dispose();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void Flush()
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Capture(Window window, string name)
    {
        Flush();
        var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        Assert.IsTrue(frame.PixelSize.Width > 0 && frame.PixelSize.Height > 0);

        if (screenshotDirectory is not null)
        {
            frame.Save(Path.Combine(screenshotDirectory, name + ".png"), PngBitmapEncoderOptions.Default);
        }
    }

    private sealed class FixedDriver : IDriverAccess
    {
        private readonly Profile active;

        public FixedDriver(Profile active)
        {
            this.active = active;
        }

        public DriverConfig ReadActive() => DriverConfig.FromProfile(ProfileCopy.Clone(active));

        public void Write(DriverConfig config)
        {
        }

        public void Reset()
        {
        }
    }
}
