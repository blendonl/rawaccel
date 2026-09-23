using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using grapher.Platform;
using grapher.Theming;
using grapher.ViewModels;

namespace grapher.Views;

public partial class SpeedOverlayWindow : Window
{
    private readonly SpeedOverlayViewModel? viewModel;
    private readonly ThemeService? themes;
    private readonly Win32Properties.CustomWndProcHookCallback wndProcHook;
    private readonly Win32Properties.CustomWindowStylesCallback stylesHook;
    private readonly List<OverlayAction> registered = new();
    private bool isLocked = true;
    private OverlayHotkeys hotkeys = OverlayHotkeys.Default;

    public SpeedOverlayWindow()
    {
        InitializeComponent();
        wndProcHook = OnWndProc;
        stylesHook = (style, exStyle) => (style, OverlayInterop.ExtendedStyle(exStyle, isLocked));
        Win32Properties.AddWndProcHookCallback(this, wndProcHook);
        Win32Properties.AddWindowStylesCallback(this, stylesHook);
    }

    public SpeedOverlayWindow(SpeedOverlayViewModel viewModel, ThemeService themes)
        : this()
    {
        this.viewModel = viewModel;
        this.themes = themes;
        DataContext = viewModel;
        Graph.Source = viewModel;
        viewModel.Refreshed += OnRefreshed;
        themes.Changed += OnThemeChanged;
        ApplyScheme(themes.Current);
    }

    public event EventHandler<IReadOnlyList<Hotkey>>? HotkeysUnavailable;

    public bool IsLocked
    {
        get => isLocked;
        set
        {
            isLocked = value;
            ApplyLock();
        }
    }

    public OverlayHotkeys Hotkeys
    {
        get => hotkeys;
        set
        {
            hotkeys = value;

            if (IsVisible)
            {
                RegisterHotkeys();
            }
        }
    }

    public void Perform(OverlayAction action)
    {
        switch (action)
        {
            case OverlayAction.Lock:
                IsLocked = !IsLocked;
                break;
            case OverlayAction.Reset:
                viewModel?.ResetStatsCommand.Execute(null);
                break;
            case OverlayAction.Close:
                Close();
                break;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyLock();
        RegisterHotkeys();
        viewModel?.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        UnregisterHotkeys();
        Win32Properties.RemoveWndProcHookCallback(this, wndProcHook);
        Win32Properties.RemoveWindowStylesCallback(this, stylesHook);

        if (viewModel is not null)
        {
            viewModel.Stop();
            viewModel.Refreshed -= OnRefreshed;
        }

        if (themes is not null)
        {
            themes.Changed -= OnThemeChanged;
        }

        base.OnClosed(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!isLocked && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private IntPtr OnWndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == OverlayInterop.WmHotkey && Enum.IsDefined((OverlayAction)(int)wParam))
        {
            Perform((OverlayAction)(int)wParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private IntPtr? NativeHandle => OperatingSystem.IsWindows() && TryGetPlatformHandle() is { HandleDescriptor: "HWND" } handle
        ? handle.Handle
        : null;

    private void ApplyLock()
    {
        if (NativeHandle is { } hwnd)
        {
            OverlayInterop.SetClickThrough(hwnd, isLocked);
        }

        if (themes is not null)
        {
            ApplyScheme(themes.Current);
        }
    }

    private void RegisterHotkeys()
    {
        UnregisterHotkeys();

        if (NativeHandle is not { } hwnd)
        {
            return;
        }

        var unavailable = new List<Hotkey>();

        foreach (var (action, hotkey) in hotkeys.All)
        {
            if (OverlayInterop.RegisterHotkey(hwnd, (int)action, hotkey))
            {
                registered.Add(action);
            }
            else
            {
                unavailable.Add(hotkey);
            }
        }

        if (unavailable.Count > 0)
        {
            HotkeysUnavailable?.Invoke(this, unavailable);
        }
    }

    private void UnregisterHotkeys()
    {
        if (NativeHandle is { } hwnd)
        {
            foreach (var action in registered)
            {
                OverlayInterop.UnregisterHotkey(hwnd, (int)action);
            }
        }

        registered.Clear();
    }

    private void OnRefreshed(object? sender, EventArgs e) => Graph.InvalidateVisual();

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (themes is not null)
        {
            ApplyScheme(themes.Current);
        }
    }

    private void ApplyScheme(ColorScheme scheme)
    {
        var background = ThemeService.ToAvalonia(scheme.ChartBackground);
        var foreground = ThemeService.ToAvalonia(scheme.ChartForeground);
        var accent = ThemeService.ToAvalonia(scheme.MouseMovement);

        Background = new SolidColorBrush(background);
        Foreground = new SolidColorBrush(foreground);
        Frame.BorderBrush = isLocked ? new SolidColorBrush(foreground, 0.2) : new SolidColorBrush(accent);
        Frame.BorderThickness = new Thickness(isLocked ? 1 : 2);
        Graph.SetColors(foreground, accent);
    }

    private void OnLockClick(object? sender, RoutedEventArgs e) => IsLocked = true;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
