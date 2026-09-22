using System;
using System.Collections.Generic;
using System.Linq;
using grapher.Parameters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class ParameterCatalogTests
{
    [TestMethod]
    public void EveryCurveTypeHasOneDefinition()
    {
        foreach (var type in Enum.GetValues<CurveType>())
        {
            Assert.AreEqual(1, ParameterCatalog.Curves.Count(c => c.Type == type), type.ToString());
        }
    }

    [TestMethod]
    [DataRow(AccelMode.jump, CurveType.Jump)]
    [DataRow(AccelMode.natural, CurveType.Natural)]
    [DataRow(AccelMode.synchronous, CurveType.Synchronous)]
    [DataRow(AccelMode.power, CurveType.Power)]
    [DataRow(AccelMode.lut, CurveType.LookupTable)]
    [DataRow(AccelMode.noaccel, CurveType.Off)]
    public void ModesMapToCurveTypes(AccelMode mode, CurveType expected)
    {
        var args = new Profile().argsX;
        args.mode = mode;

        Assert.AreEqual(expected, CurveTypes.FromArgs(args));
        Assert.AreEqual(mode, CurveTypes.ToMode(expected));
    }

    [TestMethod]
    public void ClassicWithExponentTwoLoadsAsLinear()
    {
        var args = new Profile().argsX;
        args.mode = AccelMode.classic;
        args.exponentClassic = 2;
        Assert.AreEqual(CurveType.Linear, CurveTypes.FromArgs(args));

        args.exponentClassic = 2.5;
        Assert.AreEqual(CurveType.Classic, CurveTypes.FromArgs(args));
    }

    [TestMethod]
    public void ApplyingLinearForcesExponentTwo()
    {
        var args = new Profile().argsX;
        args.exponentClassic = 3;

        CurveTypes.Apply(CurveType.Linear, ref args);

        Assert.AreEqual(AccelMode.classic, args.mode);
        Assert.AreEqual(2, args.exponentClassic);
    }

    [TestMethod]
    [DataRow(CapMode.input, true, true, false)]
    [DataRow(CapMode.output, true, false, true)]
    [DataRow(CapMode.in_out, false, true, true)]
    public void CapRowsFollowCapType(CapMode capMode, bool slope, bool capInput, bool capOutput)
    {
        var args = new Profile().argsX;
        args.capMode = capMode;

        Assert.AreEqual(slope, ParameterCatalog.Acceleration.IsVisible(args));
        Assert.AreEqual(slope, ParameterCatalog.Scale.IsVisible(args));
        Assert.AreEqual(capInput, ParameterCatalog.CapInput.IsVisible(args));
        Assert.AreEqual(capOutput, ParameterCatalog.CapOutput.IsVisible(args));
    }

    [TestMethod]
    public void OutputOffsetIsDisabledOnlyForLegacyWithBothCaps()
    {
        var args = new Profile().argsX;
        args.capMode = CapMode.in_out;
        args.gain = false;
        Assert.IsFalse(ParameterCatalog.OutputOffset.IsEnabled(args));

        args.gain = true;
        Assert.IsTrue(ParameterCatalog.OutputOffset.IsEnabled(args));

        args.gain = false;
        args.capMode = CapMode.output;
        Assert.IsTrue(ParameterCatalog.OutputOffset.IsEnabled(args));
    }

    [TestMethod]
    public void NumberRowsWriteTheFieldTheyRead()
    {
        foreach (var spec in AllRows().OfType<NumberSpec>())
        {
            var args = new Profile().argsX;
            spec.Set(ref args, 123.25);
            Assert.AreEqual(123.25, spec.Get(args), spec.Key);
        }
    }

    [TestMethod]
    public void NumberRowsInOneCurveEditDistinctFields()
    {
        foreach (var curve in ParameterCatalog.Curves)
        {
            var numbers = curve.Rows.OfType<NumberSpec>().ToList();

            for (int i = 0; i < numbers.Count; i++)
            {
                var args = new Profile().argsX;
                var before = numbers.Select(n => n.Get(args)).ToList();

                numbers[i].Set(ref args, before[i] + 7);

                for (int j = 0; j < numbers.Count; j++)
                {
                    if (j != i)
                    {
                        Assert.AreEqual(before[j], numbers[j].Get(args), $"{curve.Name}: {numbers[i].Key} changed {numbers[j].Key}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void EveryRowHasLabelAndDescription()
    {
        foreach (var spec in AllRows())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(spec.Label), spec.Key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(spec.Description), spec.Key);
        }

        foreach (var curve in ParameterCatalog.Curves)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(curve.Description), curve.Name);
        }
    }

    [TestMethod]
    public void DefaultsOfEveryCurvePassDriverValidation()
    {
        foreach (var curve in ParameterCatalog.Curves.Where(c => c.Type != CurveType.LookupTable))
        {
            var profile = new Profile();
            CurveTypes.Apply(curve.Type, ref profile.argsX);
            profile.argsY = ProfileCopy.Clone(profile.argsX);

            var errors = new ProfileErrors(new List<Profile> { profile });

            Assert.IsTrue(errors.Empty(), $"{curve.Name}: {errors}");
        }
    }

    private static IEnumerable<RowSpec> AllRows() => ParameterCatalog.Curves.SelectMany(c => c.Rows).Distinct();
}
