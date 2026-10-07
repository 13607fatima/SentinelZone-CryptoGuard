using System.Runtime.InteropServices;
using CryptoGuard.Core;
using System.Text;

namespace CryptoGuard.Platform.Linux;

internal static partial class Native
{
    [LibraryImport("libc", SetLastError = true)] internal static partial long sysconf(int name);
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)] private static partial int open(string path, int flags);
    [LibraryImport("libc", SetLastError = true)] private static partial int fsync(int fd);
    [LibraryImport("libc")] private static partial int close(int fd);
    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)] private static unsafe partial nint readlink(string path, byte* buffer, nuint size);
    public static unsafe string ReadLink(string path)
    {
        byte* buffer = stackalloc byte[4096];
        nint count = readlink(path, buffer, 4096);
        if (count < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error is 1 or 13) throw new UnauthorizedAccessException("link_access_denied");
            throw new IOException("link_unavailable");
        }
        if (count >= 4096) throw new IOException("link_too_long");
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(buffer, (int)count));
    }
    public static void EnableDurability()
    {
        AtomicFile.SyncDirectory = path =>
        {
            int fd = open(path, 65536); // O_RDONLY | O_DIRECTORY on Linux.
            if (fd < 0) throw new IOException("directory_open_failed");
            try { if (fsync(fd) != 0) throw new IOException("directory_fsync_failed"); }
            finally { close(fd); }
        };
    }
}
public static class LinuxDurability
{
    public static void Initialize() => Native.EnableDurability();
    public static void PrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
}
