using System;
using System.Collections.Generic;
using System.IO;
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
