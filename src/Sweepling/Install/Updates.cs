using Velopack;
using Velopack.Sources;

namespace Sweepling.Install;

/// <summary>
/// Velopack's check against GitHub Releases, the only network call Sweepling makes: a minute after it starts,
/// then every 4 hours, while updates are on. Each update downloads in the background and is announced on the
/// UI thread, where <see cref="UpdateGate"/> decides when to restart into it.
/// </summary>
public sealed class Updates : IDisposable
{
    /// <summary>A folder with a Velopack release feed, used instead of GitHub to try an update locally.</summary>
    public const string SourceVariable = "SWEEPLING_UPDATE_SOURCE";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Every = TimeSpan.FromHours(4);

    private readonly UpdateManager _manager;
    private readonly Func<bool> _enabled;
    private readonly SynchronizationContext _ui;
    private readonly CancellationTokenSource _stop = new();
    private UpdateInfo? _ready;

    public Updates(Func<bool> enabled, SynchronizationContext ui)
    {
        var source = Environment.GetEnvironmentVariable(SourceVariable);
        _manager = string.IsNullOrWhiteSpace(source)
            ? new UpdateManager(new GithubSource("https://github.com/hazeliscoding/sweepling", accessToken: null, prerelease: false))
            : new UpdateManager(source);
        _enabled = enabled;
        _ui = ui;
    }

    public event Action? Downloaded;

    public void Start() => _ = Task.Run(WatchAsync);

    /// <summary>
    /// Starts Velopack's updater, which waits for Sweepling to exit, installs the update and starts the new
    /// version. The caller then shuts down as usual, so the tray icon goes and no Sweepling is left behind.
    /// </summary>
    public void InstallAfterExit() =>
        _manager.WaitExitThenApplyUpdates(_ready?.TargetFullRelease ?? throw new InvalidOperationException("No update is downloaded."), silent: true, restart: true);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task WatchAsync()
    {
        try
        {
            await Task.Delay(FirstCheck, _stop.Token);
            using var timer = new PeriodicTimer(Every);
            do
            {
                if (_ready is null && _enabled() && await DownloadAsync() is { } update)
                {
                    _ready = update;
                    _ui.Post(_ => Downloaded?.Invoke(), null);
                }
            }
            while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<UpdateInfo?> DownloadAsync()
    {
        try
        {
            if (await _manager.CheckForUpdatesAsync() is not { } update)
            {
                return null;
            }

            await _manager.DownloadUpdatesAsync(update, null, _stop.Token);
            return update;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline, or GitHub out of reach: the next check tries again.
            return null;
        }
    }
}
