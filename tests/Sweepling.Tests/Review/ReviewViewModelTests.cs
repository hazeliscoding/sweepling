using Sweepling.Leftovers;
using Sweepling.Review;
using static Sweepling.Tests.Leftovers.Fake;

namespace Sweepling.Tests.Review;

public sealed class ReviewViewModelTests
{
    private static readonly Proc NextDev = P(300, 200, -3600 * 35, Node, @"node C:\app\node_modules\next\dist\bin\next dev");
    private static readonly Proc Watcher = P(400, 201, -600, Tail, $"tail -f {TaskOutput}");

    private static readonly Group Leftover = new(SessionState.Ended, Tie.Traced, [NextDev], [NextDev], [3001]);
    private static readonly Group Running = new(SessionState.Running, Tie.CommandLine, [Watcher], [Watcher], []);

    private readonly List<Group> _closed = [];
    private readonly List<(Group Group, string What)> _logged = [];

    private ReviewViewModel Model(Func<Group, CloseOutcome>? outcome = null) => new(
        new ReviewFacts(_ => @"C:\repos\pdfcraft", _ => 212L << 20),
        g =>
        {
            _closed.Add(g);
            return [.. g.Members.Select(m => new CloseResult(m, outcome?.Invoke(g) ?? CloseOutcome.Closed))];
        },
        (g, what, _, _) => _logged.Add((g, what)),
        () => T0);

    [Fact]
    public void Leftovers_and_strays_of_running_sessions_are_listed_apart()
    {
        var model = Model();

        model.Show([Leftover, Running]);

        Assert.Equal([Leftover], model.Leftovers.Select(r => r.Group));
        Assert.Equal([Running], model.StillRunning.Select(r => r.Group));
    }

    [Fact]
    public void Leftovers_start_ticked_and_strays_of_running_sessions_never_do()
    {
        var model = Model();

        model.Show([Leftover, Running]);

        Assert.True(model.Leftovers[0].IsChecked);
        Assert.False(model.StillRunning[0].IsChecked);
    }

    [Fact]
    public void A_row_says_what_where_how_old_which_port_and_how_much_memory()
    {
        var model = Model();

        model.Show([Leftover]);

        var row = model.Leftovers[0];
        Assert.Equal(("Next.js dev server", @"C:\repos\pdfcraft", "35 h", "port 3001", "212 MB"), (row.What, row.Folder, row.Age, row.Port, row.Memory));
    }

    [Fact]
    public void A_refresh_keeps_what_the_person_ticked()
    {
        var model = Model();
        model.Show([Leftover, Running]);
        model.Leftovers[0].IsChecked = false;
        model.StillRunning[0].IsChecked = true;

        model.Show([Leftover, Running]);

        Assert.False(model.Leftovers[0].IsChecked);
        Assert.True(model.StillRunning[0].IsChecked);
    }

    [Fact]
    public async Task Close_closes_and_logs_only_the_ticked_groups()
    {
        var model = Model();
        model.Show([Leftover, Running]);

        await model.CloseCommand.ExecuteAsync(null);

        Assert.Equal([Leftover], _closed);
        Assert.Equal([(Leftover, "Next.js dev server")], _logged);
        Assert.Empty(model.Leftovers);
        Assert.Equal("Closed 1 group.", model.Status);
    }

    [Fact]
    public async Task A_group_left_alone_stays_listed_and_says_why()
    {
        var model = Model(_ => CloseOutcome.InUse);
        model.Show([Leftover]);

        await model.CloseCommand.ExecuteAsync(null);

        Assert.Single(model.Leftovers);
        Assert.False(model.Leftovers[0].IsChecked);
        Assert.Equal("Left 1 group alone: something connected to it since it was listed.", model.Status);
    }

    [Fact]
    public void Close_is_named_by_how_many_groups_are_ticked_and_off_when_none_are()
    {
        var model = Model();
        model.Show([Leftover, Running]);
        Assert.Equal(("Close 1 group", true), (model.CloseLabel, model.CloseCommand.CanExecute(null)));

        model.StillRunning[0].IsChecked = true;
        Assert.Equal("Close 2 groups", model.CloseLabel);

        model.Leftovers[0].IsChecked = false;
        model.StillRunning[0].IsChecked = false;
        Assert.False(model.CloseCommand.CanExecute(null));
    }

    [Fact]
    public void The_heading_counts_the_leftovers()
    {
        var model = Model();
        Assert.Equal("Nothing to sweep", model.Heading);

        model.Show([Leftover, Running]);
        Assert.Equal("1 leftover", model.Heading);
    }
}
