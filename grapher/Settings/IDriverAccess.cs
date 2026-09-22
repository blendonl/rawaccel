namespace grapher.Settings;

public interface IDriverAccess
{
    DriverConfig ReadActive();

    void Write(DriverConfig config);

    void Reset();
}

public sealed class DriverAccess : IDriverAccess
{
    public DriverConfig ReadActive() => DriverConfig.GetActive();

    public void Write(DriverConfig config) => config.Activate();

    public void Reset() => DriverConfig.Deactivate();
}
