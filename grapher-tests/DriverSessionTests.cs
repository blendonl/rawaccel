using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using grapher.Parameters;
using grapher.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class DriverSessionTests
{
    private string directory = null!;
    private AppPaths paths = null!;
    private FakeDriver driver = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), "rawaccel-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        paths = new AppPaths(directory);
        driver = new FakeDriver();
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public void MissingSettingsFileIsCreatedFromDriverWithoutWriting()
    {
        var session = new DriverSession(driver, paths);

        var warning = session.Load(applyOnStartup: true);

        Assert.IsNull(warning);
        Assert.IsTrue(File.Exists(paths.SettingsFile));
        Assert.AreEqual(0, driver.Writes.Count);
        Assert.AreSame(session.ActiveConfig, session.UserConfig);
    }

    [TestMethod]
    public void ValidSettingsFileIsNotWrittenWhenAutoApplyIsOff()
    {
        WriteSettings(Custom("mine", 1.5));
        var session = new DriverSession(driver, paths);

        var warning = session.Load(applyOnStartup: false);

        Assert.IsNull(warning);
        Assert.AreEqual(0, driver.Writes.Count);
        Assert.AreEqual("mine", session.UserProfile.name);
        Assert.AreEqual("default", session.ActiveProfile.name);
    }

    [TestMethod]
    public async Task ValidSettingsFileIsWrittenWhenAutoApplyIsOn()
    {
        WriteSettings(Custom("mine", 1.5));
        var session = new DriverSession(driver, paths);

        session.Load(applyOnStartup: true);
        await session.Activation;

        Assert.AreEqual(1, driver.Writes.Count);
        Assert.AreEqual("mine", session.ActiveProfile.name);
    }

    [TestMethod]
    public void InvalidSettingsFileIsBackedUpAndReported()
    {
        var bad = Custom("mine", 1.5);
        bad.outputDPI = 0;
        WriteSettings(bad);
        var original = File.ReadAllText(paths.SettingsFile);
        var session = new DriverSession(driver, paths);

        var warning = session.Load(applyOnStartup: true);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "output DPI is 0");
        Assert.AreEqual(original, File.ReadAllText(paths.SettingsBackupFile));
        Assert.AreEqual(0, driver.Writes.Count);
    }

    [TestMethod]
    public async Task ApplyKeepsProfileNameAndHiddenOptions()
    {
        var user = Custom("mine", 1.5);
        user.snap = 5;
        user.maximumSpeed = 80;
        WriteSettings(user);
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);

        var draft = ProfileCopy.CreateDraft(session.UserProfile, session.UserProfile);
        draft.outputDPI = 2000;
        var result = session.Apply(draft);
        await result.Activation;

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, driver.Writes.Count);
        var (saved, errors) = DriverConfig.Convert(File.ReadAllText(paths.SettingsFile));
        Assert.IsNull(errors);
        Assert.AreEqual("mine", saved.profiles[0].name);
        Assert.AreEqual(5, saved.profiles[0].snap);
        Assert.AreEqual(80, saved.profiles[0].maximumSpeed);
        Assert.AreEqual(2000, saved.profiles[0].outputDPI);
    }

    [TestMethod]
    public void InvalidApplyIsRejectedAndLeavesSettingsUntouched()
    {
        WriteSettings(Custom("mine", 1.5));
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);
        var before = File.ReadAllText(paths.SettingsFile);

        var draft = ProfileCopy.Clone(session.UserProfile);
        draft.argsX.mode = AccelMode.synchronous;
        draft.argsX.motivity = 1;
        var result = session.Apply(draft);

        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(result.Errors, "motivity must be greater than 1");
        Assert.AreEqual(0, driver.Writes.Count);
        Assert.AreEqual(before, File.ReadAllText(paths.SettingsFile));
        Assert.AreEqual(1.5, session.UserProfile.outputDPI / 1000);
    }

    [TestMethod]
    public void DraftTakesAppliedCurveAndUserHiddenOptions()
    {
        var active = Custom("mine", 2);
        active.argsX.mode = AccelMode.natural;
        var user = Custom("mine", 1);
        user.snap = 3;
        user.lrOutputDPIRatio = 0.8;
        user.inputSpeedArgs.outputSmoothHalflife = 4;

        var draft = ProfileCopy.CreateDraft(active, user);

        Assert.AreEqual(2000, draft.outputDPI);
        Assert.AreEqual(AccelMode.natural, draft.argsX.mode);
        Assert.AreEqual(3, draft.snap);
        Assert.AreEqual(0.8, draft.lrOutputDPIRatio);
        Assert.AreEqual(4, draft.inputSpeedArgs.outputSmoothHalflife);
    }

    [TestMethod]
    public async Task ResetClearsDriverWithoutChangingSettingsFile()
    {
        WriteSettings(Custom("mine", 1.5));
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);
        var before = File.ReadAllText(paths.SettingsFile);

        await session.Reset();

        Assert.AreEqual(1, driver.Resets);
        Assert.AreEqual(before, File.ReadAllText(paths.SettingsFile));
        Assert.AreEqual(1000, session.ActiveProfile.outputDPI);
    }

    [TestMethod]
    public void SelectingProfileSwitchesUserAndActiveProfile()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        int changes = 0;
        session.ActiveChanged += (_, _) => changes++;

        session.SelectProfile("fast");

        Assert.AreEqual("fast", session.UserProfile.name);
        Assert.AreEqual("fast", session.ActiveProfile.name);
        Assert.AreEqual(1600, session.UserDeviceConfig.dpi);
        Assert.IsFalse(session.IsDefaultSelected);
        Assert.AreEqual(1, changes);
    }

    [TestMethod]
    public async Task ApplyEditsOnlySelectedProfileAndKeepsItsName()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        session.SelectProfile("fast");

        var draft = ProfileCopy.Clone(session.UserProfile);
        draft.name = "something else";
        draft.outputDPI = 3000;
        var result = session.Apply(draft, new ProfileDeviceConfig { dpi = 3200, pollingRate = 4000 });
        await result.Activation;

        Assert.IsTrue(result.Succeeded);
        var saved = LoadSaved();
        Assert.AreEqual("slow", saved.profiles[0].name);
        Assert.AreEqual(1000, saved.profiles[0].outputDPI);
        Assert.AreEqual("fast", saved.profiles[1].name);
        Assert.AreEqual(3000, saved.profiles[1].outputDPI);
        Assert.AreEqual(3200, saved.profileDeviceConfigs["fast"].dpi);
        Assert.AreEqual(4000, saved.profileDeviceConfigs["fast"].pollingRate);
        Assert.AreEqual("fast", session.ActiveProfile.name);
    }

    [TestMethod]
    public void ProfileDpiAndPollingRateAreWrittenToDevicesUsingIt()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        session.SelectProfile("fast");

        session.Apply(ProfileCopy.Clone(session.UserProfile), new ProfileDeviceConfig { dpi = 3200, pollingRate = 4000 });

        var saved = LoadSaved();
        var device = saved.devices.Single(d => d.id == "fast-mouse");
        Assert.AreEqual(3200, device.config.dpi);
        Assert.AreEqual(4000, device.config.pollingRate);
        Assert.AreEqual(800, saved.defaultDeviceConfig.dpi);
        Assert.AreEqual(0, saved.defaultDeviceConfig.pollingRate);
    }

    [TestMethod]
    public async Task AddProfileCopiesSourceSelectsItAndWritesDriver()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);
        var source = Custom("ignored", 2.5);

        var result = session.AddProfile("  travel ", source, new ProfileDeviceConfig { dpi = 400, pollingRate = 500 });
        await result.Activation;

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(new[] { "slow", "fast", "travel" }, session.ProfileNames.ToList());
        Assert.AreEqual("travel", session.UserProfile.name);
        Assert.AreEqual(2500, session.ActiveProfile.outputDPI);
        Assert.AreEqual(400, session.ActiveDeviceConfig.dpi);
        Assert.AreEqual(1, driver.Writes.Count);
        Assert.AreEqual(3, driver.Writes[0].accels.Count);
    }

    [TestMethod]
    public void AddProfileRejectsTakenName()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);

        var result = session.AddProfile("FAST", new Profile(), session.UserDeviceConfig);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Another profile already has this name.", result.Errors);
        Assert.AreEqual(2, session.ProfileNames.Count);
        Assert.AreEqual(0, driver.Writes.Count);
    }

    [TestMethod]
    public void ProfileNamesAreValidated()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: false);

        Assert.AreEqual("Enter a name.", session.ValidateProfileName("   "));
        Assert.AreEqual("Another profile already has this name.", session.ValidateProfileName("Slow"));
        Assert.IsNull(session.ValidateProfileName("Slow", currentName: "slow"));
        Assert.IsNotNull(session.ValidateProfileName(new string('x', DriverSession.MaxProfileNameLength + 1)));
        Assert.IsNull(session.ValidateProfileName(new string('x', DriverSession.MaxProfileNameLength)));
    }

    [TestMethod]
    public async Task RenameProfileMovesDeviceAssignmentsAndDpi()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        await session.Activation;
        session.SelectProfile("fast");

        var result = session.RenameProfile("gaming");
        await result.Activation;

        Assert.IsTrue(result.Succeeded);
        var saved = LoadSaved();
        Assert.AreEqual("gaming", saved.profiles[1].name);
        Assert.AreEqual("gaming", saved.devices.Single(d => d.id == "fast-mouse").profile);
        Assert.AreEqual(1600, saved.profileDeviceConfigs["gaming"].dpi);
        Assert.IsFalse(saved.profileDeviceConfigs.ContainsKey("fast"));
        Assert.AreEqual("gaming", session.ActiveProfile.name);
        Assert.AreEqual("gaming", driver.Writes.Last().accels[1].Settings.name);
    }

    [TestMethod]
    public void DeleteProfileSendsItsDevicesToTheDefault()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        session.SelectProfile("fast");

        var result = session.DeleteProfile();

        Assert.IsTrue(result.Succeeded);
        var saved = LoadSaved();
        Assert.AreEqual(1, saved.profiles.Count);
        Assert.IsFalse(saved.profileDeviceConfigs.ContainsKey("fast"));
        var device = saved.devices.Single(d => d.id == "fast-mouse");
        Assert.AreEqual(string.Empty, device.profile);
        Assert.AreEqual(800, device.config.dpi);
        Assert.AreEqual("slow", session.UserProfile.name);

        var last = session.DeleteProfile();

        Assert.IsFalse(last.Succeeded);
        Assert.AreEqual(1, session.ProfileNames.Count);
    }

    [TestMethod]
    public async Task MakeDefaultMovesProfileFirstAndAppliesItsDpiToUnassignedDevices()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        await session.Activation;
        session.SelectProfile("fast");

        var result = session.MakeDefaultProfile();
        await result.Activation;

        Assert.IsTrue(result.Succeeded);
        var saved = LoadSaved();
        CollectionAssert.AreEqual(new[] { "fast", "slow" }, saved.profiles.Select(p => p.name).ToList());
        Assert.AreEqual(1600, saved.defaultDeviceConfig.dpi);
        Assert.AreEqual("fast", driver.Writes.Last().accels[0].Settings.name);
        Assert.IsTrue(session.IsDefaultSelected);
        Assert.AreEqual("fast", session.DefaultProfileName);
    }

    [TestMethod]
    public void OldSettingsDeriveProfileDpiAndReportDevicesThatWillChange()
    {
        var config = TwoProfiles();
        config.profileDeviceConfigs.Clear();
        config.devices.Add(Device("own-dpi", "Own DPI Mouse", string.Empty, dpi: 400));
        WriteSettings(config);
        var session = new DriverSession(driver, paths);

        var warning = session.Load(applyOnStartup: false);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "the next time settings are applied");
        StringAssert.Contains(warning, "Own DPI Mouse (profile \"slow\"): DPI 400, polling rate auto → DPI 800, polling rate auto");
        Assert.IsFalse(warning.Contains("Fast Mouse"));
        Assert.AreEqual(800, session.UserDeviceConfig.dpi);
        session.SelectProfile("fast");
        Assert.AreEqual(1600, session.UserDeviceConfig.dpi);
    }

    [TestMethod]
    public void OldSettingsAppliedOnStartupMoveDevicesToTheirProfileDpi()
    {
        var config = TwoProfiles();
        config.profileDeviceConfigs.Clear();
        config.devices.Add(Device("own-dpi", "Own DPI Mouse", string.Empty, dpi: 400));
        WriteSettings(config);
        var session = new DriverSession(driver, paths);

        var warning = session.Load(applyOnStartup: true);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "now use their profile's");
        StringAssert.Contains(warning, "Own DPI Mouse (profile \"slow\")");
        var saved = LoadSaved();
        Assert.AreEqual(800, saved.devices.Single(d => d.id == "own-dpi").config.dpi);
        Assert.AreEqual(1600, saved.profileDeviceConfigs["fast"].dpi);
    }

    [TestMethod]
    public void SingleProfileSettingsFollowTheDefaultInsteadOfNamingIt()
    {
        var config = DriverConfig.FromProfile(Custom("only", 1));
        config.devices.Add(Device("mouse", "Mouse", "only", dpi: 0));
        WriteSettings(config);
        var session = new DriverSession(driver, paths);

        session.Load(applyOnStartup: false);

        Assert.AreEqual(string.Empty, session.FindDeviceSettings("mouse")!.profile);
    }

    [TestMethod]
    public void TrackedDevicesFollowTheSelectedProfile()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        var fast = TestDevices.Connected("fast-mouse", "Fast Mouse", 1);
        var other = TestDevices.Connected("other-mouse", "Other Mouse", 2);
        session.UpdateSystemDevices(new[] { fast, other });

        CollectionAssert.AreEquivalent(new[] { (IntPtr)2 }, session.TrackedDevices.Keys.ToList());
        Assert.IsTrue(session.TrackedDevices[(IntPtr)2]);

        session.SelectProfile("fast");

        CollectionAssert.AreEquivalent(new[] { (IntPtr)1 }, session.TrackedDevices.Keys.ToList());
    }

    [TestMethod]
    public void ApplyDevicesStoresEachDevicesProfile()
    {
        WriteSettings(TwoProfiles());
        var session = new DriverSession(driver, paths);
        session.Load(applyOnStartup: true);
        var defaults = session.UserConfig.defaultDeviceConfig;

        var result = session.ApplyDevices(
            defaults,
            new[]
            {
                new DeviceOverride("fast-mouse", "Fast Mouse", false, "fast", defaults),
                new DeviceOverride("new-mouse", "New Mouse", true, "fast", defaults),
            },
            ProfileCopy.Clone(session.UserProfile),
            session.UserDeviceConfig);

        Assert.IsTrue(result.Succeeded);
        var saved = LoadSaved();
        Assert.IsNull(saved.devices.Find(d => d.id == "fast-mouse"));
        var added = saved.devices.Single(d => d.id == "new-mouse");
        Assert.AreEqual("fast", added.profile);
        Assert.AreEqual(1600, added.config.dpi);
    }

    private static DriverConfig TwoProfiles()
    {
        var config = DriverConfig.FromProfile(Custom("slow", 1));
        var fast = Custom("fast", 2);
        config.profiles.Add(fast);
        config.accels.Add(new ManagedAccel(fast));
        config.defaultDeviceConfig.dpi = 800;
        config.devices.Add(Device("fast-mouse", "Fast Mouse", "fast", dpi: 1600));
        config.profileDeviceConfigs["slow"] = new ProfileDeviceConfig { dpi = 800 };
        config.profileDeviceConfigs["fast"] = new ProfileDeviceConfig { dpi = 1600 };
        return config;
    }

    private static DeviceSettings Device(string id, string name, string profile, int dpi)
    {
        var device = new DeviceSettings { id = id, name = name, profile = profile };
        device.config.dpi = dpi;
        return device;
    }

    private DriverConfig LoadSaved()
    {
        var (saved, errors) = DriverConfig.Convert(File.ReadAllText(paths.SettingsFile));
        Assert.IsNull(errors);
        return saved;
    }

    private void WriteSettings(DriverConfig config) =>
        File.WriteAllText(paths.SettingsFile, config.ToJSON());

    private static Profile Custom(string name, double sensitivity)
    {
        var profile = new Profile();
        profile.name = name;
        profile.outputDPI = sensitivity * 1000;
        return profile;
    }

    private void WriteSettings(Profile profile) =>
        File.WriteAllText(paths.SettingsFile, DriverConfig.FromProfile(profile).ToJSON());

    private sealed class FakeDriver : IDriverAccess
    {
        public List<DriverConfig> Writes { get; } = new();

        public int Resets { get; private set; }

        public DriverConfig ReadActive() => DriverConfig.GetDefault();

        public void Write(DriverConfig config)
        {
            lock (Writes)
            {
                Writes.Add(config);
            }
        }

        public void Reset() => Resets++;
    }
}
