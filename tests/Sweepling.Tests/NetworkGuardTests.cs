using System.Net.Http.Json;
using System.Reflection;

namespace Sweepling.Tests;

/// <summary>
/// No telemetry: the update check is the only network call, and it belongs to Velopack,
/// so Sweepling's own code never references a networking assembly.
/// </summary>
public sealed class NetworkGuardTests
{
    private static readonly string[] NetworkAssemblies =
    [
        "System.Net.Http",
        "System.Net.Http.Json",
        "System.Net.Mail",
        "System.Net.Requests",
        "System.Net.Sockets",
        "System.Net.WebClient",
        "System.Net.WebSockets",
        "System.Net.WebSockets.Client",
    ];

    [Fact]
    public void Sweepling_references_no_networking_assembly() =>
        Assert.Empty(NetworkReferences(typeof(App).Assembly));

    [Fact]
    public void The_guard_finds_a_networking_reference() =>
        Assert.Contains("System.Net.Http", NetworkReferences(typeof(JsonContent).Assembly));

    private static string[] NetworkReferences(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(NetworkAssemblies.Contains)];
}
