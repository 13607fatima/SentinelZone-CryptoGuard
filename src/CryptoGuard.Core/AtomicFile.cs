namespace CryptoGuard.Core;

public static class AtomicFile
{
    public static Action<string> SyncDirectory { get; set; } = _ => { };
    public static void Write(string path, ReadOnlySpan<byte> data)
    {
        string temporary = path + ".new";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { stream.Write(data); stream.Flush(true); }
        File.Move(temporary, path, true);
        SyncDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }
}
