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

public sealed record DeviceOverride(string Id, string Name, bool Override, string Profile, DeviceConfig Config);

public sealed class DriverSession
{
    public const int MaxProfileNameLength = 255;

    private readonly IDriverAccess driver;
    private readonly AppPaths paths;
    private IReadOnlyList<MultiHandleDevice> systemDevices = Array.Empty<MultiHandleDevice>();
    private Dictionary<IntPtr, bool> trackedDevices = new();
    private string selectedName = string.Empty;

    public DriverSession(IDriverAccess driver, AppPaths paths)
    {
        this.driver = driver;
        this.paths = paths;
    }

    public event EventHandler? ActiveChanged;

    public event EventHandler? DevicesChanged;

    public DriverConfig ActiveConfig { get; private set; } = null!;

    public DriverConfig UserConfig { get; private set; } = null!;

    public Profile ActiveProfile => ActiveConfig.profiles[IndexOf(ActiveConfig, selectedName)];

    public Profile UserProfile => UserConfig.profiles[IndexOf(UserConfig, selectedName)];

    public ProfileDeviceConfig ActiveDeviceConfig => ProfileDevices.Get(ActiveConfig, ActiveProfile.name);

    public ProfileDeviceConfig UserDeviceConfig => ProfileDevices.Get(UserConfig, UserProfile.name);

    public IReadOnlyList<string> ProfileNames => UserConfig.profiles.Select(p => p.name).ToList();

    public string DefaultProfileName => UserConfig.profiles[0].name;

    public bool IsDefaultSelected => IndexOf(UserConfig, selectedName) == 0;

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
                    var conflicts = NormalizeLoaded(config);
                    UserConfig = config;

                    if (applyOnStartup)
                    {
                        ProfileDevices.Materialize(config);
                        return JoinMessages(ConflictNotice(conflicts, applied: true), Commit(config).Warning);
                    }

