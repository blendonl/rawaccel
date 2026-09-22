using System.Reflection;

namespace grapher.Parameters;

public static class ProfileCopy
{
    private static readonly FieldInfo[] ProfileFields = typeof(Profile).GetFields(BindingFlags.Public | BindingFlags.Instance);

    public static Profile Clone(Profile source)
    {
        var copy = new Profile();

        foreach (var field in ProfileFields)
        {
            field.SetValue(copy, field.GetValue(source));
        }

        copy.argsX = Clone(source.argsX);
        copy.argsY = Clone(source.argsY);
        return copy;
    }

    public static Profile CreateDraft(Profile active, Profile user)
    {
        var draft = Clone(active);
        draft.snap = user.snap;
        draft.maximumSpeed = user.maximumSpeed;
        draft.minimumSpeed = user.minimumSpeed;
        draft.lrOutputDPIRatio = user.lrOutputDPIRatio;
        draft.udOutputDPIRatio = user.udOutputDPIRatio;
        draft.inputSpeedArgs.inputSmoothHalflife = user.inputSpeedArgs.inputSmoothHalflife;
        draft.inputSpeedArgs.scaleSmoothHalflife = user.inputSpeedArgs.scaleSmoothHalflife;
        draft.inputSpeedArgs.outputSmoothHalflife = user.inputSpeedArgs.outputSmoothHalflife;
        return draft;
    }

    public static AccelArgs Clone(AccelArgs source)
    {
        var copy = source;
        copy.data = source.data is null ? new float[AccelArgs.MaxLutPoints * 2] : (float[])source.data.Clone();
        return copy;
    }
}
