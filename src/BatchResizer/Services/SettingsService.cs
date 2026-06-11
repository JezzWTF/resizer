using System.IO;
using System.Text.Json;
using BatchResizer.Models;

namespace BatchResizer.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JezzWTF", "BatchResizer", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly Action<string>? _logError;

    public SettingsService(Action<string>? logError = null)
    {
        _logError = logError;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return Validate(settings);
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"Failed to load settings: {ex.Message}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Validate(settings), JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"Failed to save settings: {ex.Message}");
        }
    }

    private static AppSettings Validate(AppSettings settings)
    {
        settings.SelectedPresetName = LimitLength(settings.SelectedPresetName, 128, "Large (1920×1080)");
        settings.ResizeMode = Enum.IsDefined(settings.ResizeMode) ? settings.ResizeMode : ResizeMode.Fit;
        settings.TargetWidth = Math.Clamp(settings.TargetWidth, 1, AppSettings.MaxDimension);
        settings.TargetHeight = Math.Clamp(settings.TargetHeight, 1, AppSettings.MaxDimension);
        settings.Percentage = double.IsFinite(settings.Percentage)
            ? Math.Clamp(settings.Percentage, AppSettings.MinPercentage, AppSettings.MaxPercentage)
            : 50;

        settings.OutputMode = Enum.IsDefined(settings.OutputMode) ? settings.OutputMode : OutputMode.Subfolder;
        settings.SubfolderName = ResizeOptionsValidator.SanitizePathComponent(settings.SubfolderName, "resized", allowEmpty: false);
        settings.FilePrefix = ResizeOptionsValidator.SanitizePathComponent(settings.FilePrefix, "", allowEmpty: true);
        settings.FileSuffix = ResizeOptionsValidator.SanitizePathComponent(settings.FileSuffix, "", allowEmpty: true);
        settings.CustomOutputFolder = NormalizeOptionalFullPath(settings.CustomOutputFolder);

        settings.OutputFormat = Enum.IsDefined(settings.OutputFormat) ? settings.OutputFormat : OutputFormat.KeepOriginal;
        settings.JpegQuality = Math.Clamp(settings.JpegQuality, 1, 100);
        settings.WebPQuality = Math.Clamp(settings.WebPQuality, 1, 100);
        settings.MetadataMode = Enum.IsDefined(settings.MetadataMode) ? settings.MetadataMode : MetadataMode.PreserveAll;

        settings.RecentFolders = (settings.RecentFolders ?? [])
            .Select(NormalizeOptionalFullPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(AppSettings.MaxRecentFolders)
            .ToList();

        return settings;
    }

    private static string NormalizeOptionalFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 32_767 || !Path.IsPathFullyQualified(path))
            return "";

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return "";
        }
    }

    private static string LimitLength(string? value, int maxLength, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value.Length > maxLength ? fallback : value;
}
