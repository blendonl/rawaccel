using System.Globalization;
using System.Text;

namespace grapher.Parameters;

public static class LookupTable
{
    public const int MaxPoints = AccelArgs.MaxLutPoints;

    private const int RawCapacity = MaxPoints * 2;

    public static string Format(AccelArgs args)
    {
        if (args.data is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        int floats = System.Math.Min(args.length, args.data.Length) & ~1;

        for (int i = 0; i < floats; i += 2)
        {
            builder.Append(args.data[i].ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(args.data[i + 1].ToString(CultureInfo.InvariantCulture))
                .Append(';');

            if (i + 2 < floats)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    public static string? TryApply(ref AccelArgs args, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Text must be entered in text box to fill Look Up Table.";
        }

        var entries = text.Trim().Trim(';').Split(';');

        if (entries.Length < 2)
        {
            return "At least 2 points required";
        }

        if (entries.Length > MaxPoints)
        {
            return $"Number of points exceeds max ({MaxPoints})";
        }

        var data = new float[RawCapacity];
        float lastX = 0;

        for (int index = 0; index < entries.Length; index++)
        {
            var entry = entries[index].Trim();
            var parts = entry.Split(',');

            if (parts.Length != 2)
            {
                return $"Point at index {index} is malformed. Expected format: x,y; Given: {entry}";
            }

            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            {
                return $"X-value for point at index {index} is malformed. Expected: float. Given: {parts[0]}";
            }

            if (x <= 0)
            {
                return $"X-value for point at index {index} is less than or equal to 0. Point (0,0) is implied and should not be specified in points text.";
            }

            if (x <= lastX)
            {
                return $"X-value for point at index {index} is less than or equal to previous x-value. Value: {x} Previous: {lastX}";
            }

            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                return $"Y-value for point at index {index} is malformed. Expected: float. Given: {parts[1]}";
            }

            if (y <= 0)
            {
                return $"Y-value for point at index {index} is less than or equal to 0. Value: {y}";
            }

            data[index * 2] = x;
            data[index * 2 + 1] = y;
            lastX = x;
        }

        args.data = data;
        args.length = entries.Length * 2;
        return null;
    }
}
