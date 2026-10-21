using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Sweepling.Review;

public sealed partial class ReviewWindow : Window
{
    private const int TrayGap = 12;

    public ReviewWindow()
    {
        InitializeComponent();
    }

    public ReviewWindow(ReviewViewModel model)
        : this()
    {
        DataContext = model;
    }

    public ReviewViewModel Model => (ReviewViewModel)DataContext!;

    /// <summary>Opens above the tray, in the corner of the primary screen's working area.</summary>
    public void ShowNearTray()
    {
        Place(new Size(Width, Height));
        Show();

        // The title bar and borders are only known once the window is shown.
        if (FrameSize is { } frame)
        {
            Place(frame);
        }

        Activate();
    }

    private void Place(Size frame)
    {
        if (Screens.Primary is { } screen)
        {
            var area = screen.WorkingArea;
            Position = new PixelPoint(
                area.Right - (int)((frame.Width + TrayGap) * screen.Scaling),
                area.Bottom - (int)((frame.Height + TrayGap) * screen.Scaling));
        }
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
