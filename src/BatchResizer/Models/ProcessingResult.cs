namespace BatchResizer.Models;

public enum FileResultStatus { Success, Skipped, Error }

public class FileResult
{
    public string SourcePath { get; set; } = "";
    public string? OutputPath { get; set; }
    public FileResultStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public long OriginalBytes { get; set; }
    public long OutputBytes { get; set; }
}

public class ProcessingResult
{
    public List<FileResult> FileResults { get; set; } = [];
    public int TotalProcessed => FileResults.Count(r => r.Status == FileResultStatus.Success);
    public int TotalSkipped => FileResults.Count(r => r.Status == FileResultStatus.Skipped);
    public int TotalErrors => FileResults.Count(r => r.Status == FileResultStatus.Error);
    public bool WasCancelled { get; set; }
    public TimeSpan Duration { get; set; }
    public long TotalOriginalBytes => FileResults.Where(r => r.Status == FileResultStatus.Success).Sum(r => r.OriginalBytes);
    public long TotalOutputBytes => FileResults.Where(r => r.Status == FileResultStatus.Success).Sum(r => r.OutputBytes);
}
