using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sweepling.Leftovers;

namespace Sweepling.Review;

/// <summary>What the window shows beyond the group itself, read from the running processes.</summary>
public sealed record ReviewFacts(Func<Group, string?> Folder, Func<Group, long?> Memory);

public sealed record ProcessRow(string Name, int Pid, string CommandLine);

/// <summary>One group in the review window: what it is, where, how old, which port and how much memory.</summary>
public sealed partial class GroupRow : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private string _age = "";

    [ObservableProperty]
    private string _memory = "";

    public GroupRow(Group group, string? folder, string? userProfile)
    {
        Group = group;
        What = Describe.What(group);
        Folder = folder is null ? "" : CommandLineMask.Apply(folder, userProfile);
        Port = group.Ports.Count switch
        {
            0 => "",
            1 => $"port {group.Ports[0]}",
            _ => "ports " + string.Join(", ", group.Ports),
        };
        Processes = [.. group.Members.Select(m => new ProcessRow(m.Name, m.Pid, CommandLineMask.Apply(m.CommandLine, userProfile)))];

        // A leftover's session has ended; a stray of a running session may still be needed.
        _isChecked = group.Session == SessionState.Ended;
    }

    public Group Group { get; private set; }

    public string What { get; }

    public string Folder { get; }

    public string Port { get; }

    public IReadOnlyList<ProcessRow> Processes { get; }

    internal (int Pid, DateTimeOffset Started) Key => KeyOf(Group);

    internal static (int Pid, DateTimeOffset Started) KeyOf(Group group) => (group.Strays[0].Pid, group.Strays[0].Started);

    internal void Refresh(Group group, DateTimeOffset now, long? memory)
    {
        Group = group;
        Age = AgeOf(now - group.Strays.Min(s => s.Started));
        Memory = memory is { } bytes ? Size(bytes) : "";
    }

    private static string AgeOf(TimeSpan age) => age switch
    {
        { TotalMinutes: < 1 } => "under 1 min",
        { TotalHours: < 1 } => $"{(int)age.TotalMinutes} min",
        { TotalHours: < 48 } => $"{(int)age.TotalHours} h",
        _ => $"{(int)age.TotalDays} days",
    };

    private static string Size(long bytes) => bytes < 1L << 30 ? $"{bytes >> 20} MB" : $"{bytes / (double)(1L << 30):0.0} GB";
}

/// <summary>
/// The review window: leftovers ticked, strays of running sessions never ticked, and one Close for what
/// the person ticked. Closing is the confirmation; nothing closes without it.
/// </summary>
public sealed partial class ReviewViewModel : ObservableObject
{
    private readonly ReviewFacts _facts;
    private readonly Func<Group, IReadOnlyList<CloseResult>> _close;
    private readonly Action<Group, string, string?, IReadOnlyList<CloseResult>> _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly string? _userProfile;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private bool _isClosing;

    [ObservableProperty]
    private string _status = "";

    public ReviewViewModel(
        ReviewFacts facts,
        Func<Group, IReadOnlyList<CloseResult>> close,
        Action<Group, string, string?, IReadOnlyList<CloseResult>> log,
        Func<DateTimeOffset> now,
        string? userProfile = null)
    {
        _facts = facts;
        _close = close;
        _log = log;
        _now = now;
        _userProfile = userProfile;
    }

    public ObservableCollection<GroupRow> Leftovers { get; } = [];

    public ObservableCollection<GroupRow> StillRunning { get; } = [];

    public string Heading => Leftovers.Count switch
    {
        0 => "Nothing to sweep",
        1 => "1 leftover",
        var n => $"{n} leftovers",
    };

    public string CloseLabel => Ticked().Count() switch
    {
        0 => "Close",
        1 => "Close 1 group",
        var n => $"Close {n} groups",
    };

    /// <summary>Takes the latest groups, keeping what the person ticked for the ones already listed.</summary>
    public void Show(IReadOnlyList<Group> groups)
    {
        var now = _now();
        Sync(Leftovers, groups.Where(g => g.Session == SessionState.Ended), now);
        Sync(StillRunning, groups.Where(g => g.Session == SessionState.Running), now);
        OnPropertyChanged(nameof(Heading));
        Changed();
    }

