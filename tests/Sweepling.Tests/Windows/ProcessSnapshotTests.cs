using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Sweepling.Leftovers;
using Sweepling.Windows;

namespace Sweepling.Tests.Windows;

/// <summary>Real processes, started and stopped by the test itself; nothing else on the machine is touched.</summary>
public sealed class ProcessSnapshotTests
{
    private const int FakeAgentPid = 424242;

    [Fact]
    public void A_real_orphan_listening_on_a_port_is_offered_until_something_connects()
    {
        var marker = "sweepling-test-" + Guid.NewGuid().ToString("N")[..8];
        var port = FreePort();
        // Like the shipped rules, but the listener is Windows PowerShell, so only its console host is protected.
        var rules = Rules.Parse([$$"""
            {
              "version": 1,
              "agentPidVariable": "CLAUDE_PID",
              "agentRoots": [ { "name": "test agent", "commandLine": "{{marker}}" } ],
              "protect": [ { "name": "console host", "image": "[\\\\/]conhost\\.exe$" } ]
            }
            """], Environment.GetEnvironmentVariable);
        var tracker = new Tracker(rules);

        // The parent stands in for an agent: it starts a child that listens on the port, waits, then exits.
        // The child gives up on its own after a minute, so a failed test can't leave it running.
        var listen = $"$l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, {port}); $l.Start(); $c = $l.AcceptTcpClientAsync(); if ($c.Wait(60000)) {{ Start-Sleep -Seconds 60 }}";
        var agent = $"Start-Process -NoNewWindow -FilePath powershell.exe -ArgumentList '-NoProfile -NonInteractive -EncodedCommand {Encode(listen)}'; Start-Sleep -Seconds 2 # {marker}";
        var start = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"{agent}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // Its children inherit these pipes rather than the test runner's output, which they could hold open.
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["CLAUDE_PID"] = FakeAgentPid.ToString();

        using var parent = Process.Start(start)!;
        var born = new Dictionary<int, Proc>();
        try
        {
            // Watch while the agent runs, the way Sweepling does, so the child's session is traced.
            Proc? child = null;
            WaitUntil("the child to start", () =>
            {
                var snapshot = ProcessSnapshot.Take(rules.AgentPidVariable);
                tracker.Update(snapshot);
                foreach (var p in snapshot.Processes.Where(p => p.ParentPid == parent.Id))
                {
                    born.TryAdd(p.Pid, p);
                }

                child = born.Values.FirstOrDefault(p => p.Name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase));
                return child is not null;
            });
            var pid = child!.Pid;
            Assert.True(parent.WaitForExit(TimeSpan.FromSeconds(30)), "the agent didn't exit");

            var listening = WaitFor("the child to listen", () => ProcessSnapshot.Take(rules.AgentPidVariable), s => s.Listening.GetValueOrDefault(pid)?.Contains(port) == true);
            Assert.Equal(FakeAgentPid, Assert.Single(listening.Processes, p => p.Pid == pid).AgentPid);
            var group = Assert.Single(tracker.Update(listening, ignoreGrace: true), g => g.Members.Any(m => m.Pid == pid));
            Assert.Equal((SessionState.Ended, Tie.Traced, port), (group.Session, group.Tie, Assert.Single(group.Ports)));

            using var client = new TcpClient();
            client.Connect(IPAddress.Loopback, port);
            var connected = WaitFor("the connection", () => ProcessSnapshot.Take(rules.AgentPidVariable), s => s.Connected.Contains(pid));
            Assert.DoesNotContain(tracker.Update(connected, ignoreGrace: true), g => g.Members.Any(m => m.Pid == pid));
        }
        finally
        {
            Stop(parent);
            foreach (var p in born.Values)
            {
                StopIfStill(p);
            }
        }
    }

    [Fact]
    public void A_snapshot_sees_this_users_processes_but_not_itself_or_the_systems()
    {
        var marker = "sweepling-test-" + Guid.NewGuid().ToString("N")[..8];
        using var sleeper = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30 # {marker}\"") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            var snapshot = ProcessSnapshot.Take("CLAUDE_PID");

            var seen = Assert.Single(snapshot.Processes, p => p.Pid == sleeper.Id);
            Assert.Contains(marker, seen.CommandLine);
            Assert.EndsWith(@"\powershell.exe", seen.Image, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(new DateTimeOffset(sleeper.StartTime), seen.Started);
            Assert.Equal(Environment.ProcessId, seen.ParentPid);
            Assert.DoesNotContain(snapshot.Processes, p => p.Pid == Environment.ProcessId);
            Assert.DoesNotContain(snapshot.Processes, p => p.Name.Equals("lsass.exe", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Stop(sleeper);
        }
    }

    private static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static void WaitUntil(string what, Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            Thread.Sleep(100);
        }
    }

    private static Snapshot WaitFor(string what, Func<Snapshot> take, Func<Snapshot, bool> done)
    {
        Snapshot snapshot = take();
        WaitUntil(what, () => done(snapshot = take()));
        return snapshot;
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    // Only the process the test started: the PID must still belong to it.
    private static void StopIfStill(Proc started)
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
