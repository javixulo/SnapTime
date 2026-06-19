using MetadataExtractor;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

// [F1-US-009]
namespace SnapTime.Infrastructure.Services;

public class MetadataExtractorService(ILogger<MetadataExtractorService> logger) : IMetadataExtractor
{
    private static readonly Dictionary<string, HashSet<string>> ImageTargetDirectories = new()
    {
        ["Exif SubIFD"] = new HashSet<string>(["Date/Time Original", "Sub-Sec Time Original"], StringComparer.OrdinalIgnoreCase),
        ["Exif IFD0"] = new HashSet<string>(["Date/Time Digitized", "Date/Time"], StringComparer.OrdinalIgnoreCase)
    };

    private static readonly Dictionary<string, HashSet<string>> VideoTargetDirectories = new()
    {
        ["QuickTime Movie Header"] = new HashSet<string>(["Created", "Modified"], StringComparer.OrdinalIgnoreCase),
        ["QuickTime Metadata"] = new HashSet<string>(["Creation Date"], StringComparer.OrdinalIgnoreCase),
        ["QuickTime Track Header"] = new HashSet<string>(["Created"], StringComparer.OrdinalIgnoreCase)
    };

    private const string ExifSource = "exif";
    private const string QuickTimeSource = "quicktime";

    public async Task<List<MetadataEntry>> ExtractAsync(string filePath, MediaType mediaType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            logger.LogWarning("File not found: {FilePath}", filePath);
            return [];
        }

        try
        {
            var directories = await Task.Run(() => ImageMetadataReader.ReadMetadata(filePath), ct);
            var targetDirectories = mediaType == MediaType.Image ? ImageTargetDirectories : VideoTargetDirectories;
            var source = mediaType == MediaType.Image ? ExifSource : QuickTimeSource;
            var entries = new Dictionary<string, MetadataEntry>(StringComparer.Ordinal);

            foreach (var directory in directories)
            {
                ct.ThrowIfCancellationRequested();

                if (!targetDirectories.TryGetValue(directory.Name, out var targetTags))
                    continue;

                foreach (var tag in directory.Tags)
                {
                    if (!targetTags.Contains(tag.Name))
                        continue;

                    var key = $"{directory.Name}:{tag.Name}";
                    if (entries.ContainsKey(key))
                        continue;

                    entries[key] = new MetadataEntry
                    {
                        Tag = key,
                        Value = tag.Description,
                        Source = source
                    };
                }
            }

            return entries.Values.ToList();
        }
        catch (ImageProcessingException ex)
        {
            logger.LogWarning(ex, "Corrupt or unreadable metadata in {FilePath}", filePath);
            return [];
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
            return [];
        }
    }
}
