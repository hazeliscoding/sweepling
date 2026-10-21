using System.Text.RegularExpressions;

namespace Sweepling.Leftovers;

/// <summary>What a group is, in words, and the project folder a Claude Code temp path names.</summary>
public static partial class Describe
{
    // Most specific first: a dev server's own process names the group before the npm script that started it.
    private static readonly (Regex Pattern, string Label)[] Kinds =
    [
        (NextJs(), "Next.js dev server"),
        (Angular(), "Angular dev server"),
        (Vite(), "Vite dev server"),
        (Webpack(), "webpack dev server"),
        (DotnetWatch(), ".NET watch"),
        (NpmScript(), "npm ${what}"),
        (TaskOutputWatcher(), "Task output watcher"),
        (WatcherFilter(), "Watcher filter"),
        (ShellScript(), "Shell script"),
        (McpServer(), "MCP server"),
    ];

    public static string What(Group group)
    {
        foreach (var (pattern, label) in Kinds)
        {
            foreach (var member in group.Members)
            {
                if (pattern.Match(member.CommandLine) is { Success: true } match)
                {
                    return WhiteSpace().Replace(match.Result(label), " ");
                }
            }
        }

        return Path.GetFileNameWithoutExtension(group.Strays[0].Image);
    }

    /// <summary>
    /// The project folder in a Claude Code temp path, such as <c>%TEMP%\claude\C--code-gil-sweep\...</c>.
    /// Claude writes both separators and hyphens as hyphens, so the folder is rebuilt from what exists on disk;
    /// when nothing matches, the name is returned as Claude wrote it.
    /// </summary>
    public static string? ProjectFromClaudeTemp(string commandLine, Func<string, bool> directoryExists)
    {
        if (ClaudeTempProject().Match(commandLine) is not { Success: true } match)
        {
            return null;
        }

        var name = match.Groups["project"].Value;
        if (DriveAndRest().Match(name) is not { Success: true } drive)
        {
            return name;
        }

        return Rebuild(drive.Groups["drive"].Value + @":\", drive.Groups["rest"].Value.Split('-'), directoryExists, first: true) ?? name;
    }

    private static string? Rebuild(string folder, ReadOnlySpan<string> parts, Func<string, bool> directoryExists, bool first)
    {
        if (parts.IsEmpty)
        {
            return folder;
        }

        for (var take = 1; take <= parts.Length; take++)
        {
            var candidate = (first ? folder : folder + @"\") + string.Join('-', parts[..take].ToArray());
            if (directoryExists(candidate) && Rebuild(candidate, parts[take..], directoryExists, first: false) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    [GeneratedRegex(@"\bnext\s+dev\b|[\\/]next[\\/]dist[\\/]|next-server", RegexOptions.IgnoreCase)]
    private static partial Regex NextJs();

    [GeneratedRegex(@"\bng\s+serve\b", RegexOptions.IgnoreCase)]
    private static partial Regex Angular();

    [GeneratedRegex(@"[\\/]vite[\\/]bin[\\/]|\bvite(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex Vite();

    [GeneratedRegex(@"webpack-dev-server|webpack(\.js)?\s+serve\b", RegexOptions.IgnoreCase)]
    private static partial Regex Webpack();

    [GeneratedRegex(@"\bdotnet(\.exe)?""?\s+watch\b", RegexOptions.IgnoreCase)]
    private static partial Regex DotnetWatch();

    [GeneratedRegex(@"\bnpm(-cli\.js)?""?\s+(?<what>run\s+\S+|start|test)", RegexOptions.IgnoreCase)]
    private static partial Regex NpmScript();

    [GeneratedRegex(@"\btail(\.exe)?""?\s.*-f\b.*\.output\b", RegexOptions.IgnoreCase)]
    private static partial Regex TaskOutputWatcher();

    [GeneratedRegex(@"\bgrep(\.exe)?""?\s.*--line-buffered", RegexOptions.IgnoreCase)]
    private static partial Regex WatcherFilter();

    [GeneratedRegex(@"\b(ba)?sh(\.exe)?""?\s+\S+\.sh\b", RegexOptions.IgnoreCase)]
    private static partial Regex ShellScript();

    [GeneratedRegex(@"(^|[\W_])mcp([\W_]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex McpServer();

    [GeneratedRegex(@"[\\/]Temp[\\/]claude[\\/](?<project>[^\\/\s""]+)[\\/]", RegexOptions.IgnoreCase)]
    private static partial Regex ClaudeTempProject();

    [GeneratedRegex(@"^(?<drive>[A-Za-z])--(?<rest>.+)$")]
    private static partial Regex DriveAndRest();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
