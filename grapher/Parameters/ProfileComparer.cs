using System;
using Newtonsoft.Json;

namespace grapher.Parameters;

public static class ProfileComparer
{
    public static bool Equivalent(Profile first, Profile second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.Ordinal);

    private static string Normalize(Profile profile)
    {
        var copy = ProfileCopy.Clone(profile);

        if (copy.inputSpeedArgs.combineMagnitudes)
        {
            copy.argsY = ProfileCopy.Clone(copy.argsX);
        }

        copy.argsX = TrimTable(copy.argsX);
        copy.argsY = TrimTable(copy.argsY);
        return JsonConvert.SerializeObject(copy);
    }

    private static AccelArgs TrimTable(AccelArgs args)
    {
        args.data = args.mode == AccelMode.lut
            ? args.data[..Math.Clamp(args.length, 0, args.data.Length)]
            : Array.Empty<float>();
        return args;
    }
}
