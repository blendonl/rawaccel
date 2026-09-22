using grapher.Speed;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class SpeedRecorderTests
{
    [TestMethod]
    public void HistoryBinsDistanceOverBinTime()
    {
        var history = new SpeedHistory(8);
        history.Add(10, 2, 20);
        history.Add(15, 2, 20);
        history.Add(20, 4, 40);
        var bins = new double[2];

        history.FillSpeeds(10, 5, input: true, bins);

        CollectionAssert.AreEqual(new[] { 0.4, 0.4 }, bins);
    }

    [TestMethod]
    public void HistoryDropsTheOldestSampleWhenFull()
    {
        var history = new SpeedHistory(4);

        for (int time = 0; time <= 20; time += 5)
        {
            history.Add(time, 1, 10);
        }

        var bins = new double[2];
        history.FillSpeeds(0, 5, input: false, bins);

        CollectionAssert.AreEqual(new[] { 0.0, 2.0 }, bins);
    }

    [TestMethod]
    public void RecorderIgnoresSamplesWhileDisabled()
    {
        var recorder = new SpeedRecorder();

        recorder.Record(5, 7, 1, movedFromRest: false);

        Assert.AreEqual(0, recorder.InputStatistics.Count);
    }

    [TestMethod]
    public void MovingFromRestIsGraphedButNotCounted()
    {
        var recorder = new SpeedRecorder { Enabled = true };

        recorder.Record(5, 7, 1, movedFromRest: false);
        recorder.Record(0.01, 0.01, 100, movedFromRest: true);

        Assert.AreEqual(1, recorder.InputStatistics.Count);
        Assert.AreEqual(5, recorder.InputStatistics.Min);
        Assert.AreEqual(7, recorder.OutputStatistics.Max);

        var bins = new double[1];
        recorder.History.FillSpeeds(recorder.NowMs - 1000, 2000, input: false, bins);
        Assert.AreEqual((7 + 1) / 2000.0, bins[0], 1e-9);
    }

    [TestMethod]
    public void ResetClearsHistoryAndStatistics()
    {
        var recorder = new SpeedRecorder { Enabled = true };
        recorder.Record(5, 7, 1, movedFromRest: false);

        recorder.Reset();

        var bins = new double[1];
        recorder.History.FillSpeeds(recorder.NowMs - 1000, 2000, input: true, bins);
        Assert.AreEqual(0, bins[0]);
        Assert.AreEqual(0, recorder.InputStatistics.Count);
        Assert.AreEqual(0, recorder.OutputStatistics.Count);
    }
}
