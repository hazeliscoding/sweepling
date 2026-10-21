using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace Sweepling;

public sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window: Sweepling lives in the tray until Quit.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var quit = new NativeMenuItem("Quit");
            quit.Click += (_, _) => desktop.Shutdown();
            var tray = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Sweepling/Assets/sweepling.ico"))),
                ToolTipText = "Sweepling",
                Menu = new NativeMenu { Items = { quit } },
            };
            TrayIcon.SetIcons(this, [tray]);
            // Removing the icon on exit keeps a dead icon from lingering in the tray.
            desktop.Exit += (_, _) => tray.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
