using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace grapher.Theming;

public sealed class ThemeCatalog
{
    private const string RootName = nameof(ColorScheme);

    private static readonly PropertyInfo[] ColorProperties = typeof(ColorScheme)
        .GetProperties()
        .Where(p => p.PropertyType == typeof(Color))
        .ToArray();

    private static readonly PropertyInfo[] FlagProperties = typeof(ColorScheme)
        .GetProperties()
        .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
        .ToArray();

    private readonly string directory;

    public ThemeCatalog(string directory)
    {
        this.directory = directory;
    }

    public IReadOnlyList<ColorScheme> Load()
    {
        try
        {
            Directory.CreateDirectory(directory);

            if (Directory.GetFiles(directory, "*.xml").Length == 0)
            {
                foreach (var (fileName, scheme) in BuiltInSchemes.Files)
                {
                    Save(scheme, Path.Combine(directory, fileName + ".xml"));
                }
            }

            var schemes = Directory.GetFiles(directory, "*.xml")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(TryRead)
                .OfType<ColorScheme>()
                .GroupBy(s => s.Name)
                .Select(g => g.First())
                .ToList();

            if (schemes.Count > 0)
            {
                return schemes;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return BuiltInSchemes.Files.Select(f => f.Scheme).ToList();
    }

    public static ColorScheme? Read(Stream stream)
    {
        var root = XDocument.Load(stream).Root;

        if (root is null || root.Name.LocalName != RootName)
        {
            return null;
        }

        var name = root.Element(nameof(ColorScheme.Name))?.Value;

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var scheme = new ColorScheme { Name = name };

        foreach (var property in ColorProperties)
        {
            if (root.Element(property.Name) is XElement element)
            {
                property.SetValue(scheme, ParseColor(element));
            }
        }

        foreach (var property in FlagProperties)
        {
            if (bool.TryParse(root.Element(property.Name)?.Value, out bool flag))
            {
                property.SetValue(scheme, flag);
            }
        }

        FillMissingColors(scheme);
        return scheme;
    }

    public static void Save(ColorScheme scheme, string path)
    {
        var root = new XElement(RootName, new XElement(nameof(ColorScheme.Name), scheme.Name));

        foreach (var property in ColorProperties)
        {
            var color = (Color)property.GetValue(scheme)!;
            var element = new XElement(property.Name, new XAttribute("Web", ColorTranslator.ToHtml(Color.FromArgb(255, color))));

            if (color.A < 255)
            {
                element.Add(new XAttribute("Alpha", color.A));
            }

            root.Add(element);
        }

        foreach (var property in FlagProperties)
        {
            root.Add(new XElement(property.Name, ((bool)property.GetValue(scheme)!).ToString().ToLowerInvariant()));
        }

        new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(path);
    }

    private static ColorScheme? TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Read(stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException)
        {
            return null;
        }
    }

    private static Color ParseColor(XElement element)
    {
        Color color;

        try
        {
            color = ColorTranslator.FromHtml(element.Attribute("Web")?.Value ?? string.Empty);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            color = Color.Black;
        }

        if (byte.TryParse(element.Attribute("Alpha")?.Value, out byte alpha) && alpha != 255)
        {
            color = Color.FromArgb(alpha, color);
        }

        return color.IsEmpty ? Color.Black : color;
    }

    private static void FillMissingColors(ColorScheme scheme)
    {
        bool dark = !scheme.Background.IsEmpty && scheme.IsDark;
        var fallback = dark ? BuiltInSchemes.Dark : BuiltInSchemes.Light;

        foreach (var property in ColorProperties)
        {
            if (((Color)property.GetValue(scheme)!).IsEmpty)
            {
                property.SetValue(scheme, property.GetValue(fallback));
            }
        }
    }
}
