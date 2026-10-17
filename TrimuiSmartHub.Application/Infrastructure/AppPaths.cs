using System.IO;

namespace TrimuiSmartHub.Application.Infrastructure;

public sealed class AppPaths
{
    public AppPaths()
    {
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrimuiSmartHub");
        LocalData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrimuiSmartHub");

        SettingsFile = Path.Combine(roaming, "settings.json");
        Cache = Path.Combine(LocalData, "cache");
        Logs = Path.Combine(LocalData, "logs");
        DownloadsTemp = Path.Combine(LocalData, "downloads");
        GameDatabaseSource = Path.Combine(AppContext.BaseDirectory, "Data", "data.tsh");
    }

    public string LocalData { get; }

    public string SettingsFile { get; }

    public string Cache { get; }

    public string Logs { get; }

    public string DownloadsTemp { get; }

    public string GameDatabaseSource { get; }
}
