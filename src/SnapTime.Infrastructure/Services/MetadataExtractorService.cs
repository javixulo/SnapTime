using System.Globalization;
using MetadataExtractor;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class MetadataExtractorService(ILogger<MetadataExtractorService> logger) : IMetadataExtractor
{
    private static readonly Dictionary<string, HashSet<string>> ImageTargetDirectories = new()
    {
        ["Exif SubIFD"] = new HashSet<string>(["DateTime Original", "Sub Sec Time Original"], StringComparer.OrdinalIgnoreCase),
        ["Exif IFD0"] = new HashSet<string>(["Date/Time Digitized", "Date/Time"], StringComparer.OrdinalIgnoreCase)
    };

    private static readonly Dictionary<string, HashSet<string>> VideoTargetDirectories = new()
    {
        ["QuickTime Movie Header"] = new HashSet<string>(["Create Date", "Modify Date"], StringComparer.OrdinalIgnoreCase),
        ["QuickTime Metadata"] = new HashSet<string>(["Creation Date"], StringComparer.OrdinalIgnoreCase),
        ["QuickTime Media Header"] = new HashSet<string>(["Media Create Date"], StringComparer.OrdinalIgnoreCase),
        ["QuickTime Track Header"] = new HashSet<string>(["Track Create Date"], StringComparer.OrdinalIgnoreCase)
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
            var entries = new List<MetadataEntry>();

            foreach (var directory in directories)
            {
                ct.ThrowIfCancellationRequested();

                if (!targetDirectories.TryGetValue(directory.Name, out var targetTags))
                    continue;

                foreach (var tag in directory.Tags)
                {
                    if (!targetTags.Contains(tag.Name))
                        continue;

                    if (!DateTime.TryParseExact(tag.Description, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        continue;

                    entries.Add(new MetadataEntry
                    {
                        Tag = $"{directory.Name}:{tag.Name}",
                        Value = tag.Description,
                        Source = source
                    });
                }
            }

            return entries;
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
