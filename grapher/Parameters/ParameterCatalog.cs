using System;
using System.Collections.Generic;
using System.Linq;

namespace grapher.Parameters;

public sealed record CurveDefinition(CurveType Type, string Name, string Description, IReadOnlyList<RowSpec> Rows)
{
    public override string ToString() => Name;
}

public static class ParameterCatalog
{
    public static readonly ToggleSpec Gain = new(
        "gain",
        "Gain",
        "On: the curve's shape is applied to gain, the slope of the velocity chart. Speeding your hand up a little always speeds the cursor up smoothly. Recommended.\n" +
        "Off (legacy): the shape is applied to sensitivity directly, as in older acceleration programs.",
        static a => a.gain,
        static (ref AccelArgs a, bool v) => a.gain = v,
        "On",
        "Legacy");

    public static readonly NumberSpec Acceleration = new(
        "acceleration",
        "Acceleration",
        "How quickly sensitivity rises as input speed increases.",
        static a => a.acceleration,
        static (ref AccelArgs a, double v) => a.acceleration = v)
    {
        IsVisible = UsesSlope,
    };

    public static readonly NumberSpec ClassicExponent = new(
        "exponentClassic",
        "Exponent",
        "Power that (acceleration × speed) is raised to. Must be greater than 1. An exponent of 2 is the Linear curve.",
        static a => a.exponentClassic,
        static (ref AccelArgs a, double v) => a.exponentClassic = v);

    public static readonly ChoiceSpec CapType = new(
        "capMode",
        "Cap type",
        "Where acceleration stops.\n" +
        "Input: at an input speed.\n" +
        "Output: when sensitivity (or gain) reaches a value.\n" +
        "Both: at a given speed and value, which sets the rate for you.",
        new[]
        {
            new ChoiceOption((int)CapMode.input, "Input"),
            new ChoiceOption((int)CapMode.output, "Output"),
            new ChoiceOption((int)CapMode.in_out, "Both"),
        },
        static a => (int)a.capMode,
        static (ref AccelArgs a, int v) => a.capMode = (CapMode)v);

    public static readonly NumberSpec CapInput = new(
        "capInput",
        "Cap input",
        "Input speed in counts/ms after which acceleration stops. 0 disables the cap, except when Cap type is Both.",
        static a => a.cap.x,
        static (ref AccelArgs a, double v) => a.cap.x = v)
    {
        IsVisible = static a => a.capMode != CapMode.output,
    };

    public static readonly NumberSpec CapOutput = new(
        "capOutput",
        "Cap output",
        "Sensitivity (or gain, with Gain on) at which acceleration stops. 0 disables the cap, except when Cap type is Both.",
        static a => a.cap.y,
        static (ref AccelArgs a, double v) => a.cap.y = v)
    {
        IsVisible = static a => a.capMode != CapMode.input,
    };

    public static readonly NumberSpec InputOffset = new(
        "inputOffset",
        "Input offset",
        "Input speed in counts/ms below which there is no acceleration. Applied as a gain offset, so there is no jolt when you cross it.",
        static a => a.inputOffset,
        static (ref AccelArgs a, double v) => a.inputOffset = v);

    public static readonly NumberSpec JumpInput = new(
        "jumpInput",
        "Jump speed",
        "Input speed in counts/ms where the jump happens.",
        static a => a.cap.x,
        static (ref AccelArgs a, double v) => a.cap.x = v);

    public static readonly NumberSpec JumpOutput = new(
        "jumpOutput",
        "Jump output",
        "Sensitivity (or gain, with Gain on) above the jump speed. Below it, sensitivity is 1.",
        static a => a.cap.y,
        static (ref AccelArgs a, double v) => a.cap.y = v);

    public static readonly NumberSpec JumpSmooth = new(
        "smoothJump",
        "Smooth",
        "0 jumps instantly at the jump speed. Up to 1 blends the jump into a small S-curve.\n" +
        "Unrelated to mouse smoothing: this only shapes the curve.",
        static a => a.smooth,
        static (ref AccelArgs a, double v) => a.smooth = v);

    public static readonly NumberSpec DecayRate = new(
        "decayRate",
        "Decay rate",
        "How quickly the curve approaches its limit. Higher reaches the limit at lower speeds.",
        static a => a.decayRate,
        static (ref AccelArgs a, double v) => a.decayRate = v);

    public static readonly NumberSpec Limit = new(
        "limit",
        "Limit",
        "Sensitivity (or gain, with Gain on) that the curve approaches at high speed.",
        static a => a.limit,
        static (ref AccelArgs a, double v) => a.limit = v);

    public static readonly NumberSpec SyncSpeed = new(
        "syncSpeed",
        "Sync speed",
        "The anchor speed in counts/ms that the curve is centred on. Sensitivity is exactly 1 here.\n" +
        "This is the most important setting: if it matches the speed you naturally judge movements by, the curve feels predictable.",
        static a => a.syncSpeed,
        static (ref AccelArgs a, double v) => a.syncSpeed = v);

