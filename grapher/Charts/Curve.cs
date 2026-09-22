using System;
using System.Collections.Generic;

namespace grapher.Charts;

public readonly record struct Dot(double Input, double Sensitivity, double Velocity, double Gain);

public readonly record struct ValueRange(double Min, double Max)
{
    public static ValueRange Padded(double min, double max)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || min > max)
        {
            return new ValueRange(0, 1);
        }

        if (min == 0 && max == 0)
        {
            return new ValueRange(0, 1);
        }

        return new ValueRange(min - Math.Abs(min) * 0.1, max + Math.Abs(max) * 0.1);
    }

    public ValueRange Union(ValueRange other) => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));
}

public sealed class Curve
{
    private readonly double[] runningMaxVelocity;

    public Curve(IReadOnlyList<double> input, IReadOnlyList<double> sensitivity, IReadOnlyList<double> velocity, IReadOnlyList<double> gain)
    {
        Input = ToArray(input);
        Sensitivity = ToArray(sensitivity);
        Velocity = ToArray(velocity);
        Gain = ToArray(gain);

        runningMaxVelocity = new double[Velocity.Length];
        double max = double.MinValue;
        for (int i = 0; i < Velocity.Length; i++)
        {
            max = Math.Max(max, Velocity[i]);
            runningMaxVelocity[i] = max;
        }
    }

    public double[] Input { get; }

    public double[] Sensitivity { get; }

    public double[] Velocity { get; }

    public double[] Gain { get; }

    public int Count => Input.Length;

    public (double Min, double Max) SensitivityExtent => Extent(Sensitivity);

    public (double Min, double Max) GainExtent => Extent(Gain);

    public int IndexForOutputSpeed(double outputSpeed)
    {
        if (Count == 0)
        {
            return -1;
        }

        int index = Array.BinarySearch(runningMaxVelocity, outputSpeed);

        if (index < 0)
        {
            index = ~index;
        }

        return Math.Clamp(index, 0, Count - 1);
    }

    public double InputSpeedFor(double outputSpeed)
    {
        if (!double.IsFinite(outputSpeed) || outputSpeed <= 0 || Count == 0)
        {
            return 0;
        }

        int upper = Array.BinarySearch(runningMaxVelocity, outputSpeed);

        if (upper >= 0)
        {
            return Input[upper];
        }

        upper = ~upper;

        if (upper == 0 || upper == Count)
        {
            int edge = Math.Min(upper, Count - 1);
            return runningMaxVelocity[edge] > 0 ? outputSpeed * Input[edge] / runningMaxVelocity[edge] : Input[edge];
        }

        int lower = upper - 1;
        double fraction = (outputSpeed - runningMaxVelocity[lower]) / (runningMaxVelocity[upper] - runningMaxVelocity[lower]);
        return Input[lower] + fraction * (Input[upper] - Input[lower]);
    }

    public Dot DotAt(int index, double measuredVelocity) =>
        new(Input[index], Sensitivity[index], measuredVelocity, Gain[index]);

    public bool SameShapeAs(Curve other) =>
        Input.AsSpan().SequenceEqual(other.Input) &&
        Velocity.AsSpan().SequenceEqual(other.Velocity);

    private static (double Min, double Max) Extent(double[] values)
    {
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var value in values)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        return values.Length == 0 ? (0, 1) : (min, max);
    }

    private static double[] ToArray(IReadOnlyList<double> values)
    {
        var array = new double[values.Count];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = values[i];
        }

        return array;
    }
}
