using System;
using System.IO;

namespace grapher.Settings;

public sealed class AppPaths
{
    public AppPaths(string directory)
    {
        Directory = directory;
    }

    public static AppPaths Default { get; } = new(AppContext.BaseDirectory);

    public string Directory { get; }

    public string SettingsFile => Path.Combine(Directory, "settings.json");

    public string SettingsBackupFile => Path.Combine(Directory, "settings.json.bak");

    public string GuiSettingsFile => Path.Combine(Directory, ".config");

    public string ThemesDirectory => Path.Combine(Directory, "themes");
}
