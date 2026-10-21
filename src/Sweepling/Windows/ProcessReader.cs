using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices.ComTypes;
using Sweepling.Leftovers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Diagnostics.ToolHelp;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;
using Ntdll = Windows.Wdk.PInvoke;
using PROCESSINFOCLASS = Windows.Wdk.System.Threading.PROCESSINFOCLASS;

namespace Sweepling.Windows;

/// <summary>
/// Reads the current user's processes in this Windows session, and the TCP table, without admin rights.
/// A process Sweepling can't open belongs to someone else or is protected by Windows, and is left out.
/// What never changes for a process is kept by its PID and start time, so a snapshot every few seconds
/// reads only the processes that are new. Use one reader from one thread.
/// </summary>
public sealed unsafe class ProcessReader(string? agentPidVariable)
{
    // x64 layout: PEB.ProcessParameters, then RTL_USER_PROCESS_PARAMETERS.Environment and EnvironmentSize.
    private const int ProcessParametersOffset = 0x20;
    private const int EnvironmentOffset = 0x80;
    private const int EnvironmentSizeOffset = 0x3F0;
    private const int MaxEnvironmentBytes = 1 << 20;
    private const int MaxCommandLineBytes = 1 << 20;

    private readonly uint _own = (uint)Environment.ProcessId;
    private readonly ImageSubsystem _images = new();
    private uint _session;
    private byte[]? _me;

    // A process's details, or null when it isn't the current user's; and agent PIDs already looked up.
    private Dictionary<(int Pid, DateTimeOffset Started), Proc?> _known = [];
    private Dictionary<(int Pid, DateTimeOffset Started), int?> _agentPids = [];

    public Snapshot Take()
    {
        var processes = Processes();
        var (connected, listening) = TcpTable.Read();
        return new Snapshot(DateTimeOffset.Now, processes, connected) { Listening = listening };
    }

