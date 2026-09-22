using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace grapher.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        GuiVersion.Text = VersionHelper.VersionString;
    }

    private async void OnProjectClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/a1xd/rawaccel"));

    private async void OnGuideClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/a1xd/rawaccel/blob/master/doc/Guide.md"));

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
