using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CryptoGuard.Platform.Windows;

public sealed partial class ExecutableInspector
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct CatalogInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct CatalogSubject
    {
        public uint Size,Version;
        [MarshalAs(UnmanagedType.LPWStr)] public string CatalogPath;
        [MarshalAs(UnmanagedType.LPWStr)] public string MemberTag;
        [MarshalAs(UnmanagedType.LPWStr)] public string MemberPath;
        public IntPtr File,Hash; public uint HashLength; public IntPtr CatalogContext,AdminContext;
    }
    [DllImport("wintrust.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminAcquireContext2(out IntPtr context,IntPtr subsystem,string algorithm,IntPtr policy,uint flags);
    [DllImport("wintrust.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr context,SafeFileHandle file,ref uint size,[Out] byte[]? hash,uint flags);
    [DllImport("wintrust.dll",SetLastError=true)] private static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr context,byte[] hash,uint size,uint flags,IntPtr previous);
    [DllImport("wintrust.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptCATCatalogInfoFromContext(IntPtr catalog,ref CatalogInfo info,uint flags);
    [DllImport("wintrust.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptCATAdminReleaseCatalogContext(IntPtr context,IntPtr catalog,uint flags);
    [DllImport("wintrust.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptCATAdminReleaseContext(IntPtr context,uint flags);

    private static (string Trust,string? Signer) VerifyCatalog(string path,SafeFileHandle handle)
    {
        var unavailable=false;var candidateFound=false;
        foreach(var algorithm in new[]{"SHA256","SHA1"})
        {
            if(!CryptCATAdminAcquireContext2(out var admin,IntPtr.Zero,algorithm,IntPtr.Zero,0)){unavailable=true;continue;}
            try
            {
                uint size=0;
                _=CryptCATAdminCalcHashFromFileHandle2(admin,handle,ref size,null,0);
                if(size is 0 or >128){unavailable=true;continue;}
                var hash=new byte[size];
                if(!CryptCATAdminCalcHashFromFileHandle2(admin,handle,ref size,hash,0)){unavailable=true;continue;}
                var catalog=CryptCATAdminEnumCatalogFromHash(admin,hash,size,0,IntPtr.Zero);
                if(catalog==IntPtr.Zero){if(Marshal.GetLastWin32Error() is not (0 or 1168))unavailable=true;continue;}
                candidateFound=true;
                try
                {
                    var info=new CatalogInfo{Size=(uint)Marshal.SizeOf<CatalogInfo>(),Path=""};
                    if(!CryptCATCatalogInfoFromContext(catalog,ref info,0)){unavailable=true;continue;}
                    var pinned=GCHandle.Alloc(hash,GCHandleType.Pinned);
                    var subject=new CatalogSubject{Size=(uint)Marshal.SizeOf<CatalogSubject>(),CatalogPath=info.Path,
                        MemberTag=Convert.ToHexString(hash),MemberPath=path,File=handle.DangerousGetHandle(),Hash=pinned.AddrOfPinnedObject(),HashLength=size,AdminContext=admin};
                    var ptr=Marshal.AllocHGlobal(Marshal.SizeOf<CatalogSubject>());
                    try
                    {
                        Marshal.StructureToPtr(subject,ptr,false);
                        var result=VerifySubject(ptr,2);
                        if(result.Status==0)return ("trusted_offline",result.Signer);
                    }
                    finally{Marshal.DestroyStructure<CatalogSubject>(ptr);Marshal.FreeHGlobal(ptr);pinned.Free();}
                }
                finally{_=CryptCATAdminReleaseCatalogContext(admin,catalog,0);}
            }
            finally{_=CryptCATAdminReleaseContext(admin,0);}
        }
        return (candidateFound?"catalog_candidate_untrusted_or_revocation_unknown":unavailable?"catalog_temporarily_unavailable":"unsigned",null);
    }
}
