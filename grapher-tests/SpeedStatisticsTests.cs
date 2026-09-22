using grapher.Speed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class SpeedStatisticsTests
{
    [TestMethod]
    public void IgnoresSpeedsThatAreNotPositiveAndFinite()
    {
        var statistics = new SpeedStatistics();

        foreach (var speed in new[] { 0, 1, 2, 3, 4, 100, double.NaN, double.PositiveInfinity, -5 })
        {
            statistics.Add(speed);
        }

        Assert.AreEqual(5, statistics.Count);
        Assert.AreEqual(1, statistics.Min);
        Assert.AreEqual(100, statistics.Max);
        Assert.AreEqual(22, statistics.Average, 1e-12);
    }

    [TestMethod]
    public void PercentilesStayWithinTwoPercent()
    {
        var statistics = new SpeedStatistics();

        for (int i = 1; i <= 10000; i++)
        {
            statistics.Add(i / 100.0);
        }

        Assert.AreEqual(50, statistics.Median, 1);
        Assert.AreEqual(95, statistics.Percentile95, 1.9);
        Assert.AreEqual(0.01, statistics.Min);
        Assert.AreEqual(100, statistics.Max);
    }

    [TestMethod]
    public void PercentilesAreClampedToObservedSpeeds()
    {
        var statistics = new SpeedStatistics();
        statistics.Add(3);
        statistics.Add(100);

        Assert.AreEqual(100, statistics.Percentile95);
        Assert.AreEqual(3, statistics.Percentile(0));
    }

    [TestMethod]
    public void ResetClearsEverything()
    {
        var statistics = new SpeedStatistics();
        statistics.Add(5);
        statistics.Reset();

        Assert.AreEqual(0, statistics.Count);
        Assert.AreEqual(0, statistics.Min);
        Assert.AreEqual(0, statistics.Max);
        Assert.AreEqual(0, statistics.Average);
        Assert.AreEqual(0, statistics.Median);
    }
}
