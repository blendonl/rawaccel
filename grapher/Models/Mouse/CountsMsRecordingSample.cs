namespace grapher.Models.Mouse
{
    public class CountsMsRecordingSample
    {
        public CountsMsRecordingSample(double elapsedMilliseconds, double countsPerMillisecond)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            CountsPerMillisecond = countsPerMillisecond;
        }

        public double ElapsedMilliseconds { get; }

        public double CountsPerMillisecond { get; }
    }
}
