using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BatchResizer.ViewModels;

public partial class FolderItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private bool _exists;

    [ObservableProperty]
    private int _imageCount = -1; // -1 = not scanned yet

    [ObservableProperty]
    private long _totalSizeBytes = -1;

    public string DisplayName => Path.Length > 60
        ? "..." + Path[^57..]
        : Path;

    public string CountLabel
    {
        get
        {
            if (ImageCount == -1) return "Not scanned";
            var size = TotalSizeBytes >= 0 ? $" · {FormatBytes(TotalSizeBytes)}" : "";
            return $"{ImageCount} images{size}";
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
    };

    partial void OnPathChanged(string value)
    {
        Exists = Directory.Exists(value);
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnImageCountChanged(int value)
    {
        OnPropertyChanged(nameof(CountLabel));
    }

    partial void OnTotalSizeBytesChanged(long value)
    {
        OnPropertyChanged(nameof(CountLabel));
    }
}
