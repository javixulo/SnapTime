using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Extracts date-related metadata entries from media files.
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extracts relevant metadata entries (EXIF / QuickTime) from the specified file.
    /// </summary>
    /// <param name="filePath">Absolute path to the media file.</param>
    /// <param name="mediaType">Type of media (Image or Video).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of extracted metadata entries.</returns>
    Task<List<MetadataEntry>> ExtractAsync(string filePath, MediaType mediaType, CancellationToken ct);
}
