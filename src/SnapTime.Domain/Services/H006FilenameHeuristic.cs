// [F3-US-001]
using System.Globalization;
using System.Text.RegularExpressions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Domain.Services;

public class H006FilenameHeuristic : IHeuristic
{
    private static readonly string[] DateTagPriority =
    [
        "EXIF:SubSecDateTimeOriginal",
        "EXIF:SubSecCreateDate",
        "EXIF:DateTimeOriginal",
        "EXIF:CreationDate",
        "EXIF:CreateDate",
        "QuickTime:MediaCreateDate"
    ];

    private static readonly string[] ExifDateFormats =
    [
        "yyyy:MM:dd HH:mm:ss",
        "yyyy:MM:dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd"
    ];

    public string Id => "H-006";
    public string Name => "ParseFilenameDateHeuristic";
    public bool IsEnabled => true;

    public Task<EvidenceEntry?> EvaluateAsync(
        MediaAsset asset,
        IReadOnlyList<MetadataEntry> metadata,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ct.ThrowIfCancellationRequested();

        var fileName = Path.GetFileNameWithoutExtension(asset.FileName);

        var parsedDate = TryParseDate(fileName);
        if (parsedDate == null)
            return Task.FromResult<EvidenceEntry?>(null);

        var canonicalDate = ResolveCanonicalDate(metadata);

        if (canonicalDate == null)
            return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Correction, parsedDate.Value,
                $"Filename suggests {parsedDate:yyyy-MM-dd}, no metadata date available"));

        if (canonicalDate.Value.Date == parsedDate.Value.Date)
            return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Positive, null,
                $"Filename date {parsedDate:yyyy-MM-dd} matches metadata date", weight: 0.3));

        return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Correction, parsedDate.Value,
            $"Filename suggests {parsedDate:yyyy-MM-dd}, but metadata has {canonicalDate:yyyy-MM-dd}", weight: 0.7));
    }

    private static EvidenceEntry CreateEvidence(
        Guid assetId,
        EvidenceDirection direction,
        DateTime? suggestedDate,
        string description,
        double weight = 0.5)
    {
        return new EvidenceEntry
        {
            Id = Guid.NewGuid(),
            HeuristicId = "H-006",
            HeuristicName = "ParseFilenameDateHeuristic",
            Weight = weight,
            Direction = direction,
            SuggestedDate = suggestedDate?.Date.AddHours(5),
            Description = description,
            MediaAssetId = assetId
        };
    }

    private static DateTime? TryParseDate(string fileName)
    {
        var match = Regex.Match(fileName, @"^(\d{8})");
        if (match.Success && DateTime.TryParseExact(
                match.Groups[1].Value, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        match = Regex.Match(fileName, @"^(\d{4}-\d{2}-\d{2})");
        if (match.Success && DateTime.TryParseExact(
                match.Groups[1].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        return null;
    }

    private static DateTime? ResolveCanonicalDate(IReadOnlyList<MetadataEntry> metadata)
    {
        foreach (var tag in DateTagPriority)
        {
            var entry = metadata.FirstOrDefault(m =>
                m.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (entry == null) continue;

            if (DateTime.TryParseExact(entry.Value, ExifDateFormats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date;

            if (DateTime.TryParse(entry.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return date;
        }

        return null;
    }
}
