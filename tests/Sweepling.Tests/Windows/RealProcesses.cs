using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Sweepling.Leftovers;
using Sweepling.Windows;

namespace Sweepling.Tests.Windows;

/// <summary>Helpers for tests that start real processes. They stop only what they started, checked by PID and start time.</summary>
internal static class RealProcesses
{
    public static string Marker() => "sweepling-test-" + Guid.NewGuid().ToString("N")[..8];

    // Output is redirected, because a child holding the test runner's output pipe hangs `dotnet test`.
    public static Process StartSleeper(string marker) =>
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30 # {marker}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

    public static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    public static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public static void WaitUntil(string what, Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            Thread.Sleep(100);
        }
    }

    public static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    public static void StopIfStill(Proc started)
    {
        try
        {
            using var process = Process.GetProcessById(started.Pid);
            if (new DateTimeOffset(process.StartTime) == started.Started)
            {
                process.Kill();
            }
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
    }
}

/// <summary>
/// An agent stand-in: a PowerShell parent, marked as an agent root, starts a child that listens on a free
/// port, waits, then exits, leaving the child orphaned. The child gives up on its own after a minute.
/// </summary>
internal sealed class FakeAgent : IDisposable
{
    public const int AgentPid = 424242;

    private readonly Process _parent;
    private readonly Dictionary<int, Proc> _born = [];

    private FakeAgent()
    {
        var marker = RealProcesses.Marker();
        Port = RealProcesses.FreePort();

        // Like the shipped rules, but the listener is Windows PowerShell, so only its console host is protected.
        Rules = Rules.Parse([$$"""
            {
              "version": 1,
              "agentPidVariable": "CLAUDE_PID",
              "agentRoots": [ { "name": "test agent", "commandLine": "{{marker}}" } ],
              "protect": [ { "name": "console host", "image": "[\\\\/]conhost\\.exe$" } ]
            }
            """], Environment.GetEnvironmentVariable);
        Tracker = new Tracker(Rules);
        Reader = new ProcessReader(Rules.AgentPidVariable);

        var listen = $"$l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, {Port}); $l.Start(); $c = $l.AcceptTcpClientAsync(); if ($c.Wait(60000)) {{ Start-Sleep -Seconds 60 }}";
        var agent = $"Start-Process -NoNewWindow -FilePath powershell.exe -ArgumentList '-NoProfile -NonInteractive -EncodedCommand {RealProcesses.Encode(listen)}'; Start-Sleep -Seconds 2 # {marker}";
        var start = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{agent}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["CLAUDE_PID"] = AgentPid.ToString();
        _parent = Process.Start(start)!;
    }

    public Rules Rules { get; }

    public Tracker Tracker { get; }

    public ProcessReader Reader { get; }

    public int Port { get; }

    public Proc Child { get; private set; } = null!;

    /// <summary>Watches the agent run, as Sweepling does, until it exits and its child listens on the port.</summary>
    public static FakeAgent StartAndOrphan()
    {
        var agent = new FakeAgent();
        try
        {
            agent.WatchUntilOrphaned();
            return agent;
        }
        catch
        {
            agent.Dispose();
            throw;
        }
    }

    public Snapshot WaitFor(string what, Func<Snapshot, bool> done)
    {
        var snapshot = Reader.Take();
        RealProcesses.WaitUntil(what, () => done(snapshot = Reader.Take()));
        return snapshot;
    }

    /// <summary>The group the tracker offers for the child in this snapshot, ignoring the grace period.</summary>
    public Group GroupOf(Snapshot snapshot) =>
        Assert.Single(Tracker.Update(snapshot, ignoreGrace: true), g => g.Members.Any(m => m.Pid == Child.Pid));

    public bool ChildIsAlive()
    {
        try
        {
            using var process = Process.GetProcessById(Child.Pid);
            return !process.HasExited && new DateTimeOffset(process.StartTime) == Child.Started;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        RealProcesses.Stop(_parent);
        foreach (var p in _born.Values)
        {
            RealProcesses.StopIfStill(p);
        }

        _parent.Dispose();
    }

    private void WatchUntilOrphaned()
    {
        RealProcesses.WaitUntil("the child to start", () =>
        {
            var snapshot = Reader.Take();
            Tracker.Update(snapshot);
            foreach (var p in snapshot.Processes.Where(p => p.ParentPid == _parent.Id))
            {
                _born.TryAdd(p.Pid, p);
            }

            Child = _born.Values.FirstOrDefault(p => p.Name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))!;
            return Child is not null;
        });
        Assert.True(_parent.WaitForExit(TimeSpan.FromSeconds(30)), "the agent didn't exit");
    }
}
