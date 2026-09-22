using Avalonia.Controls;
using Avalonia.Interactivity;

namespace grapher.Views;

public partial class DeviceMenuWindow : Window
{
    public DeviceMenuWindow()
    {
        InitializeComponent();
    }

    private void OnApply(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
