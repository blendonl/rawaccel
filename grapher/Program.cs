using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Avalonia;
using grapher.Platform;

namespace grapher;

public static class Program
{
    private const string InstanceMutexName = "RawAccelGrapher";

    [STAThread]
    public static int Main(string[] args)
    {
        using var mutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            NativeDialogs.Show("Another instance of the Raw Accel Grapher is already running.", "Raw Accel");
            return 1;
        }

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz()
            .LogToTrace();

    public static void ReportFatal(Exception exception)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "error.log"), exception.ToString());
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        NativeDialogs.Show(exception.Message, "Error");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        ReportFatal((Exception)e.ExceptionObject);
}
