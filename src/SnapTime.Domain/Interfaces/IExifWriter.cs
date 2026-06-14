// [F8-US-002] IExifWriter interface for writing EXIF/QuickTime metadata
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Interfaces;

public interface IExifWriter
{
    Task<ExifWriteResult> WriteAsync(
        string filePath,
        MediaType mediaType,
        DateTime newDate,
        DateTime? originalDate,
        IReadOnlyList<string> heuristicIds,
        CancellationToken ct = default);
}

public record ExifWriteResult(bool Success, string? ErrorMessage);
