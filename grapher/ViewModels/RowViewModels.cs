using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using grapher.Parameters;

namespace grapher.ViewModels;

public abstract partial class RowViewModel : ObservableObject
{
    private bool reloading;

    protected RowViewModel(string label, string description)
    {
        Label = label;
        Description = description;
    }

    public event EventHandler? Edited;

    public string Label { get; }

    public string Description { get; }

    [ObservableProperty]
    private bool isVisible = true;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private bool isEdited;

    [ObservableProperty]
    private string activeText = string.Empty;

    [ObservableProperty]
    private bool showActive = true;

    public void Reload()
    {
        reloading = true;
        try
        {
            HasError = false;
            PullDraft();
        }
        finally
        {
            reloading = false;
        }

        RefreshActive();
    }

    public void RefreshActive()
    {
        ActiveText = FormatActive();
        IsEdited = !HasError && ShowActive && DiffersFromActive();
    }

    protected bool IsReloading => reloading;

    protected abstract void PullDraft();

    protected abstract string FormatActive();

    protected abstract bool DiffersFromActive();

    protected void CommitEdit()
    {
        RefreshActive();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnEnabledChanged(bool value)
    {
    }

    partial void OnShowActiveChanged(bool value) => RefreshActive();

    partial void OnIsEnabledChanged(bool value) => OnEnabledChanged(value);
}

public sealed partial class NumberRowViewModel : RowViewModel
{
    private readonly Func<double> read;
    private readonly Action<double> write;
    private readonly Func<double> readActive;

    public NumberRowViewModel(string label, string description, Func<double> read, Action<double> write, Func<double> readActive, double? lockValue = null)
        : base(label, description)
    {
        this.read = read;
        this.write = write;
        this.readActive = readActive;
        LockValue = lockValue;
    }

    public double? LockValue { get; }

    public bool CanLock => LockValue.HasValue;

    public bool IsEditable => IsEnabled && !IsLocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    private bool isLocked;

    [ObservableProperty]
    private string text = string.Empty;

    public double Value => read();

    protected override void PullDraft()
    {
        Text = NumberText.Field(read());
    }

    protected override string FormatActive() => NumberText.Active(readActive());

    protected override bool DiffersFromActive() => read() != readActive();

    partial void OnTextChanged(string value)
    {
        if (IsReloading)
        {
            return;
        }

        if (NumberText.TryParse(value, out double parsed))
        {
            HasError = false;
            write(parsed);
        }
        else
        {
            HasError = true;
        }

        CommitEdit();
    }

    partial void OnIsLockedChanged(bool value)
    {
        if (value && LockValue is double locked)
        {
            write(locked);
            Reload();
            CommitEdit();
        }
    }

    protected override void OnEnabledChanged(bool value) => OnPropertyChanged(nameof(IsEditable));
}

public sealed partial class PairRowViewModel : RowViewModel
{
    private readonly Func<double> readActiveX;
    private readonly Func<double> readActiveY;

    public PairRowViewModel(string label, string description, NumberRowViewModel x, NumberRowViewModel y, Func<double> readActiveX, Func<double> readActiveY)
        : base(label, description)
    {
        X = x;
        Y = y;
        this.readActiveX = readActiveX;
        this.readActiveY = readActiveY;
        X.Edited += (_, _) => OnPartEdited();
        Y.Edited += (_, _) => OnPartEdited();
    }

    public NumberRowViewModel X { get; }

    public NumberRowViewModel Y { get; }

    protected override void PullDraft()
    {
        X.Reload();
        Y.Reload();
    }

    protected override string FormatActive()
    {
        double x = readActiveX();
        double y = readActiveY();
        return x == y ? NumberText.Active(x) : $"X {NumberText.Active(x)}  Y {NumberText.Active(y)}";
    }

    protected override bool DiffersFromActive() => X.Value != readActiveX() || Y.Value != readActiveY();

    protected override void OnEnabledChanged(bool value)
    {
        X.IsEnabled = value;
        Y.IsEnabled = value;
    }

    private void OnPartEdited()
    {
        HasError = X.HasError || Y.HasError;
        CommitEdit();
    }
}

public sealed partial class ToggleRowViewModel : RowViewModel
{
    private readonly Func<bool> read;
    private readonly Action<bool> write;
    private readonly Func<bool> readActive;

    public ToggleRowViewModel(string label, string description, Func<bool> read, Action<bool> write, Func<bool> readActive, string onText, string offText)
        : base(label, description)
    {
        this.read = read;
        this.write = write;
        this.readActive = readActive;
        OnText = onText;
        OffText = offText;
    }

    public string OnText { get; }

    public string OffText { get; }

    [ObservableProperty]
    private bool isChecked;

    protected override void PullDraft() => IsChecked = read();

    protected override string FormatActive() => readActive() ? OnText : OffText;

    protected override bool DiffersFromActive() => read() != readActive();

    partial void OnIsCheckedChanged(bool value)
    {
        if (IsReloading)
        {
            return;
        }

        write(value);
        CommitEdit();
    }
}

public sealed partial class ChoiceRowViewModel : RowViewModel
{
    private readonly Func<int> read;
    private readonly Action<int> write;
    private readonly Func<int> readActive;

    public ChoiceRowViewModel(string label, string description, IReadOnlyList<ChoiceOption> options, Func<int> read, Action<int> write, Func<int> readActive)
        : base(label, description)
    {
        Options = options;
        this.read = read;
        this.write = write;
        this.readActive = readActive;
    }

    public IReadOnlyList<ChoiceOption> Options { get; }

    [ObservableProperty]
    private ChoiceOption? selected;

    protected override void PullDraft() => Selected = Find(read());

    protected override string FormatActive() => Find(readActive())?.Label ?? string.Empty;

    protected override bool DiffersFromActive() => read() != readActive();

    partial void OnSelectedChanged(ChoiceOption? value)
    {
        if (IsReloading || value is null)
        {
            return;
        }

        write(value.Value);
        CommitEdit();
    }

    private ChoiceOption? Find(int value) => Options.FirstOrDefault(o => o.Value == value);
}

public sealed partial class TextRowViewModel : RowViewModel
{
    private readonly Func<string> read;
    private readonly Func<string, string?> tryWrite;
    private readonly Func<string> readActive;

    public TextRowViewModel(string label, string description, Func<string> read, Func<string, string?> tryWrite, Func<string> readActive)
        : base(label, description)
    {
        this.read = read;
        this.tryWrite = tryWrite;
        this.readActive = readActive;
    }

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    protected override void PullDraft()
    {
        Text = read();
        ErrorMessage = null;
    }

    protected override string FormatActive() => readActive();

    protected override bool DiffersFromActive() => read() != readActive();

    partial void OnTextChanged(string value)
    {
        if (IsReloading)
        {
            return;
        }

        ErrorMessage = tryWrite(value);
        HasError = ErrorMessage is not null;
        CommitEdit();
    }
}
