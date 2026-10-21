using Sweepling.Windows;

namespace Sweepling.Tests.Windows;

public sealed class NotifyIconTests
{
    // The tray icons hold 16, 20, 24, 32, 40 and 48 px images.
    private static readonly byte[] Icon = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Tray", "tray-3.ico"));

    [Theory]
    [InlineData(16, 16)]
    [InlineData(20, 20)]
    [InlineData(18, 20)]
    [InlineData(30, 32)]
    [InlineData(64, 48)]
    public void The_tray_shows_the_smallest_image_at_least_as_big_as_it_needs(int wanted, int picked) =>
        Assert.Equal(picked, NotifyIcon.Pick(Icon, wanted).Size);
}
