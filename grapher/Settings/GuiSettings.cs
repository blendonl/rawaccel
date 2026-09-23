using System;
using System.IO;
using grapher.Platform;
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

    [JsonProperty(Order = 7)]
    public string SpeedOverlayLockHotkey { get; set; } = OverlayHotkeys.Default.Lock.ToString();

    [JsonProperty(Order = 8)]
    public string SpeedOverlayResetHotkey { get; set; } = OverlayHotkeys.Default.Reset.ToString();

    [JsonProperty(Order = 9)]
    public string SpeedOverlayCloseHotkey { get; set; } = OverlayHotkeys.Default.Close.ToString();

    [JsonIgnore]
    public OverlayHotkeys SpeedOverlayHotkeys
    {
        get => OverlayHotkeys.Parse(SpeedOverlayLockHotkey, SpeedOverlayResetHotkey, SpeedOverlayCloseHotkey);
        set
        {
            SpeedOverlayLockHotkey = value.Lock.ToString();
            SpeedOverlayResetHotkey = value.Reset.ToString();
            SpeedOverlayCloseHotkey = value.Close.ToString();
        }
    }

    [JsonProperty(Order = 10, NullValueHandling = NullValueHandling.Ignore)]
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

        SpeedOverlayHotkeys = SpeedOverlayHotkeys;

        return this;
    }
}
