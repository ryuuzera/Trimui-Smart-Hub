using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using TrimuiSmartHub.Application.Localization;

namespace TrimuiSmartHub.Application.Infrastructure;

/// <summary>One place for message/confirm/input dialogs (previously copy-pasted in every page).</summary>
public sealed class DialogService
{
    private static MetroWindow Window => (MetroWindow)System.Windows.Application.Current.MainWindow;

    private static MetroDialogSettings Settings(string? affirmative = null, string? negative = null) => new()
    {
        AffirmativeButtonText = affirmative ?? Loc.Get("Ok"),
        NegativeButtonText = negative ?? Loc.Get("Cancel"),
        AnimateShow = true,
        AnimateHide = false,
        ColorScheme = MetroDialogColorScheme.Theme,
        DialogTitleFontSize = 22,
        DialogMessageFontSize = 15,
        DefaultButtonFocus = MessageDialogResult.Affirmative,
    };

    public Task ShowAsync(string title, string message) =>
        Window.ShowMessageAsync(title, message, MessageDialogStyle.Affirmative, Settings());

    public async Task<bool> ConfirmAsync(string title, string message, string? confirmText = null)
    {
        var result = await Window.ShowMessageAsync(title, message, MessageDialogStyle.AffirmativeAndNegative, Settings(confirmText));
        return result == MessageDialogResult.Affirmative;
    }

    public Task<string?> InputAsync(string title, string message, string? initial = null)
    {
        var settings = Settings();
        settings.DefaultText = initial ?? string.Empty;
        return Window.ShowInputAsync(title, message, settings);
    }

    public string? PickFolder(string title, string? initial = null)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title, InitialDirectory = initial ?? string.Empty };
        return dialog.ShowDialog(Window) == true ? dialog.FolderName : null;
    }

    public IReadOnlyList<string> PickFiles(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = title, Multiselect = true };
        return dialog.ShowDialog(Window) == true ? dialog.FileNames : Array.Empty<string>();
    }
}
