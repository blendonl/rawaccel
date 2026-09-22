using System.Drawing;

namespace grapher.Theming;

public sealed class ColorScheme
{
    public string Name { get; set; } = string.Empty;

    public Color ChartBackground { get; set; }
    public Color ChartForeground { get; set; }
    public Color Primary { get; set; }
    public Color Secondary { get; set; }
    public Color MouseMovement { get; set; }
    public Color Field { get; set; }
    public Color OnField { get; set; }
    public Color OnFocusedField { get; set; }
    public Color EditedField { get; set; }
    public Color OnEditedField { get; set; }
    public Color ButtonFace { get; set; }
    public Color ButtonBorder { get; set; }
    public Color Control { get; set; }
    public Color ControlBorder { get; set; }
    public Color OnControl { get; set; }
    public Color DisabledControl { get; set; }
    public Color OnDisabledControl { get; set; }
    public Color Background { get; set; }
    public Color OnBackground { get; set; }
    public Color Surface { get; set; }
    public Color MenuBackground { get; set; }
    public Color MenuSelectedBorder { get; set; }
    public Color MenuSelected { get; set; }
    public Color CheckBoxBackground { get; set; }
    public Color CheckBoxBorder { get; set; }
    public Color CheckBoxHover { get; set; }
    public Color CheckBoxChecked { get; set; }

    public bool UseAccentGradientsForCheckboxes { get; set; }

    public bool UseAccentGradientsForButtons { get; set; }

    public bool IsDark => Luminance(Background) < 0.5;

    public override string ToString() => Name;

    public static double Luminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
}
