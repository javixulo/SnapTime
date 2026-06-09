using SnapTime.Domain.Enums;

namespace SnapTime.Server.Models;

/// <summary>
/// Represents a single entry in the photo grid — either a file or a subdirectory.
/// </summary>
/// <param name="Id">Database ID for indexed assets; <see cref="Guid.Empty"/> for files/dirs not yet in DB.</param>
/// <param name="Name">File or directory name.</param>
/// <param name="Path">Full file system path.</param>
/// <param name="IsDirectory">True if this is a directory, false if it's a file.</param>
/// <param name="ThumbnailUrl">URL to the thumbnail image, or null for directories/files without thumbnails.</param>
/// <param name="MediaStatus">Current processing status of the asset.</param>
/// <param name="HasSuggestion">Whether the asset has a suggested date correction.</param>
/// <param name="SuggestedDate">The suggested corrected date, if any.</param>
/// <param name="MediaType">Type of media (Image or Video).</param>
public record PhotoGridItem(
    Guid Id,
    string Name,
    string Path,
    bool IsDirectory,
    string? ThumbnailUrl,
    MediaStatus MediaStatus,
    bool HasSuggestion,
    DateTime? SuggestedDate,
    MediaType MediaType
);
