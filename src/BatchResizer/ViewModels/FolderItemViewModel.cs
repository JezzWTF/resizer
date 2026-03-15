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

    public string DisplayName => Path.Length > 60
        ? "..." + Path[^57..]
        : Path;

    public string CountLabel => ImageCount == -1 ? "Not scanned" : $"{ImageCount} images";

    partial void OnPathChanged(string value)
    {
        Exists = Directory.Exists(value);
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnImageCountChanged(int value)
    {
        OnPropertyChanged(nameof(CountLabel));
    }
}
