using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.Metadata;

public class InMemoryMetadataExtractor : IMetadataExtractor
{
    private readonly Dictionary<string, List<MetadataEntry>> _results = new();
    private readonly HashSet<string> _corruptFiles = new();

    public void AddResult(string filePath, List<MetadataEntry> entries)
    {
        _results[filePath] = entries;
    }

    public void AddCorruptFile(string filePath)
    {
        _corruptFiles.Add(filePath);
    }

    public Task<List<MetadataEntry>> ExtractAsync(string filePath, MediaType mediaType, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(filePath);

        if (_corruptFiles.Contains(filePath))
        {
            try
            {
                throw new InvalidOperationException("Simulated corrupt image");
            }
            catch (InvalidOperationException)
            {
                return Task.FromResult(new List<MetadataEntry>());
            }
        }

        if (_results.TryGetValue(filePath, out var entries))
            return Task.FromResult(entries);

        return Task.FromResult(new List<MetadataEntry>());
    }

    public static List<MetadataEntry> CreateExifEntries(
        DateTime? dateTimeOriginal = null,
        string? subSecDateTimeOriginal = null,
        DateTime? createDate = null,
        DateTime? modifyDate = null)
    {
        var entries = new List<MetadataEntry>();

        if (dateTimeOriginal.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "Exif SubIFD:DateTime Original",
                Value = dateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss"),
                Source = "exif"
            });
        }

        if (subSecDateTimeOriginal != null)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "Exif SubIFD:Sub Sec Time Original",
                Value = subSecDateTimeOriginal,
                Source = "exif"
            });
        }

        if (createDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "Exif IFD0:Date/Time Digitized",
                Value = createDate.Value.ToString("yyyy:MM:dd HH:mm:ss"),
                Source = "exif"
            });
        }

        if (modifyDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "Exif IFD0:Date/Time",
                Value = modifyDate.Value.ToString("yyyy:MM:dd HH:mm:ss"),
                Source = "exif"
            });
        }

        return entries;
    }

    public static List<MetadataEntry> CreateQuickTimeEntries(
        DateTime? createDate = null,
        DateTime? modifyDate = null,
        DateTime? creationDate = null,
        DateTime? mediaCreateDate = null,
        DateTime? trackCreateDate = null)
    {
        var entries = new List<MetadataEntry>();

        if (createDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Movie Header:Create Date",
                Value = createDate.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }

        if (modifyDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Movie Header:Modify Date",
                Value = modifyDate.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }

        if (creationDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Metadata:Creation Date",
                Value = creationDate.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }

        if (mediaCreateDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Media Header:Media Create Date",
                Value = mediaCreateDate.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }

        if (trackCreateDate.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Track Header:Track Create Date",
                Value = trackCreateDate.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }

        return entries;
    }
}
