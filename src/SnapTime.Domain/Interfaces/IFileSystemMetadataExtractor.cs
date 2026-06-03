using SnapTime.Domain.Entities;

namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Extracts file system metadata (creation and last write timestamps) for a given file path.
/// </summary>
public interface IFileSystemMetadataExtractor
{
    /// <summary>
    /// Extracts creation time (ctime) and last write time (mtime) from the file system.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the file.</param>
    /// <returns>A list of <see cref="MetadataEntry"/> representing ctime and mtime, or an empty list if the file does not exist.</returns>
    List<MetadataEntry> ExtractFileSystemDates(string filePath);
}
