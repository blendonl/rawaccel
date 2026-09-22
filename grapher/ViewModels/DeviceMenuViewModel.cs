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

    public DeviceMenuViewModel(DriverSession session)
    {
        this.session = session;
        defaults = session.UserConfig.defaultDeviceConfig;
        defaultDisabled = defaults.disable;
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
                : new DeviceItemViewModel(device.id, device.name, session.FindDeviceSettings(device.id)?.config, defaults, () => DefaultDisabled);
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
    private readonly DeviceConfig defaults;
    private readonly Func<bool> defaultDisabled;
    private DeviceConfig config;
    private bool refreshing;

    public DeviceItemViewModel(string id, string name, DeviceConfig? existing, DeviceConfig defaults, Func<bool> defaultDisabled)
    {
        Id = id;
        RawName = name;
        this.defaults = defaults;
        this.defaultDisabled = defaultDisabled;
        config = existing ?? defaults;
        overrideDefaults = existing.HasValue;
        RefreshDefaults();
    }

    public string Id { get; }

    public string RawName { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(RawName) ? Id : RawName;

    public bool CanEdit => OverrideDefaults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool overrideDefaults;

    [ObservableProperty]
    private bool disable;

    [ObservableProperty]
    private decimal? dpi;

    [ObservableProperty]
    private decimal? pollingRate;

    public void RefreshDefaults()
    {
        refreshing = true;
        var shown = OverrideDefaults ? config : defaults;
        Disable = OverrideDefaults ? config.disable : defaultDisabled();
        Dpi = shown.dpi;
        PollingRate = shown.pollingRate;
        refreshing = false;
    }

    public DeviceOverride ToOverride() => new(Id, RawName, OverrideDefaults, config);

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

    partial void OnDpiChanged(decimal? value)
    {
        if (!refreshing && OverrideDefaults)
        {
            config.dpi = (int)(value ?? 0);
        }
    }

    partial void OnPollingRateChanged(decimal? value)
    {
        if (!refreshing && OverrideDefaults)
        {
            config.pollingRate = (int)(value ?? 0);
        }
    }

    public override string ToString() => DisplayName;
}
