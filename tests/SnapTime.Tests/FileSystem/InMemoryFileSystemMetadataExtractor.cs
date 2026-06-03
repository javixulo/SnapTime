using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.FileSystem;

public class InMemoryFileSystemMetadataExtractor : IFileSystemMetadataExtractor
{
    private readonly Dictionary<string, (DateTime creationTime, DateTime lastWriteTime)> _dates = new();

    public void AddResult(string filePath, DateTime creationTime, DateTime lastWriteTime)
    {
        _dates[filePath] = (creationTime, lastWriteTime);
    }

    public List<MetadataEntry> ExtractFileSystemDates(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!_dates.TryGetValue(filePath, out var dates))
            return new List<MetadataEntry>();

        return new List<MetadataEntry>
        {
            new()
            {
                Tag = "Filesystem:ctime",
                Value = dates.creationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "filesystem"
            },
            new()
            {
                Tag = "Filesystem:mtime",
                Value = dates.lastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "filesystem"
            }
        };
    }
}
