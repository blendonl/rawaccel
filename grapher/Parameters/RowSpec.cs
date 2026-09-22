using System;
using System.Collections.Generic;

namespace grapher.Parameters;

public delegate void ArgsSetter<in T>(ref AccelArgs args, T value);

public delegate string? ArgsTextParser(ref AccelArgs args, string text);

public abstract class RowSpec
{
    protected RowSpec(string key, string label, string description)
    {
        Key = key;
        Label = label;
        Description = description;
    }

    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public Func<AccelArgs, bool> IsVisible { get; init; } = static _ => true;

    public Func<AccelArgs, bool> IsEnabled { get; init; } = static _ => true;
}

public sealed class NumberSpec : RowSpec
{
    public NumberSpec(string key, string label, string description, Func<AccelArgs, double> get, ArgsSetter<double> set)
        : base(key, label, description)
    {
        Get = get;
        Set = set;
    }

    public Func<AccelArgs, double> Get { get; }

    public ArgsSetter<double> Set { get; }
}

public sealed class ToggleSpec : RowSpec
{
    public ToggleSpec(string key, string label, string description, Func<AccelArgs, bool> get, ArgsSetter<bool> set, string onText, string offText)
        : base(key, label, description)
    {
        Get = get;
        Set = set;
        OnText = onText;
        OffText = offText;
    }

    public Func<AccelArgs, bool> Get { get; }

    public ArgsSetter<bool> Set { get; }

    public string OnText { get; }

    public string OffText { get; }
}

public sealed record ChoiceOption(int Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class ChoiceSpec : RowSpec
{
    public ChoiceSpec(string key, string label, string description, IReadOnlyList<ChoiceOption> options, Func<AccelArgs, int> get, ArgsSetter<int> set)
        : base(key, label, description)
    {
        Options = options;
        Get = get;
        Set = set;
    }

    public IReadOnlyList<ChoiceOption> Options { get; }

    public Func<AccelArgs, int> Get { get; }

    public ArgsSetter<int> Set { get; }
}

public sealed class TextSpec : RowSpec
{
    public TextSpec(string key, string label, string description, Func<AccelArgs, string> get, ArgsTextParser parse)
        : base(key, label, description)
    {
        Get = get;
        Parse = parse;
    }

    public Func<AccelArgs, string> Get { get; }

    public ArgsTextParser Parse { get; }
}
