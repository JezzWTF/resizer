using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using IS_ResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using IS_ResizeOptions = SixLabors.ImageSharp.Processing.ResizeOptions;
using Opts = BatchResizer.Models.ResizeOptions;
using BM_ResizeMode = BatchResizer.Models.ResizeMode;
using BatchResizer.Models;

namespace BatchResizer.Services;

public class ImageProcessingService
{
    private readonly FileDiscoveryService _discovery = new();

    public async Task<ProcessingResult> ProcessAsync(
        Opts options,
        IProgress<ProcessingProgress> progress,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var files = _discovery.DiscoverFiles(options.SourceFolders, options.Recursive, options.IncludedExtensions);
        var result = new ProcessingResult();
        var progressState = new ProcessingProgress { Total = files.Count };

        var semaphore = new SemaphoreSlim(options.MaxParallelism, options.MaxParallelism);
        var tasks = new List<Task>();
        var resultsLock = new object();

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            var capturedFile = file;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var fileResult = await ProcessFileAsync(capturedFile, options, ct).ConfigureAwait(false);
                    lock (resultsLock)
                    {
                        result.FileResults.Add(fileResult);
                        progressState.CurrentFile = capturedFile;
                        switch (fileResult.Status)
                        {
                            case FileResultStatus.Success: progressState.Processed++; break;
                            case FileResultStatus.Skipped: progressState.Skipped++; break;
                            case FileResultStatus.Error: progressState.Errors++; break;
                        }
                        progress.Report(new ProcessingProgress
                        {
                            Total = progressState.Total,
                            Processed = progressState.Processed,
                            Skipped = progressState.Skipped,
                            Errors = progressState.Errors,
                            CurrentFile = capturedFile,
                            CompletedFile = fileResult,
                        });
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<FileResult> ProcessFileAsync(string sourcePath, Opts options, CancellationToken ct)
    {
        var fileResult = new FileResult { SourcePath = sourcePath };

        try
        {
            var outputPath = BuildOutputPath(sourcePath, options);
            fileResult.OutputPath = outputPath;

            if (options.SkipExisting && options.OutputMode != OutputMode.InPlace && File.Exists(outputPath))
            {
                fileResult.Status = FileResultStatus.Skipped;
                return fileResult;
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var sourceInfo = new FileInfo(sourcePath);
            fileResult.OriginalBytes = sourceInfo.Length;

            using var image = await Image.LoadAsync(sourcePath, ct).ConfigureAwait(false);

            if (options.SkipSmallerThanTarget && !ShouldResize(image.Width, image.Height, options))
            {
                fileResult.Status = FileResultStatus.Skipped;
                return fileResult;
            }

            ApplyResize(image, options);
            ApplyMetadataMode(image, options.MetadataMode);

            var encoder = GetEncoder(sourcePath, options);
            await image.SaveAsync(outputPath, encoder, ct).ConfigureAwait(false);

            if (options.PreserveTimestamps)
                CopyTimestamps(sourceInfo, outputPath);

            fileResult.OutputBytes = new FileInfo(outputPath).Length;
            fileResult.Status = FileResultStatus.Success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            fileResult.Status = FileResultStatus.Error;
            fileResult.ErrorMessage = ex.Message;
        }

        return fileResult;
    }

    private static bool ShouldResize(int width, int height, Opts options)
    {
        return options.ResizeMode switch
        {
            BM_ResizeMode.Percentage => true,
            BM_ResizeMode.LongestSide => Math.Max(width, height) > options.Width,
            BM_ResizeMode.ShortestSide => Math.Min(width, height) > options.Width,
            _ => width > options.Width || height > options.Height,
        };
    }

    private static void ApplyResize(Image image, Opts options)
    {
        int targetW = options.Width;
        int targetH = options.Height;

        switch (options.ResizeMode)
        {
            case BM_ResizeMode.Percentage:
            {
                double factor = options.Percentage / 100.0;
                targetW = (int)(image.Width * factor);
                targetH = (int)(image.Height * factor);
                image.Mutate(x => x.Resize(targetW, targetH, KnownResamplers.Lanczos3));
                break;
            }
            case BM_ResizeMode.LongestSide:
            {
                int longest = Math.Max(image.Width, image.Height);
                double ratio = options.Width / (double)longest;
                targetW = (int)(image.Width * ratio);
                targetH = (int)(image.Height * ratio);
                image.Mutate(x => x.Resize(targetW, targetH, KnownResamplers.Lanczos3));
                break;
            }
            case BM_ResizeMode.ShortestSide:
            {
                int shortest = Math.Min(image.Width, image.Height);
                double ratio = options.Width / (double)shortest;
                targetW = (int)(image.Width * ratio);
                targetH = (int)(image.Height * ratio);
                image.Mutate(x => x.Resize(targetW, targetH, KnownResamplers.Lanczos3));
                break;
            }
            case BM_ResizeMode.Fit:
                image.Mutate(x => x.Resize(new IS_ResizeOptions
                {
                    Size = new Size(targetW, targetH),
                    Mode = IS_ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3,
                }));
                break;
            case BM_ResizeMode.Fill:
                image.Mutate(x => x.Resize(new IS_ResizeOptions
                {
                    Size = new Size(targetW, targetH),
                    Mode = IS_ResizeMode.Crop,
                    Sampler = KnownResamplers.Lanczos3,
                }));
                break;
            case BM_ResizeMode.Stretch:
                image.Mutate(x => x.Resize(new IS_ResizeOptions
                {
                    Size = new Size(targetW, targetH),
                    Mode = IS_ResizeMode.Stretch,
                    Sampler = KnownResamplers.Lanczos3,
                }));
                break;
        }
    }

    private static void ApplyMetadataMode(Image image, MetadataMode mode)
    {
        switch (mode)
        {
            case MetadataMode.StripAll:
                image.Metadata.ExifProfile = null;
                image.Metadata.XmpProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.IptcProfile = null;
                break;
            case MetadataMode.ExifOnly:
                image.Metadata.XmpProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.IptcProfile = null;
                break;
            // PreserveAll: do nothing, ImageSharp keeps everything by default
        }
    }

    private static void CopyTimestamps(FileInfo source, string outputPath)
    {
        try
        {
            File.SetCreationTime(outputPath, source.CreationTime);
            File.SetLastWriteTime(outputPath, source.LastWriteTime);
        }
        catch
        {
            // Non-critical — some file systems don't support timestamp writes
        }
    }

    private static string BuildOutputPath(string sourcePath, Opts options)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath)!;
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = options.OutputFormat == OutputFormat.KeepOriginal
            ? Path.GetExtension(sourcePath)
            : GetExtensionForFormat(options.OutputFormat);

        var outFileName = $"{options.FilePrefix}{fileName}{options.FileSuffix}{ext}";

        return options.OutputMode switch
        {
            OutputMode.InPlace => Path.Combine(sourceDir, outFileName),
            OutputMode.Subfolder => Path.Combine(sourceDir, options.SubfolderName, outFileName),
            OutputMode.CustomFolder => Path.Combine(options.CustomOutputFolder, outFileName),
            OutputMode.MirrorStructure => BuildMirroredPath(sourcePath, options, outFileName),
            _ => Path.Combine(sourceDir, outFileName),
        };
    }

    private static string BuildMirroredPath(string sourcePath, Opts options, string outFileName)
    {
        if (string.IsNullOrEmpty(options.CustomOutputFolder))
            return Path.Combine(Path.GetDirectoryName(sourcePath)!, outFileName);

        var sourceDir = Path.GetDirectoryName(sourcePath)!;

        string? commonBase = null;
        foreach (var folder in options.SourceFolders)
        {
            if (sourceDir.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            {
                if (commonBase == null || folder.Length > commonBase.Length)
                    commonBase = folder;
            }
        }

        if (commonBase == null)
            return Path.Combine(options.CustomOutputFolder, outFileName);

        var relative = Path.GetRelativePath(commonBase, sourceDir);
        return Path.Combine(options.CustomOutputFolder, relative, outFileName);
    }

    private static string GetExtensionForFormat(OutputFormat format) => format switch
    {
        OutputFormat.Jpeg => ".jpg",
        OutputFormat.Png => ".png",
        OutputFormat.WebP => ".webp",
        OutputFormat.Bmp => ".bmp",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static IImageEncoder GetEncoder(string sourcePath, Opts options)
    {
        var format = options.OutputFormat == OutputFormat.KeepOriginal
            ? GetFormatFromExtension(Path.GetExtension(sourcePath))
            : options.OutputFormat;

        return format switch
        {
            OutputFormat.Jpeg => new JpegEncoder { Quality = options.JpegQuality },
            OutputFormat.Png => new PngEncoder(),
            OutputFormat.WebP => new WebpEncoder { Quality = options.WebPQuality },
            OutputFormat.Bmp => new BmpEncoder(),
            _ => new JpegEncoder { Quality = options.JpegQuality },
        };
    }

    private static OutputFormat GetFormatFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => OutputFormat.Jpeg,
        ".png" => OutputFormat.Png,
        ".webp" => OutputFormat.WebP,
        ".bmp" => OutputFormat.Bmp,
        _ => OutputFormat.Jpeg,
    };
}
