namespace grapher.Parameters;

public enum CurveType
{
    Linear,
    Classic,
    Jump,
    Natural,
    Synchronous,
    Power,
    LookupTable,
    Off,
}

public static class CurveTypes
{
    public const double LinearExponent = 2;

    public static AccelMode ToMode(CurveType type) => type switch
    {
        CurveType.Linear or CurveType.Classic => AccelMode.classic,
        CurveType.Jump => AccelMode.jump,
        CurveType.Natural => AccelMode.natural,
        CurveType.Synchronous => AccelMode.synchronous,
        CurveType.Power => AccelMode.power,
        CurveType.LookupTable => AccelMode.lut,
        _ => AccelMode.noaccel,
    };

    public static CurveType FromArgs(AccelArgs args) => args.mode switch
    {
        AccelMode.classic => args.exponentClassic == LinearExponent ? CurveType.Linear : CurveType.Classic,
        AccelMode.jump => CurveType.Jump,
        AccelMode.natural => CurveType.Natural,
        AccelMode.synchronous => CurveType.Synchronous,
        AccelMode.power => CurveType.Power,
        AccelMode.lut => CurveType.LookupTable,
        _ => CurveType.Off,
    };

    public static void Apply(CurveType type, ref AccelArgs args)
    {
        args.mode = ToMode(type);

        if (type == CurveType.Linear)
        {
            args.exponentClassic = LinearExponent;
        }
    }
}