    public static readonly NumberSpec Motivity = new(
        "motivity",
        "Motivity",
        "How much sensitivity changes. It goes from 1/motivity at slow speeds to motivity at fast speeds, symmetrically around the sync speed. Must be greater than 1.",
        static a => a.motivity,
        static (ref AccelArgs a, double v) => a.motivity = v);

    public static readonly NumberSpec Gamma = new(
        "gamma",
        "Gamma",
        "How quickly the change happens around the sync speed. Equivalent to the exponent in Power mode.",
        static a => a.gamma,
        static (ref AccelArgs a, double v) => a.gamma = v);

    public static readonly NumberSpec SyncSmooth = new(
        "smoothSync",
        "Smooth",
        "How gradually the curve tails in and out, between 0 and 1. Leave at 0.5 unless you have a reason to change it; 0 makes the S-shape an abrupt Z-shape.",
        static a => a.smooth,
        static (ref AccelArgs a, double v) => a.smooth = v);

    public static readonly NumberSpec Scale = new(
        "scale",
        "Scale",
        "Multiplies input speed before the exponent is applied. To match a Source engine game, use 1000 / (in-game fps).",
        static a => a.scale,
        static (ref AccelArgs a, double v) => a.scale = v)
    {
        IsVisible = UsesSlope,
    };

    public static readonly NumberSpec PowerExponent = new(
        "exponentPower",
        "Exponent",
        "Exponent of the power curve. The default m_customaccel_exponent of 1.05 in Source engine games is 0.05 here.",
        static a => a.exponentPower,
        static (ref AccelArgs a, double v) => a.exponentPower = v);

    public static readonly NumberSpec OutputOffset = new(
        "outputOffset",
        "Output offset",
        "Sensitivity the curve starts from, as a fraction of the sens multiplier. 1 matches Source engine games.\n" +
        "Ignored when Cap type is Both and Gain is off.",
        static a => a.outputOffset,
        static (ref AccelArgs a, double v) => a.outputOffset = v)
    {
        IsEnabled = static a => !(a.capMode == CapMode.in_out && !a.gain),
    };

    public static readonly ChoiceSpec LookupApplyAs = new(
        "lutApplyAs",
        "Apply as",
        "Sensitivity: each y is the sensitivity at input speed x.\nVelocity: each y is the output speed at input speed x.",
        new[]
        {
            new ChoiceOption(0, "Sensitivity"),
            new ChoiceOption(1, "Velocity"),
        },
        static a => a.gain ? 1 : 0,
        static (ref AccelArgs a, int v) => a.gain = v == 1);

    public static readonly TextSpec LookupPoints = new(
        "lutPoints",
        "Points",
        "For experts. Format: x1,y1;x2,y2;...xn,yn;\n" +
        "x is input speed in counts/ms and must increase. (0,0) is implied. 2 to 257 points.",
        LookupTable.Format,
        LookupTable.TryApply);

    public static IReadOnlyList<CurveDefinition> Curves { get; } = new[]
    {
        new CurveDefinition(
            CurveType.Linear,
            "Linear",
            "Sensitivity rises in a straight line with speed.",
            new RowSpec[] { Gain, Acceleration, CapType, CapInput, CapOutput, InputOffset }),
        new CurveDefinition(
            CurveType.Classic,
            "Classic",
            "Quake-style: (acceleration × speed) raised to an exponent.",
            new RowSpec[] { Gain, Acceleration, ClassicExponent, CapType, CapInput, CapOutput, InputOffset }),
        new CurveDefinition(
            CurveType.Jump,
            "Jump",
            "One sensitivity below a speed, another above it.",
            new RowSpec[] { Gain, JumpInput, JumpOutput, JumpSmooth }),
        new CurveDefinition(
            CurveType.Natural,
            "Natural",
            "Starts at 1 and eases towards a limit.",
            new RowSpec[] { Gain, DecayRate, Limit, InputOffset }),
        new CurveDefinition(
            CurveType.Synchronous,
            "Synchronous",
            "Changes symmetrically around an anchor speed. A good place to start.",
            new RowSpec[] { Gain, SyncSpeed, Motivity, Gamma, SyncSmooth }),
        new CurveDefinition(
            CurveType.Power,
            "Power",
            "Source engine style (m_customaccel 3).",
            new RowSpec[] { Gain, Scale, PowerExponent, CapType, CapInput, CapOutput, OutputOffset }),
        new CurveDefinition(
            CurveType.LookupTable,
            "Look-up table",
            "Your own curve from a list of points. For experts.",
            new RowSpec[] { LookupApplyAs, LookupPoints }),
        new CurveDefinition(
            CurveType.Off,
            "Off",
            "No acceleration. The sens multiplier and anisotropy still apply.",
            Array.Empty<RowSpec>()),
    };

    public static CurveDefinition For(CurveType type) => Curves.First(c => c.Type == type);

    private static bool UsesSlope(AccelArgs args) => args.capMode != CapMode.in_out;
}
