using System;
using System.Collections.Generic;
using System.Linq;

namespace grapher.Charts;

public static class CurveSampler
{
    public const int AngleDivisions = 19;
    public const double NormalizedDpi = 1000.0;

    private const int Resolution = 500;
    private const double MaxSpeedPerDpi = 0.05;
    private static readonly double ChartLimit = Convert.ToDouble(decimal.MaxValue) / 10;

    private static readonly double[] SlowSpeeds =
    {
        0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4, 1.6, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 3.0, 3.333, 3.666, 4.0, 4.333, 4.666,
    };

    private readonly record struct InputSample(int X, int Y, double Time, double Speed);

    public static ChartLayout LayoutFor(Profile profile)
    {
        if (!profile.inputSpeedArgs.combineMagnitudes)
        {
            return ChartLayout.ByComponent;
        }

        bool anisotropic =
            profile.yxOutputDPIRatio != 1 ||
            profile.domainXY.x != profile.domainXY.y ||
            profile.rangeXY.x != profile.rangeXY.y;

        return anisotropic ? ChartLayout.Directional : ChartLayout.Combined;
    }

    public static double MaxSpeed(int chartDpi) => Math.Max(1, chartDpi) * MaxSpeedPerDpi;

    public static int NearestAngleDivision(double angle) =>
        (int)Math.Round(angle * 2 / Math.PI * (AngleDivisions - 1));

    public static CurveSet Sample(Profile profile, int chartDpi)
    {
        var graphing = WithoutSmoothing(profile);
        using var accel = new ManagedAccel(graphing);
        double maxSpeed = MaxSpeed(chartDpi);
        double step = maxSpeed / Resolution;
        var layout = LayoutFor(graphing);

        switch (layout)
        {
            case ChartLayout.ByComponent:
            {
                double sensitivity = graphing.outputDPI / NormalizedDpi;
                var x = Measure(accel, AxisSamples(maxSpeed, step, horizontal: true), sensitivity);
                var y = Measure(accel, AxisSamples(maxSpeed, step, horizontal: false), sensitivity * graphing.yxOutputDPIRatio);
                return new CurveSet(layout, x, y, null);
            }
            case ChartLayout.Directional:
            {
                double sensitivity = graphing.outputDPI / NormalizedDpi;
                var directions = Angles()
                    .Select(angle => Measure(accel, AngledSamples(angle, maxSpeed, step), sensitivity))
                    .ToList();
                return new CurveSet(layout, directions[0], directions[^1], directions);
            }
            default:
            {
                var combined = Measure(accel, CombinedSamples(maxSpeed, step), graphing.outputDPI / NormalizedDpi);
                return new CurveSet(layout, combined, null, null);
            }
        }
    }

    private static Profile WithoutSmoothing(Profile profile)
    {
        var copy = Parameters.ProfileCopy.Clone(profile);
        copy.inputSpeedArgs.inputSmoothHalflife = 0;
        copy.inputSpeedArgs.scaleSmoothHalflife = 0;
        copy.inputSpeedArgs.outputSmoothHalflife = 0;
        return copy;
    }

    private static Curve Measure(ManagedAccel accel, IEnumerable<InputSample> samples, double starter)
    {
        var input = new List<double>();
        var sensitivity = new List<double>();
        var velocity = new List<double>();
        var gain = new List<double>();
        var seen = new HashSet<double>();
        double lastInput = 0;
        double lastOutput = 0;

        foreach (var sample in samples)
        {
            if (sample.Speed <= 0)
            {
                continue;
            }

            var output = accel.Accelerate(sample.X, sample.Y, 1, sample.Time);
            double outSpeed = Clamp(Magnitude(output.Item1, output.Item2) / sample.Time);
            double inDiff = Math.Round(sample.Speed - lastInput, 5);
            double outDiff = Math.Round(outSpeed - lastOutput, 5);

            if (inDiff == 0 || !seen.Add(sample.Speed))
            {
                continue;
            }

            double ratio = Clamp(outSpeed / sample.Speed);
            double slope = Clamp(inDiff > 0 ? outDiff / inDiff : starter);

            if (double.IsFinite(ratio) && double.IsFinite(slope) && double.IsFinite(outSpeed))
            {
                input.Add(sample.Speed);
                sensitivity.Add(ratio);
                velocity.Add(outSpeed);
                gain.Add(slope);
            }

            lastInput = sample.Speed;
            lastOutput = outSpeed;
        }

        return new Curve(input, sensitivity, velocity, gain);
    }

