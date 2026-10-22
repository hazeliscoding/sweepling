using Sweepling.Install;

namespace Sweepling.Tests.Install;

public sealed class UpdateGateTests
{
    private int _restarts;

    [Fact]
    public void A_downloaded_update_restarts_at_once_while_the_window_is_closed()
    {
        var gate = new UpdateGate(() => _restarts++);

        gate.Downloaded();

        Assert.Equal(1, _restarts);
    }

    [Fact]
    public void An_update_waits_for_the_review_window_to_close()
    {
        var gate = new UpdateGate(() => _restarts++);
        gate.WindowOpened();

        gate.Downloaded();
        Assert.Equal(0, _restarts);

        gate.WindowClosed();
        Assert.Equal(1, _restarts);
    }

    [Fact]
    public void Closing_the_window_with_no_update_waiting_restarts_nothing()
    {
        var gate = new UpdateGate(() => _restarts++);
        gate.WindowOpened();

        gate.WindowClosed();

        Assert.Equal(0, _restarts);
    }
}
