using System;

namespace grapher.Models.Mouse
{
    public class SpeedStatistics
    {
        #region Constants

        private const double LowestBucketSpeed = 0.01;

        private const int BucketsPerDecade = 100;

        private const int Decades = 7;

        #endregion Constants

        #region Constructors

        public SpeedStatistics()
        {
            Histogram = new long[BucketsPerDecade * Decades + 1];
            Reset();
        }

        #endregion Constructors

        #region Properties

        public long Count { get; private set; }

        public double Min => Count > 0 ? LowestSpeed : 0;

        public double Max { get; private set; }

        public double Average => Count > 0 ? Sum / Count : 0;

        public double Median => Percentile(0.5);

        public double Percentile95 => Percentile(0.95);

        private double LowestSpeed { get; set; }

        private double Sum { get; set; }

        private long[] Histogram { get; }

        #endregion Properties

        #region Methods

        public void Add(double speed)
        {
            if (!(speed > 0) || double.IsInfinity(speed))
            {
                return;
            }

            Count++;
            Sum += speed;
            LowestSpeed = Math.Min(LowestSpeed, speed);
            Max = Math.Max(Max, speed);
            Histogram[BucketOf(speed)]++;
        }

        public void Reset()
        {
            Count = 0;
            Sum = 0;
            Max = 0;
            LowestSpeed = double.MaxValue;
            Array.Clear(Histogram, 0, Histogram.Length);
        }

        public double Percentile(double fraction)
        {
            if (Count == 0)
            {
                return 0;
            }

            var target = Math.Max(1, (long)Math.Ceiling(fraction * Count));
            long seen = 0;

            for (int bucket = 0; bucket < Histogram.Length; bucket++)
            {
                seen += Histogram[bucket];

                if (seen >= target)
                {
                    return Math.Min(Max, Math.Max(Min, BucketCenter(bucket)));
                }
            }

            return Max;
        }

        private static int BucketOf(double speed)
        {
            var bucket = (int)Math.Floor(Math.Log10(speed / LowestBucketSpeed) * BucketsPerDecade);
            return Math.Min(BucketsPerDecade * Decades, Math.Max(0, bucket));
        }

        private static double BucketCenter(int bucket)
        {
            return LowestBucketSpeed * Math.Pow(10, (bucket + 0.5) / BucketsPerDecade);
        }

        #endregion Methods
    }
}
