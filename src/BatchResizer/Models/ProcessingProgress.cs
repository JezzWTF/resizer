namespace BatchResizer.Models;

public class ProcessingProgress
{
    public int Processed { get; set; }
    public int Total { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public string CurrentFile { get; set; } = "";
    public string? StatusMessage { get; set; }
    public double PercentComplete => Total > 0 ? (Processed + Skipped + Errors) / (double)Total * 100 : 0;
}
