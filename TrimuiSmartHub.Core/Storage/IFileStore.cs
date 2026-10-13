namespace TrimuiSmartHub.Core.Storage;

/// <summary>A file in a store. <see cref="Path"/> is relative to the store root and always uses '/'.</summary>
public sealed record FileEntry(string Path, long Size, DateTime LastWriteUtc)
{
    public string Name => RelPath.GetFileName(Path);
}

/// <summary>
/// Root-relative file operations. The local library, a USB drive and an SD card in a reader are all
/// <see cref="LocalFileStore"/>s; a network (SFTP) target can implement this interface later.
/// </summary>
public interface IFileStore
{
    string DisplayName { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    FileEntry? GetFile(string path);

    /// <summary>Lists files under <paramref name="directory"/>. Returns nothing when it doesn't exist.</summary>
    IEnumerable<FileEntry> EnumerateFiles(string directory, bool recursive);

    IEnumerable<string> EnumerateDirectories(string directory);

    Stream OpenRead(string path);

    /// <summary>
    /// Writes to a temporary file and renames it over the destination, so an interrupted copy
    /// never leaves a truncated file with the final name.
    /// </summary>
    Task WriteAtomicAsync(string path, Stream content, DateTime? lastWriteUtc = null, IProgress<long>? bytesWritten = null, CancellationToken cancellationToken = default);

    void Move(string from, string to, bool overwrite = false);

    void Delete(string path);

    void CreateDirectory(string path);

    long? AvailableFreeSpace { get; }

    /// <summary>File system name (FAT32, exFAT, NTFS...) when known.</summary>
    string? Format { get; }
}
