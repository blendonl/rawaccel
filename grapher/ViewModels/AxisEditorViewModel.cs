using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using grapher.Parameters;

namespace grapher.ViewModels;

public sealed partial class AxisEditorViewModel : ObservableObject
{
    private readonly List<(RowSpec Spec, RowViewModel Row)> rows = new();
    private AccelArgs draft = ProfileCopy.Clone(new Profile().argsX);
    private AccelArgs active = new Profile().argsX;
    private bool loading;

    public event EventHandler? Edited;

    public IReadOnlyList<CurveDefinition> Curves => ParameterCatalog.Curves;

    public ObservableCollection<RowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private CurveDefinition selectedCurve = ParameterCatalog.For(CurveType.Off);

    [ObservableProperty]
    private string activeCurveName = string.Empty;

    [ObservableProperty]
    private bool isCurveEdited;

    public bool HasTitle => !string.IsNullOrEmpty(Title);

    public bool HasErrors
    {
        get
        {
            foreach (var (_, row) in rows)
            {
                if (row.IsVisible && row.HasError)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public AccelArgs Draft => ProfileCopy.Clone(draft);

    public void Load(AccelArgs draftArgs, AccelArgs activeArgs)
    {
        loading = true;
        try
        {
            draft = ProfileCopy.Clone(draftArgs);
            active = activeArgs;
            SelectedCurve = ParameterCatalog.For(CurveTypes.FromArgs(draft));
            RebuildRows();
        }
        finally
        {
            loading = false;
        }

        RefreshActive();
    }

    public void SetActive(AccelArgs activeArgs)
    {
        active = activeArgs;
        RefreshActive();
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(HasTitle));

    partial void OnSelectedCurveChanged(CurveDefinition value)
    {
        if (loading || value is null)
        {
            return;
        }

        CurveTypes.Apply(value.Type, ref draft);
        RebuildRows();
        RefreshActive();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshActive()
    {
        var activeType = CurveTypes.FromArgs(active);
        bool sameCurve = activeType == SelectedCurve.Type;

        ActiveCurveName = ParameterCatalog.For(activeType).Name;
        IsCurveEdited = !sameCurve;

        foreach (var (_, row) in rows)
        {
            row.ShowActive = sameCurve;
            row.RefreshActive();
        }
    }

    private void RebuildRows()
    {
        foreach (var (_, row) in rows)
        {
            row.Edited -= OnRowEdited;
        }

        rows.Clear();
        Rows.Clear();

        foreach (var spec in SelectedCurve.Rows)
        {
            var row = CreateRow(spec);
            row.Reload();
            row.Edited += OnRowEdited;
            rows.Add((spec, row));
            Rows.Add(row);
        }

        UpdateRowStates();
    }

    private void OnRowEdited(object? sender, EventArgs e)
    {
        UpdateRowStates();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateRowStates()
    {
        foreach (var (spec, row) in rows)
        {
            row.IsVisible = spec.IsVisible(draft);
            row.IsEnabled = spec.IsEnabled(draft);
        }
    }

    private RowViewModel CreateRow(RowSpec spec) => spec switch
    {
        NumberSpec number => new NumberRowViewModel(
            number.Label,
            number.Description,
            () => number.Get(draft),
            value => number.Set(ref draft, value),
            () => number.Get(active)),
        ToggleSpec toggle => new ToggleRowViewModel(
            toggle.Label,
            toggle.Description,
            () => toggle.Get(draft),
            value => toggle.Set(ref draft, value),
            () => toggle.Get(active),
            toggle.OnText,
            toggle.OffText),
        ChoiceSpec choice => new ChoiceRowViewModel(
            choice.Label,
            choice.Description,
            choice.Options,
            () => choice.Get(draft),
            value => choice.Set(ref draft, value),
            () => choice.Get(active)),
        TextSpec text => new TextRowViewModel(
            text.Label,
            text.Description,
            () => text.Get(draft),
            value => text.Parse(ref draft, value),
            () => text.Get(active)),
        _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.GetType().Name),
    };
}
