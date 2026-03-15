namespace BatchResizer.Models;

public class ResizeOptions
{
    // Source
    public List<string> SourceFolders { get; set; } = [];
    public bool Recursive { get; set; } = true;
    public HashSet<string> IncludedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tiff", ".tif", ".gif"];

    // Resize
    public ResizeMode ResizeMode { get; set; } = ResizeMode.Fit;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public double Percentage { get; set; } = 50;

    // Output
    public OutputMode OutputMode { get; set; } = OutputMode.Subfolder;
    public string SubfolderName { get; set; } = "resized";
    public string CustomOutputFolder { get; set; } = "";
    public string FilePrefix { get; set; } = "";
    public string FileSuffix { get; set; } = "";
    public bool SkipExisting { get; set; } = true;
    public bool SkipLargerThanTarget { get; set; } = false;

    // Format & Quality
    public OutputFormat OutputFormat { get; set; } = OutputFormat.KeepOriginal;
    public int JpegQuality { get; set; } = 85;
    public int WebPQuality { get; set; } = 80;

    // Metadata
    public bool PreserveTimestamps { get; set; } = true;
    public MetadataMode MetadataMode { get; set; } = MetadataMode.PreserveAll;

    // Performance
    public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
}
