using grapher.Parameters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class ProfileCopyTests
{
    [TestMethod]
    public void CopiesEveryField()
    {
        var source = new Profile();
        source.name = "mine";
        source.outputDPI = 1600;
        source.yxOutputDPIRatio = 1.2;
        source.lrOutputDPIRatio = 0.9;
        source.udOutputDPIRatio = 1.1;
        source.rotation = 4;
        source.snap = 3;
        source.maximumSpeed = 90;
        source.domainXY.y = 2;
        source.rangeXY.x = 0.5;
        source.inputSpeedArgs.combineMagnitudes = false;
        source.inputSpeedArgs.outputSmoothHalflife = 7;
        source.argsX.mode = AccelMode.synchronous;
        source.argsY.mode = AccelMode.jump;

        var copy = ProfileCopy.Clone(source);

        Assert.AreNotSame(source, copy);
        Assert.AreEqual("mine", copy.name);
        Assert.AreEqual(1600, copy.outputDPI);
        Assert.AreEqual(1.2, copy.yxOutputDPIRatio);
        Assert.AreEqual(0.9, copy.lrOutputDPIRatio);
        Assert.AreEqual(1.1, copy.udOutputDPIRatio);
        Assert.AreEqual(4, copy.rotation);
        Assert.AreEqual(3, copy.snap);
        Assert.AreEqual(90, copy.maximumSpeed);
        Assert.AreEqual(2, copy.domainXY.y);
        Assert.AreEqual(0.5, copy.rangeXY.x);
        Assert.IsFalse(copy.inputSpeedArgs.combineMagnitudes);
        Assert.AreEqual(7, copy.inputSpeedArgs.outputSmoothHalflife);
        Assert.AreEqual(AccelMode.synchronous, copy.argsX.mode);
        Assert.AreEqual(AccelMode.jump, copy.argsY.mode);
    }

    [TestMethod]
    public void LookupDataIsNotShared()
    {
        var source = new Profile();
        source.argsX.data[0] = 5;

        var copy = ProfileCopy.Clone(source);
        copy.argsX.data[0] = 9;
        copy.argsY.data[0] = 9;

        Assert.AreEqual(5, source.argsX.data[0]);
        Assert.AreEqual(0, source.argsY.data[0]);
        Assert.AreNotSame(copy.argsX.data, copy.argsY.data);
    }
}
