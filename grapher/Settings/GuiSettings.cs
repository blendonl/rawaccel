using System;
using System.IO;
using Newtonsoft.Json;

namespace grapher.Settings;

public sealed class WindowPlacement
{
    public int X { get; set; }

    public int Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool Maximized { get; set; }

    public double OptionsWidth { get; set; }
}

public sealed class GuiSettings
{
    public const int DefaultDpi = 1200;
    public const int DefaultPollRate = 1000;
    public const string SystemThemeName = "System";

    [JsonProperty(Order = 1)]
    public int DPI { get; set; } = DefaultDpi;

    [JsonProperty(Order = 2)]
    public int PollRate { get; set; } = DefaultPollRate;

    [JsonProperty(Order = 3)]
    public bool ShowLastMouseMove { get; set; } = true;

    [JsonProperty(Order = 4)]
    public bool ShowVelocityAndGain { get; set; }

    [JsonProperty(Order = 5)]
    public bool AutoWriteToDriverOnStartup { get; set; } = true;

    [JsonProperty(Order = 6)]
    public string CurrentColorScheme { get; set; } = SystemThemeName;

    [JsonProperty(Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public WindowPlacement? Window { get; set; }

    public static GuiSettings Load(string path)
    {
        try
        {
            var settings = JsonConvert.DeserializeObject<GuiSettings>(File.ReadAllText(path));
            return settings is null ? new GuiSettings() : settings.Normalized();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new GuiSettings();
        }
    }

    public bool TrySave(string path)
    {
        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private GuiSettings Normalized()
    {
        if (DPI < 1)
        {
            DPI = DefaultDpi;
        }

        if (PollRate < 1)
        {
            PollRate = DefaultPollRate;
        }

        if (string.IsNullOrWhiteSpace(CurrentColorScheme))
        {
            CurrentColorScheme = SystemThemeName;
        }

        return this;
    }
}
