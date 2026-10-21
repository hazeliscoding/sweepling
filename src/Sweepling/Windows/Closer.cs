using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Sweepling.Leftovers;
using Windows.Win32;
using Windows.Win32.System.Threading;

namespace Sweepling.Windows;

public enum CloseOutcome
{
    Closed,

    /// <summary>It had already ended.</summary>
    Gone,

    /// <summary>Its PID now belongs to another process, so it was left alone.</summary>
    Reused,

    /// <summary>Something in its group gained a connection since it was offered, so the group was left alone.</summary>
    InUse,

    Failed,
}

public sealed record CloseResult(Proc Process, CloseOutcome Outcome);

/// <summary>
/// Closes a group the person confirmed. Right before closing, it checks that nothing in the group has
/// gained a connection, and that each PID still belongs to the process that was offered.
/// </summary>
public static unsafe class Closer
{
    private const int ErrorInvalidParameter = 87;
    private const uint StillActive = 259;
    private const uint ExitWaitMilliseconds = 5000;

    public static IReadOnlyList<CloseResult> Close(Group group)
    {
        // Someone may have opened the dev server in a browser since it was offered.
        var (connected, _) = TcpTable.Read();
        if (group.Members.Any(m => connected.Contains(m.Pid)))
        {
            return [.. group.Members.Select(m => new CloseResult(m, CloseOutcome.InUse))];
        }

        // Parents first, as the group lists them, so a supervisor can't restart a child it just lost.
        return [.. group.Members.Select(m => new CloseResult(m, CloseOne(m)))];
    }

    private static CloseOutcome CloseOne(Proc process)
    {
        const PROCESS_ACCESS_RIGHTS access = PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE
            | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION
            | PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE;
        var handle = PInvoke.OpenProcess(access, false, (uint)process.Pid);
        if (handle.IsNull)
        {
            return Marshal.GetLastPInvokeError() == ErrorInvalidParameter ? CloseOutcome.Gone : CloseOutcome.Failed;
        }

        try
        {
            FILETIME created, exited, kernel, user;
            uint exitCode;
            if (!PInvoke.GetProcessTimes(handle, &created, &exited, &kernel, &user) || !PInvoke.GetExitCodeProcess(handle, &exitCode))
            {
                return CloseOutcome.Failed;
            }

            // Windows reuses PIDs: the one offered may belong to a new process by now.
            if (ProcessReader.Started(created) != process.Started)
            {
                return CloseOutcome.Reused;
            }

            if (exitCode != StillActive)
            {
                return CloseOutcome.Gone;
            }

            if (!PInvoke.TerminateProcess(handle, 1))
            {
                return CloseOutcome.Failed;
            }

            PInvoke.WaitForSingleObject(handle, ExitWaitMilliseconds);
            return CloseOutcome.Closed;
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }
}
