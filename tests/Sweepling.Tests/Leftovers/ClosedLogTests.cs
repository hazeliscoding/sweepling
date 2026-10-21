using System.Text.Json;
using Sweepling.Leftovers;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Leftovers;

public sealed class ClosedLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "sweepling-test-" + Guid.NewGuid().ToString("N")[..8]);

    private string LogPath => Path.Combine(_folder, "logs", "closed.log");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Each_process_gets_a_line_with_what_happened_and_its_command_line_masked()
    {
        var tail = P(300, 200, 0, Tail, $"tail -f {TaskOutput}");
        var grep = P(301, 200, 0, Grep, "grep --line-buffered -E .");
        var group = new Group(SessionState.Ended, Tie.CommandLine, [tail, grep], [tail, grep], []);

        new ClosedLog(LogPath, Profile).Write(T0, group, "Task output watcher", Profile + @"\code\app", [new(tail, CloseOutcome.Closed), new(grep, CloseOutcome.Reused)]);

        var lines = File.ReadAllLines(LogPath).Select(l => JsonDocument.Parse(l).RootElement).ToList();
        Assert.Equal(["closed", "reused"], lines.Select(l => l.GetProperty("outcome").GetString()));
        Assert.Equal("Task output watcher", lines[0].GetProperty("what").GetString());
        Assert.Equal(@"%USERPROFILE%\code\app", lines[0].GetProperty("folder").GetString());
        Assert.Equal("leftovers", lines[0].GetProperty("section").GetString());
        Assert.StartsWith(@"tail -f %USERPROFILE%\AppData", lines[0].GetProperty("commandLine").GetString());
    }

    [Fact]
    public void The_log_reads_as_plain_text()
    {
        var node = P(300, 200, 0, Node, "\"C:\\nodejs\\node.exe\" -e require('net') vite");
        var group = new Group(SessionState.Ended, Tie.Environment, [node], [node], []);

        new ClosedLog(LogPath, Profile).Write(T0, group, "Vite dev server", null, [new(node, CloseOutcome.Closed)]);

        Assert.Contains(@"\""C:\\nodejs\\node.exe\"" -e require('net') vite", File.ReadAllText(LogPath));
    }

    [Fact]
    public void Later_closes_are_added_to_the_log()
    {
        var node = P(300, 200, 0, Node, "node next dev");
        var group = new Group(SessionState.Running, Tie.Traced, [node], [node], [3001]);
        var log = new ClosedLog(LogPath, Profile);

        log.Write(T0, group, "Next.js dev server", null, [new(node, CloseOutcome.Closed)]);
        log.Write(T0.AddMinutes(1), group, "Next.js dev server", null, [new(node, CloseOutcome.Gone)]);

        Assert.Equal(2, File.ReadAllLines(LogPath).Length);
        Assert.Contains("\"section\":\"sessionStillRunning\"", File.ReadAllLines(LogPath)[0]);
    }
}
