using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
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
        var result = new ProcessingResult();
        IReadOnlyList<string> files;
        try
        {
            files = _discovery.DiscoverFiles(
                options.SourceFolders, options.Recursive, options.IncludedExtensions, ct);
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }

        var progressState = new ProcessingProgress { Total = files.Count };
        var parallelismLimit = Math.Max(1, Environment.ProcessorCount * 2);
        var maxParallelism = Math.Clamp(options.MaxParallelism, 1, parallelismLimit);

        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var tasks = new List<Task>();
        var resultsLock = new object();

        try
        {
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
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
        }
        catch (OperationCanceledException)
        {
            result.WasCancelled = true;
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
            if (!Path.IsPathFullyQualified(sourcePath))
                throw new InvalidOperationException("The source path must be fully qualified.");

            sourcePath = Path.GetFullPath(sourcePath);
            fileResult.SourcePath = sourcePath;
            if (!options.SourceFolders.Any(root => IsPathWithinRoot(sourcePath, root)))
                throw new InvalidOperationException("The source path is outside the selected source folders.");

            var outputPath = BuildOutputPath(sourcePath, options);
            fileResult.OutputPath = outputPath;

            if (options.SkipExisting && options.OutputMode != OutputMode.InPlace && File.Exists(outputPath))
            {
                fileResult.Status = FileResultStatus.Skipped;
                return fileResult;
            }

            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
                throw new FileNotFoundException("The source file no longer exists.", sourcePath);

            fileResult.OriginalBytes = sourceInfo.Length;
            if (sourceInfo.Length > options.MaxInputFileSizeBytes)
                throw new InvalidOperationException(
                    $"File exceeds the {FormatMegabytes(options.MaxInputFileSizeBytes)} MB input size limit.");

            var imageInfo = await Image.IdentifyAsync(sourcePath, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The image format could not be identified.");
            if ((long)imageInfo.Width * imageInfo.Height > options.MaxInputPixels)
                throw new InvalidOperationException(
                    $"Image exceeds the {options.MaxInputPixels:N0}-pixel safety limit.");

            if (options.SkipSmallerThanTarget && !ShouldResize(imageInfo.Width, imageInfo.Height, options))
            {
                fileResult.Status = FileResultStatus.Skipped;
                return fileResult;
            }

            using var image = await Image.LoadAsync(sourcePath, ct).ConfigureAwait(false);

            ApplyResize(image, options);
            ApplyMetadataMode(image, options.MetadataMode);

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var encoder = GetEncoder(sourcePath, options);
            try
            {
                await image.SaveAsync(outputPath, encoder, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!string.Equals(outputPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(outputPath); } catch { /* best-effort cleanup of partial write */ }
                }
                throw;
            }

            if (options.PreserveTimestamps)
                fileResult.WarningMessage = CopyTimestamps(sourceInfo, outputPath);

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

    private static string? CopyTimestamps(FileInfo source, string outputPath)
    {
        try
        {
            File.SetCreationTime(outputPath, source.CreationTime);
            File.SetLastWriteTime(outputPath, source.LastWriteTime);
            return null;
        }
        catch (Exception ex)
        {
            return $"Resized, but timestamps could not be preserved: {ex.Message}";
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

        string outputRoot;
        string outputPath;
        switch (options.OutputMode)
        {
            case OutputMode.InPlace:
                outputRoot = sourceDir;
                outputPath = Path.Combine(outputRoot, outFileName);
                break;
            case OutputMode.Subfolder:
                outputRoot = Path.Combine(sourceDir, options.SubfolderName);
                outputPath = Path.Combine(outputRoot, outFileName);
                break;
            case OutputMode.CustomFolder:
                outputRoot = options.CustomOutputFolder;
                outputPath = Path.Combine(outputRoot, outFileName);
                break;
            case OutputMode.MirrorStructure:
                outputRoot = options.CustomOutputFolder;
                outputPath = BuildMirroredPath(sourcePath, options, outputRoot, outFileName);
                break;
            default:
                throw new InvalidOperationException("Unsupported output mode.");
        }

        var normalizedRoot = Path.GetFullPath(outputRoot);
        var normalizedOutput = Path.GetFullPath(outputPath);
        if (!IsPathWithinRoot(normalizedOutput, normalizedRoot))
            throw new InvalidOperationException("The resolved output path is outside the selected output folder.");

        return normalizedOutput;
    }

    private static string BuildMirroredPath(
        string sourcePath,
        Opts options,
        string outputRoot,
        string outFileName)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath)!;
        var sourceRoot = options.SourceFolders
            .Where(folder => IsPathWithinRoot(sourcePath, folder))
            .OrderByDescending(folder => Path.GetFullPath(folder).Length)
            .FirstOrDefault();

        if (sourceRoot == null)
            throw new InvalidOperationException("The source path is outside the selected source folders.");

        var relative = Path.GetRelativePath(Path.GetFullPath(sourceRoot), sourceDir);
        return Path.Combine(outputRoot, relative, outFileName);
    }

    private static bool IsPathWithinRoot(string candidatePath, string rootPath)
    {
        if (!Path.IsPathFullyQualified(rootPath))
            return false;

        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(rootPath), Path.GetFullPath(candidatePath));
            return relative != ".." &&
                   !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                   !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) &&
                   !Path.IsPathFullyQualified(relative);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static long FormatMegabytes(long bytes) => bytes / (1024 * 1024);

    private static string GetExtensionForFormat(OutputFormat format) => format switch
    {
        OutputFormat.Jpeg => ".jpg",
        OutputFormat.Png => ".png",
        OutputFormat.WebP => ".webp",
        OutputFormat.Bmp => ".bmp",
        OutputFormat.Tiff => ".tiff",
        OutputFormat.Gif => ".gif",
        _ => ".jpg",
    };

    private static IImageEncoder GetEncoder(string sourcePath, Opts options)
    {
        var format = options.OutputFormat == OutputFormat.KeepOriginal
            ? GetFormatFromExtension(Path.GetExtension(sourcePath))
            : options.OutputFormat;

        return format switch
        {
            OutputFormat.Jpeg => new JpegEncoder
            {
                Quality = options.JpegQuality,
                ColorType = SixLabors.ImageSharp.Formats.Jpeg.JpegEncodingColor.YCbCrRatio420,
            },
            OutputFormat.Png => new PngEncoder
            {
                CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.BestCompression,
            },
            OutputFormat.WebP => new WebpEncoder { Quality = options.WebPQuality },
            OutputFormat.Bmp => new BmpEncoder(),
            OutputFormat.Tiff => new TiffEncoder(),
            OutputFormat.Gif => new GifEncoder(),
            _ => new JpegEncoder { Quality = options.JpegQuality },
        };
    }

    private static OutputFormat GetFormatFromExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => OutputFormat.Jpeg,
        ".png" => OutputFormat.Png,
        ".webp" => OutputFormat.WebP,
        ".bmp" => OutputFormat.Bmp,
        ".tiff" or ".tif" => OutputFormat.Tiff,
        ".gif" => OutputFormat.Gif,
        _ => OutputFormat.Jpeg,
    };
}
