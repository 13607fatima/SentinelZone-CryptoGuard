using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace CryptoGuard.Agent.Linux;

internal static class Frame
{
    public static async Task Write(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }
    public static async Task<byte[]> Read(Stream stream, int limit, CancellationToken token)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        int count = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (count <= 0 || count > limit) throw new InvalidDataException("ipc_frame_size");
        byte[] bytes = new byte[count];
        await stream.ReadExactlyAsync(bytes, token);
        return bytes;
    }
}
internal static class SystemdNotify
{
    public static void Send(string message)
    {
        string? path = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        if (string.IsNullOrEmpty(path)) return;
        if (path[0] == '@') path = "\0" + path[1..];
        try { using var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified); socket.SendTo(Encoding.UTF8.GetBytes(message), new UnixDomainSocketEndPoint(path)); }
        catch (SocketException) { }
    }
}
