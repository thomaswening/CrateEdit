using Avalonia;
using CrateEdit.Core;
using CrateEdit.Core.Diagnostics;
using Serilog;

namespace CrateEdit.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Log.Logger = DiagnosticLog.Create(AppPaths.Default, PathRedactor.ForCurrentUser, TimeProvider.System);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log.Error(e.Exception, "Unobserved task exception");

        try
        {
            Log.Information("{App} {Version} starting on {OS}",
                AppInfo.Name, typeof(Program).Assembly.GetName().Version, Environment.OSVersion);
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
