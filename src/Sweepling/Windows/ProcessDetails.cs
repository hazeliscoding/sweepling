using System.Collections.Concurrent;
using System.Runtime.InteropServices.ComTypes;
using Sweepling.Leftovers;
using Windows.Win32;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.System.Threading;

namespace Sweepling.Windows;

/// <summary>
/// What the review window shows about a process, read when it is shown rather than in every snapshot.
/// Each answer is checked against the start time, so a reused PID reports nothing.
/// </summary>
public static unsafe class ProcessDetails
{
    private static readonly ConcurrentDictionary<string, bool> MsysFolders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The folder it runs in, or null for a Git Bash program, whose Windows folder is a placeholder.</summary>
    public static string? CurrentDirectory(Proc process) =>
        IsMsys(process.Image) || Peb.CurrentDirectory(process.Pid, process.Started) is not { } folder
            ? null
            : folder.Length > 3 ? folder.TrimEnd('\\') : folder;

    /// <summary>Its private bytes: the memory only it holds.</summary>
    public static long? PrivateBytes(Proc process)
    {
        var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Pid);
        if (handle.IsNull)
        {
            return null;
        }

        try
        {
            FILETIME created, exited, kernel, user;
            var counters = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)sizeof(PROCESS_MEMORY_COUNTERS_EX) };
            return PInvoke.GetProcessTimes(handle, &created, &exited, &kernel, &user) && ProcessReader.Started(created) == process.Started
                && PInvoke.GetProcessMemoryInfo(handle, (PROCESS_MEMORY_COUNTERS*)&counters, counters.cb)
                ? (long)counters.PrivateUsage
                : null;
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }

    /// <summary>Git Bash's programs are MSYS2 programs, which sit beside <c>msys-2.0.dll</c>.</summary>
    public static bool IsMsys(string image) =>
        Path.GetDirectoryName(image) is { } folder
        && MsysFolders.GetOrAdd(folder, f => File.Exists(Path.Combine(f, "msys-2.0.dll")) || File.Exists(Path.Combine(f, "cygwin1.dll")));
}
