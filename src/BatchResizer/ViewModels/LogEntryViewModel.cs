using System.IO;
using BatchResizer.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BatchResizer.ViewModels;

public partial class LogEntryViewModel : ObservableObject
{
    public FileResultStatus Status { get; set; }
    public string FilePath { get; set; } = "";
    public string? Message { get; set; }

    public string StatusIcon => Status switch
    {
        FileResultStatus.Success => "✓",
        FileResultStatus.Skipped => "–",
        FileResultStatus.Error => "✗",
        _ => "?",
    };

    public string DisplayText => Status switch
    {
        FileResultStatus.Error => $"{Path.GetFileName(FilePath)} — {Message}",
        FileResultStatus.Skipped => $"{Path.GetFileName(FilePath)} (skipped)",
        _ => Path.GetFileName(FilePath),
    };
}
