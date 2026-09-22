using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using grapher.Parameters;
using Newtonsoft.Json;

namespace grapher.Settings;

public sealed record ApplyResult(string? Errors, string? Warning, Task Activation)
{
    public bool Succeeded => Errors is null;

    public static ApplyResult Rejected(string errors) => new(errors, null, Task.CompletedTask);
}

public sealed record DeviceOverride(string Id, string Name, bool Override, DeviceConfig Config);

public sealed class DriverSession
{
    private readonly IDriverAccess driver;
    private readonly AppPaths paths;
    private IReadOnlyList<MultiHandleDevice> systemDevices = Array.Empty<MultiHandleDevice>();
    private Dictionary<IntPtr, bool> trackedDevices = new();

    public DriverSession(IDriverAccess driver, AppPaths paths)
    {
        this.driver = driver;
        this.paths = paths;
    }

    public event EventHandler? ActiveChanged;

    public event EventHandler? DevicesChanged;

    public DriverConfig ActiveConfig { get; private set; } = null!;

    public DriverConfig UserConfig { get; private set; } = null!;

    public Profile ActiveProfile => ActiveConfig.profiles[0];

    public Profile UserProfile => UserConfig.profiles[0];

    public IReadOnlyList<MultiHandleDevice> SystemDevices => systemDevices;

    public IReadOnlyDictionary<IntPtr, bool> TrackedDevices => trackedDevices;

    public Task Activation { get; private set; } = Task.CompletedTask;

    public string? Load(bool applyOnStartup)
    {
        string? warning = null;

        if (File.Exists(paths.SettingsFile))
        {
            try
            {
                var (config, errors) = DriverConfig.Convert(File.ReadAllText(paths.SettingsFile));

                if (errors is null)
                {
                    UserConfig = config;

                    if (applyOnStartup)
                    {
                        return Commit(config).Warning;
                    }

                    ActiveConfig = driver.ReadActive();
                    UpdateTrackedDevices();
                    return null;
                }

                warning = $"settings.json has errors, so the driver's current settings were loaded instead. The old file was saved as settings.json.bak.\n\n{errors.Trim()}";
            }
            catch (JsonException e)
            {
                warning = $"settings.json could not be read, so the driver's current settings were loaded instead. The old file was saved as settings.json.bak.\n\n{e.Message}";
            }

            TryBackUpSettingsFile();
        }

        ActiveConfig = driver.ReadActive();
        UserConfig = ActiveConfig;
        UpdateTrackedDevices();
        return JoinMessages(warning, TryWriteSettingsFile(ActiveConfig));
    }

    public static string? Validate(Profile profile)
    {
        var errors = new ProfileErrors(new List<Profile> { profile });
        return errors.Empty() ? null : CleanMessages(errors.ToString());
    }

    public ApplyResult Apply(Profile profile)
    {
        var previous = UserConfig.profiles[0];
        UserConfig.SetProfileAt(0, ProfileCopy.Clone(profile));

        var errors = UserConfig.Errors();

        if (errors is not null)
        {
            UserConfig.SetProfileAt(0, previous);
            return ApplyResult.Rejected(CleanMessages(errors));
        }

        return Commit(UserConfig);
    }

    public ApplyResult ApplyDevices(DeviceConfig defaults, IEnumerable<DeviceOverride> overrides, Profile profile)
    {
        var previousDefaults = UserConfig.defaultDeviceConfig;
        var previousDevices = UserConfig.devices.Select(CloneDevice).ToList();

        UserConfig.defaultDeviceConfig = defaults;

        foreach (var item in overrides)
        {
            var existing = UserConfig.devices.Find(d => d.id == item.Id);

            if (item.Override)
            {
                if (existing is null)
                {
                    UserConfig.devices.Add(new DeviceSettings
                    {
                        name = item.Name,
                        profile = UserProfile.name,
                        id = item.Id,
                        config = item.Config,
                    });
                }
                else
                {
                    existing.config = item.Config;
                }
            }
            else if (existing is not null)
            {
                UserConfig.devices.Remove(existing);
            }
        }

        var result = Apply(profile);

        if (!result.Succeeded)
        {
            UserConfig.defaultDeviceConfig = previousDefaults;
            UserConfig.devices.Clear();
            UserConfig.devices.AddRange(previousDevices);
        }

        return result;
    }

    public Task Reset()
    {
        ActiveConfig = DriverConfig.GetDefault();
        UpdateTrackedDevices();
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        Activation = Task.Run(driver.Reset);
        return Activation;
    }

    public void UpdateSystemDevices(IReadOnlyList<MultiHandleDevice> devices)
    {
        systemDevices = devices;
        UpdateTrackedDevices();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    public DeviceSettings? FindDeviceSettings(string id) => UserConfig.devices.Find(d => d.id == id);

    private ApplyResult Commit(DriverConfig config)
    {
        ActiveConfig = config;
        UserConfig = config;
        var warning = TryWriteSettingsFile(config);
        var activation = Task.Run(() => driver.Write(config));
        Activation = activation;
        UpdateTrackedDevices();
        ActiveChanged?.Invoke(this, EventArgs.Empty);
        return new ApplyResult(null, warning, activation);
    }

    private void UpdateTrackedDevices()
    {
        var tracked = new Dictionary<IntPtr, bool>();
        var profileNames = new HashSet<string>(ActiveConfig.profiles.Select(p => p.name));
        var activeName = ActiveProfile.name;

        foreach (var device in systemDevices)
        {
            var settings = ActiveConfig.devices.Find(d => d.id == device.id);
            bool include;
            bool normalized;

            if (settings is null)
            {
                include = !ActiveConfig.defaultDeviceConfig.disable;
                normalized = ActiveConfig.defaultDeviceConfig.dpi > 0;
            }
            else
            {
                include = !settings.config.disable &&
                    (string.IsNullOrEmpty(settings.profile) ||
                        !profileNames.Contains(settings.profile) ||
                        settings.profile == activeName);
                normalized = settings.config.dpi > 0;
            }

            if (include)
            {
                foreach (var handle in device.handles)
                {
                    tracked[handle] = normalized;
                }
            }
        }

        trackedDevices = tracked;
    }

    private string? TryWriteSettingsFile(DriverConfig config)
    {
        try
        {
            File.WriteAllText(paths.SettingsFile, config.ToJSON());
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Settings were applied but could not be saved to settings.json: {e.Message}";
        }
    }

    private void TryBackUpSettingsFile()
    {
        try
        {
            File.Copy(paths.SettingsFile, paths.SettingsBackupFile, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static DeviceSettings CloneDevice(DeviceSettings source) => new()
    {
        name = source.name,
        profile = source.profile,
        id = source.id,
        config = source.config,
    };

    private static string? JoinMessages(string? first, string? second) =>
        first is null ? second : second is null ? first : $"{first}\n\n{second}";

    private static string CleanMessages(string messages) =>
        string.Join("\n", messages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
