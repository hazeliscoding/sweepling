namespace Sweepling.Install;

/// <summary>Restarts into a downloaded update only while the review window is closed, so it never ends a review.</summary>
public sealed class UpdateGate(Action restart)
{
    private bool _windowOpen;
    private bool _waiting;

    public void Downloaded()
    {
        _waiting = true;
        RestartIfFree();
    }

    public void WindowOpened() => _windowOpen = true;

    public void WindowClosed()
    {
        _windowOpen = false;
        RestartIfFree();
    }

    private void RestartIfFree()
    {
        if (_waiting && !_windowOpen)
        {
            _waiting = false;
            restart();
        }
    }
}
