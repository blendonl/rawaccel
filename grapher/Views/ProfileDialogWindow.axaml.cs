using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace grapher.Views;

public partial class ProfileDialogWindow : Window
{
    public ProfileDialogWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (NameBox.IsVisible)
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
