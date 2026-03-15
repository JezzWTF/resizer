namespace BatchResizer.Models;

public class ResizePreset
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public ResizeMode Mode { get; set; } = ResizeMode.Fit;
    public double Percentage { get; set; } = 50;
    public bool IsCustom { get; set; }

    public static ResizePreset[] Defaults =>
    [
        new() { Name = "Small (854×480)", Width = 854, Height = 480, Mode = ResizeMode.Fit },
        new() { Name = "Medium (1366×768)", Width = 1366, Height = 768, Mode = ResizeMode.Fit },
        new() { Name = "Large (1920×1080)", Width = 1920, Height = 1080, Mode = ResizeMode.Fit },
        new() { Name = "2K (2560×1440)", Width = 2560, Height = 1440, Mode = ResizeMode.Fit },
        new() { Name = "4K (3840×2160)", Width = 3840, Height = 2160, Mode = ResizeMode.Fit },
        new() { Name = "Square 512", Width = 512, Height = 512, Mode = ResizeMode.Fill },
        new() { Name = "Square 1024", Width = 1024, Height = 1024, Mode = ResizeMode.Fill },
        new() { Name = "Thumbnail (320×240)", Width = 320, Height = 240, Mode = ResizeMode.Fit },
        new() { Name = "Custom", Width = 1920, Height = 1080, Mode = ResizeMode.Fit, IsCustom = true },
    ];
}
