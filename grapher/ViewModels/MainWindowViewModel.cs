using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using grapher.Charts;
using grapher.Parameters;
using grapher.Platform;
using grapher.Settings;
using grapher.Theming;

namespace grapher.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const double MaxMoveIntervalMs = 100;

    private readonly DriverSession session;
    private readonly AppPaths paths;
    private readonly ThemeService themes;
    private readonly Stopwatch moveTimer = Stopwatch.StartNew();
    private readonly DispatcherTimer frameTimer;
    private Profile draft = new();
    private bool loading;
    private bool previewQueued;
    private bool dotsDirty;
    private DotSet? pendingDots;
    private (int X, int Y, bool Normalized) lastMove;

    public MainWindowViewModel(DriverSession session, GuiSettings gui, AppPaths paths, ThemeService themes, Version driverVersion)
    {
        this.session = session;
        this.paths = paths;
        this.themes = themes;
        Gui = gui;
        DriverVersion = driverVersion;

        Sensitivity = new NumberRowViewModel(
            "Sens multiplier",
            "Multiplies the final output, like changing DPI. At 1000 DPI, 0.8 feels like 800 DPI.",
            () => draft.outputDPI / CurveSampler.NormalizedDpi,
            v => draft.outputDPI = v * CurveSampler.NormalizedDpi,
            () => session.ActiveProfile.outputDPI / CurveSampler.NormalizedDpi);
        VerticalRatio = new NumberRowViewModel(
            "Y/X ratio",
            "Vertical sensitivity relative to horizontal, applied to vertical output only. Lock it to keep it at 1.",
            () => draft.yxOutputDPIRatio,
            v => draft.yxOutputDPIRatio = v,
            () => session.ActiveProfile.yxOutputDPIRatio,
            lockValue: 1);
        Rotation = new NumberRowViewModel(
            "Rotation",
            "Rotates input by this many degrees, to correct for the angle you hold the mouse at.",
            () => draft.rotation,
            v => draft.rotation = v,
            () => session.ActiveProfile.rotation);

        Domain = new PairRowViewModel(
            "Domain",
            "Stretches input speed separately for horizontal (X) and vertical (Y) movement. A Y of 2 makes vertical movements reach offsets and caps at half the speed.",
            new NumberRowViewModel("Domain X", string.Empty, () => draft.domainXY.x, v => draft.domainXY.x = v, () => session.ActiveProfile.domainXY.x),
            new NumberRowViewModel("Domain Y", string.Empty, () => draft.domainXY.y, v => draft.domainXY.y = v, () => session.ActiveProfile.domainXY.y),
            () => session.ActiveProfile.domainXY.x,
            () => session.ActiveProfile.domainXY.y);
        Range = new PairRowViewModel(
            "Range",
            "Scales how far the curve moves away from 1 for horizontal (X) and vertical (Y) movement. A Y of 0.5 halves the effect for vertical movement.",
            new NumberRowViewModel("Range X", string.Empty, () => draft.rangeXY.x, v => draft.rangeXY.x = v, () => session.ActiveProfile.rangeXY.x),
            new NumberRowViewModel("Range Y", string.Empty, () => draft.rangeXY.y, v => draft.rangeXY.y = v, () => session.ActiveProfile.rangeXY.y),
            () => session.ActiveProfile.rangeXY.x,
            () => session.ActiveProfile.rangeXY.y);
        LpNorm = new NumberRowViewModel(
            "Lp norm",
            "How horizontal and vertical movement combine into one speed. 2 is real-world distance and is right for almost everyone. Only used in Whole mode.",
            () => draft.inputSpeedArgs.lpNorm,
            v => draft.inputSpeedArgs.lpNorm = v,
            () => session.ActiveProfile.inputSpeedArgs.lpNorm);

        GlobalRows = new ObservableCollection<RowViewModel> { Sensitivity, VerticalRatio, Rotation };
        AnisotropyRows = new ObservableCollection<RowViewModel> { Domain, Range, LpNorm };

        foreach (var row in GlobalRows.Concat(AnisotropyRows))
        {
            row.Edited += OnDraftEdited;
        }

        EditorX.Edited += OnDraftEdited;
        EditorY.Edited += OnDraftEdited;

        showVelocityAndGain = gui.ShowVelocityAndGain;
        showLastMouseMove = gui.ShowLastMouseMove;
        autoApplyOnStartup = gui.AutoWriteToDriverOnStartup;
        chartDpiText = gui.DPI.ToString();
        chartPollRateText = gui.PollRate.ToString();
        selectedTheme = themes.SelectedName;

        session.ActiveChanged += (_, _) => LoadFromSession();
        session.DevicesChanged += (_, _) => DevicesChanged?.Invoke(this, EventArgs.Empty);

        frameTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnFrame);
        frameTimer.Start();

        LoadFromSession();
    }

    public event EventHandler? ChartsChanged;

    public event EventHandler? DotsChanged;

    public event EventHandler? DevicesChanged;

    public event EventHandler? DeviceMenuRequested;

    public event EventHandler? AboutRequested;

    public GuiSettings Gui { get; }

    public DriverSession Session => session;

    public Version DriverVersion { get; }

    public ThemeService Themes => themes;

    public NumberRowViewModel Sensitivity { get; }

    public NumberRowViewModel VerticalRatio { get; }

    public NumberRowViewModel Rotation { get; }

    public PairRowViewModel Domain { get; }

    public PairRowViewModel Range { get; }

    public NumberRowViewModel LpNorm { get; }

    public ObservableCollection<RowViewModel> GlobalRows { get; }

    public ObservableCollection<RowViewModel> AnisotropyRows { get; }

    public AxisEditorViewModel EditorX { get; } = new();

    public AxisEditorViewModel EditorY { get; } = new();

    public IReadOnlyList<string> ThemeNames => themes.Names;

    public bool IsByComponent
    {
        get => !IsWhole;
        set => IsWhole = !value;
    }

    public bool ShowEditorY => !IsWhole && !LockXY;

    public bool CanApply => ValidationMessage is null && !IsApplying;

    public bool CanReset => !IsApplying;

    public bool CanRevert => HasUnappliedChanges && !IsApplying;

    public bool HasStatus => StatusMessage is not null;

    public bool HasValidationMessage => ValidationMessage is not null;

    public DotSet? LastDots { get; private set; }

    public double ChartMaxSpeed => CurveSampler.MaxSpeed(Gui.DPI);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsByComponent), nameof(ShowEditorY))]
    private bool isWhole = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditorY))]
    private bool lockXY = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply), nameof(HasValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private string? validationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? statusMessage;

    [ObservableProperty]
    private bool statusIsError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply), nameof(CanReset), nameof(CanRevert))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(ResetCommand), nameof(RevertCommand), nameof(OpenDeviceMenuCommand))]
    private bool isApplying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRevert))]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private bool hasUnappliedChanges;

    [ObservableProperty]
    private CurveSet? previewCurves;

    [ObservableProperty]
    private CurveSet? appliedCurves;

    [ObservableProperty]
    private bool showVelocityAndGain;

    [ObservableProperty]
    private bool showLastMouseMove;

    [ObservableProperty]
    private bool autoApplyOnStartup;

    [ObservableProperty]
    private string chartDpiText;

    [ObservableProperty]
    private bool chartDpiHasError;

    [ObservableProperty]
    private string chartPollRateText;

    [ObservableProperty]
    private bool chartPollRateHasError;

    [ObservableProperty]
    private string lastMoveText = FormatLastMove(0, 0, false);

    [ObservableProperty]
    private string selectedTheme;

    [ObservableProperty]
    private bool isAnisotropyExpanded;

    public void ShowStartupMessage(string? message)
    {
        if (message is not null)
        {
            ShowStatus(message, isError: false);
        }
    }

    public async Task WatchActivation(Task activation)
    {
        try
        {
            await activation;
        }
        catch (Exception e)
        {
            ShowStatus($"The driver could not be updated: {e.Message}", isError: true);
        }
    }

    public void OnMouseMoved(RawMouseMove move)
    {
        if (!session.TrackedDevices.TryGetValue(move.Device, out bool normalized))
        {
            return;
        }

        double time = moveTimer.Elapsed.TotalMilliseconds;
        moveTimer.Restart();
        time = Math.Max(Math.Min(time, MaxMoveIntervalMs), 0.8 * 1000.0 / Gui.PollRate);

        double x = move.X;
        double y = move.Y;
        var profile = session.ActiveProfile;

        if (profile.lrOutputDPIRatio > 0 && x < 0)
        {
            x /= profile.lrOutputDPIRatio;
        }

        if (profile.udOutputDPIRatio > 0 && y < 0)
        {
            y /= profile.udOutputDPIRatio;
        }

        lastMove = (move.X, move.Y, normalized);
        pendingDots = AppliedCurves?.FindDots(x, y, time);
        dotsDirty = true;
    }

    public void OnDevicesChanged() => session.UpdateSystemDevices(MultiHandleDevice.GetList().ToList());

    public async Task ApplyDevices(DeviceConfig defaults, IReadOnlyList<DeviceOverride> overrides)
    {
        var result = session.ApplyDevices(defaults, overrides, ComposeDraft());
        await Finish(result);
    }

    public void SaveGuiSettings()
    {
        Gui.ShowVelocityAndGain = ShowVelocityAndGain;
        Gui.ShowLastMouseMove = ShowLastMouseMove;
        Gui.AutoWriteToDriverOnStartup = AutoApplyOnStartup;
        Gui.CurrentColorScheme = themes.SelectedName;
        Gui.TrySave(paths.GuiSettingsFile);
    }

    public void Dispose() => frameTimer.Stop();

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task Apply()
    {
        var result = session.Apply(ComposeDraft());
        await Finish(result);
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task Reset()
    {
        IsApplying = true;
        var delay = Task.Delay(TimeSpan.FromMilliseconds(DriverConfig.WriteDelayMs));
        StatusMessage = null;
        await WatchActivation(session.Reset());
        await delay;
        IsApplying = false;
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private void Revert() => LoadFromSession();

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void OpenDeviceMenu() => DeviceMenuRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void DismissStatus() => StatusMessage = null;

    [RelayCommand]
    private void SelectTheme(string name) => SelectedTheme = name;

    private async Task Finish(ApplyResult result)
    {
        if (!result.Succeeded)
        {
            ShowStatus(result.Errors!, isError: true);
            return;
        }

        SaveGuiSettings();
        IsApplying = true;
        StatusMessage = null;
        var delay = Task.Delay(TimeSpan.FromMilliseconds(DriverConfig.WriteDelayMs));
        await WatchActivation(result.Activation);

        if (result.Warning is not null)
        {
            ShowStatus(result.Warning, isError: false);
        }

        await delay;
        IsApplying = false;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusIsError = isError;
        StatusMessage = message;
    }

    private void LoadFromSession()
    {
        loading = true;
        try
        {
            var active = session.ActiveProfile;
            draft = ProfileCopy.CreateDraft(active, session.UserProfile);
            IsWhole = draft.inputSpeedArgs.combineMagnitudes;
            LockXY = draft.argsX.IsEquivalentTo(draft.argsY);
            EditorX.Load(draft.argsX, active.argsX);
            EditorY.Load(draft.argsY, active.argsY);
            VerticalRatio.IsLocked = draft.yxOutputDPIRatio == 1;
            IsAnisotropyExpanded |=
                draft.domainXY.x != 1 || draft.domainXY.y != 1 ||
                draft.rangeXY.x != 1 || draft.rangeXY.y != 1 ||
                draft.inputSpeedArgs.lpNorm != 2;

            foreach (var row in GlobalRows.Concat(AnisotropyRows))
            {
                row.Reload();
            }

            UpdateModeState();
            AppliedCurves = CurveSampler.Sample(active, Gui.DPI);
        }
        finally
        {
            loading = false;
        }

        UpdatePreview();
    }

    partial void OnIsWholeChanged(bool value)
    {
        UpdateModeState();
        OnDraftEdited(this, EventArgs.Empty);
    }

    partial void OnLockXYChanged(bool value)
    {
        if (!loading && !value)
        {
            EditorY.Load(EditorX.Draft, session.ActiveProfile.argsY);
        }

        UpdateModeState();
        OnDraftEdited(this, EventArgs.Empty);
    }

    partial void OnShowVelocityAndGainChanged(bool value)
    {
        SaveGuiSettings();
        ChartsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnShowLastMouseMoveChanged(bool value)
    {
        SaveGuiSettings();

        if (!value)
        {
            LastDots = null;
            DotsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnAutoApplyOnStartupChanged(bool value) => SaveGuiSettings();

    partial void OnSelectedThemeChanged(string value)
    {
        themes.Select(value);
        SaveGuiSettings();
        ChartsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnChartDpiTextChanged(string value)
    {
        ChartDpiHasError = !int.TryParse(value, out int dpi) || dpi < 1;

        if (!ChartDpiHasError)
        {
            Gui.DPI = dpi;
            SaveGuiSettings();
            OnPropertyChanged(nameof(ChartMaxSpeed));
            AppliedCurves = CurveSampler.Sample(session.ActiveProfile, Gui.DPI);
            QueuePreview();
        }
    }

    partial void OnChartPollRateTextChanged(string value)
    {
        ChartPollRateHasError = !int.TryParse(value, out int pollRate) || pollRate < 1;

        if (!ChartPollRateHasError)
        {
            Gui.PollRate = pollRate;
            SaveGuiSettings();
        }
    }

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    private void UpdateModeState()
    {
        LpNorm.IsEnabled = IsWhole;
        EditorX.Title = IsWhole ? string.Empty : LockXY ? "Horizontal = Vertical" : "Horizontal (X)";
        EditorY.Title = "Vertical (Y)";
    }

    private void OnDraftEdited(object? sender, EventArgs e)
    {
        if (!loading)
        {
            QueuePreview();
        }
    }

    private void QueuePreview()
    {
        if (previewQueued)
        {
            return;
        }

        previewQueued = true;
        Dispatcher.UIThread.Post(UpdatePreview, DispatcherPriority.Background);
    }

    private void UpdatePreview()
    {
        previewQueued = false;
        var profile = ComposeDraft();
        var message = FieldErrorMessage() ?? DriverSession.Validate(profile);

        ValidationMessage = message;
        HasUnappliedChanges = !ProfileComparer.Equivalent(profile, session.ActiveProfile);
        PreviewCurves = message is null ? CurveSampler.Sample(profile, Gui.DPI) : null;
        ChartsChanged?.Invoke(this, EventArgs.Empty);
    }

    private string? FieldErrorMessage()
    {
        var editors = ShowEditorY ? new[] { EditorX, EditorY } : new[] { EditorX };

        foreach (var editor in editors)
        {
            foreach (var row in editor.Rows.OfType<TextRowViewModel>())
            {
                if (row.IsVisible && row.ErrorMessage is not null)
                {
                    return row.ErrorMessage;
                }
            }
        }

        bool invalidNumber =
            GlobalRows.Concat(AnisotropyRows).Any(r => r.IsEnabled && r.HasError) ||
            editors.Any(e => e.HasErrors);

        return invalidNumber ? "Some fields don't contain a valid number." : null;
    }

    private Profile ComposeDraft()
    {
        var profile = ProfileCopy.Clone(draft);
        profile.inputSpeedArgs.combineMagnitudes = IsWhole;
        profile.argsX = EditorX.Draft;
        profile.argsY = IsWhole || LockXY ? ProfileCopy.Clone(profile.argsX) : EditorY.Draft;

        if (!IsWhole)
        {
            profile.inputSpeedArgs.lpNorm = 2;
        }

        return profile;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (!dotsDirty)
        {
            return;
        }

        dotsDirty = false;
        LastMoveText = FormatLastMove(lastMove.X, lastMove.Y, lastMove.Normalized);

        if (ShowLastMouseMove)
        {
            LastDots = pendingDots;
            DotsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FormatLastMove(int x, int y, bool normalized) =>
        normalized ? $"Last (x, y): ({x}, {y}) (n)" : $"Last (x, y): ({x}, {y})";
}
