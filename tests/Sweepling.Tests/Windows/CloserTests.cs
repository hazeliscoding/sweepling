using System.Net;
using System.Net.Sockets;
using Sweepling.Leftovers;
using Sweepling.Windows;
using static Sweepling.Tests.Windows.RealProcesses;

namespace Sweepling.Tests.Windows;

public sealed class CloserTests
{
    [Fact]
    public void Closing_an_orphaned_dev_server_frees_its_port()
    {
        using var agent = FakeAgent.StartAndOrphan();
        var group = agent.GroupOf(agent.WaitFor("the child to listen", s => s.Listening.GetValueOrDefault(agent.Child.Pid)?.Contains(agent.Port) == true));

        var results = Closer.Close(group);

        Assert.Equal(CloseOutcome.Closed, Assert.Single(results, r => r.Process.Pid == agent.Child.Pid).Outcome);
        Assert.False(agent.ChildIsAlive());
        var reuse = new TcpListener(IPAddress.Loopback, agent.Port);
        reuse.Start();
        reuse.Stop();
    }

    [Fact]
    public void A_pid_that_now_belongs_to_another_process_is_not_closed()
    {
        using var sleeper = StartSleeper(Marker());
        try
        {
            var stale = new Proc(sleeper.Id, 0, new DateTimeOffset(sleeper.StartTime).AddSeconds(-1), "powershell.exe", "");

            var results = Closer.Close(new Group(SessionState.Ended, Tie.Traced, [stale], [stale], []));

            Assert.Equal(CloseOutcome.Reused, Assert.Single(results).Outcome);
            Assert.False(sleeper.HasExited);
        }
        finally
        {
            Stop(sleeper);
        }
    }

    [Fact]
    public void A_group_that_gained_a_connection_since_it_was_offered_is_not_closed()
    {
        using var agent = FakeAgent.StartAndOrphan();
        var group = agent.GroupOf(agent.WaitFor("the child to listen", s => s.Listening.GetValueOrDefault(agent.Child.Pid)?.Contains(agent.Port) == true));
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, agent.Port);
        agent.WaitFor("the connection", s => s.Connected.Contains(agent.Child.Pid));

        var results = Closer.Close(group);

        Assert.All(results, r => Assert.Equal(CloseOutcome.InUse, r.Outcome));
        Assert.True(agent.ChildIsAlive());
    }

    [Fact]
    public void A_process_that_already_ended_is_reported_gone()
    {
        var sleeper = StartSleeper(Marker());
        var ended = Assert.Single(new ProcessReader(null).Take().Processes, p => p.Pid == sleeper.Id);
        Stop(sleeper);
        sleeper.WaitForExit();
        sleeper.Dispose();

        var results = Closer.Close(new Group(SessionState.Ended, Tie.Traced, [ended], [ended], []));

        Assert.Contains(Assert.Single(results).Outcome, new[] { CloseOutcome.Gone, CloseOutcome.Reused });
    }
}