    private bool CanClose() => !IsClosing && Ticked().Any();

    [RelayCommand(CanExecute = nameof(CanClose))]
    private async Task Close()
    {
        IsClosing = true;
        try
        {
            var ticked = Ticked().ToList();
            var outcomes = await Task.Run(() => ticked.Select(row =>
            {
                var results = _close(row.Group);
                _log(row.Group, row.What, row.Folder.Length > 0 ? row.Folder : null, results);
                return (Row: row, Outcome: Overall(results));
            }).ToList());

            foreach (var (row, outcome) in outcomes)
            {
                if (outcome is CloseOutcome.Closed or CloseOutcome.Gone)
                {
                    Leftovers.Remove(row);
                    StillRunning.Remove(row);
                }
                else
                {
                    row.IsChecked = false;
                }
            }

            Status = StatusOf(outcomes.Select(o => o.Outcome).ToList());
            OnPropertyChanged(nameof(Heading));
        }
        finally
        {
            IsClosing = false;
            Changed();
        }
    }

    // A group left alone for one reason was left alone as a whole; the first reason in this order is the one shown.
    private static CloseOutcome Overall(IReadOnlyList<CloseResult> results) =>
        results.Any(r => r.Outcome == CloseOutcome.InUse) ? CloseOutcome.InUse
        : results.Any(r => r.Outcome == CloseOutcome.Failed) ? CloseOutcome.Failed
        : results.Any(r => r.Outcome == CloseOutcome.Reused) ? CloseOutcome.Reused
        : results.All(r => r.Outcome == CloseOutcome.Gone) ? CloseOutcome.Gone
        : CloseOutcome.Closed;

    private static string StatusOf(IReadOnlyList<CloseOutcome> outcomes)
    {
        static string Groups(int n) => n == 1 ? "1 group" : $"{n} groups";
        var closed = outcomes.Count(o => o is CloseOutcome.Closed or CloseOutcome.Gone);
        var inUse = outcomes.Count(o => o == CloseOutcome.InUse);
        var reused = outcomes.Count(o => o == CloseOutcome.Reused);
        var failed = outcomes.Count(o => o == CloseOutcome.Failed);
        var parts = new List<string>();
        if (closed > 0)
        {
            parts.Add($"Closed {Groups(closed)}.");
        }

        if (inUse > 0)
        {
            parts.Add(inUse == 1
                ? "Left 1 group alone: something connected to it since it was listed."
                : $"Left {inUse} groups alone: something connected to them since they were listed.");
        }

        if (reused > 0)
        {
            parts.Add($"Left {Groups(reused)} alone: a process ID now belongs to another program.");
        }

        if (failed > 0)
        {
            parts.Add($"Couldn't close {Groups(failed)}.");
        }

        return string.Join(" ", parts);
    }

    private IEnumerable<GroupRow> Ticked() => Leftovers.Concat(StillRunning).Where(r => r.IsChecked);

    private void Sync(ObservableCollection<GroupRow> rows, IEnumerable<Group> groups, DateTimeOffset now)
    {
        var latest = groups.ToDictionary(GroupRow.KeyOf);
        foreach (var gone in rows.Where(r => !latest.ContainsKey(r.Key)).ToList())
        {
            rows.Remove(gone);
            gone.PropertyChanged -= RowChanged;
        }

        var listed = rows.ToDictionary(r => r.Key);
        foreach (var (key, group) in latest)
        {
            if (!listed.TryGetValue(key, out var row))
            {
                row = new GroupRow(group, _facts.Folder(group), _userProfile);
                row.PropertyChanged += RowChanged;
                rows.Add(row);
            }

            row.Refresh(group, now, _facts.Memory(group));
        }
    }

    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupRow.IsChecked))
        {
            Changed();
        }
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CloseLabel));
        CloseCommand.NotifyCanExecuteChanged();
    }
}