                    ActiveConfig = driver.ReadActive();
                    NormalizeLoaded(ActiveConfig);
                    UpdateTrackedDevices();
                    return ConflictNotice(conflicts, applied: false);
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
        var driverConflicts = NormalizeLoaded(ActiveConfig);
        UserConfig = ActiveConfig;
        UpdateTrackedDevices();
        return JoinMessages(JoinMessages(warning, ConflictNotice(driverConflicts, applied: false)), TryWriteSettingsFile(ActiveConfig));
    }

    public static string? Validate(Profile profile)
    {
        var errors = new ProfileErrors(new List<Profile> { profile });
        return errors.Empty() ? null : CleanMessages(errors.ToString());
    }

    public string? ValidateProfileName(string name, string? currentName = null)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            return "Enter a name.";
        }

        if (trimmed.Length > MaxProfileNameLength)
        {
            return $"Use at most {MaxProfileNameLength} characters.";
        }

        bool taken = UserConfig.profiles.Any(p =>
            p.name != currentName && string.Equals(p.name, trimmed, StringComparison.OrdinalIgnoreCase));

        return taken ? "Another profile already has this name." : null;
    }

    public IReadOnlyList<string> DevicesAssignedTo(string profileName) =>
        UserConfig.devices
            .Where(d => !d.config.disable && d.profile == profileName)
            .Select(d => string.IsNullOrWhiteSpace(d.name) ? d.id : d.name)
            .ToList();

    public void SelectProfile(string name)
    {
        selectedName = name;
        UpdateTrackedDevices();
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    public ApplyResult Apply(Profile profile) => Apply(profile, UserDeviceConfig);

    public ApplyResult Apply(Profile profile, ProfileDeviceConfig deviceConfig)
    {
        var name = UserProfile.name;
        return Change(config => ReplaceProfile(config, name, profile, deviceConfig));
    }

    public ApplyResult ApplyDevices(DeviceConfig defaults, IEnumerable<DeviceOverride> overrides, Profile profile, ProfileDeviceConfig deviceConfig)
    {
        var name = UserProfile.name;

        return Change(config =>
        {
            config.defaultDeviceConfig = defaults;

            foreach (var item in overrides)
            {
                var existing = config.devices.Find(d => d.id == item.Id);

                if (item.Override)
                {
                    if (existing is null)
                    {
                        config.devices.Add(new DeviceSettings
                        {
                            name = item.Name,
                            profile = item.Profile,
                            id = item.Id,
                            config = item.Config,
                        });
                    }
                    else
                    {
                        existing.profile = item.Profile;
                        existing.config = item.Config;
                    }
                }
                else if (existing is not null)
                {
                    config.devices.Remove(existing);
                }
            }

            ReplaceProfile(config, name, profile, deviceConfig);
        });
    }

    public ApplyResult AddProfile(string name, Profile source, ProfileDeviceConfig deviceConfig)
    {
        var trimmed = name.Trim();
        var error = ValidateProfileName(trimmed);

        if (error is not null)
        {
            return ApplyResult.Rejected(error);
        }

        return Change(config =>
        {
            var profile = ProfileCopy.Clone(source);
            profile.name = trimmed;
            config.profiles.Add(profile);
            config.accels.Add(new ManagedAccel(profile));
            config.profileDeviceConfigs[trimmed] = deviceConfig;
        }, select: trimmed);
    }

    public ApplyResult RenameProfile(string newName)
    {
        var oldName = UserProfile.name;
        var trimmed = newName.Trim();
        var error = ValidateProfileName(trimmed, oldName);

        if (error is not null)
        {
            return ApplyResult.Rejected(error);
        }

        return Change(config =>
        {
            int index = IndexOf(config, oldName);
            var profile = ProfileCopy.Clone(config.profiles[index]);
            profile.name = trimmed;
            config.SetProfileAt(index, profile);
            config.profileDeviceConfigs[trimmed] = ProfileDevices.Get(config, oldName);
            config.profileDeviceConfigs.Remove(oldName);

            foreach (var device in config.devices.Where(d => d.profile == oldName))
            {
                device.profile = trimmed;
            }
        }, select: trimmed);
    }

    public ApplyResult DeleteProfile()
    {
        if (UserConfig.profiles.Count < 2)
        {
            return ApplyResult.Rejected("The only profile can't be deleted.");
        }

        var name = UserProfile.name;

        return Change(config =>
        {
            int index = IndexOf(config, name);
            config.profiles.RemoveAt(index);
            config.accels.RemoveAt(index);
            config.profileDeviceConfigs.Remove(name);

            foreach (var device in config.devices.Where(d => d.profile == name))
            {
                device.profile = string.Empty;
            }
        }, select: string.Empty);
    }

    public ApplyResult MakeDefaultProfile()
    {
        var name = UserProfile.name;

        return Change(config =>
        {
            int index = IndexOf(config, name);
            var profile = config.profiles[index];
            var accel = config.accels[index];
            config.profiles.RemoveAt(index);
            config.accels.RemoveAt(index);
            config.profiles.Insert(0, profile);
            config.accels.Insert(0, accel);
        }, select: name);
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

    private ApplyResult Change(Action<DriverConfig> edit, string? select = null)
    {
        var (config, copyErrors) = DriverConfig.Convert(UserConfig.ToJSON());

        if (copyErrors is not null)
        {
            return ApplyResult.Rejected(CleanMessages(copyErrors));
        }

        edit(config);
        ProfileDevices.Materialize(config);

        var errors = config.Errors();

        if (errors is not null)
        {
            return ApplyResult.Rejected(CleanMessages(errors));
        }

        if (select is not null)
        {
            selectedName = select;
        }

        return Commit(config);
    }

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
        var selected = ActiveProfile.name;

        foreach (var device in systemDevices)
        {
            var settings = ActiveConfig.devices.Find(d => d.id == device.id);
            var config = settings?.config ?? ActiveConfig.defaultDeviceConfig;

            if (config.disable || ProfileDevices.EffectiveProfile(ActiveConfig, settings) != selected)
            {
                continue;
            }

            foreach (var handle in device.handles)
            {
                tracked[handle] = config.dpi > 0;
            }
        }

        trackedDevices = tracked;
    }

    private static void ReplaceProfile(DriverConfig config, string name, Profile profile, ProfileDeviceConfig deviceConfig)
    {
        var edited = ProfileCopy.Clone(profile);
        edited.name = name;
        config.SetProfileAt(IndexOf(config, name), edited);
        config.profileDeviceConfigs[name] = deviceConfig;
    }

    private static IReadOnlyList<string> NormalizeLoaded(DriverConfig config)
    {
        ProfileDevices.ReleaseDefaultAssignments(config);
        ProfileDevices.Complete(config);
        return ProfileDevices.DescribeConflicts(config);
    }

    private static string? ConflictNotice(IReadOnlyList<string> conflicts, bool applied) =>
        conflicts.Count == 0
            ? null
            : (applied
                ? "DPI and polling rate are now set per profile. These devices had their own values and now use their profile's:\n"
                : "DPI and polling rate are now set per profile. These devices had their own values and will use their profile's the next time settings are applied:\n") +
                string.Join("\n", conflicts);

    private static int IndexOf(DriverConfig config, string name) =>
        Math.Max(0, config.profiles.FindIndex(p => p.name == name));

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

    private static string? JoinMessages(string? first, string? second) =>
        first is null ? second : second is null ? first : $"{first}\n\n{second}";

    private static string CleanMessages(string messages) =>
        string.Join("\n", messages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
