using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Sweepling.Leftovers;

/// <summary>
/// What Sweepling knows about agent tools, kept as data because they change how they spawn processes
/// often: which processes are agent roots, which command lines tie a stray to a session, and what is
/// never touched. The shipped rules live in <c>rules.json</c>; a user file adds to them.
/// </summary>
public sealed partial class Rules
{
    private readonly Rule[] _agentRoots;
    private readonly Rule[] _strays;
    private readonly Rule[] _protect;

    private Rules(TimeSpan grace, string? agentPidVariable, Rule[] agentRoots, Rule[] strays, Rule[] protect)
    {
        Grace = grace;
        AgentPidVariable = agentPidVariable;
        _agentRoots = agentRoots;
        _strays = strays;
        _protect = protect;
    }

    /// <summary>How long a parent must be gone before its stray is offered.</summary>
    public TimeSpan Grace { get; }

    /// <summary>The environment variable an agent sets to its own process ID in everything it starts.</summary>
    public string? AgentPidVariable { get; }

    public static string ShippedJson { get; } = ReadShipped();

    public bool IsAgentRoot(Proc process) => _agentRoots.Any(r => r.Matches(process));

    public bool TiesStray(Proc process) => _strays.Any(r => r.Matches(process));

    public bool IsProtected(Proc process) => _protect.Any(r => r.Matches(process));

    /// <summary>Reads rules files in order: their lists add up, and a later file's settings win.</summary>
    public static Rules Parse(IEnumerable<string> files, Func<string, string?> environment)
    {
        var grace = TimeSpan.FromSeconds(60);
        string? agentPidVariable = null;
        List<Rule> agentRoots = [], strays = [], protect = [];
        foreach (var json in files)
        {
            var file = Deserialize(json);
            if (file.Version != 1)
            {
                throw new InvalidDataException($"Rules file version {file.Version} isn't supported. This Sweepling reads version 1.");
            }

            if (file.GraceSeconds is { } seconds)
            {
                grace = TimeSpan.FromSeconds(seconds);
            }

            agentPidVariable = file.AgentPidVariable ?? agentPidVariable;
            agentRoots.AddRange(Compile(file.AgentRoots, environment));
            strays.AddRange(Compile(file.Strays, environment));
            protect.AddRange(Compile(file.Protect, environment));
        }

        return new Rules(grace, agentPidVariable, [.. agentRoots], [.. strays], [.. protect]);
    }

    private static RulesFile Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, RulesJson.Default.RulesFile) ?? throw new InvalidDataException("A rules file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"A rules file isn't valid: {ex.Message}", ex);
        }
    }

    private static IEnumerable<Rule> Compile(List<RuleEntry>? entries, Func<string, string?> environment) =>
        (entries ?? []).Select(e =>
        {
            var name = e.Name ?? "(unnamed)";
            if (e.Image is null && e.CommandLine is null)
            {
                throw new InvalidDataException($"The rule \"{name}\" has neither an image nor a command line pattern, so it would match everything.");
            }

            return new Rule(Pattern(name, e.Image, environment), Pattern(name, e.CommandLine, environment));
        });

    private static Regex? Pattern(string name, string? pattern, Func<string, string?> environment)
    {
        if (pattern is null)
        {
            return null;
        }

        // A missing variable becomes a pattern that never matches, rather than one that matches too much.
        var expanded = Placeholder().Replace(pattern, m => environment(m.Groups[1].Value) is { Length: > 0 } value
            ? Regex.Escape(value.TrimEnd('\\', '/')).Replace(@"\\", @"[\\/]")
            : "(?!)");
        try
        {
            return new Regex(expanded, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"The rule \"{name}\" has a pattern that isn't a valid regular expression: {ex.Message}", ex);
        }
    }

    private static string ReadShipped()
    {
        using var stream = typeof(Rules).Assembly.GetManifestResourceStream("Sweepling.rules.json")
            ?? throw new InvalidOperationException("The shipped rules are missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex("%([A-Za-z_][A-Za-z0-9_]*)%")]
    private static partial Regex Placeholder();

    private sealed record Rule(Regex? Image, Regex? CommandLine)
    {
        public bool Matches(Proc p) => (Image is null || Image.IsMatch(p.Image)) && (CommandLine is null || CommandLine.IsMatch(p.CommandLine));
    }
}

internal sealed record RulesFile(
    int Version,
    int? GraceSeconds = null,
    string? AgentPidVariable = null,
    List<RuleEntry>? AgentRoots = null,
    List<RuleEntry>? Strays = null,
    List<RuleEntry>? Protect = null);

internal sealed record RuleEntry(string? Name = null, string? Image = null, string? CommandLine = null);

// A misspelt key is an error, so a user rule can't be silently ignored.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RulesFile))]
internal sealed partial class RulesJson : JsonSerializerContext;
