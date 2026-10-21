using Sweepling.Windows;

namespace Sweepling.Tests.Windows;

public sealed class ImageSubsystemTests
{
    private static readonly string System32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

    [Fact]
    public void A_windowed_app_is_windowed() =>
        Assert.True(ImageSubsystem.IsWindowed(Path.Combine(System32, "notepad.exe")));

    [Fact]
    public void A_console_program_is_not_windowed() =>
        Assert.False(ImageSubsystem.IsWindowed(Path.Combine(System32, "cmd.exe")));

    [Fact]
    public void An_image_that_cant_be_read_counts_as_windowed() =>
        Assert.True(ImageSubsystem.IsWindowed(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe")));
}
