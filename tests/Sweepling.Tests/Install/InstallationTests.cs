using Microsoft.Win32;
using Sweepling.Install;

namespace Sweepling.Tests.Install;

/// <summary>Each test works under its own scratch key, never the real Run key or Sweepling's settings.</summary>
public sealed class InstallationTests : IDisposable
{
    // One key per test, directly under Software, so deleting it leaves nothing behind.
    private readonly string _scratch = $@"Software\Sweepling.Tests.{Guid.NewGuid():N}";

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_scratch, throwOnMissingSubKey: false);

    [Fact]
    public void A_build_run_from_source_is_not_an_installed_copy() =>
        Assert.False(Installation.IsInstalled);

    [Fact]
    public void Login_start_writes_the_quoted_exe_and_removes_it_again()
    {
        var login = new LoginStart(_scratch + @"\Run");
        const string exe = @"C:\Apps\Sweepling\current\Sweepling.exe";

        login.TurnOn(exe);
        using (var run = Registry.CurrentUser.OpenSubKey(_scratch + @"\Run"))
        {
            Assert.Equal($"\"{exe}\"", run?.GetValue("Sweepling"));
        }

        Assert.True(login.IsOn);

        login.TurnOff();
        Assert.False(login.IsOn);
        login.TurnOff();
    }

    [Fact]
    public void Updates_are_on_until_turned_off_and_back_on_once_the_settings_are_removed()
    {
        var preferences = new Preferences(_scratch + @"\Settings");
        Assert.True(preferences.UpdateAutomatically);

        preferences.UpdateAutomatically = false;
        Assert.False(new Preferences(_scratch + @"\Settings").UpdateAutomatically);

        preferences.Remove();
        Assert.True(preferences.UpdateAutomatically);
        preferences.Remove();
    }
}
