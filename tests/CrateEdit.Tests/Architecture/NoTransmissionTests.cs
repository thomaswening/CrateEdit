using System.Reflection;
using CrateEdit.Core;

namespace CrateEdit.Tests.Architecture;

/// <summary>
/// Guards <see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-117">REQ-117</see>: diagnostics must never be sent anywhere automatically. Until a feature
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

    private static IEnumerable<string> ReferencedAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    private static IEnumerable<string> ReferencesStartingWith(Assembly assembly, params string[] prefixes) =>
        ReferencedAssemblies(assembly).Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));

    [Theory]
    [MemberData(nameof(ProductAssemblies))]
    public void Product_assemblies_do_not_reference_networking(string assemblyName)
    {
        // Arrange
        var assembly = Assembly.Load(assemblyName);

        // Act
        var networking = ReferencesStartingWith(assembly, ForbiddenPrefixes);

        // Assert
        Assert.Empty(networking);
    }

    [Fact]
    public void Logging_uses_only_the_local_file_sink()
    {
        // Act
        var sinks = ReferencesStartingWith(typeof(AppInfo).Assembly, "Serilog.Sinks.");

        // Assert
        Assert.Equal(["Serilog.Sinks.File"], sinks);
    }
}
