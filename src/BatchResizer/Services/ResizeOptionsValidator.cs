using System.IO;
using BatchResizer.Models;
using Opts = BatchResizer.Models.ResizeOptions;

namespace BatchResizer.Services;

public static class ResizeOptionsValidator
{
    private const int MaxPathComponentLength = 128;
    private static readonly char[] InvalidWindowsFileNameChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string? Validate(Opts options)
    {
        if (options.SourceFolders.Count == 0)
            return "Add at least one source folder.";

        foreach (var folder in options.SourceFolders)
        {
            if (!Path.IsPathFullyQualified(folder))
                return $"Source folder must be a fully qualified path: {folder}";
        }

        if (options.Width is < 1 or > AppSettings.MaxDimension ||
            options.Height is < 1 or > AppSettings.MaxDimension)
            return $"Width and height must be between 1 and {AppSettings.MaxDimension:N0} pixels.";

        if (!double.IsFinite(options.Percentage) ||
            options.Percentage is < AppSettings.MinPercentage or > AppSettings.MaxPercentage)
            return $"Percentage must be between {AppSettings.MinPercentage} and {AppSettings.MaxPercentage:N0}.";

        if (options.JpegQuality is < 1 or > 100 || options.WebPQuality is < 1 or > 100)
            return "Image quality must be between 1 and 100.";

        if (options.MaxInputFileSizeBytes <= 0 || options.MaxInputPixels <= 0)
            return "Input file safety limits must be greater than zero.";

        var componentError = ValidatePathComponent(options.FilePrefix, "File prefix", allowEmpty: true)
            ?? ValidatePathComponent(options.FileSuffix, "File suffix", allowEmpty: true);
        if (componentError != null)
            return componentError;

        if (options.OutputMode == OutputMode.Subfolder)
        {
            componentError = ValidatePathComponent(options.SubfolderName, "Subfolder name", allowEmpty: false);
            if (componentError != null)
                return componentError;
        }

        if ((options.OutputMode is OutputMode.CustomFolder or OutputMode.MirrorStructure) &&
            (string.IsNullOrWhiteSpace(options.CustomOutputFolder) ||
             !Path.IsPathFullyQualified(options.CustomOutputFolder)))
            return "Select a fully qualified custom output folder.";

        return null;
    }

    public static string? ValidatePathComponent(string? value, string displayName, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(value))
            return allowEmpty ? null : $"{displayName} cannot be empty.";

        if (value.Length > MaxPathComponentLength)
            return $"{displayName} cannot exceed {MaxPathComponentLength} characters.";

        if (value is "." or ".." || value.Any(c => c < ' ' || InvalidWindowsFileNameChars.Contains(c)))
            return $"{displayName} contains invalid path characters.";

        if (value.EndsWith(' ') || value.EndsWith('.'))
            return $"{displayName} cannot end with a space or period.";

        var stem = Path.GetFileNameWithoutExtension(value);
        if (WindowsReservedNames.Contains(stem))
            return $"{displayName} uses a reserved Windows device name.";

        return null;
    }

    public static string SanitizePathComponent(string? value, string fallback, bool allowEmpty)
    {
        var candidate = (value ?? string.Empty).Trim();
        return ValidatePathComponent(candidate, "Value", allowEmpty) == null ? candidate : fallback;
    }
}
