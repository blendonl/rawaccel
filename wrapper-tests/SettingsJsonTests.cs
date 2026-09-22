using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace wrapper_tests
{
    [TestClass]
    public class SettingsJsonTests
    {
        [TestMethod]
        public void DefaultConfig_RoundTripsThroughJson()
        {
            var json = DriverConfig.GetDefault().ToJSON();

            var (config, errors) = DriverConfig.Convert(json);

            Assert.IsNull(errors);
            Assert.IsNotNull(config);
            Assert.AreEqual(1, config.profiles.Count);
            Assert.AreEqual(config.profiles.Count, config.accels.Count);
        }

        [TestMethod]
        public void ModifiedProfile_RoundTripsThroughJson()
        {
            var profile = new Profile();
            profile.name = "test";
            profile.outputDPI = 1600;
            profile.rotation = 3;
            profile.argsX.mode = AccelMode.synchronous;
            profile.argsX.syncSpeed = 12;
            profile.argsX.motivity = 1.7;
            profile.argsY.mode = AccelMode.classic;
            profile.argsY.exponentClassic = 2.5;
            profile.inputSpeedArgs.combineMagnitudes = false;

            var json = DriverConfig.FromProfile(profile).ToJSON();
            var (config, errors) = DriverConfig.Convert(json);

            Assert.IsNull(errors);
            var loaded = config.profiles[0];
            Assert.AreEqual("test", loaded.name);
            Assert.AreEqual(1600, loaded.outputDPI);
            Assert.AreEqual(3, loaded.rotation);
            Assert.AreEqual(AccelMode.synchronous, loaded.argsX.mode);
            Assert.AreEqual(12, loaded.argsX.syncSpeed);
            Assert.AreEqual(1.7, loaded.argsX.motivity);
            Assert.AreEqual(AccelMode.classic, loaded.argsY.mode);
            Assert.AreEqual(2.5, loaded.argsY.exponentClassic);
            Assert.IsFalse(loaded.inputSpeedArgs.combineMagnitudes);
        }

        [TestMethod]
        public void InvalidProfile_ReportsErrors()
        {
            var profile = new Profile();
            profile.outputDPI = 0;

            var json = DriverConfig.FromProfile(profile).ToJSON();
            var (config, errors) = DriverConfig.Convert(json);

            Assert.IsNull(config);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errors));
        }

        [TestMethod]
        public void EquivalentPowerArgs_AreEquivalent()
        {
            var profile = new Profile();
            profile.argsX.mode = AccelMode.power;
            profile.argsX.capMode = CapMode.output;
            profile.argsX.scale = 2;
            profile.argsX.acceleration = 5;
            profile.argsY = profile.argsX;
            profile.argsY.acceleration = 7;

            Assert.IsTrue(profile.argsX.IsEquivalentTo(profile.argsY));

            profile.argsY.scale = 3;

            Assert.IsFalse(profile.argsX.IsEquivalentTo(profile.argsY));
        }

        [TestMethod]
        public void NoAccelArgs_AreEquivalentRegardlessOfOtherFields()
        {
            var profile = new Profile();
            profile.argsX.mode = AccelMode.noaccel;
            profile.argsY = profile.argsX;
            profile.argsY.syncSpeed = profile.argsX.syncSpeed + 1;

            Assert.IsTrue(profile.argsX.IsEquivalentTo(profile.argsY));
        }
    }
}
