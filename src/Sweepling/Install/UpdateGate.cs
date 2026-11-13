namespace Sweepling.Install;

/// <summary>
/// Restarts into a downloaded update only while no window is open, so it never ends a review or closes a crash report
/// before it's filed.
/// </summary>
public sealed class UpdateGate(Action restart)
{
    private int _windowsOpen;
    private bool _waiting;

    public void Downloaded()
    {
        _waiting = true;
        RestartIfFree();
    }

    public void WindowOpened() => _windowsOpen++;

    public void WindowClosed()
    {
        _windowsOpen--;
        RestartIfFree();
    }

    private void RestartIfFree()
    {
        if (_waiting && _windowsOpen == 0)
        {
            _waiting = false;
            restart();
        }
    }
}
