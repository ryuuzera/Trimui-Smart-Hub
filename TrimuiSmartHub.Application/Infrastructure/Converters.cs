using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrimuiSmartHub.Application.Localization;

namespace TrimuiSmartHub.Application.Infrastructure;

public static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", value, units[unit]);
    }
}

public sealed class BytesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        long l => Format.Bytes(l),
        int i => Format.Bytes(i),
        _ => string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// true → Visible. Also treats non-null objects, non-empty strings/collections and non-zero numbers as true.
/// Parameter "invert" flips it; "multi" requires a collection with more than one item.
/// </summary>
public sealed class VisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            System.Collections.ICollection c when parameter as string == "multi" => c.Count > 1,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            string s => !string.IsNullOrEmpty(s),
            System.Collections.ICollection c => c.Count > 0,
            null => false,
            _ => true,
        };
        if (parameter as string == "invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
}

/// <summary>
/// Loads a thumbnail from disk without locking the file (so it can be replaced or deleted) and decoded at
/// a small size, which keeps long ROM lists light. Parameter: decode width (default 96).
/// </summary>
public sealed class ThumbnailConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = int.TryParse(parameter as string, out var width) ? width : 96;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Platform id → bundled emulator icon (pack URI), or null when there is none.</summary>
public sealed class PlatformIconConverter : IValueConverter
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string id) return null;
        if (Cache.TryGetValue(id, out var cached)) return cached;

        ImageSource? source = null;
        try
        {
            var uri = new Uri($"pack://application:,,,/Resources/Images/Emulators/{id.ToUpperInvariant()}.png");
            var info = System.Windows.Application.GetResourceStream(uri);
            if (info != null)
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = info.Stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                source = image;
            }
        }
        catch (IOException)
        {
        }

        Cache[id] = source;
        return source;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Enum value → localized text, using the key "{EnumType}_{Value}".</summary>
public sealed class EnumLocConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value == null ? string.Empty : Loc.Get($"{value.GetType().Name}_{value}");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class LocalTimeConverter : IValueConverter
{
    public static readonly LocalTimeConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTime d ? d.ToLocalTime() : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public static class ViewHelpers
{
    /// <summary>[count, isLoading] → Visible when there is nothing to show and nothing is loading (empty states).</summary>
    public static readonly IMultiValueConverter EmptyAndIdle = new EmptyAndIdleConverter();

    private sealed class EmptyAndIdleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length == 2 && values[0] is int count && values[1] is bool loading && count == 0 && !loading
                ? Visibility.Visible
                : Visibility.Collapsed;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

/// <summary>Fraction 0..1 → percentage 0..100 for progress bars.</summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double d ? d * 100 : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