    private List<Proc> Processes()
    {
        if (_me is null)
        {
            if (!PInvoke.ProcessIdToSessionId(_own, out _session) || UserSid(PInvoke.GetCurrentProcess()) is not { } me)
            {
                throw new Win32Exception();
            }

            _me = me;
        }

        var processes = new List<Proc>();
        var seen = new Dictionary<(int Pid, DateTimeOffset Started), Proc?>();
        var snapshot = PInvoke.CreateToolhelp32Snapshot(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0);
        if ((nint)snapshot.Value == -1)
        {
            throw new Win32Exception();
        }

        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)sizeof(PROCESSENTRY32W) };
            for (var more = PInvoke.Process32FirstW(snapshot, &entry); more; more = PInvoke.Process32NextW(snapshot, &entry))
            {
                // Sweepling itself, the idle process and System are never candidates.
                if (entry.th32ProcessID == _own || entry.th32ProcessID <= 4
                    || !PInvoke.ProcessIdToSessionId(entry.th32ProcessID, out var theirs) || theirs != _session)
                {
                    continue;
                }

                if (Read(entry.th32ProcessID, entry.th32ParentProcessID, seen) is { } process)
                {
                    processes.Add(process);
                }
            }
        }
        finally
        {
            PInvoke.CloseHandle(snapshot);
        }

        _known = seen;
        if (agentPidVariable is null)
        {
            return processes;
        }

        // Only a process whose parent is gone needs its agent PID, and reading another process's memory isn't free.
        var byPid = processes.ToDictionary(p => p.Pid);
        var looked = new Dictionary<(int Pid, DateTimeOffset Started), int?>();
        for (var i = 0; i < processes.Count; i++)
        {
            var p = processes[i];
            if (byPid.TryGetValue(p.ParentPid, out var parent) && parent.Started <= p.Started)
            {
                continue;
            }

            var key = (p.Pid, p.Started);
            if (!_agentPids.TryGetValue(key, out var agentPid))
            {
                agentPid = AgentPid((uint)p.Pid, p.Started, agentPidVariable);
            }

            looked[key] = agentPid;
            if (agentPid is { } found)
            {
                processes[i] = p with { AgentPid = found };
            }
        }

        _agentPids = looked;
        return processes;
    }

    private Proc? Read(uint pid, uint parentPid, Dictionary<(int Pid, DateTimeOffset Started), Proc?> seen)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsNull)
        {
            return null;
        }

        try
        {
            // The start time tells a known process from a new one that reused its PID.
            FILETIME created, exited, kernel, user;
            if (!PInvoke.GetProcessTimes(process, &created, &exited, &kernel, &user))
            {
                return null;
            }

            var key = ((int)pid, Started(created));
            if (!_known.TryGetValue(key, out var known))
            {
                known = UserSid(process) is { } owner && owner.AsSpan().SequenceEqual(_me) && ImagePath(process) is { } image
                    ? new Proc((int)pid, (int)parentPid, key.Item2, image, CommandLine(process) ?? "") { Windowed = _images.IsWindowed(image) }
                    : null;
            }

            seen[key] = known;
            return known;
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    private static byte[]? UserSid(HANDLE process)
    {
        HANDLE token;
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
        {
            return null;
        }

        try
        {
            const int size = 256;
            var buffer = stackalloc byte[size];
            uint length;
            if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, buffer, size, &length))
            {
                return null;
            }

            var sid = ((TOKEN_USER*)buffer)->User.Sid;
            return new ReadOnlySpan<byte>(sid.Value, (int)PInvoke.GetLengthSid(sid)).ToArray();
        }
        finally
        {
            PInvoke.CloseHandle(token);
        }
    }

    private static string? ImagePath(HANDLE process)
    {
        const int size = 1024;
        var buffer = stackalloc char[size];
        uint length = size;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, &length)
            ? new string(buffer, 0, (int)length)
            : null;
    }

    private static string? CommandLine(HANDLE process)
    {
        uint needed = 0;
        Ntdll.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessCommandLineInformation, null, 0, &needed);
        if (needed < sizeof(UNICODE_STRING) || needed > MaxCommandLineBytes)
        {
            return null;
        }

        var buffer = ArrayPool<byte>.Shared.Rent((int)needed);
        try
        {
            fixed (byte* p = buffer)
            {
                if (Ntdll.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessCommandLineInformation, p, needed, &needed).Value < 0)
                {
                    return null;
                }

                var text = (UNICODE_STRING*)p;
                if (text->Length > needed - sizeof(UNICODE_STRING))
                {
                    return null;
                }

                return new string(text->Buffer.Value, 0, text->Length / sizeof(char));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>The agent's PID from the process's environment, read from its PEB.</summary>
    private static int? AgentPid(uint pid, DateTimeOffset started, string variable)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ, false, pid);
        if (process.IsNull)
        {
            return null;
        }

        try
        {
            // The PID may have been reused since the snapshot, and a 32-bit process has a different PEB layout.
            FILETIME created, exited, kernel, user;
            IMAGE_FILE_MACHINE machine;
            PROCESS_BASIC_INFORMATION basic;
            uint length = 0;
            if (!PInvoke.GetProcessTimes(process, &created, &exited, &kernel, &user) || Started(created) != started
                || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10586)
                || !PInvoke.IsWow64Process2(process, &machine, null) || machine != IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_UNKNOWN
                || Ntdll.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessBasicInformation, &basic, (uint)sizeof(PROCESS_BASIC_INFORMATION), &length).Value < 0
                || !TryRead(process, (byte*)basic.PebBaseAddress + ProcessParametersOffset, out var parameters)
                || !TryRead(process, (byte*)parameters + EnvironmentOffset, out var environment)
                || !TryRead(process, (byte*)parameters + EnvironmentSizeOffset, out var size)
                || environment == 0 || size <= 0 || size > MaxEnvironmentBytes)
            {
                return null;
            }

            // The size comes from the other process's memory: read whole characters, never more than the buffer holds.
            var chars = (int)size / sizeof(char);
            var block = ArrayPool<char>.Shared.Rent(chars);
            try
            {
                chars = Math.Min(chars, block.Length);
                fixed (char* p = block)
                {
                    if (!PInvoke.ReadProcessMemory(process, (void*)environment, p, (nuint)(chars * sizeof(char)), null))
                    {
                        return null;
                    }
                }

                return Find(block.AsSpan(0, chars), variable);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(block);
            }
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    // The block is "NAME=value\0NAME=value\0\0".
    private static int? Find(ReadOnlySpan<char> block, string variable)
    {
        while (!block.IsEmpty && block[0] != '\0')
        {
            var end = block.IndexOf('\0');
            var entry = end < 0 ? block : block[..end];
            if (entry.Length > variable.Length && entry[variable.Length] == '='
                && entry[..variable.Length].Equals(variable, StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(entry[(variable.Length + 1)..], out var pid) ? pid : null;
            }

            block = end < 0 ? [] : block[(end + 1)..];
        }

        return null;
    }

    private static bool TryRead(HANDLE process, void* address, out nint value)
    {
        nint read;
        var ok = PInvoke.ReadProcessMemory(process, address, &read, (nuint)sizeof(nint), null);
        value = ok ? read : 0;
        return ok;
    }

    private static DateTimeOffset Started(FILETIME created) =>
        DateTimeOffset.FromFileTime(((long)(uint)created.dwHighDateTime << 32) | (uint)created.dwLowDateTime);
}
