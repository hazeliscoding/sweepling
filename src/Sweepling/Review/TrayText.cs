using Sweepling.Leftovers;

namespace Sweepling.Review;

/// <summary>The tray icon's tooltip and the notification that leftovers appeared, which count leftovers only.</summary>
public static class TrayText
{
    public static int Leftovers(IReadOnlyList<Group> groups) => groups.Count(g => g.Session == SessionState.Ended);

    public static string Tooltip(IReadOnlyList<Group> groups) => Leftovers(groups) switch
    {
        0 => "Sweepling: nothing to sweep",
        1 => "Sweepling: 1 leftover",
        var n => $"Sweepling: {n} leftovers",
    };

    /// <summary>A notification when there are more leftovers than <paramref name="previous"/>, otherwise null.</summary>
    public static (string Title, string Body)? Notification(int previous, IReadOnlyList<Group> groups)
    {
        var leftovers = groups.Where(g => g.Session == SessionState.Ended).ToList();
        if (leftovers.Count <= previous)
        {
            return null;
        }

        var first = leftovers[0];
        var what = Describe.What(first) + (first.Ports.Count > 0 ? $" on port {first.Ports[0]}" : "");
        return leftovers.Count == 1
            ? ("1 leftover from an ended Claude Code session", $"{what}. Open Sweepling to review it.")
            : ($"{leftovers.Count} leftovers from ended Claude Code sessions", $"{what}, and {leftovers.Count - 1} more. Open Sweepling to review them.");
    }
}
