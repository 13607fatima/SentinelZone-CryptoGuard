using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Platform.Windows;

public sealed class NetworkCollector : INetworkCollector
{
    public Connection[] Collect(IReadOnlyDictionary<int,string> processIds) => Read(2, processIds).Concat(Read(23, processIds)).ToArray();
    private static IEnumerable<Connection> Read(int family, IReadOnlyDictionary<int,string> processIds)
    {
        int size = 0;
        var status = Native.GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 5, 0);
        if (status is not (0 or 122)) throw new Win32Exception((int)status);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (size < 4 || size > 64 * 1024 * 1024) throw new InvalidDataException("TCP table size outside bounds.");
            var p = Marshal.AllocHGlobal(size);
            try
            {
                var allocated = size;
                status = Native.GetExtendedTcpTable(p, ref size, false, family, 5, 0);
                if (status == 122) continue;
                if (status != 0) throw new Win32Exception((int)status);
                var count = Marshal.ReadInt32(p); var rowSize = family == 2 ? 24 : 56;
                if (count < 0 || 4L + count * (long)rowSize > allocated) throw new InvalidDataException("Malformed TCP table.");
                var rows = new List<Connection>(count);
                for (int i = 0; i < count; i++)
                {
                    var b = new byte[rowSize]; Marshal.Copy(p + 4 + i * rowSize, b, 0, rowSize);
                    var pid = BitConverter.ToInt32(b, family == 2 ? 20 : 52);
                    var state = (TcpState)BitConverter.ToInt32(b, family == 2 ? 0 : 48);
                    var local = family == 2 ? new IPAddress(b.AsSpan(4, 4)) : new IPAddress(b.AsSpan(0, 16), BitConverter.ToUInt32(b, 16));
                    var remote = family == 2 ? new IPAddress(b.AsSpan(12, 4)) : new IPAddress(b.AsSpan(24, 16), BitConverter.ToUInt32(b, 40));
                    int lp = family == 2 ? 8 : 20, rp = family == 2 ? 16 : 44;
                    rows.Add(new(pid, processIds.GetValueOrDefault(pid), family == 2 ? "ipv4" : "ipv6", local.ToString(), (b[lp] << 8) | b[lp + 1],
                        remote.ToString(), (b[rp] << 8) | b[rp + 1], state.ToString(), "GetExtendedTcpTable.owner_pid_snapshot"));
                }
                return rows;
            }
            finally { Marshal.FreeHGlobal(p); }
        }
        throw new IOException("TCP table kept changing; retry next interval.");
    }
}
