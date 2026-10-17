using System.IO.Compression;
using System.Text;

namespace TrimuiSmartHub.Core.Tests;

/// <summary>A temporary folder deleted at the end of the test, with helpers to build fake SD cards.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tsh-tests", Guid.NewGuid().ToString("N"));

    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }

    public string Sub(string relative)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(full);
        return full;
    }

    public string Write(string relative, string content, DateTime? lastWriteUtc = null) =>
        Write(relative, Encoding.UTF8.GetBytes(content), lastWriteUtc);

    public string Write(string relative, byte[] content, DateTime? lastWriteUtc = null)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        if (lastWriteUtc.HasValue) File.SetLastWriteTimeUtc(full, lastWriteUtc.Value);
        return full;
    }

    public string WriteZip(string relative, params (string Name, string Content)[] entries)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        using var archive = ZipFile.Open(full, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }
        return full;
    }

    public bool Exists(string relative) => File.Exists(System.IO.Path.Combine(Path, relative));

    public string Read(string relative) => File.ReadAllText(System.IO.Path.Combine(Path, relative));

    /// <summary>Creates a stock-firmware SD card with one emulator per id.</summary>
    public void MakeTrimuiCard(params string[] platformIds)
    {
        foreach (var id in platformIds)
        {
            Write($"Emus/{id}/config.json", $$"""
                {
                  "label": "{{id}}",
                  "launch": "launch.sh",
                  "rompath": "../../Roms/{{id}}",
                  "imgpath": "../../Imgs/{{id}}",
                  "extlist": "gba|zip|7z",
                }
                """);
            Sub($"Roms/{id}");
            Sub($"Imgs/{id}");
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
