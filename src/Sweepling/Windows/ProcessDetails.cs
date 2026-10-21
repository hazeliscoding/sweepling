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
    /// <summary>The folder it runs in. Git Bash programs keep theirs in step with their own, too.</summary>
    public static string? CurrentDirectory(Proc process) =>
        Peb.CurrentDirectory(process.Pid, process.Started) is not { } folder
            ? null
            : folder.Length > 3 ? folder.TrimEnd('\\') : folder;

    /// <summary>Its private bytes: the memory only it holds.</summary>
    public static long? PrivateBytes(Proc process)
    {
        // GetProcessMemoryInfo needs PROCESS_VM_READ as well, as its documentation says; Windows Server enforces it.
        var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ, false, (uint)process.Pid);
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
}
