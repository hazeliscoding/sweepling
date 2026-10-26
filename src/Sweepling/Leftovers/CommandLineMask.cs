using System.Text.RegularExpressions;

namespace Sweepling.Leftovers;

/// <summary>
/// Makes a command line safe to show or log: command lines carry tokens, keys and private paths.
/// It masks too much rather than too little.
/// </summary>
public static partial class CommandLineMask
{
    public const int MaxLength = 200;

    public static string Apply(string commandLine, string? userProfile)
    {
        var masked = commandLine;
        if (!string.IsNullOrEmpty(userProfile))
        {
            masked = masked
                .Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
                .Replace(userProfile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
                .Replace(NotLetterOrDigit().Replace(userProfile, "-"), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        masked = SecretValue().Replace(masked, "${name}${separator}<masked>");
        masked = KnownToken().Replace(masked, "<masked>");
        masked = LongOpaque().Replace(masked, "<masked>");
        return masked.Length > MaxLength ? masked[..MaxLength] + "…" : masked;
    }

    // Claude Code names a project's temp folder after its path this way, as in C--Users-<name>-code-app.
    [GeneratedRegex("[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NotLetterOrDigit();

    // The value after a name like --token=, --api-key or PASSWORD=.
    [GeneratedRegex(@"(?<name>[\w-]*(token|secret|password|passwd|api[-_]?key|auth)[\w-]*)(?<separator>=|\s+)(?!<masked>)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretValue();

    // GitHub, OpenAI and Anthropic, Slack and AWS key prefixes, and JWTs.
    [GeneratedRegex(@"(gh[pousr]_|github_pat_|sk-|xox[abpr]-|AKIA|eyJ)[A-Za-z0-9._\-]{8,}", RegexOptions.CultureInvariant)]
    private static partial Regex KnownToken();

    // A run of 32 or more letters and digits that has both, like a key or an ID.
    [GeneratedRegex(@"(?=[A-Za-z0-9+=_-]*\d)(?=[A-Za-z0-9+=_-]*[A-Za-z])[A-Za-z0-9+=_-]{32,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongOpaque();
}
