using System;
using System.Collections.Generic;

namespace grapher.Charts;

public enum ChartLayout
{
    Combined,
    Directional,
    ByComponent,
}

public sealed record DotSet(Dot? First, Dot? Second);

public sealed class CurveSet
{
    public CurveSet(ChartLayout layout, Curve first, Curve? second, IReadOnlyList<Curve>? directions)
    {
        Layout = layout;
        First = first;
        Second = second;
        Directions = directions;
    }

    public ChartLayout Layout { get; }

    public Curve First { get; }

    public Curve? Second { get; }

    public IReadOnlyList<Curve>? Directions { get; }

    public bool SameShapeAs(CurveSet other) =>
        Layout == other.Layout &&
        First.SameShapeAs(other.First) &&
        (Second is null ? other.Second is null : other.Second is not null && Second.SameShapeAs(other.Second));

    public ValueRange SensitivityRange(bool second) => Range(second, static c => c.SensitivityExtent);

    public ValueRange GainRange(bool second) => Range(second, static c => c.GainExtent);

    public DotSet FindDots(double x, double y, double timeMs)
    {
        switch (Layout)
        {
            case ChartLayout.ByComponent:
            {
                double outX = Math.Abs(x) / timeMs;
                double outY = Math.Abs(y) / timeMs;
                return new DotSet(Lookup(First, outX), Second is null ? null : Lookup(Second, outY));
            }
            case ChartLayout.Directional when Directions is not null && Second is not null:
            {
                double outSpeed = Math.Sqrt(x * x + y * y) / timeMs;
                double angle = Math.Atan2(Math.Abs(y), Math.Abs(x));
                int division = Math.Clamp(CurveSampler.NearestAngleDivision(angle), 0, Directions.Count - 1);
                int index = Directions[division].IndexForOutputSpeed(outSpeed);

                if (index < 0)
                {
                    return new DotSet(null, null);
                }

                double input = Directions[division].Input[index];
                return new DotSet(DotFrom(First, index, input), DotFrom(Second, index, input));
            }
            default:
            {
                double outSpeed = Math.Sqrt(x * x + y * y) / timeMs;
                return new DotSet(Lookup(First, outSpeed), null);
            }
        }
    }

    private ValueRange Range(bool second, Func<Curve, (double Min, double Max)> extent)
    {
        if (Layout == ChartLayout.ByComponent)
        {
            var curve = second && Second is not null ? Second : First;
            var (min, max) = extent(curve);
            return ValueRange.Padded(min, max);
        }

        var (firstMin, firstMax) = extent(First);

        if (Second is null)
        {
            return ValueRange.Padded(firstMin, firstMax);
        }

        var (secondMin, secondMax) = extent(Second);
        return ValueRange.Padded(Math.Min(firstMin, secondMin), Math.Max(firstMax, secondMax));
    }

    private static Dot? Lookup(Curve curve, double outputSpeed)
    {
        int index = curve.IndexForOutputSpeed(outputSpeed);
        return index < 0 ? null : curve.DotAt(index, outputSpeed);
    }

    private static Dot? DotFrom(Curve curve, int index, double input)
    {
        if (curve.Count == 0)
        {
            return null;
        }

        int clamped = Math.Clamp(index, 0, curve.Count - 1);
        return new Dot(input, curve.Sensitivity[clamped], curve.Velocity[clamped], curve.Gain[clamped]);
    }
}
