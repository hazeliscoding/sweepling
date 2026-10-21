using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sweepling.Leftovers;

public enum CloseOutcome
{
    Closed,

    /// <summary>It had already ended.</summary>
    Gone,

    /// <summary>Its PID now belongs to another process, so it was left alone.</summary>
    Reused,

    /// <summary>Something in its group gained a connection since it was offered, so the group was left alone.</summary>
    InUse,

    Failed,
}

public sealed record CloseResult(Proc Process, CloseOutcome Outcome);

/// <summary>
/// The local record of what Sweepling closed: one JSON line per process, with command lines masked and the
/// profile folder replaced. It stays on this computer, and it is what the dogfooding log is written from.
/// </summary>
public sealed class ClosedLog(string path, string? userProfile)
{
    // A local file people read, never HTML: quotes and slashes stay as they are.
    private static readonly ClosedLogJson Json = new(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sweepling", "closed.log");

    public void Write(DateTimeOffset when, Group group, string what, string? folder, IReadOnlyList<CloseResult> results)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var section = group.Session == SessionState.Ended ? "leftovers" : "sessionStillRunning";
        var maskedFolder = folder is null ? null : CommandLineMask.Apply(folder, userProfile);
        var lines = results.Select(r => JsonSerializer.Serialize(
            new ClosedLine(when, what, maskedFolder, section, r.Process.Pid, r.Process.Name, r.Process.Started,
                JsonNamingPolicy.CamelCase.ConvertName(r.Outcome.ToString()), CommandLineMask.Apply(r.Process.CommandLine, userProfile)),
            Json.ClosedLine));
        File.AppendAllLines(path, lines);
    }
}

internal sealed record ClosedLine(
    DateTimeOffset Time,
    string What,
    string? Folder,
    string Section,
    int Pid,
    string Name,
    DateTimeOffset Started,
    string Outcome,
    string CommandLine);

[JsonSerializable(typeof(ClosedLine))]
internal sealed partial class ClosedLogJson : JsonSerializerContext;
