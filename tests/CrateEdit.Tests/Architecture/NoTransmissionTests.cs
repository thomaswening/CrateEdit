using System.Reflection;
using CrateEdit.Core;

namespace CrateEdit.Tests.Architecture;

/// <summary>
/// Guards REQ-117: diagnostics must never be sent anywhere automatically. Until a feature
/// legitimately needs the network, no CrateEdit assembly may reference networking libraries.
/// </summary>
[Trait("Req", "REQ-117")]
public class NoTransmissionTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "System.Net.Mail",
        "Serilog.Sinks.Http",
        "Serilog.Sinks.Seq",
    ];

    public static TheoryData<string> ProductAssemblies() =>
    [
        typeof(AppInfo).Assembly.GetName().Name!,
        typeof(App.App).Assembly.GetName().Name!,
    ];

    [Theory]
    [MemberData(nameof(ProductAssemblies))]
    public void Product_assemblies_do_not_reference_networking(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(references,
            name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void Logging_uses_only_the_local_file_sink()
    {
        var sinks = typeof(AppInfo).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("Serilog.Sinks.", StringComparison.Ordinal));

        Assert.Equal(["Serilog.Sinks.File"], sinks);
    }
}
