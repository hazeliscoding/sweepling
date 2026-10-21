using System.Diagnostics;
using Sweepling.Leftovers;
using Sweepling.Windows;

namespace Sweepling;

/// <summary>
/// Takes a snapshot every 5 seconds on a background thread, and hands the groups to offer to the UI thread.
/// The reader and tracker live on that one thread, as they need.
/// </summary>
internal sealed class Watcher(Rules rules, SynchronizationContext ui) : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stop = new();

    /// <summary>Raised on the UI thread with each snapshot's groups.</summary>
    public event Action<IReadOnlyList<Group>>? Updated;

    public void Start() => _ = Task.Run(Watch);

    public void Dispose() => _stop.Cancel();

    private async Task Watch()
    {
        var reader = new ProcessReader(rules.AgentPidVariable);
        var tracker = new Tracker(rules);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var groups = tracker.Update(reader.Take());
                ui.Post(_ => Updated?.Invoke(groups), null);
            }
            catch (Exception ex)
            {
                // One failed snapshot shouldn't stop the watch; the next one starts fresh.
                Trace.WriteLine($"Sweepling: snapshot failed: {ex}");
            }
        }
        while (await NextTick(timer));
    }

    private async Task<bool> NextTick(PeriodicTimer timer)
    {
        try
        {
            return await timer.WaitForNextTickAsync(_stop.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
