using System.IO.Compression;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace TrimuiSmartHub.Core.Games;

public static class FileHasher
{
    /// <summary>
    /// Above this size files are identified by name only: hashing a 1.5 GB PSP ISO over USB would take
    /// minutes and the database only has CRCs of individual disc tracks anyway.
    /// </summary>
    public const long MaxHashSize = 128L * 1024 * 1024;

    public static async Task<uint> Crc32Async(Stream stream, CancellationToken cancellationToken = default)
    {
        var crc = new Crc32();
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            crc.Append(buffer.AsSpan(0, read));
        }
        return crc.GetCurrentHashAsUInt32();
    }

    public static async Task<string> Md5Async(Stream stream, CancellationToken cancellationToken = default)
    {
        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Reads the CRC32 of the entries in a zip from its central directory, without decompressing anything.
    /// </summary>
    public static IReadOnlyList<(string Name, uint Crc32, long Size)> ReadZipEntries(Stream zipStream)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        return archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => (e.FullName, e.Crc32, e.Length))
            .ToList();
    }
}
