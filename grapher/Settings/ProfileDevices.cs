using System.Collections.Generic;
using System.Linq;

namespace grapher.Settings;

public static class ProfileDevices
{
    public static string EffectiveProfile(DriverConfig config, DeviceSettings? device) =>
        device is not null && config.profiles.Exists(p => p.name == device.profile)
            ? device.profile
            : config.profiles[0].name;

    public static ProfileDeviceConfig Get(DriverConfig config, string profileName) =>
        config.profileDeviceConfigs.TryGetValue(profileName, out var value) ? value : Derive(config, profileName);

    public static void Complete(DriverConfig config)
    {
        var names = config.profiles.Select(p => p.name).ToHashSet();

        foreach (var stale in config.profileDeviceConfigs.Keys.Where(k => !names.Contains(k)).ToList())
        {
            config.profileDeviceConfigs.Remove(stale);
        }

        foreach (var name in names.Where(n => !config.profileDeviceConfigs.ContainsKey(n)))
        {
            config.profileDeviceConfigs[name] = Derive(config, name);
        }
    }

    public static void ReleaseDefaultAssignments(DriverConfig config)
    {
        if (config.profiles.Count != 1)
        {
            return;
        }

        foreach (var device in config.devices.Where(d => d.profile == config.profiles[0].name))
        {
            device.profile = string.Empty;
        }
    }

    public static void Materialize(DriverConfig config)
    {
        Complete(config);
        config.defaultDeviceConfig = WithProfileValues(config.defaultDeviceConfig, Get(config, config.profiles[0].name));

        foreach (var device in config.devices)
        {
            device.config = WithProfileValues(device.config, Get(config, EffectiveProfile(config, device)));
        }
    }

    public static IReadOnlyList<string> DescribeConflicts(DriverConfig config)
    {
        var lines = new List<string>();

        foreach (var device in config.devices.Where(d => !d.config.disable))
        {
            var profile = EffectiveProfile(config, device);
            var expected = Get(config, profile);

            if (device.config.dpi != expected.dpi || device.config.pollingRate != expected.pollingRate)
            {
                var name = string.IsNullOrWhiteSpace(device.name) ? device.id : device.name;
                lines.Add($"{name} (profile \"{profile}\"): {Describe(device.config.dpi, device.config.pollingRate)} → {Describe(expected.dpi, expected.pollingRate)}");
            }
        }

        return lines;
    }

    public static string Describe(int dpi, int pollingRate) =>
        $"DPI {(dpi > 0 ? dpi.ToString() : "off")}, polling rate {(pollingRate > 0 ? $"{pollingRate} Hz" : "auto")}";

    private static ProfileDeviceConfig Derive(DriverConfig config, string profileName)
    {
        var source = profileName == config.profiles[0].name
            ? config.defaultDeviceConfig
            : config.devices.Find(d => EffectiveProfile(config, d) == profileName)?.config ?? config.defaultDeviceConfig;

        return new ProfileDeviceConfig { dpi = source.dpi, pollingRate = source.pollingRate };
    }

    private static DeviceConfig WithProfileValues(DeviceConfig device, ProfileDeviceConfig profile)
    {
        device.dpi = profile.dpi;
        device.pollingRate = profile.pollingRate;
        return device;
    }
}
