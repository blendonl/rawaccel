using System;
using System.Collections.Generic;

namespace grapher.Models.Mouse
{
    public class CountsMsRecorder
    {
        private const double WindowMilliseconds = 250;
        private const int MaxStoredSamples = 600;

        public CountsMsRecorder()
        {
            Reset();
        }

        private List<CountsMsRecordingSample> Samples { get; } = new List<CountsMsRecordingSample>();

        private double TotalCounts { get; set; }

        private double TotalMilliseconds { get; set; }

        private double WindowCounts { get; set; }

        private double WindowTime { get; set; }

        private double MinWindowAverage { get; set; }

        private double MaxWindowAverage { get; set; }

        private int WindowCount { get; set; }

        private int SampleCount { get; set; }

        public bool IsRecording { get; private set; }

        public void Start()
        {
            Reset();
            IsRecording = true;
        }

        public CountsMsRecordingResult Stop()
        {
            if (WindowTime > 0)
            {
                FlushWindow();
            }

            IsRecording = false;

            return GetResult(
                WindowCount > 0 ? MinWindowAverage : 0,
                WindowCount > 0 ? MaxWindowAverage : 0);
        }

        public CountsMsRecordingResult Snapshot()
        {
            var minWindowAverage = WindowCount > 0 ? MinWindowAverage : 0;
            var maxWindowAverage = WindowCount > 0 ? MaxWindowAverage : 0;

            if (WindowTime > 0)
            {
                var currentWindowAverage = GetAverage(WindowCounts, WindowTime);

                if (WindowCount == 0)
                {
                    minWindowAverage = currentWindowAverage;
                    maxWindowAverage = currentWindowAverage;
                }
                else
                {
                    minWindowAverage = Math.Min(minWindowAverage, currentWindowAverage);
                    maxWindowAverage = Math.Max(maxWindowAverage, currentWindowAverage);
                }
            }

            return GetResult(minWindowAverage, maxWindowAverage);
        }

        public void AddSample(double x, double y, double milliseconds)
        {
            if (!IsRecording || milliseconds <= 0)
            {
                return;
            }

            var counts = Math.Sqrt(x * x + y * y);

            TotalCounts += counts;
            TotalMilliseconds += milliseconds;
            WindowCounts += counts;
            WindowTime += milliseconds;
            SampleCount++;
            Samples.Add(new CountsMsRecordingSample(
                TotalMilliseconds,
                GetAverage(counts, milliseconds)));
            if (Samples.Count > MaxStoredSamples)
            {
                Samples.RemoveAt(0);
            }

            if (WindowTime >= WindowMilliseconds)
            {
                FlushWindow();
            }
        }

        private void Reset()
        {
            Samples.Clear();
            TotalCounts = 0;
            TotalMilliseconds = 0;
            WindowCounts = 0;
            WindowTime = 0;
            MinWindowAverage = double.MaxValue;
            MaxWindowAverage = double.MinValue;
            WindowCount = 0;
            SampleCount = 0;
            IsRecording = false;
        }

        private void FlushWindow()
        {
            if (WindowTime <= 0)
            {
                return;
            }

            var average = GetAverage(WindowCounts, WindowTime);
            MinWindowAverage = Math.Min(MinWindowAverage, average);
            MaxWindowAverage = Math.Max(MaxWindowAverage, average);
            WindowCount++;

            WindowCounts = 0;
            WindowTime = 0;
        }

        private static double GetAverage(double counts, double milliseconds)
        {
            return milliseconds > 0 ? counts / milliseconds : 0;
        }

        private CountsMsRecordingResult GetResult(double minWindowAverage, double maxWindowAverage)
        {
            return new CountsMsRecordingResult(
                SampleCount,
                TotalMilliseconds,
                GetAverage(TotalCounts, TotalMilliseconds),
                minWindowAverage,
                maxWindowAverage,
                new List<CountsMsRecordingSample>(Samples));
        }
    }
}
