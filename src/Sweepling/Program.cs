using Avalonia;
using Avalonia.Threading;
using Sweepling.Leftovers;
using Sweepling.Windows;
using Windows.Win32;

namespace Sweepling;

internal static class Program
{
    private const string InstanceName = @"Local\hazeliscoding.Sweepling";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args is ["--scan"])
        {
            return Scan();
        }

        // One Sweepling at a time: a second launch opens the review window of the one already running.
        using var mutex = new Mutex(true, InstanceName, out var first);
        using var show = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + ".Show");
        if (!first)
        {
            show.Set();
            return 0;
        }

        var listener = new Thread(() =>
        {
            while (show.WaitOne())
            {
                Dispatcher.UIThread.Post(() => (Application.Current as App)?.ShowReview());
            }
        })
        {
            IsBackground = true,
            Name = "Second launch",
        };
        listener.Start();

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
