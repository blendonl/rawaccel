using grapher.Parameters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class ProfileComparerTests
{
    [TestMethod]
    public void CopiesAreEquivalent()
    {
        var profile = new Profile();
        profile.argsX.mode = AccelMode.synchronous;

        Assert.IsTrue(ProfileComparer.Equivalent(profile, ProfileCopy.Clone(profile)));
    }

    [TestMethod]
    public void ChangedValueIsDetected()
    {
        var profile = new Profile();
        var changed = ProfileCopy.Clone(profile);
        changed.rotation = 2;

        Assert.IsFalse(ProfileComparer.Equivalent(profile, changed));
    }

    [TestMethod]
    public void VerticalArgsAreIgnoredInWholeMode()
    {
        var profile = new Profile();
        var changed = ProfileCopy.Clone(profile);
        changed.argsY.mode = AccelMode.natural;

        Assert.IsTrue(ProfileComparer.Equivalent(profile, changed));

        profile.inputSpeedArgs.combineMagnitudes = false;
        changed.inputSpeedArgs.combineMagnitudes = false;

        Assert.IsFalse(ProfileComparer.Equivalent(profile, changed));
    }

    [TestMethod]
    public void TableDataOnlyMattersInLookupMode()
    {
        var profile = new Profile();
        var changed = ProfileCopy.Clone(profile);
        changed.argsX.data[0] = 3;

        Assert.IsTrue(ProfileComparer.Equivalent(profile, changed));

        profile.argsX.mode = AccelMode.lut;
        changed.argsX.mode = AccelMode.lut;
        profile.argsX.length = 2;
        changed.argsX.length = 2;

        Assert.IsFalse(ProfileComparer.Equivalent(profile, changed));
    }
}
