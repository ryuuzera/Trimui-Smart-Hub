using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace TrimuiSmartHub.Application.Infrastructure;

/// <summary>Windows shell integration: open folders/links, recycle bin, safe removal.</summary>
public static class ShellService
{
    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public static void OpenUrl(Uri url) =>
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });

    /// <summary>Deleting from the library goes to the recycle bin, so a mistake can be undone.</summary>
    public static void RecycleFile(string path)
    {
        if (File.Exists(path)) FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    public static void RecycleDirectory(string path)
    {
        if (Directory.Exists(path)) FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
    }

    /// <summary>Asks Windows to safely eject the drive (same as "Eject" in Explorer). Must run on an STA thread.</summary>
    public static bool Eject(string driveRoot)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return false;

            dynamic shell = Activator.CreateInstance(shellType)!;
            // 17 = ssfDRIVES ("This PC").
            var item = shell.NameSpace(17).ParseName(driveRoot.TrimEnd('\\'));
            if (item == null) return false;
            item.InvokeVerb("Eject");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
