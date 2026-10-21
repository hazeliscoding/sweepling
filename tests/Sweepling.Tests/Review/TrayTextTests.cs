using Sweepling.Leftovers;
using Sweepling.Review;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Review;

public sealed class TrayTextTests
{
    private static Group Ended(string commandLine, params int[] ports)
    {
        var p = P(300, 200, 0, Node, commandLine);
        return new Group(SessionState.Ended, Tie.Traced, [p], [p], ports);
    }

    private static readonly Group Running = new(SessionState.Running, Tie.CommandLine, [P(400, 201, 0, Tail)], [P(400, 201, 0, Tail)], []);

    [Fact]
    public void The_tooltip_counts_leftovers_only() =>
        Assert.Equal(("Sweepling: nothing to sweep", "Sweepling: 2 leftovers"),
            (TrayText.Tooltip([Running]), TrayText.Tooltip([Ended("node next dev", 3001), Ended("node vite"), Running])));

    [Fact]
    public void A_notification_comes_only_when_the_count_rises()
    {
        Assert.Null(TrayText.Notification(previous: 2, [Ended("node next dev"), Ended("node vite")]));
        Assert.Null(TrayText.Notification(previous: 2, [Ended("node next dev")]));
        Assert.NotNull(TrayText.Notification(previous: 1, [Ended("node next dev"), Ended("node vite")]));
    }

    [Fact]
    public void A_notification_names_the_first_leftover_and_counts_the_rest()
    {
        var (title, body) = TrayText.Notification(previous: 0, [Ended("node next dev", 3001), Ended("node vite"), Running])!.Value;

        Assert.Equal("2 leftovers from ended Claude Code sessions", title);
        Assert.Equal("Next.js dev server on port 3001, and 1 more. Open Sweepling to review them.", body);
    }

    [Fact]
    public void A_single_leftover_is_named_on_its_own()
    {
        var (title, body) = TrayText.Notification(previous: 0, [Ended("node next dev")])!.Value;

        Assert.Equal(("1 leftover from an ended Claude Code session", "Next.js dev server. Open Sweepling to review it."), (title, body));
    }
}
