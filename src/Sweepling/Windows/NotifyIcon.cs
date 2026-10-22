using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Sweepling.Windows;

/// <summary>
/// Sweepling's tray icon through Shell_NotifyIcon: its count, its tooltip, a notification when leftovers
/// appear (Windows shows it as a toast), and a native menu. Avalonia's own tray icon can't notify.
/// Create and use it on the UI thread, whose message loop delivers its window's messages; its events are
/// posted back to that thread rather than run inside the window procedure.
/// </summary>
public sealed unsafe class NotifyIcon : IDisposable
{
    private const uint CallbackMessage = PInvoke.WM_APP + 1;
    private const uint KeySelect = PInvoke.NIN_SELECT | 1;
    private const uint IconId = 1;
    private const uint MenuReview = 1;
    private const uint MenuQuit = 2;
    private const uint MenuOptions = 10;
    private const uint TrackReturnCommand = 0x0100;
    private const uint TrackRightButton = 0x0002;
    private const uint TrackBottomAlign = 0x0020;
    private const string ClassName = "Sweepling.Tray";

    private static readonly uint TaskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");

    // The window procedure is static, and Sweepling has one tray icon.
    private static NotifyIcon? _current;

    private readonly SynchronizationContext _ui;
    private readonly HWND _window;
    private HICON _icon;
    private string _tooltip;

    public NotifyIcon(byte[] icon, string tooltip)
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Create the tray icon on the UI thread.");
        _current = this;
        var instance = PInvoke.GetModuleHandle((PCWSTR)null);
        fixed (char* name = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProcedure,
                hInstance = (HINSTANCE)instance.Value,
                lpszClassName = name,
            };
            PInvoke.RegisterClassEx(&windowClass);
            _window = PInvoke.CreateWindowEx(0, name, name, 0, 0, 0, 0, 0, HWND.Null, HMENU.Null, (HINSTANCE)instance.Value, null);
        }

        if (_window.IsNull)
        {
            throw new Win32Exception();
        }

        _icon = Load(icon);
        _tooltip = tooltip;
        Add();
    }

    /// <summary>A left click, Enter, a click on the notification, or Review leftovers in the menu.</summary>
    public event Action? Opened;

    public event Action? QuitChosen;

    /// <summary>Ticked items between Review leftovers and Quit, each read again whenever the menu opens.</summary>
    public IReadOnlyList<MenuOption> Options { get; set; } = [];

    public void Update(byte[] icon, string tooltip)
    {
        var old = _icon;
        _icon = Load(icon);
        _tooltip = tooltip;
        var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
        PInvoke.DestroyIcon(old);
    }

    /// <summary>Shows a notification beside the tray. Windows holds it back during quiet hours and focus.</summary>
    public void Notify(string title, string body)
    {
        var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        data.szInfoTitle = Fit(title, 64);
        data.szInfo = Fit(body, 256);
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_USER | NOTIFY_ICON_INFOTIP_FLAGS.NIIF_LARGE_ICON | NOTIFY_ICON_INFOTIP_FLAGS.NIIF_RESPECT_QUIET_TIME;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
    }

    public void Dispose()
    {
        var data = Data(0);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, &data);
        PInvoke.DestroyWindow(_window);
        PInvoke.DestroyIcon(_icon);
        _current = null;
    }

    /// <summary>The smallest image in an .ico file at least <paramref name="size"/> pixels wide, else the largest.</summary>
    internal static (int Size, int Offset, int Length) Pick(byte[] ico, int size)
    {
        var count = BitConverter.ToUInt16(ico, 4);
        var images = Enumerable.Range(0, count).Select(i =>
        {
            var entry = 6 + (16 * i);
            var width = ico[entry] == 0 ? 256 : ico[entry];
            return (Size: width, Offset: BitConverter.ToInt32(ico, entry + 12), Length: BitConverter.ToInt32(ico, entry + 8));
        }).OrderBy(e => e.Size).ToList();
        return images.FirstOrDefault(e => e.Size >= size, images[^1]);
    }

    private HICON Load(byte[] ico)
    {
        // At system DPI, which is what the tray draws at, in a per-monitor-aware process.
        var size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
        var (_, offset, length) = Pick(ico, size);
        fixed (byte* image = &ico[offset])
        {
            var icon = PInvoke.CreateIconFromResourceEx(image, (uint)length, true, 0x00030000, size, size, IMAGE_FLAGS.LR_DEFAULTCOLOR);
            return icon.IsNull ? throw new Win32Exception() : icon;
        }
    }

    private void Add()
    {
        var data = Data(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, &data);
    }

    private NOTIFYICONDATAW Data(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _window,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = Fit(_tooltip, 128),
    };

    // Leaves room for the terminating null.
    private static string Fit(string text, int capacity) => text.Length < capacity ? text : text[..(capacity - 1)];

    private void Post(Action? handler)
    {
        if (handler is not null)
        {
            _ui.Post(_ => handler(), null);
        }
    }

    private void OnCallback(uint notification, WPARAM anchor)
    {
        if (notification is PInvoke.NIN_SELECT or KeySelect or PInvoke.NIN_BALLOONUSERCLICK)
        {
            Post(Opened);
        }
        else if (notification == PInvoke.WM_CONTEXTMENU)
        {
            ShowMenu((short)(anchor.Value & 0xFFFF), (short)((anchor.Value >> 16) & 0xFFFF));
        }
    }

    private void ShowMenu(int x, int y)
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            var options = Options;
            fixed (char* review = "Review leftovers")
            fixed (char* quit = "Quit Sweepling")
            {
                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuReview, review);
                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (PCWSTR)null);
                for (var i = 0; i < options.Count; i++)
                {
                    fixed (char* label = options[i].Label)
                    {
                        var flags = MENU_ITEM_FLAGS.MF_STRING | (options[i].IsOn() ? MENU_ITEM_FLAGS.MF_CHECKED : 0);
                        PInvoke.AppendMenu(menu, flags, MenuOptions + (uint)i, label);
                    }
                }

                if (options.Count > 0)
                {
                    PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (PCWSTR)null);
                }

                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuQuit, quit);
            }

            PInvoke.SetMenuDefaultItem(menu, MenuReview, 0);

            // Without the foreground, the menu wouldn't close when the person clicks elsewhere.
            PInvoke.SetForegroundWindow(_window);
            var chosen = (uint)PInvoke.TrackPopupMenuEx(menu, TrackReturnCommand | TrackRightButton | TrackBottomAlign, x, y, _window, null).Value;
            PInvoke.PostMessage(_window, PInvoke.WM_NULL, default, default);
            Post(chosen switch
            {
                MenuReview => Opened,
                MenuQuit => QuitChosen,
                >= MenuOptions when chosen - MenuOptions < options.Count => options[(int)(chosen - MenuOptions)].Toggle,
                _ => null,
            });
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProcedure(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (_current is { } icon && window == icon._window)
        {
            if (message == CallbackMessage)
            {
                icon.OnCallback((uint)(lParam.Value & 0xFFFF), wParam);
                return default;
            }

            // Explorer restarted, and with it the taskbar: the icon has to be added again.
            if (message == TaskbarCreated)
            {
                icon.Add();
                return default;
            }
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }
}

/// <summary>A tray menu item with a tick, such as Start at login. Toggle runs on the UI thread.</summary>
public sealed record MenuOption(string Label, Func<bool> IsOn, Action Toggle);
