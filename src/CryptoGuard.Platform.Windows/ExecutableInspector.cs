using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CryptoGuard.Compatibility.Windows.Contracts;

namespace CryptoGuard.Platform.Windows;

public sealed partial class ExecutableInspector : IExecutableInspector
{
    private sealed record Cached(long Length, DateTime Write, DateTime Checked, string Hash, string Trust, string? Signer);
    private readonly Dictionary<string,Cached> cache = new(StringComparer.OrdinalIgnoreCase);
    public bool IsCached(string path)
    {
        try { var f = new FileInfo(path); return cache.TryGetValue(path, out var c) && c.Length == f.Length && c.Write == f.LastWriteTimeUtc && DateTime.UtcNow - c.Checked < TimeSpan.FromMinutes(15); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    public (string? Hash, string Trust, string? Signer) Inspect(string path)
    {
        try
        {
            if (IsCached(path)) { var c = cache[path]; return (c.Hash, c.Trust, c.Signer); }
            var before = new FileInfo(path);
            if (before.Length > 512 * 1024 * 1024) return (null, "size_limit", null);
            var length = before.Length; var write = before.LastWriteTimeUtc;
            string hash;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            stream.Position=0;
            var (trust, signer) = Verify(path,stream.SafeFileHandle);
            before.Refresh(); if (before.Length != length || before.LastWriteTimeUtc != write) return (null, "changed_during_inspection", null);
            if (cache.Count >= 4096) cache.Clear();
            cache[path] = new(length, write, DateTime.UtcNow, hash, trust, signer); return (hash, trust, signer);
        }
        catch (UnauthorizedAccessException) { return (null, "permission_denied", null); }
        catch (IOException) { return (null, "temporarily_unavailable", null); }
        catch (CryptographicException) { return (null, "temporarily_unavailable", null); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct FileInfoNative { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr File, Subject; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] private struct TrustData
    {
        public uint Size; public IntPtr Policy, Sip; public uint UiChoice, Revocation, UnionChoice; public IntPtr File;
        public uint StateAction; public IntPtr State; public IntPtr Url; public uint Flags, UiContext; public IntPtr Signature;
    }
    [DllImport("wintrust.dll", ExactSpelling=true, CharSet=CharSet.Unicode)] private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    [DllImport("wintrust.dll", ExactSpelling=true)] private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling=true)] private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint signer, [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
    [StructLayout(LayoutKind.Sequential)] private struct ProviderSigner { public uint Size; public Native.FileTime VerifyTime; public uint Count; public IntPtr Certificates; }
    [StructLayout(LayoutKind.Sequential)] private struct ProviderCertificate { public uint Size; public IntPtr Context; }
    [StructLayout(LayoutKind.Sequential)] private struct CertificateContext { public uint Encoding; public IntPtr Encoded; public uint Length; public IntPtr Info, Store; }
    private static (string Trust, string? Signer) Verify(string path,Microsoft.Win32.SafeHandles.SafeFileHandle handle)
    {
        var file = new FileInfoNative { Size = (uint)Marshal.SizeOf<FileInfoNative>(), Path = path,File=handle.DangerousGetHandle() };
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoNative>()); Marshal.StructureToPtr(file, ptr, false);
        try
        {
            var result=VerifySubject(ptr,1);
            if(result.Status==0)return ("trusted_offline",result.Signer);
            if(result.Status is unchecked((int)0x800B0100) or unchecked((int)0x800B0003))return VerifyCatalog(path,handle);
            return ("untrusted_or_revocation_unknown",null);
        }
        finally { Marshal.DestroyStructure<FileInfoNative>(ptr);Marshal.FreeHGlobal(ptr); }
    }
    private static (int Status,string? Signer) VerifySubject(IntPtr ptr,uint unionChoice)
    {
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var data = new TrustData { Size=(uint)Marshal.SizeOf<TrustData>(), UiChoice=2, UnionChoice=unionChoice, File=ptr, StateAction=1, Flags=0x1010 };
        try
        {
            var status = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            string? signer = null;
            if (status == 0 && data.State != IntPtr.Zero)
            {
                var provider = WTHelperProvDataFromStateData(data.State);
                var signed = provider == IntPtr.Zero ? IntPtr.Zero : WTHelperGetProvSignerFromChain(provider,0,false,0);
                if (signed != IntPtr.Zero)
                {
                    var chain = Marshal.PtrToStructure<ProviderSigner>(signed);
                    if (chain.Count > 0 && chain.Certificates != IntPtr.Zero)
                    {
                        var certificate = Marshal.PtrToStructure<ProviderCertificate>(chain.Certificates);
                        var context = Marshal.PtrToStructure<CertificateContext>(certificate.Context);
                        if (context.Length is > 0 and < 1048576)
                        {
                            var bytes = new byte[context.Length]; Marshal.Copy(context.Encoded,bytes,0,bytes.Length);
                            using var cert = X509CertificateLoader.LoadCertificate(bytes); signer = cert.Subject;
                        }
                    }
                }
            }
            return (status,signer);
        }
        finally { data.StateAction=2; _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data); }
    }
}
