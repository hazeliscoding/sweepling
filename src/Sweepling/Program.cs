using Avalonia;
using Sweepling.Leftovers;
using Sweepling.Windows;
using Windows.Win32;

namespace Sweepling;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--scan"])
        {
            return Scan();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>Prints the groups Sweepling would offer right now, ignoring the grace period, and closes nothing.</summary>
    private static int Scan()
    {
        // A WinExe has no console of its own: write to the one it was started from, unless output is redirected.
        if (!Console.IsOutputRedirected)
        {
            PInvoke.AttachConsole(PInvoke.ATTACH_PARENT_PROCESS);
        }

        Rules rules;
        try
        {
            rules = Rules.Load();
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var snapshot = new ProcessReader(rules.AgentPidVariable).Take();
        var groups = new Tracker(rules).Update(snapshot, ignoreGrace: true);
        Console.WriteLine(ScanReport.Json(groups, snapshot.Taken, Environment.GetEnvironmentVariable("USERPROFILE")));
        return 0;
    }
}
