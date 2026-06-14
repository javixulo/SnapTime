// [F3-US-008]
using System.Globalization;
using System.Linq;
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

    private static readonly Dictionary<string, int> MonthAbbr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ene"]=1,["feb"]=2,["mar"]=3,["abr"]=4,["may"]=5,["jun"]=6,
        ["jul"]=7,["ago"]=8,["sep"]=9,["oct"]=10,["nov"]=11,["dic"]=12,
        ["jan"]=1,["feb"]=2,["mar"]=3,["apr"]=4,["may"]=5,["jun"]=6,
        ["jul"]=7,["aug"]=8,["sep"]=9,["oct"]=10,["nov"]=11,["dec"]=12
    };

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

        var fileName = asset.FileName;
        // Strip last extension manually — Path.GetFileNameWithoutExtension treats /
        // as a directory separator on Windows, breaking DD/MM/YYYY patterns (P9)
        var dot = fileName.LastIndexOf('.');
        if (dot > 0) fileName = fileName.Substring(0, dot);

        var parsedDate = TryParseDate(fileName);
        if (parsedDate == null)
            return Task.FromResult<EvidenceEntry?>(null);

        var canonicalDate = ResolveCanonicalDate(metadata);

        if (canonicalDate == null)
            return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Correction, parsedDate.Value,
                $"Filename suggests <strong>{parsedDate:dd/MM/yyyy}</strong>, no metadata date available"));

        if (canonicalDate.Value.Date == parsedDate.Value.Date)
            return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Positive, null,
                $"Filename date <strong>{parsedDate:dd/MM/yyyy}</strong> matches metadata date", weight: 0.3));

        return Task.FromResult<EvidenceEntry?>(CreateEvidence(asset.Id, EvidenceDirection.Correction, parsedDate.Value,
            $"Filename suggests <strong>{parsedDate:dd/MM/yyyy}</strong>, but metadata has <strong>{canonicalDate:dd/MM/yyyy}</strong>", weight: 0.7));
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
        // P4 evaluated before P1 because underscore normalization (line 102) removes _ separators.
        // For realistic filenames, P1 (8 consecutive digits) and P4 (yyyy_MM_dd) are mutually exclusive.

        // P4: yyyy_MM_dd — check before underscore normalization
        var match = Regex.Match(fileName, @"(?<=^|[\W_])(\d{4}_\d{2}_\d{2})(?=$|[\W_])");
        if (match.Success && DateTime.TryParseExact(
                match.Groups[1].Value.Replace('_', '-'), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        // Normalize underscores so \b boundaries work (underscore is \w in .NET regex)
        fileName = fileName.Replace('_', ' ');

        // P1: yyyyMMdd (8 dígitos consecutivos en cualquier posición)
        match = Regex.Match(fileName, @"\b(\d{8})\b");
        if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        // P2: yyyy-MM-dd
        match = Regex.Match(fileName, @"\b(\d{4}-\d{2}-\d{2})\b");
        if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        // P3: yyyy.MM.dd
        match = Regex.Match(fileName, @"\b(\d{4}\.\d{2}\.\d{2})\b");
        if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyy.MM.dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        var monthPattern = string.Join("|", MonthAbbr.Keys.Select(k => Regex.Escape(k)));

        // P5: DD MMM YYYY
        match = Regex.Match(fileName, $@"\b(\d{{1,2}})\s+({monthPattern})\s+(\d{{4}})\b", RegexOptions.IgnoreCase);
        if (match.Success && MonthAbbr.TryGetValue(match.Groups[2].Value, out var month5) &&
            int.TryParse(match.Groups[1].Value, out var day5) && day5 >= 1 && day5 <= 31)
            return new DateTime(int.Parse(match.Groups[3].Value), month5, day5);

        // P6: MMM DD YYYY
        match = Regex.Match(fileName, $@"\b({monthPattern})\s+(\d{{1,2}})\s+(\d{{4}})\b", RegexOptions.IgnoreCase);
        if (match.Success && MonthAbbr.TryGetValue(match.Groups[1].Value, out var month6) &&
            int.TryParse(match.Groups[2].Value, out var day6) && day6 >= 1 && day6 <= 31)
            return new DateTime(int.Parse(match.Groups[3].Value), month6, day6);

        // [F3-US-008] DD-MM-YYYY, DD.MM.YYYY, DD/MM/YYYY
        var europeanResult = TryParseDdMmYyyy(fileName, "-")
                             ?? TryParseDdMmYyyy(fileName, ".")
                             ?? TryParseDdMmYyyy(fileName, "/");
        if (europeanResult != null) return europeanResult;

        return null;
    }

    // [F3-US-008]
    private static DateTime? TryParseDdMmYyyy(string fileName, string separator)
    {
        var escaped = Regex.Escape(separator);
        var match = Regex.Match(fileName, $@"\b(\d{{2}}){escaped}(\d{{2}}){escaped}(\d{{4}})\b");
        if (match.Success && int.TryParse(match.Groups[2].Value, out var month) && month >= 1 && month <= 12)
            return new DateTime(int.Parse(match.Groups[3].Value), month, int.Parse(match.Groups[1].Value));
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
