namespace Sweepling.Leftovers;

/// <summary>
/// Keeps the family tree of each agent session across snapshots and decides which strays to offer.
/// Windows never reparents an orphan, so a parent counts as gone when its PID is missing or now
/// belongs to a process that started after the child.
/// </summary>
public sealed class Tracker(Rules rules)
{
    // Strays of one dead parent that started this close together came from one command, like `tail -f | grep`.
    private static readonly TimeSpan SiblingWindow = TimeSpan.FromSeconds(10);

    private readonly Dictionary<Key, Key> _sessionOf = [];
    private readonly Dictionary<Key, DateTimeOffset> _orphanedAt = [];

    /// <summary>Learns from a snapshot and returns the groups to offer. <paramref name="ignoreGrace"/> is for one-off scans.</summary>
    public IReadOnlyList<Group> Update(Snapshot snapshot, bool ignoreGrace = false)
    {
        var byPid = snapshot.Processes.ToDictionary(p => p.Pid);
        Proc? ParentOf(Proc p) =>
            p.ParentPid != p.Pid && byPid.TryGetValue(p.ParentPid, out var parent) && parent.Started <= p.Started ? parent : null;

        var live = snapshot.Processes.Select(KeyOf).ToHashSet();
        Forget(_sessionOf, live);
        Forget(_orphanedAt, live);

        var children = snapshot.Processes.Where(p => ParentOf(p) is not null).ToLookup(p => p.ParentPid);
        var agentRoots = snapshot.Processes.Where(rules.IsAgentRoot).ToList();

        // Learn each process's session while its ancestry is still visible; a stray keeps what was learned.
        var seen = new Dictionary<int, Key?>();
        Key? SessionOf(Proc p)
        {
            if (seen.TryGetValue(p.Pid, out var known))
            {
                return known;
            }

            seen[p.Pid] = null;
            Key? session = rules.IsAgentRoot(p) ? KeyOf(p)
                : ParentOf(p) is { } parent && SessionOf(parent) is { } inherited ? inherited
                : _sessionOf.TryGetValue(KeyOf(p), out var learned) ? learned
                : null;
            return seen[p.Pid] = session;
        }

        foreach (var p in snapshot.Processes)
        {
            if (SessionOf(p) is { } session)
            {
                _sessionOf[KeyOf(p)] = session;
            }
        }

        var strays = snapshot.Processes.Where(p => ParentOf(p) is null && !rules.IsAgentRoot(p)).ToList();
        foreach (var stray in strays)
        {
            _orphanedAt.TryAdd(KeyOf(stray), snapshot.Taken);
        }

        (Tie Tie, SessionState State)? TieOf(Proc stray)
        {
            if (_sessionOf.TryGetValue(KeyOf(stray), out var session))
            {
                var alive = byPid.TryGetValue(session.Pid, out var root) && root.Started == session.Started;
                return (Tie.Traced, alive ? SessionState.Running : SessionState.Ended);
            }

            // The agent set its PID before starting the stray, so the agent must have started first.
            if (stray.AgentPid is { } agentPid)
            {
                var alive = byPid.TryGetValue(agentPid, out var agent) && agent.Started <= stray.Started;
                return (Tie.Environment, alive ? SessionState.Running : SessionState.Ended);
            }

            // Which session is unknown, and unknown counts as running, unless no live agent is old enough.
            if (rules.TiesStray(stray))
            {
                var possible = agentRoots.Any(r => r.Started <= stray.Started);
                return (Tie.CommandLine, possible ? SessionState.Running : SessionState.Ended);
            }

            return null;
        }

        List<Proc> Subtree(IReadOnlyList<Proc> roots)
        {
            var all = new List<Proc>(roots);
            for (var i = 0; i < all.Count; i++)
            {
                // A live agent session is never part of a group, nor is anything under it.
                all.AddRange(children[all[i].Pid].Where(c => !rules.IsAgentRoot(c)));
            }

            return all;
        }

        var groups = new List<Group>();
        foreach (var siblings in Siblings(strays))
        {
            var ties = siblings.Select(TieOf).OfType<(Tie Tie, SessionState State)>().ToList();
            if (ties.Count == 0)
            {
                continue;
            }

            var subtree = Subtree(siblings);
            if (subtree.Any(p => snapshot.Connected.Contains(p.Pid)))
            {
                continue;
            }

            var members = subtree.Where(p => !rules.IsProtected(p)).ToList();
            if (members.Count == 0)
            {
                continue;
            }

            if (!ignoreGrace && siblings.Any(s => snapshot.Taken - _orphanedAt[KeyOf(s)] < rules.Grace))
            {
                continue;
            }

            var state = ties.Any(t => t.State == SessionState.Running) ? SessionState.Running : SessionState.Ended;
            var ports = members.SelectMany(m => snapshot.Listening.GetValueOrDefault(m.Pid) ?? []).Distinct().Order().ToList();
            groups.Add(new Group(state, ties.Min(t => t.Tie), siblings, members, ports));
        }

        return groups;
    }

    private static IEnumerable<IReadOnlyList<Proc>> Siblings(IEnumerable<Proc> strays)
    {
        foreach (var byParent in strays.GroupBy(s => s.ParentPid))
        {
            var cluster = new List<Proc>();
            foreach (var stray in byParent.OrderBy(s => s.Started))
            {
                if (cluster.Count > 0 && stray.Started - cluster[0].Started > SiblingWindow)
                {
                    yield return cluster;
                    cluster = [];
                }

                cluster.Add(stray);
            }

            yield return cluster;
        }
    }

    private static void Forget<T>(Dictionary<Key, T> map, HashSet<Key> live)
    {
        foreach (var key in map.Keys.Where(k => !live.Contains(k)).ToList())
        {
            map.Remove(key);
        }
    }

    private static Key KeyOf(Proc p) => new(p.Pid, p.Started);

    private readonly record struct Key(int Pid, DateTimeOffset Started);
}
