using Avalonia;
using Avalonia.Headless;
using CrateEdit.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CrateEdit.Tests;

/// <summary>Builds the real application on Avalonia's headless platform for end-to-end tests.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
