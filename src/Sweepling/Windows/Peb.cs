using System.Buffers;
using System.Runtime.InteropServices.ComTypes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;
using Ntdll = Windows.Wdk.PInvoke;
using PROCESSINFOCLASS = Windows.Wdk.System.Threading.PROCESSINFOCLASS;

namespace Sweepling.Windows;

/// <summary>
/// Reads another 64-bit process's PEB: its environment and its current folder. What it holds is that
/// process's to forge, so every size is checked and every read is bounded by Sweepling's own buffer.
/// </summary>
internal static unsafe class Peb
{
    // x64 layout: PEB.ProcessParameters, then in RTL_USER_PROCESS_PARAMETERS the current folder
    // (CURDIR.DosPath, a UNICODE_STRING), the environment and its size.
    private const int ProcessParametersOffset = 0x20;
    private const int CurrentDirectoryOffset = 0x38;
    private const int EnvironmentOffset = 0x80;
    private const int EnvironmentSizeOffset = 0x3F0;
    private const int MaxEnvironmentBytes = 1 << 20;
    private const int MaxPathChars = 32767;

    /// <summary>A variable's whole-number value from the process's environment, like Claude Code's <c>CLAUDE_PID</c>.</summary>
    public static int? Variable(int pid, DateTimeOffset started, string variable) => Read(pid, started, (process, parameters) =>
    {
        if (!TryRead(process, (byte*)parameters + EnvironmentOffset, out var environment)
            || !TryRead(process, (byte*)parameters + EnvironmentSizeOffset, out var size)
            || environment == 0 || size <= 0 || size > MaxEnvironmentBytes)
        {
            return null;
        }

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
    });

    /// <summary>The folder the process runs in.</summary>
    public static string? CurrentDirectory(int pid, DateTimeOffset started) => Read(pid, started, (process, parameters) =>
    {
        // A UNICODE_STRING: its length in bytes, then (after padding) a pointer to the characters.
        var dosPath = (byte*)parameters + CurrentDirectoryOffset;
        ushort length;
        if (!PInvoke.ReadProcessMemory(process, dosPath, &length, sizeof(ushort), null)
            || !TryRead(process, dosPath + sizeof(nint), out var buffer)
            || buffer == 0 || length == 0 || length / sizeof(char) > MaxPathChars)
        {
            return null;
        }

        var chars = length / sizeof(char);
        var text = stackalloc char[chars];
        return PInvoke.ReadProcessMemory(process, (void*)buffer, text, (nuint)(chars * sizeof(char)), null) ? new string(text, 0, chars) : null;
    });

    private static T? Read<T>(int pid, DateTimeOffset started, Func<HANDLE, nint, T?> read)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ, false, (uint)pid);
        if (process.IsNull)
        {
            return default;
        }

        try
        {
            // The PID may have been reused, and a 32-bit process has a different PEB layout.
            FILETIME created, exited, kernel, user;
            IMAGE_FILE_MACHINE machine;
            PROCESS_BASIC_INFORMATION basic;
            uint length = 0;
            if (!PInvoke.GetProcessTimes(process, &created, &exited, &kernel, &user) || ProcessReader.Started(created) != started
                || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10586)
                || !PInvoke.IsWow64Process2(process, &machine, null) || machine != IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_UNKNOWN
                || Ntdll.NtQueryInformationProcess(process, PROCESSINFOCLASS.ProcessBasicInformation, &basic, (uint)sizeof(PROCESS_BASIC_INFORMATION), &length).Value < 0
                || !TryRead(process, (byte*)basic.PebBaseAddress + ProcessParametersOffset, out var parameters) || parameters == 0)
            {
                return default;
            }

            return read(process, parameters);
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
}