    private static IEnumerable<InputSample> CombinedSamples(double maxSpeed, double step)
    {
        var samples = new List<InputSample>();

        foreach (var slow in SlowSpeeds)
        {
            int x = (int)Math.Round(slow * 50);
            double time = x / slow;
            samples.Add(new InputSample(x, 0, time, Clamp(x / time)));
        }

        for (double speed = 5; speed < maxSpeed; speed += step)
        {
            int x = (int)Math.Ceiling(speed);
            double time = x / speed;
            samples.Add(new InputSample(x, 0, time, Clamp(x / time)));
        }

        samples.Sort(static (a, b) => a.Speed.CompareTo(b.Speed));
        return samples;
    }

    private static IEnumerable<InputSample> AxisSamples(double maxSpeed, double step, bool horizontal)
    {
        foreach (var speed in SlowSpeeds.Concat(Steps(maxSpeed, step)))
        {
            int count = (int)Math.Ceiling(speed);
            double time = count / speed;
            yield return horizontal
                ? new InputSample(count, 0, time, Clamp(count / time))
                : new InputSample(0, count, time, Clamp(count / time));
        }
    }

    private static IEnumerable<InputSample> AngledSamples(double angle, double maxSpeed, double step)
    {
        foreach (var speed in SlowSpeeds.Concat(Steps(maxSpeed, step)))
        {
            yield return Angled(angle, speed);
        }
    }

    private static IEnumerable<double> Steps(double maxSpeed, double step)
    {
        for (double speed = 5; speed < maxSpeed; speed += step)
        {
            yield return speed;
        }
    }

    private static IEnumerable<double> Angles()
    {
        for (int i = 0; i < AngleDivisions; i++)
        {
            yield return i / (AngleDivisions - 1.0) * (Math.PI / 2);
        }
    }

    private static InputSample Angled(double angle, double magnitude)
    {
        double moveX = Math.Round(magnitude * Math.Cos(angle), 4);
        double moveY = Math.Round(magnitude * Math.Sin(angle), 4);
        int x;
        int y;
        double time;

        if (moveX == 0)
        {
            x = 0;
            y = (int)Math.Ceiling(moveY);
            time = y / moveY;
        }
        else if (moveY == 0)
        {
            x = (int)Math.Ceiling(moveX);
            y = 0;
            time = x / moveX;
        }
        else
        {
            double ratio = moveY / moveX;
            double roundedRatio = -1;
            double factor = 10;
            double biggerX = 0;
            double biggerY = 0;
            x = 0;
            y = 0;

            while (Math.Abs(roundedRatio - ratio) > 0.01 && biggerX < 25000 && biggerY < 25000)
            {
                x = (int)Math.Floor(biggerX);
                y = (int)Math.Floor(biggerY);
                roundedRatio = x > 0 ? y / x : -1;
                biggerX = moveX * factor;
                biggerY = moveY * factor;
                factor *= 10;
            }

            time = Magnitude(x, y) / magnitude;
        }

        return new InputSample(x, y, time, Clamp(Magnitude(x, y) / time));
    }

    private static double Magnitude(double x, double y) =>
        x == 0 ? Math.Abs(y) : y == 0 ? Math.Abs(x) : Math.Sqrt(x * x + y * y);

    private static double Clamp(double value) => Math.Clamp(value, -ChartLimit, ChartLimit);
}
