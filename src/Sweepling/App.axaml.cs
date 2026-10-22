using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Sweepling.Install;
using Sweepling.Leftovers;
using Sweepling.Review;
using Sweepling.Windows;

namespace Sweepling;

/// <summary>
/// Sweepling lives in the tray: a watch every 5 seconds counts the leftovers on its icon and notifies when
/// the count rises, and the review window opens from the icon. Nothing closes until the person presses Close.
/// </summary>
public sealed partial class App : Application
{
    private static readonly string? UserProfile = Environment.GetEnvironmentVariable("USERPROFILE");

    private readonly ClosedLog _log = new(ClosedLog.DefaultPath, UserProfile);
    private NotifyIcon? _tray;
    private Watcher? _watcher;
    private ReviewWindow? _window;
    private IReadOnlyList<Group> _latest = [];
    private int _leftovers;
    private Updates? _updates;
    private UpdateGate? _gate;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window: Sweepling lives in the tray until Quit.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var (rules, problem) = LoadRules();
            _tray = new NotifyIcon(TrayIcons.For(0), TrayText.Tooltip([]));
            _tray.Opened += ShowReview;
            _tray.QuitChosen += () => desktop.Shutdown();
            if (problem is not null)
            {
                _tray.Notify("Sweepling's rules file has a problem", $"{problem} Sweepling is using its own rules until it's fixed.");
            }

            _watcher = new Watcher(rules, SynchronizationContext.Current!);
            _watcher.Updated += OnUpdated;
            _watcher.Start();

            if (Installation.IsInstalled)
            {
                StartUpdates(desktop);
            }

            // Removing the icon on exit keeps a dead icon from lingering in the tray.
            desktop.Exit += (_, _) =>
            {
                _updates?.Dispose();
                _watcher.Dispose();
                _tray.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens the review window, or brings it forward when it's already open.</summary>
    public void ShowReview()
    {
        if (_window is not null)
        {
            _window.Activate();
            return;
        }

        var facts = new ReviewFacts(
            g => Describe.Folder(g, ProcessDetails.CurrentDirectory, Directory.Exists),
            g => g.Members.Select(ProcessDetails.PrivateBytes).Aggregate((long?)null, (sum, bytes) => bytes is null ? sum : (sum ?? 0) + bytes));
        var model = new ReviewViewModel(facts, Closer.Close, (g, what, folder, results) => _log.Write(DateTimeOffset.Now, g, what, folder, results), () => DateTimeOffset.Now, UserProfile);
        model.Show(_latest);

        // A new window each time, released on close, so memory comes back once it's gone.
        _window = new ReviewWindow(model);
        _window.Closed += (_, _) =>
        {
            _window = null;

            // Hand the window's memory back to Windows rather than keep it for a window that may not open again.
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            _gate?.WindowClosed();
        };
        _gate?.WindowOpened();
        _window.ShowNearTray();
    }

    /// <summary>An installed copy starts at login and keeps itself up to date; the tray menu turns either off.</summary>
    private void StartUpdates(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var login = new LoginStart();
        var preferences = new Preferences();
        _tray!.Options =
        [
            new("Start at login", () => login.IsOn, () =>
            {
                if (login.IsOn)
                {
                    login.TurnOff();
                }
                else
                {
                    login.TurnOn(Environment.ProcessPath!);
                }
            }),
            new("Update automatically", () => preferences.UpdateAutomatically, () => preferences.UpdateAutomatically = !preferences.UpdateAutomatically),
        ];

        var updates = _updates = new Updates(() => preferences.UpdateAutomatically, SynchronizationContext.Current!);
        _gate = new UpdateGate(() =>
        {
            try
            {
                updates.InstallAfterExit();
            }
            catch (Exception)
            {
                // The updater didn't start: keep running, and the update installs at the next start.
                return;
            }

            desktop.Shutdown();
        });
        updates.Downloaded += _gate.Downloaded;
        updates.Start();
    }

    private static (Rules Rules, string? Problem) LoadRules()
    {
        try
        {
            return (Rules.Load(), null);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return (Rules.Parse([Rules.ShippedJson], Environment.GetEnvironmentVariable), ex.Message);
        }
    }

    private void OnUpdated(IReadOnlyList<Group> groups)
    {
        _latest = groups;
        var count = TrayText.Leftovers(groups);
        if (count != _leftovers)
        {
            _tray?.Update(TrayIcons.For(count), TrayText.Tooltip(groups));
        }

        if (TrayText.Notification(_leftovers, groups) is var (title, body))
        {
            _tray?.Notify(title, body);
        }

        _leftovers = count;
        _window?.Model.Show(groups);
    }
}

/// <summary>The tray icon for each count, built from docs/brand/tray by scripts/make-icons.cs.</summary>
internal static class TrayIcons
{
    private static readonly Dictionary<string, byte[]> Loaded = [];

    public static byte[] For(int leftovers)
    {
        var name = leftovers switch
        {
            <= 0 => "tray-0",
            > 9 => "tray-many",
            _ => $"tray-{leftovers}",
        };
        if (!Loaded.TryGetValue(name, out var icon))
        {
            using var stream = typeof(TrayIcons).Assembly.GetManifestResourceStream($"Sweepling.Tray.{name}.ico")
                ?? throw new InvalidOperationException($"The tray icon {name} is missing from the build.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            Loaded[name] = icon = copy.ToArray();
        }

        return icon;
    }
}
