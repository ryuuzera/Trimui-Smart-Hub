using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Application.Infrastructure;
using TrimuiSmartHub.Application.Localization;
using TrimuiSmartHub.Core.Scraping;
using TrimuiSmartHub.Core.Settings;
using TrimuiSmartHub.Core.Updates;

namespace TrimuiSmartHub.Application.ViewModels;

public sealed record LanguageOption(string Code, string Name);

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly SettingsStore _store;
    private readonly DialogService _dialogs;
    private readonly AppPaths _paths;

    [ObservableProperty]
    private string _libraryPath = string.Empty;

    [ObservableProperty]
    private LanguageOption? _language;

    [ObservableProperty]
    private bool _autoBackupSaves;

    [ObservableProperty]
    private int _backupKeepLatest;

    [ObservableProperty]
    private int _backupKeepMonthly;

    [ObservableProperty]
    private MediaKind _imageKind;

    [ObservableProperty]
    private bool _checkForUpdates;

    [ObservableProperty]
    private bool _syncVerifyContent;

    [ObservableProperty]
    private string? _savedMessage;

    public SettingsViewModel(SettingsStore store, DialogService dialogs, AppPaths paths)
    {
        _store = store;
        _dialogs = dialogs;
        _paths = paths;
        Languages = new[] { new LanguageOption(string.Empty, Loc.Get("LanguageSystem")) }
            .Concat(Loc.Languages.Select(l => new LanguageOption(l.Code, l.Name)))
            .ToList();
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public IReadOnlyList<MediaKind> Kinds { get; } = Enum.GetValues<MediaKind>();

    public override void OnNavigatedTo()
    {
        var s = _store.Current;
        LibraryPath = s.LibraryPath;
        Language = Languages.FirstOrDefault(l => l.Code == s.Language) ?? Languages[0];
        AutoBackupSaves = s.AutoBackupSaves;
        BackupKeepLatest = s.BackupKeepLatest;
        BackupKeepMonthly = s.BackupKeepMonthly;
        ImageKind = s.ImageKind;
        CheckForUpdates = s.CheckForUpdates;
        SyncVerifyContent = s.SyncVerifyContent;
        SavedMessage = null;
    }

    [RelayCommand]
    private void BrowseLibrary()
    {
        var folder = _dialogs.PickFolder(Loc.Get("SettingsLibraryPick"), LibraryPath);
        if (folder != null) LibraryPath = folder;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var previous = _store.Current;
        var languageChanged = (Language?.Code ?? string.Empty) != previous.Language;

        try
        {
            _store.Save(new AppSettings
            {
                LibraryPath = LibraryPath,
                Language = Language?.Code ?? string.Empty,
                AutoBackupSaves = AutoBackupSaves,
                BackupKeepLatest = Math.Clamp(BackupKeepLatest, 1, 1000),
                BackupKeepMonthly = Math.Clamp(BackupKeepMonthly, 0, 120),
                ImageKind = ImageKind,
                CheckForUpdates = CheckForUpdates,
                SyncVerifyContent = SyncVerifyContent,
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.ShowAsync(Loc.Get("Error"), ex.Message);
            return;
        }

        SavedMessage = languageChanged ? Loc.Get("SettingsSavedRestart") : Loc.Get("SettingsSaved");
    }

    [RelayCommand]
    private void OpenLogs() => ShellService.OpenFolder(_paths.Logs);

    [RelayCommand]
    private static void OpenProject() => ShellService.OpenUrl(new Uri($"https://github.com/{UpdateChecker.Repository}"));
}
