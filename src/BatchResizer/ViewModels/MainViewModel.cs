using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using BatchResizer.Models;
using BatchResizer.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;

namespace BatchResizer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ImageProcessingService _processor = new();
    private readonly FileDiscoveryService _discovery = new();
    private readonly SettingsService _settingsService = new();
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _processingStopwatch = new();
    private readonly Queue<DateTime> _completionTimestamps = new();

    public MainViewModel()
    {
        LoadSettings();
    }

    // ── Source Folders ──────────────────────────────────────────────────────

    public ObservableCollection<FolderItemViewModel> SourceFolders { get; } = [];
    public ObservableCollection<string> RecentFolders { get; } = [];

    [ObservableProperty]
    private FolderItemViewModel? _selectedFolder;

    [ObservableProperty]
    private bool _recursive = true;

    // ── File Filters ────────────────────────────────────────────────────────

    [ObservableProperty] private bool _includeJpeg = true;
    [ObservableProperty] private bool _includePng = true;
    [ObservableProperty] private bool _includeWebP = true;
    [ObservableProperty] private bool _includeBmp = true;
    [ObservableProperty] private bool _includeTiff = true;
    [ObservableProperty] private bool _includeGif = false;

    // ── Resize Settings ─────────────────────────────────────────────────────

    public ResizePreset[] Presets { get; } = ResizePreset.Defaults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomPreset))]
    [NotifyPropertyChangedFor(nameof(IsPercentageMode))]
    [NotifyPropertyChangedFor(nameof(IsSingleDimensionMode))]
    private ResizePreset _selectedPreset = ResizePreset.Defaults[2]; // Large by default

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPercentageMode))]
    [NotifyPropertyChangedFor(nameof(IsSingleDimensionMode))]
    private Models.ResizeMode _resizeMode = Models.ResizeMode.Fit;

    [ObservableProperty] private int _targetWidth = 1920;
    [ObservableProperty] private int _targetHeight = 1080;
    [ObservableProperty] private double _percentage = 50;

    public bool IsCustomPreset => SelectedPreset?.IsCustom == true;
    public bool IsPercentageMode => ResizeMode == Models.ResizeMode.Percentage;
    public bool IsSingleDimensionMode => ResizeMode is Models.ResizeMode.LongestSide or Models.ResizeMode.ShortestSide;

    public Models.ResizeMode[] ResizeModes { get; } =
        (Models.ResizeMode[])Enum.GetValues(typeof(Models.ResizeMode));

    // ── Output Settings ─────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSubfolderName))]
    [NotifyPropertyChangedFor(nameof(ShowCustomOutputFolder))]
    [NotifyPropertyChangedFor(nameof(SkipExistingEnabled))]
    private OutputMode _outputMode = OutputMode.Subfolder;

    [ObservableProperty] private string _subfolderName = "resized";
    [ObservableProperty] private string _customOutputFolder = "";
    [ObservableProperty] private string _filePrefix = "";
    [ObservableProperty] private string _fileSuffix = "";
    [ObservableProperty] private bool _skipExisting = true;
    [ObservableProperty] private bool _skipSmallerThanTarget = false;

    public bool ShowSubfolderName => OutputMode == OutputMode.Subfolder;
    public bool ShowCustomOutputFolder => OutputMode is OutputMode.CustomFolder or OutputMode.MirrorStructure;
    public bool SkipExistingEnabled => OutputMode != OutputMode.InPlace;
    public OutputMode[] OutputModes { get; } = (OutputMode[])Enum.GetValues(typeof(OutputMode));

    // ── Format & Quality ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowJpegQuality))]
    [NotifyPropertyChangedFor(nameof(ShowWebPQuality))]
    private OutputFormat _outputFormat = OutputFormat.KeepOriginal;

    [ObservableProperty] private int _jpegQuality = 85;
    [ObservableProperty] private int _webPQuality = 80;

    public bool ShowJpegQuality => OutputFormat == OutputFormat.Jpeg;
    public bool ShowWebPQuality => OutputFormat == OutputFormat.WebP;
    public OutputFormat[] OutputFormats { get; } = (OutputFormat[])Enum.GetValues(typeof(OutputFormat));

    // ── Metadata ─────────────────────────────────────────────────────────────

    [ObservableProperty] private bool _preserveTimestamps = true;
    [ObservableProperty] private MetadataMode _metadataMode = MetadataMode.PreserveAll;

    public MetadataMode[] MetadataModes { get; } = (MetadataMode[])Enum.GetValues(typeof(MetadataMode));

    // ── Progress & State ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isProcessing;

    partial void OnIsProcessingChanged(bool value)
    {
        StartResizingCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private int _progressProcessed;
    [ObservableProperty] private int _progressTotal;
    [ObservableProperty] private int _progressSkipped;
    [ObservableProperty] private int _progressErrors;
    [ObservableProperty] private string _currentFile = "";
    [ObservableProperty] private string _statusMessage = "Add folders to get started.";
    [ObservableProperty] private int _scannedFileCount = -1;
    [ObservableProperty] private string _processingSpeed = "";
    [ObservableProperty] private string _processingEta = "";
    [ObservableProperty] private bool _isDragOver;
    [ObservableProperty] private bool _isComplete;

    public bool CanStart => !IsProcessing && SourceFolders.Count > 0;
    public bool IsIdle => !IsProcessing;

    public ObservableCollection<LogEntryViewModel> Log { get; } = [];

    private ICollectionView? _logView;
    public ICollectionView LogView => _logView ??= CreateLogView();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsFilterErrors))]
    [NotifyPropertyChangedFor(nameof(IsFilterSkipped))]
    private LogFilter _activeLogFilter = LogFilter.All;

    public bool IsFilterAll    => ActiveLogFilter == LogFilter.All;
    public bool IsFilterErrors => ActiveLogFilter == LogFilter.Errors;
    public bool IsFilterSkipped => ActiveLogFilter == LogFilter.Skipped;

    partial void OnActiveLogFilterChanged(LogFilter value) => _logView?.Refresh();

    // ── Commands ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddFolder()
    {
        var dlg = new VistaFolderBrowserDialog
        {
            Description = "Select a folder containing images",
            UseDescriptionForTitle = true,
            Multiselect = true,
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.SelectedPaths)
            AddFolderPath(path);
    }

    [RelayCommand]
    private void RemoveFolder()
    {
        if (SelectedFolder == null) return;
        SourceFolders.Remove(SelectedFolder);
        SelectedFolder = null;
        ScannedFileCount = -1;
        StartResizingCommand.NotifyCanExecuteChanged();
        UpdateStatusMessage();
    }

    [RelayCommand]
    private void ClearFolders()
    {
        SourceFolders.Clear();
        ScannedFileCount = -1;
        StartResizingCommand.NotifyCanExecuteChanged();
        UpdateStatusMessage();
    }

    [RelayCommand]
    private async Task ScanFolders()
    {
        if (SourceFolders.Count == 0) return;

        IsScanning = true;
        StatusMessage = "Scanning...";

        var extensions = BuildExtensionSet();
        var folders = SourceFolders.Select(f => f.Path).ToList();

        var files = await Task.Run(() =>
            _discovery.DiscoverFiles(folders, Recursive, extensions));

        // Update per-folder counts and sizes
        foreach (var folderVm in SourceFolders)
        {
            var folderFiles = await Task.Run(() =>
                _discovery.DiscoverFiles([folderVm.Path], Recursive, extensions));
            folderVm.ImageCount = folderFiles.Count;
            folderVm.TotalSizeBytes = await Task.Run(() =>
                folderFiles.Sum(f => new FileInfo(f).Length));
        }

        ScannedFileCount = files.Count;
        IsScanning = false;
        StatusMessage = $"Found {files.Count} images across {SourceFolders.Count} folder(s). Ready to resize.";
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var dlg = new VistaFolderBrowserDialog
        {
            Description = "Select output folder",
            UseDescriptionForTitle = true,
        };
        if (dlg.ShowDialog() == true)
            CustomOutputFolder = dlg.SelectedPath;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartResizing()
    {
        Log.Clear();
        IsComplete = false;
        _cts = new CancellationTokenSource();
        IsProcessing = true;
        ProgressProcessed = 0;
        ProgressSkipped = 0;
        ProgressErrors = 0;
        ProgressPercent = 0;
        ProcessingSpeed = "";
        ProcessingEta = "";
        StatusMessage = "Processing...";
        _processingStopwatch.Restart();
        _completionTimestamps.Clear();

        try
        {
            ApplyPresetToOptions();

            var options = BuildOptions();
            var progress = new Progress<ProcessingProgress>(p =>
            {
                ProgressProcessed = p.Processed;
                ProgressSkipped = p.Skipped;
                ProgressErrors = p.Errors;
                ProgressTotal = p.Total;
                ProgressPercent = p.PercentComplete;
                CurrentFile = System.IO.Path.GetFileName(p.CurrentFile);

                var now = DateTime.UtcNow;
                _completionTimestamps.Enqueue(now);
                var windowStart = now.AddSeconds(-5);
                while (_completionTimestamps.Count > 0 && _completionTimestamps.Peek() < windowStart)
                    _completionTimestamps.Dequeue();

                int remaining = p.Total - (p.Processed + p.Skipped + p.Errors);
                if (_completionTimestamps.Count >= 2)
                {
                    double windowSecs = (now - _completionTimestamps.Peek()).TotalSeconds;
                    double imgPerSec = windowSecs > 0 ? _completionTimestamps.Count / windowSecs : 0;
                    if (imgPerSec > 0)
                    {
                        double etaSecs = remaining / imgPerSec;
                        ProcessingSpeed = $"{imgPerSec:F1} img/s";
                        ProcessingEta = etaSecs < 60
                            ? $"~{(int)etaSecs}s remaining"
                            : $"~{(int)(etaSecs / 60)}m {(int)(etaSecs % 60)}s remaining";
                    }
                }

                if (p.CompletedFile is { } fr)
                    Log.Add(new LogEntryViewModel
                    {
                        Status = fr.Status,
                        FilePath = fr.SourcePath,
                        Message = fr.ErrorMessage,
                    });
            });

            var result = await _processor.ProcessAsync(options, progress, _cts.Token);

            var savings = result.TotalOriginalBytes > 0
                ? $" | Saved {FormatBytes(result.TotalOriginalBytes - result.TotalOutputBytes)}"
                : "";

            if (result.WasCancelled)
            {
                StatusMessage = $"Cancelled. {result.TotalProcessed} done, {result.TotalSkipped} skipped, {result.TotalErrors} errors.";
            }
            else
            {
                StatusMessage = $"Done in {result.Duration.TotalSeconds:F1}s — {result.TotalProcessed} resized, {result.TotalSkipped} skipped, {result.TotalErrors} errors{savings}.";
                IsComplete = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _processingStopwatch.Stop();
            ProcessingSpeed = "";
            ProcessingEta = "";
            _cts.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void SetLogFilter(LogFilter filter) => ActiveLogFilter = filter;

    [RelayCommand]
    private void AddRecentFolder(string path) => AddFolderPath(path);

    [RelayCommand]
    private void OnPresetSelected()
    {
        if (SelectedPreset == null || SelectedPreset.IsCustom) return;
        TargetWidth = SelectedPreset.Width;
        TargetHeight = SelectedPreset.Height;
        ResizeMode = SelectedPreset.Mode;
    }

    // ── Drag & Drop ─────────────────────────────────────────────────────────

    public void HandleDroppedPaths(string[] paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                AddFolderPath(path);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void AddFolderPath(string path)
    {
        if (SourceFolders.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
            return;

        SourceFolders.Add(new FolderItemViewModel { Path = path });
        RecordRecentFolder(path);
        ScannedFileCount = -1;
        StartResizingCommand.NotifyCanExecuteChanged();
        UpdateStatusMessage();
    }

    private void RecordRecentFolder(string path)
    {
        var existing = RecentFolders.FirstOrDefault(r => r.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (existing != null) RecentFolders.Remove(existing);
        RecentFolders.Insert(0, path);
        while (RecentFolders.Count > 10)
            RecentFolders.RemoveAt(RecentFolders.Count - 1);
    }

    private void UpdateStatusMessage()
    {
        if (SourceFolders.Count == 0)
            StatusMessage = "Add folders to get started.";
        else
            StatusMessage = $"{SourceFolders.Count} folder(s) added. Click Scan to count images, or Start to begin.";
    }

    private void ApplyPresetToOptions()
    {
        if (SelectedPreset == null) return;
        if (!SelectedPreset.IsCustom)
        {
            TargetWidth = SelectedPreset.Width;
            TargetHeight = SelectedPreset.Height;
            ResizeMode = SelectedPreset.Mode;
        }
    }

    private HashSet<string> BuildExtensionSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IncludeJpeg) { set.Add(".jpg"); set.Add(".jpeg"); }
        if (IncludePng) set.Add(".png");
        if (IncludeWebP) set.Add(".webp");
        if (IncludeBmp) set.Add(".bmp");
        if (IncludeTiff) { set.Add(".tiff"); set.Add(".tif"); }
        if (IncludeGif) set.Add(".gif");
        return set;
    }

    private ResizeOptions BuildOptions() => new()
    {
        SourceFolders = SourceFolders.Select(f => f.Path).ToList(),
        Recursive = Recursive,
        IncludedExtensions = BuildExtensionSet(),
        ResizeMode = ResizeMode,
        Width = TargetWidth,
        Height = TargetHeight,
        Percentage = Percentage,
        OutputMode = OutputMode,
        SubfolderName = SubfolderName,
        CustomOutputFolder = CustomOutputFolder,
        FilePrefix = FilePrefix,
        FileSuffix = FileSuffix,
        SkipExisting = SkipExisting,
        SkipSmallerThanTarget = SkipSmallerThanTarget,
        OutputFormat = OutputFormat,
        JpegQuality = JpegQuality,
        WebPQuality = WebPQuality,
        PreserveTimestamps = PreserveTimestamps,
        MetadataMode = MetadataMode,
        MaxParallelism = Math.Max(1, Environment.ProcessorCount / 2),
    };

    private ICollectionView CreateLogView()
    {
        var view = CollectionViewSource.GetDefaultView(Log);
        view.Filter = obj => obj is LogEntryViewModel e && ActiveLogFilter switch
        {
            LogFilter.Errors  => e.Status == FileResultStatus.Error,
            LogFilter.Skipped => e.Status == FileResultStatus.Skipped,
            _                 => true,
        };
        return view;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB",
    };

    // ── Settings persistence ─────────────────────────────────────────────────

    private void LoadSettings()
    {
        var s = _settingsService.Load();

        Recursive = s.Recursive;
        IncludeJpeg = s.IncludeJpeg;
        IncludePng = s.IncludePng;
        IncludeWebP = s.IncludeWebP;
        IncludeBmp = s.IncludeBmp;
        IncludeTiff = s.IncludeTiff;
        IncludeGif = s.IncludeGif;

        ResizeMode = s.ResizeMode;
        TargetWidth = s.TargetWidth;
        TargetHeight = s.TargetHeight;
        Percentage = s.Percentage;

        SelectedPreset = Presets.FirstOrDefault(p => p.Name == s.SelectedPresetName)
                         ?? Presets[2];

        OutputMode = s.OutputMode;
        SubfolderName = s.SubfolderName;
        CustomOutputFolder = s.CustomOutputFolder;
        FilePrefix = s.FilePrefix;
        FileSuffix = s.FileSuffix;
        SkipExisting = s.SkipExisting;
        SkipSmallerThanTarget = s.SkipSmallerThanTarget;

        OutputFormat = s.OutputFormat;
        JpegQuality = s.JpegQuality;
        WebPQuality = s.WebPQuality;

        PreserveTimestamps = s.PreserveTimestamps;
        MetadataMode = s.MetadataMode;

        RecentFolders.Clear();
        foreach (var f in s.RecentFolders)
            RecentFolders.Add(f);
    }

    public void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            Recursive = Recursive,
            IncludeJpeg = IncludeJpeg,
            IncludePng = IncludePng,
            IncludeWebP = IncludeWebP,
            IncludeBmp = IncludeBmp,
            IncludeTiff = IncludeTiff,
            IncludeGif = IncludeGif,

            ResizeMode = ResizeMode,
            TargetWidth = TargetWidth,
            TargetHeight = TargetHeight,
            Percentage = Percentage,
            SelectedPresetName = SelectedPreset?.Name ?? "",

            OutputMode = OutputMode,
            SubfolderName = SubfolderName,
            CustomOutputFolder = CustomOutputFolder,
            FilePrefix = FilePrefix,
            FileSuffix = FileSuffix,
            SkipExisting = SkipExisting,
            SkipSmallerThanTarget = SkipSmallerThanTarget,

            OutputFormat = OutputFormat,
            JpegQuality = JpegQuality,
            WebPQuality = WebPQuality,

            PreserveTimestamps = PreserveTimestamps,
            MetadataMode = MetadataMode,
            RecentFolders = RecentFolders.ToList(),
        });
    }
}
