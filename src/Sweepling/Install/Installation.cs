using Microsoft.Win32;

namespace Sweepling.Install;

/// <summary>What an installed copy adds to Windows, and how to tell it from a build run from source.</summary>
public static class Installation
{
    /// <summary>
    /// True for a copy installed with Setup: the app and its Velopack manifest in <c>current\</c>, with
    /// Update.exe one folder up. Velopack only starts for these, so a build from source never registers
    /// itself to start at login or looks for updates.
    /// </summary>
    public static bool IsInstalled { get; } =
        Path.GetDirectoryName(Environment.ProcessPath) is { } folder
        && File.Exists(Path.Combine(folder, "sq.version"))
        && File.Exists(Path.Combine(folder, "..", "Update.exe"));
}

/// <summary>Sweepling's value under the current user's Run key, which starts it when they sign in.</summary>
public sealed class LoginStart(string key = @"Software\Microsoft\Windows\CurrentVersion\Run", string name = "Sweepling")
{
    public bool IsOn
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(key);
            return run?.GetValue(name) is string;
        }
    }

    /// <summary>Velopack keeps <c>current\Sweepling.exe</c> at the same path across updates, so the value outlives them.</summary>
    public void TurnOn(string exe)
    {
        using var run = Registry.CurrentUser.CreateSubKey(key);
        run.SetValue(name, $"\"{exe}\"");
    }

    public void TurnOff()
    {
        using var run = Registry.CurrentUser.OpenSubKey(key, writable: true);
        run?.DeleteValue(name, throwOnMissingValue: false);
    }
}

/// <summary>Sweepling's own settings, under <c>HKCU\Software\Sweepling</c>. A missing value means the default.</summary>
public sealed class Preferences(string key = @"Software\Sweepling")
{
    private const string Updates = "UpdateAutomatically";

    public bool UpdateAutomatically
    {
        get
        {
            using var settings = Registry.CurrentUser.OpenSubKey(key);
            return settings?.GetValue(Updates) is not 0;
        }
        set
        {
            using var settings = Registry.CurrentUser.CreateSubKey(key);
            settings.SetValue(Updates, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    /// <summary>Removes the settings when Sweepling is uninstalled.</summary>
    public void Remove() => Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
}
