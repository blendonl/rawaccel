using System;

namespace grapher.Speed;

public sealed class SpeedStatistics
{
    private const double LowestBucketSpeed = 0.01;
    private const int BucketsPerDecade = 100;
    private const int Decades = 7;

    private readonly long[] histogram = new long[BucketsPerDecade * Decades + 1];
    private double lowest = double.MaxValue;
    private double sum;

    public long Count { get; private set; }

    public double Min => Count > 0 ? lowest : 0;

    public double Max { get; private set; }

    public double Average => Count > 0 ? sum / Count : 0;

    public double Median => Percentile(0.5);

    public double Percentile95 => Percentile(0.95);

    public void Add(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0)
        {
            return;
        }

        Count++;
        sum += speed;
        lowest = Math.Min(lowest, speed);
        Max = Math.Max(Max, speed);
        histogram[BucketOf(speed)]++;
    }

    public void Reset()
    {
        Count = 0;
        sum = 0;
        Max = 0;
        lowest = double.MaxValue;
        Array.Clear(histogram);
    }

    public double Percentile(double fraction)
    {
        if (Count == 0)
        {
            return 0;
        }

        long target = Math.Max(1, (long)Math.Ceiling(fraction * Count));
        long seen = 0;

        for (int bucket = 0; bucket < histogram.Length; bucket++)
        {
            seen += histogram[bucket];

            if (seen >= target)
            {
                return Math.Clamp(BucketCenter(bucket), Min, Max);
            }
        }

        return Max;
    }

    private static int BucketOf(double speed) =>
        Math.Clamp((int)Math.Floor(Math.Log10(speed / LowestBucketSpeed) * BucketsPerDecade), 0, BucketsPerDecade * Decades);

    private static double BucketCenter(int bucket) =>
        LowestBucketSpeed * Math.Pow(10, (bucket + 0.5) / BucketsPerDecade);
}
