using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using grapher.Platform;

namespace grapher.Views;

public partial class HotkeyDialogWindow : Window
{
    private readonly OverlayHotkeys hotkeys;
    private readonly OverlayAction action;
    private Hotkey selected;

    public HotkeyDialogWindow()
        : this(OverlayHotkeys.Default, OverlayAction.Lock)
    {
    }

    public HotkeyDialogWindow(OverlayHotkeys hotkeys, OverlayAction action)
    {
        this.hotkeys = hotkeys;
        this.action = action;
        InitializeComponent();
        Prompt.Text = $"Press the key combination to {OverlayHotkeys.Describe(action)}.";
        Select(hotkeys[action]);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public Hotkey Selected => selected;

    public bool Capture(KeyModifiers modifiers, Key key)
    {
        var hotkey = new Hotkey(modifiers, key);

        if (!hotkey.IsValid)
        {
            return false;
        }

        if (hotkeys.UsedBy(hotkey, action) is { } other)
        {
            Conflict.Text = $"{hotkey} is already used to {OverlayHotkeys.Describe(other)}.";
            return false;
        }

        Select(hotkey);
        return true;
    }

    private void Select(Hotkey hotkey)
    {
        selected = hotkey;
        Combination.Text = hotkey.ToString();
        Conflict.Text = null;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Escape)
        {
            Close(null);
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Enter)
        {
            Close(selected);
        }
        else if (!Capture(e.KeyModifiers, e.Key))
        {
            return;
        }

        e.Handled = true;
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => Capture(OverlayHotkeys.Default[action].Modifiers, OverlayHotkeys.Default[action].Key);

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(selected);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
