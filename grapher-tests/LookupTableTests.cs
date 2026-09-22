using System.Collections.Generic;
using grapher.Parameters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class LookupTableTests
{
    [TestMethod]
    public void ParsesPointsIntoRawData()
    {
        var args = new Profile().argsX;

        var error = LookupTable.TryApply(ref args, " 1.5,0.8;4.375,3.3;\n13.51,15.2; ");

        Assert.IsNull(error);
        Assert.AreEqual(6, args.length);
        Assert.AreEqual(AccelArgs.MaxLutPoints * 2, args.data.Length);
        CollectionAssert.AreEqual(new[] { 1.5f, 0.8f, 4.375f, 3.3f, 13.51f, 15.2f }, args.data[..6]);
    }

    [TestMethod]
    public void FormatRoundTrips()
    {
        var args = new Profile().argsX;
        LookupTable.TryApply(ref args, "1,2;3,4.5;10,20;");

        var text = LookupTable.Format(args);
        var reparsed = new Profile().argsX;

        Assert.IsNull(LookupTable.TryApply(ref reparsed, text));
        Assert.AreEqual(args.length, reparsed.length);
        CollectionAssert.AreEqual(args.data, reparsed.data);
    }

    [TestMethod]
    [DataRow("", "Text must be entered in text box to fill Look Up Table.")]
    [DataRow("1,2;", "At least 2 points required")]
    [DataRow("1,2;3", "Point at index 1 is malformed. Expected format: x,y; Given: 3")]
    [DataRow("a,2;3,4", "X-value for point at index 0 is malformed. Expected: float. Given: a")]
    [DataRow("0,2;3,4", "X-value for point at index 0 is less than or equal to 0. Point (0,0) is implied and should not be specified in points text.")]
    [DataRow("3,2;1,4", "X-value for point at index 1 is less than or equal to previous x-value. Value: 1 Previous: 3")]
    [DataRow("1,b;3,4", "Y-value for point at index 0 is malformed. Expected: float. Given: b")]
    [DataRow("1,-2;3,4", "Y-value for point at index 0 is less than or equal to 0. Value: -2")]
    public void ReportsTheSameErrorsAsBefore(string text, string expected)
    {
        var args = new Profile().argsX;
        var original = args.length;

        Assert.AreEqual(expected, LookupTable.TryApply(ref args, text));
        Assert.AreEqual(original, args.length);
    }

    [TestMethod]
    public void RejectsTooManyPoints()
    {
        var points = new List<string>();
        for (int i = 1; i <= AccelArgs.MaxLutPoints + 1; i++)
        {
            points.Add($"{i},1");
        }

        var args = new Profile().argsX;

        Assert.AreEqual("Number of points exceeds max (257)", LookupTable.TryApply(ref args, string.Join(";", points)));
    }

    [TestMethod]
    public void ParsedTableWithMaximumPointsPassesDriverValidation()
    {
        var points = new List<string>();
        for (int i = 1; i <= AccelArgs.MaxLutPoints; i++)
        {
            points.Add($"{i},1");
        }

        var profile = new Profile();
        profile.argsX.mode = AccelMode.lut;
        Assert.IsNull(LookupTable.TryApply(ref profile.argsX, string.Join(";", points)));

        Assert.IsTrue(new ProfileErrors(new List<Profile> { profile }).Empty());
    }
}
