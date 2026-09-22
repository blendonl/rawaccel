using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using DrawingColor = System.Drawing.Color;

namespace grapher.Theming;

public sealed class ThemeService
{
    public const string SystemName = Settings.GuiSettings.SystemThemeName;

    private readonly IReadOnlyList<ColorScheme> schemes;
    private string selectedName = SystemName;

    public ThemeService(IReadOnlyList<ColorScheme> schemes)
    {
        this.schemes = schemes;
        Current = BuiltInSchemes.Light;

        if (Application.Current?.PlatformSettings is { } platform)
        {
            platform.ColorValuesChanged += (_, _) =>
            {
                if (selectedName == SystemName)
                {
                    Apply(Resolve(SystemName));
                }
            };
        }
    }

    public event EventHandler? Changed;

    public IReadOnlyList<string> Names => new[] { SystemName }.Concat(schemes.Select(s => s.Name)).ToList();

    public string SelectedName => selectedName;

    public ColorScheme Current { get; private set; }

    public void Select(string name)
    {
        selectedName = name == SystemName || schemes.Any(s => s.Name == name) ? name : SystemName;
        Apply(Resolve(selectedName));
    }

    public static Color ToAvalonia(DrawingColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private ColorScheme Resolve(string name)
    {
        if (name != SystemName)
        {
            var match = schemes.FirstOrDefault(s => s.Name == name);
            if (match is not null)
            {
                return match;
            }
        }

        bool dark = Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
        var preferred = dark ? BuiltInSchemes.DarkName : BuiltInSchemes.LightName;
        return schemes.FirstOrDefault(s => s.Name == preferred) ?? (dark ? BuiltInSchemes.Dark : BuiltInSchemes.Light);
    }

    private void Apply(ColorScheme scheme)
    {
        Current = scheme;
        var app = Application.Current;

        if (app is not null)
        {
            app.RequestedThemeVariant = scheme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            var resources = app.Resources;
            var accent = ToAvalonia(scheme.Primary);

            resources["SystemAccentColor"] = accent;
            resources["SystemAccentColorDark1"] = Mix(accent, Colors.Black, 0.15);
            resources["SystemAccentColorDark2"] = Mix(accent, Colors.Black, 0.30);
            resources["SystemAccentColorDark3"] = Mix(accent, Colors.Black, 0.45);
            resources["SystemAccentColorLight1"] = Mix(accent, Colors.White, 0.15);
            resources["SystemAccentColorLight2"] = Mix(accent, Colors.White, 0.30);
            resources["SystemAccentColorLight3"] = Mix(accent, Colors.White, 0.45);

            resources["RaBackgroundBrush"] = Brush(scheme.Background);
            resources["RaSurfaceBrush"] = Brush(scheme.Surface);
            resources["RaForegroundBrush"] = Brush(scheme.OnBackground);
            resources["RaMutedForegroundBrush"] = new SolidColorBrush(ToAvalonia(scheme.OnBackground), 0.68);
            resources["RaBorderBrush"] = Brush(scheme.ControlBorder);
            resources["RaAccentBrush"] = new SolidColorBrush(accent);
            resources["RaEditedBrush"] = Brush(scheme.EditedField);
            resources["RaEditedForegroundBrush"] = Brush(scheme.OnEditedField);
            resources["RaMenuBrush"] = Brush(scheme.MenuBackground);
            resources["RaErrorBrush"] = new SolidColorBrush(scheme.IsDark ? Color.FromRgb(255, 153, 164) : Color.FromRgb(196, 43, 28));
            resources["RaErrorBackgroundBrush"] = new SolidColorBrush(scheme.IsDark ? Color.FromRgb(68, 39, 38) : Color.FromRgb(253, 231, 233));
            resources["RaWarningBackgroundBrush"] = new SolidColorBrush(scheme.IsDark ? Color.FromRgb(67, 53, 25) : Color.FromRgb(255, 244, 206));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static SolidColorBrush Brush(DrawingColor color) => new(ToAvalonia(color));

    private static Color Mix(Color color, Color target, double amount) => Color.FromArgb(
        color.A,
        (byte)Math.Round(color.R + (target.R - color.R) * amount),
        (byte)Math.Round(color.G + (target.G - color.G) * amount),
        (byte)Math.Round(color.B + (target.B - color.B) * amount));
}
