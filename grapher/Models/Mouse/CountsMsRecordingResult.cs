namespace grapher.Models.Mouse
{
    public class CountsMsRecordingResult
    {
        public CountsMsRecordingResult(
            int sampleCount,
            double durationMilliseconds,
            double averageCountsPerMillisecond,
            double minAverageCountsPerMillisecond,
            double maxAverageCountsPerMillisecond)
        {
            SampleCount = sampleCount;
            DurationMilliseconds = durationMilliseconds;
            AverageCountsPerMillisecond = averageCountsPerMillisecond;
            MinAverageCountsPerMillisecond = minAverageCountsPerMillisecond;
            MaxAverageCountsPerMillisecond = maxAverageCountsPerMillisecond;
        }

        public int SampleCount { get; }

        public double DurationMilliseconds { get; }

        public double AverageCountsPerMillisecond { get; }

        public double MinAverageCountsPerMillisecond { get; }

        public double MaxAverageCountsPerMillisecond { get; }

        public bool HasSamples => SampleCount > 0 && DurationMilliseconds > 0;
    }
}
