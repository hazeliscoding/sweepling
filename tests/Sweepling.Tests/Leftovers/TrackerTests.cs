using Sweepling.Leftovers;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Leftovers;

public sealed class TrackerTests
{
    // A Claude Code session (100) runs a Git Bash shell (200), which starts a dev server (300).
    private static readonly Proc Session = P(100, 50, -100, Claude);
    private static readonly Proc Shell = P(200, 100, -50, Bash, "bash -c -l \"npm run dev\"");
    private static readonly Proc DevServer = P(300, 200, -40, Node, "node next dev --port 3001");

    private static Tracker Watching(params Proc[] born)
    {
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(-30, born));
        return tracker;
    }

    [Fact]
    public void An_orphan_of_an_ended_session_is_a_leftover_after_the_grace_period()
    {
        var tracker = Watching(Session, Shell, DevServer);
        tracker.Update(At(0, DevServer));

        var group = Assert.Single(tracker.Update(At(61, DevServer)));

        Assert.Equal(SessionState.Ended, group.Session);
        Assert.Equal(Tie.Traced, group.Tie);
        Assert.Equal([300], group.Members.Select(p => p.Pid));
    }

    [Fact]
    public void An_orphan_inside_the_grace_period_is_not_offered()
    {
        var tracker = Watching(Session, Shell, DevServer);
        tracker.Update(At(0, DevServer));

        Assert.Empty(tracker.Update(At(59, DevServer)));
    }

    [Fact]
    public void Ignoring_the_grace_period_offers_an_orphan_on_first_sight()
    {
        var tracker = Watching(Session, Shell, DevServer);

        Assert.Single(tracker.Update(At(0, DevServer), ignoreGrace: true));
    }

    [Fact]
    public void A_process_whose_parent_is_alive_is_not_offered()
    {
        var tracker = Watching(Session, Shell, DevServer);
        tracker.Update(At(0, Session, Shell, DevServer));

        Assert.Empty(tracker.Update(At(120, Session, Shell, DevServer)));
    }

    [Fact]
    public void A_parent_pid_now_held_by_a_newer_process_counts_as_gone()
    {
        var reused = P(200, 1, 10, Node, "node unrelated.js");
        var tracker = Watching(Session, Shell, DevServer);
        tracker.Update(At(0, reused, DevServer));

        var group = Assert.Single(tracker.Update(At(61, reused, DevServer)));

        Assert.Equal([300], group.Members.Select(p => p.Pid));
    }

    [Fact]
    public void A_stray_of_a_running_session_is_shown_as_still_running()
    {
        var tracker = Watching(Session, Shell, DevServer);
        tracker.Update(At(0, Session, DevServer));

        var group = Assert.Single(tracker.Update(At(61, Session, DevServer)));

        Assert.Equal(SessionState.Running, group.Session);
    }

    [Fact]
    public void Protected_processes_are_never_in_a_group()
    {
        var cmd = P(400, 300, -39, Cmd, "cmd.exe /d /s /c next dev");
        var next = P(500, 400, -38, Node, "node next-server.js");
        var tracker = Watching(Session, Shell, DevServer, cmd, next);
        tracker.Update(At(0, DevServer, cmd, next));

        var group = Assert.Single(tracker.Update(At(61, DevServer, cmd, next)));

        Assert.Equal([300, 500], group.Members.Select(p => p.Pid));
    }

    [Fact]
    public void A_group_of_only_protected_processes_is_not_offered()
    {
        var cmd = P(300, 200, -40, Cmd, "cmd.exe /d /s /c npm run dev");
        var tracker = Watching(Session, Shell, cmd);
        tracker.Update(At(0, cmd));

        Assert.Empty(tracker.Update(At(61, cmd)));
    }

    [Fact]
    public void An_established_connection_anywhere_in_the_group_keeps_it_from_being_offered()
    {
        var worker = P(400, 300, -39, Node, "node worker.js");
        var tracker = Watching(Session, Shell, DevServer, worker);
        tracker.Update(At(0, DevServer, worker));

        Assert.Empty(tracker.Update(At(61, DevServer, worker).Connected(400)));
    }

    [Fact]
    public void An_untraced_stray_is_tied_by_the_agent_pid_in_its_environment()
    {
        var stray = P(300, 200, -40, Node, "node server.js", agentPid: 100);
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, stray));

        var group = Assert.Single(tracker.Update(At(61, stray)));

        Assert.Equal(Tie.Environment, group.Tie);
        Assert.Equal(SessionState.Ended, group.Session);
    }

    [Fact]
    public void An_untraced_stray_whose_agent_is_alive_is_shown_as_still_running()
    {
        var stray = P(300, 200, -40, Node, "node server.js", agentPid: 100);
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, Session, stray));

        var group = Assert.Single(tracker.Update(At(61, Session, stray)));

        Assert.Equal(SessionState.Running, group.Session);
    }

    [Fact]
    public void An_agent_pid_now_held_by_a_newer_process_means_the_session_ended()
    {
        var stray = P(300, 200, -40, Node, "node server.js", agentPid: 100);
        var reused = P(100, 1, 5, Claude);
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(10, reused, stray));

        var group = Assert.Single(tracker.Update(At(71, reused, stray)));

        Assert.Equal(SessionState.Ended, group.Session);
    }

    [Fact]
    public void A_command_line_rule_ties_a_stray_and_an_unknown_session_counts_as_running()
    {
        var tail = P(300, 200, -40, Tail, $"tail -n +1 -f {TaskOutput}");
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, Session, tail));

        var group = Assert.Single(tracker.Update(At(61, Session, tail)));

        Assert.Equal(Tie.CommandLine, group.Tie);
        Assert.Equal(SessionState.Running, group.Session);
    }

    [Fact]
    public void A_command_line_stray_is_ended_when_every_live_agent_started_after_it()
    {
        var tail = P(300, 200, -40, Tail, $"tail -n +1 -f {TaskOutput}");
        var newer = P(100, 50, -10, Claude);
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, newer, tail));

        var group = Assert.Single(tracker.Update(At(61, newer, tail)));

        Assert.Equal(SessionState.Ended, group.Session);
    }

    [Fact]
    public void An_orphan_tied_to_no_session_is_not_shown()
    {
        var other = P(300, 200, -40, Node, "node my-own-server.js");
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, other));

        Assert.Empty(tracker.Update(At(61, other)));
    }

    [Fact]
    public void Strays_that_shared_a_dead_parent_form_one_group()
    {
        var tail = P(300, 200, -40, Tail, $"tail -n +1 -f {TaskOutput}");
        var grep = P(301, 200, -39.9, Grep, "grep --line-buffered -E .");
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, tail, grep));

        var group = Assert.Single(tracker.Update(At(61, tail, grep)));

        Assert.Equal([300, 301], group.Members.Select(p => p.Pid));
    }

    [Fact]
    public void A_live_agent_session_under_a_stray_is_never_in_its_group()
    {
        var stray = P(300, 200, -40, Bash, "bash", agentPid: 1);
        var nested = P(400, 300, -30, Claude);
        var child = P(500, 400, -20, Node, "node mcp-server.js");
        var tracker = new Tracker(Fake.Rules);
        tracker.Update(At(0, stray, nested, child));

        var group = Assert.Single(tracker.Update(At(61, stray, nested, child)));

        Assert.Equal([300], group.Members.Select(p => p.Pid));
    }

    [Fact]
    public void A_group_lists_the_ports_its_members_listen_on()
    {
        var tracker = Watching(Session, Shell, DevServer);
        var snapshot = At(0, DevServer) with { Listening = new Dictionary<int, IReadOnlyList<int>> { [300] = [3001] } };

        var group = Assert.Single(tracker.Update(snapshot, ignoreGrace: true));

        Assert.Equal([3001], group.Ports);
    }
}
