using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class FileSystemMetadataExtractorService : IFileSystemMetadataExtractor
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    private const string SourceName = "filesystem";
    private const string CtimeTag = "Filesystem:ctime";
    private const string MtimeTag = "Filesystem:mtime";

    public List<MetadataEntry> ExtractFileSystemDates(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty or whitespace.", nameof(filePath));

        if (!File.Exists(filePath))
            return [];

        return
        [
            CreateEntry(CtimeTag, File.GetCreationTime(filePath)),
            CreateEntry(MtimeTag, File.GetLastWriteTime(filePath))
        ];
    }

    private static MetadataEntry CreateEntry(string tag, DateTime dateTime)
    {
        return new MetadataEntry
        {
            Tag = tag,
            Value = dateTime.ToString(DateFormat),
            Source = SourceName
        };
    }
}
