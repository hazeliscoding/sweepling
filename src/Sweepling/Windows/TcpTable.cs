using System.Buffers.Binary;
using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace Sweepling.Windows;

/// <summary>Which processes hold an established TCP connection, and which ports they listen on, over IPv4 and IPv6.</summary>
internal static unsafe class TcpTable
{
    private const uint ErrorInsufficientBuffer = 122;

    public static (IReadOnlySet<int> Connected, IReadOnlyDictionary<int, IReadOnlyList<int>> Listening) Read()
    {
        var connected = new HashSet<int>();
        var listening = new Dictionary<int, SortedSet<int>>();
        void Add(MIB_TCP_STATE state, uint localPort, uint pid)
        {
            if (state == MIB_TCP_STATE.MIB_TCP_STATE_ESTAB)
            {
                connected.Add((int)pid);
            }
            else if (state == MIB_TCP_STATE.MIB_TCP_STATE_LISTEN)
            {
                // The port is in network byte order in the low 16 bits.
                var port = BinaryPrimitives.ReverseEndianness((ushort)localPort);
                (listening.TryGetValue((int)pid, out var ports) ? ports : listening[(int)pid] = []).Add(port);
            }
        }

        fixed (byte* p = Table(ADDRESS_FAMILY.AF_INET))
        {
            var table = (MIB_TCPTABLE_OWNER_PID*)p;
            foreach (var row in table->table.AsSpan((int)table->dwNumEntries))
            {
                Add(row.dwState, row.dwLocalPort, row.dwOwningPid);
            }
        }

        fixed (byte* p = Table(ADDRESS_FAMILY.AF_INET6))
        {
            var table = (MIB_TCP6TABLE_OWNER_PID*)p;
            foreach (var row in table->table.AsSpan((int)table->dwNumEntries))
            {
                Add(row.dwState, row.dwLocalPort, row.dwOwningPid);
            }
        }

        return (connected, listening.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)[.. kv.Value]));
    }

    private static byte[] Table(ADDRESS_FAMILY family)
    {
        uint size = 0;
        PInvoke.GetExtendedTcpTable(null, &size, false, (uint)family, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

        // The table can grow between the two calls, so retry a few times with the size it asks for.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var buffer = new byte[Math.Max(size, sizeof(uint))];
            fixed (byte* p = buffer)
            {
                var result = PInvoke.GetExtendedTcpTable(p, &size, false, (uint)family, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                if (result == 0)
                {
                    return buffer;
                }

                if (result != ErrorInsufficientBuffer)
                {
                    throw new Win32Exception((int)result);
                }
            }
        }

        throw new Win32Exception((int)ErrorInsufficientBuffer);
    }
}
