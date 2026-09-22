using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using grapher.Theming;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class ThemeCatalogTests
{
    private const string LegacyFile =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<ColorScheme xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">\n" +
        "  <Name>My Streamer Theme</Name>\n" +
        "  <ChartBackground Web=\"Green\" />\n" +
        "  <ChartForeground Web=\"White\" />\n" +
        "  <Primary Web=\"#936EE3\" />\n" +
        "  <Secondary Web=\"#DFB988\" Alpha=\"128\" />\n" +
        "  <Background Web=\"#393939\" />\n" +
        "  <EditedField Web=\"AntiqueWhite\" />\n" +
        "  <UseAccentGradientsForButtons>true</UseAccentGradientsForButtons>\n" +
        "</ColorScheme>";

    private string directory = null!;

    [TestInitialize]
    public void CreateDirectory()
    {
        directory = Path.Combine(Path.GetTempPath(), "rawaccel-themes-" + Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void DeleteDirectory()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ReadsLegacySerializerFiles()
    {
        var scheme = ThemeCatalog.Read(new MemoryStream(Encoding.UTF8.GetBytes(LegacyFile)));

        Assert.IsNotNull(scheme);
        Assert.AreEqual("My Streamer Theme", scheme.Name);
        Assert.AreEqual(Color.Green.ToArgb(), scheme.ChartBackground.ToArgb());
        Assert.AreEqual(Color.FromArgb(0x93, 0x6E, 0xE3).ToArgb(), scheme.Primary.ToArgb());
        Assert.AreEqual(128, scheme.Secondary.A);
        Assert.AreEqual(Color.AntiqueWhite.ToArgb(), scheme.EditedField.ToArgb());
        Assert.IsTrue(scheme.UseAccentGradientsForButtons);
        Assert.IsTrue(scheme.IsDark);
        Assert.AreEqual(BuiltInSchemes.Dark.Surface.ToArgb(), scheme.Surface.ToArgb());
    }

    [TestMethod]
    public void SavedSchemesRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "theme.xml");
        var original = BuiltInSchemes.DarkStreamer;
        original.Secondary = Color.FromArgb(100, original.Secondary);

        ThemeCatalog.Save(original, path);
        using var stream = File.OpenRead(path);
        var loaded = ThemeCatalog.Read(stream);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.Name, loaded.Name);

        foreach (var property in typeof(ColorScheme).GetProperties().Where(p => p.PropertyType == typeof(Color)))
        {
            Assert.AreEqual(((Color)property.GetValue(original)!).ToArgb(), ((Color)property.GetValue(loaded)!).ToArgb(), property.Name);
        }
    }

    [TestMethod]
    public void SeedsBuiltInThemesIntoEmptyFolder()
    {
        var schemes = new ThemeCatalog(directory).Load();

        Assert.AreEqual(5, Directory.GetFiles(directory, "*.xml").Length);
        CollectionAssert.IsSubsetOf(
            new[] { BuiltInSchemes.LightName, BuiltInSchemes.DarkName },
            schemes.Select(s => s.Name).ToList());
    }

    [TestMethod]
    public void SkipsMalformedFiles()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "broken.xml"), "<ColorScheme><Name>Broken");
        File.WriteAllText(Path.Combine(directory, "good.xml"), LegacyFile);

        var schemes = new ThemeCatalog(directory).Load();

        Assert.AreEqual(1, schemes.Count);
        Assert.AreEqual("My Streamer Theme", schemes[0].Name);
    }
}
