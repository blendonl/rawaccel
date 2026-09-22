using System;

namespace grapher.Speed;

public sealed class SpeedHistory
{
    private readonly double[] times;
    private readonly double[] inputDistances;
    private readonly double[] outputDistances;
    private int next;
    private int count;

    public SpeedHistory(int capacity)
    {
        times = new double[capacity];
        inputDistances = new double[capacity];
        outputDistances = new double[capacity];
    }

    public void Add(double timeMs, double inputDistance, double outputDistance)
    {
        times[next] = timeMs;
        inputDistances[next] = inputDistance;
        outputDistances[next] = outputDistance;
        next = (next + 1) % times.Length;
        count = Math.Min(count + 1, times.Length);
    }

    public void Clear()
    {
        next = 0;
        count = 0;
    }

    public void FillSpeeds(double startMs, double binMs, bool input, Span<double> bins)
    {
        bins.Clear();
        var distances = input ? inputDistances : outputDistances;

        for (int age = 0; age < count; age++)
        {
            int index = (next - 1 - age + times.Length) % times.Length;
            int bin = (int)Math.Floor((times[index] - startMs) / binMs);

            if (bin < 0)
            {
                break;
            }

            if (bin < bins.Length)
            {
                bins[bin] += distances[index];
            }
        }

        for (int bin = 0; bin < bins.Length; bin++)
        {
            bins[bin] /= binMs;
        }
    }
}
