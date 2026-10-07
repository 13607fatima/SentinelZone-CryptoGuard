using CryptoGuard.Contracts;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CryptoGuard.Platform.Linux;

public sealed partial class ExecutableInspector : IExecutableInspector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Stat
    {
        public ulong Device, Inode, Links;
        public uint Mode, Uid, Gid, Pad;
        public ulong Rdev;
        public long Size, BlockSize, Blocks, AccessSec, AccessNs, ModifiedSec, ModifiedNs, ChangedSec, ChangedNs, Reserved1, Reserved2, Reserved3;
    }
    [LibraryImport("libc", SetLastError = true)] private static partial int fstat(int fd, out Stat stat);
    private readonly Dictionary<string, (string? Hash, string Status, FileTrust Trust)> cache = [];
    private int remaining;
    public void BeginSample() { remaining = 4; }
    public (string? Hash, string Status, FileTrust Trust) Inspect(string path) => InspectProcess(path, path);
    public (string? Hash, string Status, FileTrust Trust) InspectProcess(string openedPath, string executablePath)
    {
        var unknown = new FileTrust("package_origin", "temporarily_unavailable", null, null);
        try
        {
            using var input = new FileStream(openedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536);
            int fd = input.SafeFileHandle.DangerousGetHandle().ToInt32();
            if (fstat(fd, out var before) != 0 || (before.Mode & 0xF000) != 0x8000 || before.Size > 128 * 1024 * 1024) return (null, "unsupported", unknown);
            string key = $"{before.Device}:{before.Inode}:{before.Size}:{before.ModifiedSec}:{before.ModifiedNs}:{before.ChangedSec}:{before.ChangedNs}";
            if (cache.TryGetValue(key, out var found)) return found;
            if (remaining-- <= 0) return (null, "temporarily_unavailable", unknown);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5); // Debian md5sums integrity metadata, not security identity.
            byte[] buffer = new byte[65536];
            Span<byte> header=stackalloc byte[16];
            int headerLength=input.Read(header);input.Position=0;
            string? format=headerLength>=4 && header[0]==0x7f && header[1]==(byte)'E' && header[2]==(byte)'L' && header[3]==(byte)'F' ? "ELF" : null;
            int n;
            while ((n = input.Read(buffer)) > 0) { sha.AppendData(buffer, 0, n); md5.AppendData(buffer, 0, n); }
            if (fstat(fd, out var after) != 0 || before.Size != after.Size || before.ModifiedSec != after.ModifiedSec || before.ModifiedNs != after.ModifiedNs || before.ChangedSec != after.ChangedSec || before.ChangedNs != after.ChangedNs)
                return (null, "temporarily_unavailable", unknown);
            string hash = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            string md5Value = Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant();
            var trust = PackageTrust(executablePath, md5Value) with { OwnerUid=before.Uid,OwnerGid=before.Gid,
                Permissions=Convert.ToString(before.Mode & 0xFFF,8).PadLeft(4,'0'),ExecutableFormat=format,FileSizeBytes=before.Size };
            if (cache.Count >= 4096) cache.Remove(cache.Keys.First());
            return cache[key] = (hash, "ok", trust);
        }
        catch (Exception ex) when (LinuxCollector.Expected(ex)) { return (null, LinuxCollector.Status(ex), unknown with { Status = LinuxCollector.Status(ex) }); }
    }
    private static FileTrust PackageTrust(string path, string digest)
    {
        if (path.EndsWith(" (deleted)", StringComparison.Ordinal)) return new("package_origin", "temporarily_unavailable", null, "deleted_executable");
        string? output = RunReadOnly("/usr/bin/dpkg-query", ["-S", path]);
        if (output is null) return new("package_origin", "temporarily_unavailable", null, null);
        string? line = output.Split('\n').FirstOrDefault(s => s.EndsWith(": " + path, StringComparison.Ordinal));
        if (line is null) return new("package_origin", "ok", null, "not_owned_by_package");
        string package = line[..^(path.Length + 2)];
        if (package.IndexOfAny(['/', '\\', ',', '\n']) >= 0) return new("package_origin", "temporarily_unavailable", null, null);
        var details=RunReadOnly("/usr/bin/dpkg-query",["-W","-f=${Version}\t${Architecture}\t${source:Package}\n",package])?.TrimEnd().Split('\t');
        FileTrust Result(string status,string integrity)=>new("package_origin",status,package,integrity)
        {
            PackageVersion=details is { Length:>=1 }?details[0]:null,
            PackageArchitecture=details is { Length:>=2 }?details[1]:null,
            // dpkg records installed package metadata, not the signing/repository origin of this file.
            Origin=details is { Length:>=3 }?details[2]:null,OriginStatus=details is null?"temporarily_unavailable":"local_source_package_only"
        };
        string metadata = "/var/lib/dpkg/info/" + package + ".md5sums";
        if (!File.Exists(metadata)) return Result("partial", "no_integrity_metadata");
        string relative = path.TrimStart('/');
        string? entry = File.ReadLines(metadata).FirstOrDefault(s => s.Length > 34 && s[34..] == relative);
        return Result("ok", entry is null ? "not_listed" : entry[..32] == digest ? "matches_local_package_metadata" : "mismatch");
    }
    internal static string? RunReadOnly(string executable, string[] args)
    {
        var info = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info);
        if (process is null) return null;
        // Caller runs in the isolated collector; IPC deadlines protect the core from a hung OS query.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(3000))
        {
            try { process.Kill(entireProcessTree:true);process.WaitForExit(1000); } catch(InvalidOperationException) { }
            return null;
        }
        return output.GetAwaiter().GetResult();
    }
}
