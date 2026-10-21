namespace Sweepling.Leftovers;

/// <summary>
/// One of the current user's processes as a snapshot saw it. <see cref="AgentPid"/> is the agent's
/// process ID from the environment (Claude Code's <c>CLAUDE_PID</c>), when it could be read.
/// </summary>
public sealed record Proc(int Pid, int ParentPid, DateTimeOffset Started, string Image, string CommandLine, int? AgentPid = null)
{
    public string Name => Path.GetFileName(Image);

    /// <summary>A windowed (GUI) app rather than a console program: someone is likely using it.</summary>
    public bool Windowed { get; init; }
}

/// <summary>The current user's processes at one moment, with the TCP state Sweepling needs.</summary>
public sealed record Snapshot(DateTimeOffset Taken, IReadOnlyList<Proc> Processes, IReadOnlySet<int> Connected)
{
    /// <summary>Listening TCP ports by owning process ID.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> Listening { get; init; } = new Dictionary<int, IReadOnlyList<int>>();
}

public enum SessionState
{
    Ended,
    Running,
}

/// <summary>How a stray was tied to its session, strongest first.</summary>
public enum Tie
{
    Traced,
    Environment,
    CommandLine,
}

/// <summary>
/// Strays that shared a dead parent, plus their descendants: what Sweepling offers to close together.
/// <see cref="Members"/> leaves out protected processes and any live agent session.
/// </summary>
public sealed record Group(SessionState Session, Tie Tie, IReadOnlyList<Proc> Strays, IReadOnlyList<Proc> Members, IReadOnlyList<int> Ports);
