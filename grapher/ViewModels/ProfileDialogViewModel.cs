using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace grapher.ViewModels;

public sealed partial class ProfileDialogViewModel : ObservableObject
{
    private readonly Func<string, string?>? validateName;

    private ProfileDialogViewModel(string title, string message, string confirmText, string initialName, Func<string, string?>? validateName)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        this.validateName = validateName;
        name = initialName;
        nameError = validateName?.Invoke(initialName);
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmText { get; }

    public bool AsksForName => validateName is not null;

    public bool HasNameError => NameError is not null;

    public bool CanConfirm => NameError is null;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameError), nameof(CanConfirm))]
    private string? nameError;

    public static ProfileDialogViewModel ForName(string title, string message, string confirmText, string initialName, Func<string, string?> validateName) =>
        new(title, message, confirmText, initialName, validateName);

    public static ProfileDialogViewModel ForConfirmation(string title, string message, string confirmText) =>
        new(title, message, confirmText, string.Empty, null);

    partial void OnNameChanged(string value) => NameError = validateName?.Invoke(value);
}
