using System.IO;
using BatchResizer.Models;

namespace BatchResizer.Services;

public class FileDiscoveryService
{
    public IReadOnlyList<string> DiscoverFiles(IEnumerable<string> folders, bool recursive, HashSet<string> extensions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();
        var enumOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = recursive,
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*", enumOptions))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!extensions.Contains(ext))
                    continue;

                var normalized = Path.GetFullPath(file);
                if (seen.Add(normalized))
                    results.Add(normalized);
            }
        }

        return results;
    }
}
