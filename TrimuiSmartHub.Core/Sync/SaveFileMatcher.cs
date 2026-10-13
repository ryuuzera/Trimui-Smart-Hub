using System.Text.RegularExpressions;

namespace TrimuiSmartHub.Core.Sync;

/// <summary>
/// Emulators name save files after the ROM ("Pokemon Emerald.gba" → "Pokemon Emerald.srm",
/// "Pokemon Emerald.state1", "Pokemon Emerald.state.auto.png", DraStic's "Pokemon Emerald_0.dss").
/// This finds the save files of a ROM so they can be renamed together with it.
/// </summary>
public static partial class SaveFileMatcher
{
    /// <summary>Returns the part after the ROM base name ("." or "_" included), or null when the file isn't a save of that ROM.</summary>
    public static string? GetSuffix(string fileName, string romBaseName)
    {
        if (fileName.Length <= romBaseName.Length) return null;
        if (!fileName.StartsWith(romBaseName, StringComparison.OrdinalIgnoreCase)) return null;

        var suffix = fileName[romBaseName.Length..];
        return SaveSuffixRegex().IsMatch(suffix) ? suffix : null;
    }

    public static string? Rename(string fileName, string oldBaseName, string newBaseName)
    {
        var suffix = GetSuffix(fileName, oldBaseName);
        return suffix == null ? null : newBaseName + suffix;
    }

    // ".srm", ".sav", ".rtc", ".dsv", ".state", ".state3", ".state.auto", any of those + ".png", "_0.dss".
    [GeneratedRegex(@"^(\.(srm|sav|rtc|dsv|eep|sra|fla|mpk|mcd|ldci|state(\d+|\.auto)?)(\.png)?|_\d+\.dss)$", RegexOptions.IgnoreCase)]
    private static partial Regex SaveSuffixRegex();
}
