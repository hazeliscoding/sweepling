using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sweepling.Leftovers;

/// <summary>What <c>--scan</c> prints: every group Sweepling would offer, as JSON, with command lines masked.</summary>
public static class ScanReport
{
    public static string Json(IEnumerable<Group> groups, DateTimeOffset taken, string? userProfile)
    {
        var report = new ScanReportJson(taken, GraceIgnored: true, [.. groups.Select(g => new ScanGroupJson(
            g.Session == SessionState.Ended ? "leftovers" : "sessionStillRunning",
            JsonNamingPolicy.CamelCase.ConvertName(g.Tie.ToString()),
            g.Ports,
            [.. g.Members.Select(m => new ScanProcessJson(m.Pid, m.Name, m.Started, g.Strays.Contains(m), CommandLineMask.Apply(m.CommandLine, userProfile)))]))]);
        return JsonSerializer.Serialize(report, ScanJson.Default.ScanReportJson);
    }
}

internal sealed record ScanReportJson(DateTimeOffset Taken, bool GraceIgnored, IReadOnlyList<ScanGroupJson> Groups);

internal sealed record ScanGroupJson(string Section, string Tie, IReadOnlyList<int> Ports, IReadOnlyList<ScanProcessJson> Processes);

internal sealed record ScanProcessJson(int Pid, string Name, DateTimeOffset Started, bool Stray, string CommandLine);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ScanReportJson))]
internal sealed partial class ScanJson : JsonSerializerContext;
