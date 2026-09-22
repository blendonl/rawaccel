using System.Linq;
using grapher.Charts;
using grapher.Parameters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class CurveSamplerTests
{
    [TestMethod]
    public void NoAccelerationIsFlatAtTheSensMultiplier()
    {
        var profile = new Profile();
        profile.outputDPI = 2000;

        var curves = CurveSampler.Sample(profile, 1200);

        Assert.AreEqual(ChartLayout.Combined, curves.Layout);
        Assert.IsTrue(curves.First.Count > 400);
        Assert.IsTrue(curves.First.Sensitivity.All(s => System.Math.Abs(s - 2) < 1e-9));
        Assert.IsTrue(curves.First.Gain.All(g => System.Math.Abs(g - 2) < 1e-3));
        Assert.AreEqual(60, curves.First.Input.Max(), 0.2);
    }

    [TestMethod]
    public void ChartDpiSetsTheSampledRange()
    {
        var curves = CurveSampler.Sample(new Profile(), 3200);

        Assert.AreEqual(160, curves.First.Input.Max(), 0.5);
    }

    [TestMethod]
    public void LayoutFollowsModeAndAnisotropy()
    {
        var profile = new Profile();
        Assert.AreEqual(ChartLayout.Combined, CurveSampler.LayoutFor(profile));

        profile.rangeXY.y = 0.5;
        Assert.AreEqual(ChartLayout.Directional, CurveSampler.LayoutFor(profile));

        profile.rangeXY.y = 1;
        profile.yxOutputDPIRatio = 1.2;
        Assert.AreEqual(ChartLayout.Directional, CurveSampler.LayoutFor(profile));

        profile.inputSpeedArgs.combineMagnitudes = false;
        Assert.AreEqual(ChartLayout.ByComponent, CurveSampler.LayoutFor(profile));
    }

    [TestMethod]
    public void AccelerationRaisesSensitivityWithSpeed()
    {
        var profile = new Profile();
        CurveTypes.Apply(CurveType.Linear, ref profile.argsX);
        profile.argsX.acceleration = 0.01;
        profile.argsX.capMode = CapMode.output;
        profile.argsX.cap.y = 0;

        var curve = CurveSampler.Sample(profile, 1200).First;

        Assert.IsTrue(curve.Sensitivity.Last() > curve.Sensitivity.First() + 0.2);
    }

    [TestMethod]
    public void DotLandsAtTheMatchingInputSpeed()
    {
        var profile = new Profile();
        profile.outputDPI = 2000;
        var curves = CurveSampler.Sample(profile, 1200);

        var dots = curves.FindDots(40, 0, 2);

        Assert.IsNotNull(dots.First);
        Assert.IsNull(dots.Second);
        Assert.AreEqual(10, dots.First.Value.Input, 0.2);
        Assert.AreEqual(2, dots.First.Value.Sensitivity, 1e-6);
        Assert.AreEqual(20, dots.First.Value.Velocity, 1e-9);
    }

    [TestMethod]
    public void InputSpeedInvertsTheCurveBeyondTheChartedRange()
    {
        var profile = new Profile();
        profile.outputDPI = 2000;
        var curves = CurveSampler.Sample(profile, 1200);

        Assert.AreEqual(10, curves.InputSpeed(40, 0, 2), 1e-6);
        Assert.AreEqual(200, curves.InputSpeed(400, 0, 1), 1e-6);
        Assert.AreEqual(0.05, curves.InputSpeed(0.1, 0, 1), 1e-9);
        Assert.AreEqual(0, curves.InputSpeed(0, 0, 1));
    }

    [TestMethod]
    public void InputSpeedInterpolatesAnAcceleratedCurve()
    {
        var profile = new Profile();
        CurveTypes.Apply(CurveType.Linear, ref profile.argsX);
        profile.argsX.acceleration = 0.01;
        profile.argsX.capMode = CapMode.output;
        profile.argsX.cap.y = 0;
        var curve = CurveSampler.Sample(profile, 1200).First;
        int i = curve.Count / 2;

        Assert.AreEqual(curve.Input[i], curve.InputSpeedFor(curve.Velocity[i]), 1e-9);

        double between = curve.InputSpeedFor((curve.Velocity[i] + curve.Velocity[i + 1]) / 2);
        Assert.IsTrue(between > curve.Input[i] && between < curve.Input[i + 1]);
    }

    [TestMethod]
    public void InputSpeedCombinesComponentsAndDirections()
    {
        var byComponent = new Profile();
        byComponent.inputSpeedArgs.combineMagnitudes = false;
        byComponent.yxOutputDPIRatio = 1.5;

        Assert.AreEqual(System.Math.Sqrt(10 * 10 + 20 * 20), CurveSampler.Sample(byComponent, 1200).InputSpeed(10, -30, 1), 0.2);

        var directional = new Profile();
        CurveTypes.Apply(CurveType.Linear, ref directional.argsX);
        directional.argsX.acceleration = 0.01;
        directional.argsX.cap.y = 0;
        directional.rangeXY.y = 0.5;
        var curves = CurveSampler.Sample(directional, 1200);

        Assert.AreEqual(ChartLayout.Directional, curves.Layout);
        Assert.AreEqual(curves.First.InputSpeedFor(25), curves.InputSpeed(-50, 0, 2), 1e-12);
        Assert.AreEqual(curves.Second!.InputSpeedFor(25), curves.InputSpeed(0, 25, 1), 1e-12);
    }

    [TestMethod]
    public void ByComponentAppliesVerticalRatioToTheVerticalCurve()
    {
        var profile = new Profile();
        profile.inputSpeedArgs.combineMagnitudes = false;
        profile.yxOutputDPIRatio = 1.5;

        var curves = CurveSampler.Sample(profile, 1200);

        Assert.AreEqual(ChartLayout.ByComponent, curves.Layout);
        Assert.AreEqual(1, curves.First.Sensitivity[^1], 1e-9);
        Assert.AreEqual(1.5, curves.Second!.Sensitivity[^1], 1e-9);

        var dots = curves.FindDots(10, -30, 1);
        Assert.AreEqual(10, dots.First!.Value.Input, 0.2);
        Assert.AreEqual(20, dots.Second!.Value.Input, 0.2);
    }

    [TestMethod]
    public void DirectionalCurvesDifferWithVerticalRange()
    {
        var profile = new Profile();
        CurveTypes.Apply(CurveType.Linear, ref profile.argsX);
        profile.argsX.acceleration = 0.01;
        profile.argsX.cap.y = 0;
        profile.rangeXY.y = 0.5;

        var curves = CurveSampler.Sample(profile, 1200);

        Assert.AreEqual(ChartLayout.Directional, curves.Layout);
        Assert.AreEqual(CurveSampler.AngleDivisions, curves.Directions!.Count);
        double horizontalGain = curves.First.Sensitivity[^1] - 1;
        double verticalGain = curves.Second!.Sensitivity[^1] - 1;
        Assert.AreEqual(horizontalGain / 2, verticalGain, horizontalGain * 0.05);
    }
}
