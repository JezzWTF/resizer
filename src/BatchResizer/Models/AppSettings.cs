namespace BatchResizer.Models;

public class AppSettings
{
    // File filters
    public bool Recursive { get; set; } = true;
    public bool IncludeJpeg { get; set; } = true;
    public bool IncludePng { get; set; } = true;
    public bool IncludeWebP { get; set; } = true;
    public bool IncludeBmp { get; set; } = true;
    public bool IncludeTiff { get; set; } = true;
    public bool IncludeGif { get; set; } = false;

    // Resize
    public string SelectedPresetName { get; set; } = "Large (1920×1080)";
    public ResizeMode ResizeMode { get; set; } = ResizeMode.Fit;
    public int TargetWidth { get; set; } = 1920;
    public int TargetHeight { get; set; } = 1080;
    public double Percentage { get; set; } = 50;

    // Output
    public OutputMode OutputMode { get; set; } = OutputMode.Subfolder;
    public string SubfolderName { get; set; } = "resized";
    public string CustomOutputFolder { get; set; } = "";
    public string FilePrefix { get; set; } = "";
    public string FileSuffix { get; set; } = "";
    public bool SkipExisting { get; set; } = true;
    public bool SkipSmallerThanTarget { get; set; } = false;

    // Format & quality
    public OutputFormat OutputFormat { get; set; } = OutputFormat.KeepOriginal;
    public int JpegQuality { get; set; } = 85;
    public int WebPQuality { get; set; } = 80;

    // Metadata
    public bool PreserveTimestamps { get; set; } = true;
    public MetadataMode MetadataMode { get; set; } = MetadataMode.PreserveAll;
}
