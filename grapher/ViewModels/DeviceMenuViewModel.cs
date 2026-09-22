using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using grapher.Settings;

namespace grapher.ViewModels;

public sealed partial class DeviceMenuViewModel : ObservableObject
{
    private readonly DriverSession session;
    private readonly DeviceConfig defaults;
    private readonly IReadOnlyList<ProfileOption> profiles;

    public DeviceMenuViewModel(DriverSession session)
    {
        this.session = session;
        defaults = session.UserConfig.defaultDeviceConfig;
        defaultDisabled = defaults.disable;
        profiles = new[] { new ProfileOption(string.Empty, $"Default ({session.DefaultProfileName})") }
            .Concat(session.ProfileNames.Select(name => new ProfileOption(name, name)))
            .ToList();
        Rebuild();
    }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public bool HasDevices => Devices.Count > 0;

    public bool HasSelection => Selected is not null;

    [ObservableProperty]
    private bool defaultDisabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private DeviceItemViewModel? selected;

    public void Rebuild()
    {
        var previous = Devices.ToDictionary(d => d.Id);
        var selectedId = Selected?.Id;
        Devices.Clear();

        foreach (var device in session.SystemDevices)
        {
            var item = previous.TryGetValue(device.id, out var existing)
                ? existing
                : new DeviceItemViewModel(
                    device.id,
                    device.name,
                    session.FindDeviceSettings(device.id),
                    defaults,
                    () => DefaultDisabled,
                    profiles,
                    session.DefaultProfileName,
                    name => ProfileDevices.Get(session.UserConfig, name));
            Devices.Add(item);
        }

        Selected = Devices.FirstOrDefault(d => d.Id == selectedId) ?? Devices.FirstOrDefault();
        OnPropertyChanged(nameof(HasDevices));
    }

    public (DeviceConfig Defaults, IReadOnlyList<DeviceOverride> Overrides) Collect()
    {
        var newDefaults = defaults;
        newDefaults.disable = DefaultDisabled;
        var overrides = Devices.Select(d => d.ToOverride()).ToList();
        return (newDefaults, overrides);
    }

    partial void OnDefaultDisabledChanged(bool value)
    {
        foreach (var device in Devices)
        {
            device.RefreshDefaults();
        }
    }
}

public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly Func<bool> defaultDisabled;
    private readonly string defaultProfileName;
    private readonly Func<string, ProfileDeviceConfig> profileValues;
    private DeviceConfig config;
    private bool refreshing;

    public DeviceItemViewModel(
        string id,
        string name,
        DeviceSettings? existing,
        DeviceConfig defaults,
        Func<bool> defaultDisabled,
        IReadOnlyList<ProfileOption> profiles,
        string defaultProfileName,
        Func<string, ProfileDeviceConfig> profileValues)
    {
        Id = id;
        RawName = name;
        this.defaultDisabled = defaultDisabled;
        this.defaultProfileName = defaultProfileName;
        this.profileValues = profileValues;
        Profiles = profiles;
        config = existing?.config ?? defaults;
        overrideDefaults = existing is not null;
        selectedProfile = profiles.FirstOrDefault(p => p.Name.Length > 0 && p.Name == existing?.profile) ?? profiles[0];
        RefreshDefaults();
    }

    public string Id { get; }

    public string RawName { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(RawName) ? Id : RawName;

    public bool CanEdit => OverrideDefaults;

    public IReadOnlyList<ProfileOption> Profiles { get; }

    public string EffectiveProfileName =>
        OverrideDefaults && SelectedProfile.Name.Length > 0 ? SelectedProfile.Name : defaultProfileName;

    public string ListHint => Disable ? "Disabled" : $"Profile: {EffectiveProfileName}";

    public string ProfileSummary
    {
        get
        {
            var values = profileValues(EffectiveProfileName);
            return $"{ProfileDevices.Describe(values.dpi, values.pollingRate)}, from profile \"{EffectiveProfileName}\". Change them in the main window's Profile section.";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit), nameof(EffectiveProfileName), nameof(ListHint), nameof(ProfileSummary))]
    private bool overrideDefaults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListHint))]
    private bool disable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveProfileName), nameof(ListHint), nameof(ProfileSummary))]
    private ProfileOption selectedProfile;

    public void RefreshDefaults()
    {
        refreshing = true;
        Disable = OverrideDefaults ? config.disable : defaultDisabled();
        refreshing = false;
    }

    public DeviceOverride ToOverride() => new(Id, RawName, OverrideDefaults, SelectedProfile.Name, config);

    partial void OnOverrideDefaultsChanged(bool value)
    {
        if (value)
        {
            config.disable = defaultDisabled();
        }

        RefreshDefaults();
    }

    partial void OnDisableChanged(bool value)
    {
        if (!refreshing && OverrideDefaults)
        {
            config.disable = value;
        }
    }

    public override string ToString() => DisplayName;
}
