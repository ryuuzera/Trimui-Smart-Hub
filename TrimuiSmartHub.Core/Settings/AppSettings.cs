using System.Text.Json;
using TrimuiSmartHub.Core.Scraping;

namespace TrimuiSmartHub.Core.Settings;

public sealed class AppSettings
{
    public string LibraryPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Trimui Smart Hub");

    /// <summary>"en" or "pt-BR". Empty = follow Windows.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Back up saves automatically every time a device is connected.</summary>
    public bool AutoBackupSaves { get; set; } = true;

    public int BackupKeepLatest { get; set; } = 10;

    public int BackupKeepMonthly { get; set; } = 12;

    public MediaKind ImageKind { get; set; } = MediaKind.Boxart;

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Compare file contents when sizes match but dates differ (slower).</summary>
    public bool SyncVerifyContent { get; set; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupted settings: start from defaults rather than refusing to start.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, _path, overwrite: true);
        Current = settings;
        Changed?.Invoke(settings);
    }
}
