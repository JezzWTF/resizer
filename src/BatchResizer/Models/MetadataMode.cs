namespace BatchResizer.Models;

public enum MetadataMode
{
    PreserveAll,  // Keep all image metadata (EXIF, XMP, ICC, etc.)
    StripAll,     // Remove all metadata (smallest file, best privacy)
    ExifOnly,     // Keep EXIF data, strip XMP and ICC profiles
}
