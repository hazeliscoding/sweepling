using System.Net;
using System.Net.Sockets;
using Sweepling.Leftovers;
using Sweepling.Windows;
using static Sweepling.Tests.Windows.RealProcesses;

namespace Sweepling.Tests.Windows;

/// <summary>Real processes, started and stopped by the test itself; nothing else on the machine is touched.</summary>
public sealed class ProcessReaderTests
{
    [Fact]
    public void A_real_orphan_listening_on_a_port_is_offered_until_something_connects()
    {
        using var agent = FakeAgent.StartAndOrphan();
        var pid = agent.Child.Pid;

        var listening = agent.WaitFor("the child to listen", s => s.Listening.GetValueOrDefault(pid)?.Contains(agent.Port) == true);
        Assert.Equal(FakeAgent.AgentPid, Assert.Single(listening.Processes, p => p.Pid == pid).AgentPid);
        var group = agent.GroupOf(listening);
        Assert.Equal((SessionState.Ended, Tie.Traced, agent.Port), (group.Session, group.Tie, Assert.Single(group.Ports)));

        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, agent.Port);
        var connected = agent.WaitFor("the connection", s => s.Connected.Contains(pid));
        Assert.DoesNotContain(agent.Tracker.Update(connected, ignoreGrace: true), g => g.Members.Any(m => m.Pid == pid));
    }

    [Fact]
    public void A_snapshot_sees_this_users_processes_but_not_itself_or_the_systems()
    {
        var marker = Marker();
        using var sleeper = StartSleeper(marker);
        try
        {
            var snapshot = new ProcessReader("CLAUDE_PID").Take();

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

    [Fact]
    public void A_reader_reuses_what_it_read_yet_sees_processes_start_and_end()
    {
        var reader = new ProcessReader("CLAUDE_PID");
        Assert.NotEmpty(reader.Take().Processes);
        var marker = Marker();
        using var sleeper = StartSleeper(marker);
        var started = new DateTimeOffset(sleeper.StartTime);
        try
        {
            var seen = Assert.Single(reader.Take().Processes, p => p.Pid == sleeper.Id);
            Assert.Contains(marker, seen.CommandLine);
            Assert.Equal(seen, Assert.Single(reader.Take().Processes, p => p.Pid == sleeper.Id));
        }
        finally
        {
            Stop(sleeper);
            sleeper.WaitForExit();
        }

        Assert.DoesNotContain(reader.Take().Processes, p => p.Pid == sleeper.Id && p.Started == started);
    }
}
