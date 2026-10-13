namespace TrimuiSmartHub.Core.Storage;

public sealed class LocalFileStore : IFileStore
{
    public const string TempSuffix = ".tsh-tmp";

    private const int BufferSize = 1024 * 1024;

    public string RootPath { get; }

    public string DisplayName { get; }

    public LocalFileStore(string rootPath, string? displayName = null)
    {
        RootPath = Path.GetFullPath(rootPath);
        DisplayName = displayName ?? RootPath;
    }

    public string GetFullPath(string path)
    {
        var resolved = RelPath.Resolve(path) ?? throw new ArgumentException($"Path escapes the store root: {path}", nameof(path));
        return resolved.Length == 0 ? RootPath : Path.Combine(RootPath, resolved.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool FileExists(string path) => File.Exists(GetFullPath(path));

    public bool DirectoryExists(string path) => Directory.Exists(GetFullPath(path));

    public FileEntry? GetFile(string path)
    {
        var info = new FileInfo(GetFullPath(path));
        return info.Exists ? new FileEntry(RelPath.Normalize(path), info.Length, info.LastWriteTimeUtc) : null;
    }

    public IEnumerable<FileEntry> EnumerateFiles(string directory, bool recursive)
    {
        var fullDirectory = GetFullPath(directory);
        if (!Directory.Exists(fullDirectory)) yield break;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
        };

        foreach (var file in new DirectoryInfo(fullDirectory).EnumerateFiles("*", options))
        {
            if (file.Name.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = Path.GetRelativePath(RootPath, file.FullName);
            yield return new FileEntry(RelPath.Normalize(relative), file.Length, file.LastWriteTimeUtc);
        }
    }

    public IEnumerable<string> EnumerateDirectories(string directory)
    {
        var fullDirectory = GetFullPath(directory);
        if (!Directory.Exists(fullDirectory)) return Enumerable.Empty<string>();

        return Directory.EnumerateDirectories(fullDirectory)
            .Select(d => RelPath.Normalize(Path.GetRelativePath(RootPath, d)));
    }

    public Stream OpenRead(string path) =>
        new FileStream(GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);

    public async Task WriteAtomicAsync(string path, Stream content, DateTime? lastWriteUtc = null, IProgress<long>? bytesWritten = null, CancellationToken cancellationToken = default)
    {
        var destination = GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var temp = destination + TempSuffix;
        try
        {
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                    bytesWritten?.Report(total);
                }
                await output.FlushAsync(cancellationToken);
            }

            if (lastWriteUtc.HasValue) File.SetLastWriteTimeUtc(temp, lastWriteUtc.Value);

            File.Move(temp, destination, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public void Move(string from, string to, bool overwrite = false)
    {
        var destination = GetFullPath(to);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(GetFullPath(from), destination, overwrite);
    }

    public void Delete(string path)
    {
        var full = GetFullPath(path);
        if (File.Exists(full)) File.Delete(full);
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(GetFullPath(path));

    public long? AvailableFreeSpace => TryGetDrive()?.AvailableFreeSpace;

    public string? Format => TryGetDrive()?.DriveFormat;

    private DriveInfo? TryGetDrive()
    {
        try
        {
            var root = Path.GetPathRoot(RootPath);
            if (string.IsNullOrEmpty(root)) return null;
            var drive = new DriveInfo(root);
            return drive.IsReady ? drive : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // A leftover temp file is harmless: it is ignored by scans and overwritten by the next copy.
        }
    }
}
