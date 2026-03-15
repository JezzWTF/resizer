using System.Globalization;
using System.Windows;
using System.Windows.Data;
using BatchResizer.Models;

namespace BatchResizer.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (Visibility)value == Visibility.Visible;
}

public class FileResultStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (FileResultStatus)value switch
        {
            FileResultStatus.Success => "#4CAF50",
            FileResultStatus.Skipped => "#9E9E9E",
            FileResultStatus.Error => "#F44336",
            _ => "#FFFFFF",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class FolderExistsToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b ? "#F44336" : "#CCCCCC";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i >= 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ResizeModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (Models.ResizeMode)value switch
        {
            Models.ResizeMode.Fit => "Fit (maintain aspect ratio)",
            Models.ResizeMode.Fill => "Fill (crop to fill)",
            Models.ResizeMode.Stretch => "Stretch (exact size, may distort)",
            Models.ResizeMode.LongestSide => "Longest Side",
            Models.ResizeMode.ShortestSide => "Shortest Side",
            Models.ResizeMode.Percentage => "Percentage",
            _ => value.ToString()!,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class OutputModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (OutputMode)value switch
        {
            OutputMode.InPlace => "Overwrite originals",
            OutputMode.Subfolder => "Save to subfolder",
            OutputMode.CustomFolder => "Save to custom folder (flat)",
            OutputMode.MirrorStructure => "Mirror structure to custom folder",
            _ => value.ToString()!,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class OutputFormatToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (OutputFormat)value switch
        {
            OutputFormat.KeepOriginal => "Keep original format",
            OutputFormat.Jpeg => "JPEG",
            OutputFormat.Png => "PNG",
            OutputFormat.WebP => "WebP",
            OutputFormat.Bmp => "BMP",
            _ => value.ToString()!,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
