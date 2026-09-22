using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using grapher;
using grapher.Charts;
using grapher.Parameters;
using grapher.Settings;
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

        var confirm = new ProfileDialogWindow
        {
            DataContext = ProfileDialogViewModel.ForConfirmation(
                "Delete profile",
                "Delete \"fast\"? Mice assigned to it will use the default profile, \"default\".",
                "Delete"),
        };
        confirm.Show();
        Flush();
        Capture(confirm, "profile-delete-dialog");
        confirm.Close();
    });

    [TestMethod]
    public void ProfilesAreCreatedEditedAndSwitched() => Run("profiles", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        Assert.AreEqual(1, vm.Profiles.Count);
        Assert.AreEqual("default (default)", vm.SelectedProfile!.Label);
        Assert.IsFalse(vm.DeleteProfileCommand.CanExecute(null));
        Assert.IsFalse(vm.MakeDefaultProfileCommand.CanExecute(null));

        vm.NewProfileCommand.Execute(null);
        Flush();
        var dialog = window.OwnedWindows.OfType<ProfileDialogWindow>().Single();
        var request = (ProfileDialogViewModel)dialog.DataContext!;
        Assert.AreEqual("New profile", request.Name);
        Capture(dialog, "profile-new-dialog");
        request.Name = "DEFAULT";
        Assert.IsFalse(request.CanConfirm);
        request.Name = "fast";
        dialog.Close(true);
        Flush();

        CollectionAssert.AreEqual(new[] { "default", "fast" }, vm.Profiles.Select(p => p.Name).ToList());
        Assert.AreEqual("fast", vm.SelectedProfile!.Name);
        Assert.AreEqual("fast", vm.Session.ActiveProfile.name);
        StringAssert.Contains(vm.ProfileUsageText, "No mouse uses this profile yet");

        vm.Dpi.Text = "1600";
        vm.PollingRate.Text = "4000";
        Flush();

        Assert.IsTrue(vm.HasUnappliedChanges);
        Assert.IsFalse(vm.CanManageProfiles);
        Assert.AreEqual("1600", vm.ChartDpiText);
        Assert.AreEqual("4000", vm.ChartPollRateText);
        Capture(window, "profiles-unapplied");

        vm.ApplyCommand.Execute(null);
        Flush();

        Assert.AreEqual(1600, vm.Session.ActiveDeviceConfig.dpi);
        Assert.AreEqual(4000, vm.Session.ActiveDeviceConfig.pollingRate);
        Assert.IsFalse(vm.HasUnappliedChanges);

        vm.SelectedProfile = vm.Profiles.Single(p => p.Name == "default");
        Flush();

        Assert.AreEqual("default", vm.Session.UserProfile.name);
        Assert.AreEqual("0", vm.Dpi.Text);
        StringAssert.Contains(vm.ProfileUsageText, "every mouse");
    });

    [TestMethod]
    public void DeviceMenuAssignsProfiles() => Run("device-profiles", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.Session.AddProfile("fast", ActiveSynchronous(), new ProfileDeviceConfig { dpi = 1600 });
        vm.Session.UpdateSystemDevices(new[]
        {
            TestDevices.Connected("mouse-a", "Mouse A", 1),
            TestDevices.Connected("mouse-b", "Mouse B", 2),
        });
        Flush();

        var menu = new DeviceMenuViewModel(vm.Session);
        var mouse = menu.Devices.Single(d => d.Id == "mouse-a");
        CollectionAssert.AreEqual(new[] { "Default (default)", "default", "fast" }, mouse.Profiles.Select(p => p.Label).ToList());
        Assert.AreEqual("Profile: default", mouse.ListHint);

        menu.Selected = mouse;
        mouse.OverrideDefaults = true;
        mouse.SelectedProfile = mouse.Profiles.Single(p => p.Name == "fast");

        Assert.AreEqual("Profile: fast", mouse.ListHint);
        StringAssert.StartsWith(mouse.ProfileSummary, "DPI 1600, polling rate auto");

        var dialog = new DeviceMenuWindow { DataContext = menu };
        dialog.Show();
        Flush();
        Capture(dialog, "device-menu-profiles");
        dialog.Close();

        var (defaults, overrides) = menu.Collect();
        _ = vm.ApplyDevices(defaults, overrides);
        Flush();

        var saved = vm.Session.FindDeviceSettings("mouse-a")!;
        Assert.AreEqual("fast", saved.profile);
        Assert.AreEqual(1600, saved.config.dpi);
        Assert.IsNull(vm.Session.FindDeviceSettings("mouse-b"));
        CollectionAssert.AreEquivalent(new[] { (IntPtr)1 }, vm.Session.TrackedDevices.Keys.ToList());
        StringAssert.Contains(vm.ProfileUsageText, "Mouse A");
    });

    [TestMethod]
    public void InvalidDpiBlocksApply() => Run("invalid-dpi", BuiltInSchemes.LightName, ActiveSynchronous(), (window, vm) =>
    {
        vm.Dpi.Text = "800.5";
        Flush();

        Assert.AreEqual("Mouse DPI must be a whole number from 0 to 999999.", vm.ValidationMessage);
        Assert.IsFalse(vm.ApplyCommand.CanExecute(null));
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
