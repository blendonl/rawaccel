using System.Collections.Generic;
using System.Drawing;

namespace grapher.Theming;

public static class BuiltInSchemes
{
    public const string LightName = "Light Theme";
    public const string DarkName = "Dark Theme";

    public static ColorScheme Light => CreateLight(LightName, Color.White, Color.FromArgb(27, 27, 27));

    public static ColorScheme LightStreamer => CreateLight("Light Streamer Theme", Color.Green, Color.White);

    public static ColorScheme Dark => CreateDark(DarkName, Color.FromArgb(39, 39, 39), Color.FromArgb(230, 230, 230), accented: false);

    public static ColorScheme AccentedDark => CreateDark("Accented Dark Theme", Color.FromArgb(39, 39, 39), Color.FromArgb(230, 230, 230), accented: true);

    public static ColorScheme DarkStreamer => CreateDark("Dark Streamer Theme", Color.Green, Color.White, accented: false);

    public static IReadOnlyList<(string FileName, ColorScheme Scheme)> Files => new[]
    {
        ("LightTheme", Light),
        ("LightStreamerTheme", LightStreamer),
        ("DarkTheme", Dark),
        ("DarkStreamerTheme", DarkStreamer),
        ("AccentedDarkTheme", AccentedDark),
    };

    private static ColorScheme CreateLight(string name, Color chartBackground, Color chartForeground) => new()
    {
        Name = name,
        Primary = Color.FromArgb(0, 103, 192),
        Secondary = Color.FromArgb(232, 145, 45),
        MouseMovement = Color.FromArgb(196, 43, 28),
        ChartBackground = chartBackground,
        ChartForeground = chartForeground,
        Field = Color.White,
        OnField = Color.FromArgb(27, 27, 27),
        OnFocusedField = Color.FromArgb(16, 16, 16),
        EditedField = Color.FromArgb(255, 244, 206),
        OnEditedField = Color.FromArgb(27, 27, 27),
        ButtonFace = Color.FromArgb(251, 251, 251),
        ButtonBorder = Color.FromArgb(209, 209, 209),
        Control = Color.FromArgb(251, 251, 251),
        ControlBorder = Color.FromArgb(214, 214, 214),
        OnControl = Color.FromArgb(27, 27, 27),
        DisabledControl = Color.FromArgb(240, 240, 240),
        OnDisabledControl = Color.FromArgb(138, 138, 138),
        Background = Color.FromArgb(243, 243, 243),
        OnBackground = Color.FromArgb(27, 27, 27),
        Surface = Color.White,
        MenuBackground = Color.FromArgb(243, 243, 243),
        MenuSelectedBorder = Color.FromArgb(0, 103, 192),
        MenuSelected = Color.FromArgb(230, 230, 230),
        CheckBoxBackground = Color.White,
        CheckBoxBorder = Color.FromArgb(0, 103, 192),
        CheckBoxHover = Color.FromArgb(230, 230, 230),
        CheckBoxChecked = Color.FromArgb(0, 103, 192),
    };

    private static ColorScheme CreateDark(string name, Color chartBackground, Color chartForeground, bool accented) => new()
    {
        Name = name,
        Primary = Color.FromArgb(154, 123, 232),
        Secondary = Color.FromArgb(223, 185, 136),
        MouseMovement = Color.FromArgb(237, 103, 103),
        ChartBackground = chartBackground,
        ChartForeground = chartForeground,
        Field = Color.FromArgb(45, 45, 45),
        OnField = Color.FromArgb(243, 243, 243),
        OnFocusedField = Color.FromArgb(255, 255, 255),
        EditedField = Color.FromArgb(74, 63, 26),
        OnEditedField = Color.FromArgb(243, 243, 243),
        ButtonFace = Color.FromArgb(55, 55, 55),
        ButtonBorder = Color.FromArgb(70, 70, 70),
        Control = Color.FromArgb(55, 55, 55),
        ControlBorder = accented ? Color.FromArgb(223, 185, 136) : Color.FromArgb(61, 61, 61),
        OnControl = Color.FromArgb(243, 243, 243),
        DisabledControl = Color.FromArgb(50, 50, 50),
        OnDisabledControl = Color.FromArgb(120, 120, 120),
        Background = Color.FromArgb(32, 32, 32),
        OnBackground = Color.FromArgb(243, 243, 243),
        Surface = Color.FromArgb(43, 43, 43),
        MenuBackground = Color.FromArgb(32, 32, 32),
        MenuSelectedBorder = Color.FromArgb(154, 123, 232),
        MenuSelected = Color.FromArgb(55, 55, 55),
        CheckBoxBackground = Color.FromArgb(45, 45, 45),
        CheckBoxBorder = accented ? Color.FromArgb(223, 185, 136) : Color.FromArgb(154, 123, 232),
        CheckBoxHover = Color.FromArgb(61, 61, 61),
        CheckBoxChecked = Color.FromArgb(154, 123, 232),
        UseAccentGradientsForCheckboxes = accented,
        UseAccentGradientsForButtons = accented,
    };
}
