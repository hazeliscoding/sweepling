using Sweepling.Leftovers;

namespace Sweepling.Tests.Leftovers;

/// <summary>Synthetic process tables on a fake machine, so every rule can be tested without real processes.</summary>
internal static class Fake
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public const string Profile = @"C:\Users\test";
    public const string Claude = Profile + @"\.local\bin\claude.exe";
    public const string Bash = @"C:\Program Files\Git\usr\bin\bash.exe";
    public const string Node = @"C:\Program Files\nodejs\node.exe";
    public const string Cmd = @"C:\WINDOWS\system32\cmd.exe";
    public const string Tail = @"C:\Program Files\Git\usr\bin\tail.exe";
    public const string Grep = @"C:\Program Files\Git\usr\bin\grep.exe";
    public const string TaskOutput = Profile + @"\AppData\Local\Temp\claude\C--code-app\0b6c1a7e-3f0e-4c11-9d6e-5a2f4c8b9e10\tasks\b1.output";

    public static string? Env(string name) => name switch
    {
        "USERPROFILE" => Profile,
        "WINDIR" => @"C:\WINDOWS",
        "TEMP" => Profile + @"\AppData\Local\Temp",
        "LOCALAPPDATA" => Profile + @"\AppData\Local",
        _ => null,
    };

    public static Rules Rules { get; } = Sweepling.Leftovers.Rules.Parse([Sweepling.Leftovers.Rules.ShippedJson], Env);

    public static Proc P(int pid, int parent, double startedSeconds, string image, string commandLine = "", int? agentPid = null) =>
        new(pid, parent, T0.AddSeconds(startedSeconds), image, commandLine, agentPid);

    public static Snapshot At(double seconds, params Proc[] processes) =>
        new(T0.AddSeconds(seconds), processes, new HashSet<int>());

    public static Snapshot Connected(this Snapshot snapshot, params int[] pids) =>
        snapshot with { Connected = new HashSet<int>(pids) };
}
