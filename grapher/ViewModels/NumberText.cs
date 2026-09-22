using System.Globalization;

namespace grapher.ViewModels;

public static class NumberText
{
    public const string FieldFormat = "0.#########";
    public const string ActiveFormat = "0.######";

    public static string Field(double value) => value.ToString(FieldFormat, CultureInfo.InvariantCulture);

    public static string Active(double value) => value.ToString(ActiveFormat, CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out double value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();

        if (!normalized.Contains('.') && normalized.Count(',') == 1)
        {
            normalized = normalized.Replace(',', '.');
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value);
    }

    private static int Count(this string text, char character)
    {
        int count = 0;
        foreach (var c in text)
        {
            if (c == character)
            {
                count++;
            }
        }

        return count;
    }
}
