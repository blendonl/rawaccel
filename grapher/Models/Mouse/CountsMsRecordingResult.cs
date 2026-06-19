using System.Collections.Generic;

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
            : this(
                sampleCount,
                durationMilliseconds,
                averageCountsPerMillisecond,
                minAverageCountsPerMillisecond,
                maxAverageCountsPerMillisecond,
                new CountsMsRecordingSample[0])
        {
        }

        public CountsMsRecordingResult(
            int sampleCount,
            double durationMilliseconds,
            double averageCountsPerMillisecond,
            double minAverageCountsPerMillisecond,
            double maxAverageCountsPerMillisecond,
            IReadOnlyList<CountsMsRecordingSample> samples)
        {
            SampleCount = sampleCount;
            DurationMilliseconds = durationMilliseconds;
            AverageCountsPerMillisecond = averageCountsPerMillisecond;
            MinAverageCountsPerMillisecond = minAverageCountsPerMillisecond;
            MaxAverageCountsPerMillisecond = maxAverageCountsPerMillisecond;
            Samples = samples;
        }

        public int SampleCount { get; }

        public double DurationMilliseconds { get; }

        public double AverageCountsPerMillisecond { get; }

        public double MinAverageCountsPerMillisecond { get; }

        public double MaxAverageCountsPerMillisecond { get; }

        public IReadOnlyList<CountsMsRecordingSample> Samples { get; }

        public bool HasSamples => SampleCount > 0 && DurationMilliseconds > 0;
    }
}
