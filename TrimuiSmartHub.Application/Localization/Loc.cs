using System.Globalization;
using System.Text.Json;
using System.Windows.Markup;

namespace TrimuiSmartHub.Application.Localization;

/// <summary>
/// UI strings from embedded JSON files (Localization/en.json, Localization/pt-BR.json).
/// English is the fallback for missing keys. The language is chosen at startup.
/// </summary>
public static class Loc
{
    public static readonly IReadOnlyList<(string Code, string Name)> Languages = new[]
    {
        ("en", "English"),
        ("pt-BR", "Português (Brasil)"),
    };

    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallback = new();

    public static string Language { get; private set; } = "en";

    public static void Initialize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? "pt-BR" : "en";
        }

        _fallback = LoadResource("en");
        _strings = language == "en" ? _fallback : LoadResource(language);
        Language = language;
    }

    public static string Get(string key) =>
        _strings.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : $"[{key}]";

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static Dictionary<string, string> LoadResource(string language)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"Localization.{language}.json");
        if (stream == null) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new Dictionary<string, string>();
    }
}

/// <summary>XAML: <c>Text="{l:Loc Library}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
