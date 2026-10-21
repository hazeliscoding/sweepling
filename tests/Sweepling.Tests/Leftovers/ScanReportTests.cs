using System.Text.Json;
using Sweepling.Leftovers;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Leftovers;

public sealed class ScanReportTests
{
    private static JsonElement Report(params Group[] groups) =>
        JsonDocument.Parse(ScanReport.Json(groups, T0, Profile)).RootElement;

    [Fact]
    public void Leftovers_and_strays_of_running_sessions_go_in_their_own_sections()
    {
        var ended = new Group(SessionState.Ended, Tie.Traced, [P(300, 200, 0, Node)], [P(300, 200, 0, Node)], [3001]);
        var running = new Group(SessionState.Running, Tie.CommandLine, [P(400, 200, 0, Tail)], [P(400, 200, 0, Tail)], []);

        var groups = Report(ended, running).GetProperty("groups").EnumerateArray().ToList();

        Assert.Equal(["leftovers", "sessionStillRunning"], groups.Select(g => g.GetProperty("section").GetString()));
        Assert.Equal(["traced", "commandLine"], groups.Select(g => g.GetProperty("tie").GetString()));
        Assert.Equal(3001, groups[0].GetProperty("ports")[0].GetInt32());
    }

    [Fact]
    public void Command_lines_are_masked()
    {
        var tail = P(300, 200, 0, Tail, $"tail -f {TaskOutput}");

        var process = Report(new Group(SessionState.Ended, Tie.CommandLine, [tail], [tail], [])).GetProperty("groups")[0].GetProperty("processes")[0];

        Assert.StartsWith(@"tail -f %USERPROFILE%\AppData", process.GetProperty("commandLine").GetString());
    }

    [Fact]
    public void A_scan_says_it_ignored_the_grace_period() =>
        Assert.True(Report().GetProperty("graceIgnored").GetBoolean());
}
