using System.Text;
using ImageMagick;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class ExifWriter : IExifWriter
{
    private static readonly byte[] AsciiPrefix = [0x41, 0x53, 0x43, 0x49, 0x49, 0x00, 0x00, 0x00];

    public async Task<ExifWriteResult> WriteAsync(
        string filePath,
        MediaType mediaType,
        DateTime newDate,
        DateTime? originalDate,
        IReadOnlyList<string> heuristicIds,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return new ExifWriteResult(false, "File not found");

            if (new FileInfo(filePath).IsReadOnly)
                return new ExifWriteResult(false, "File is read-only");

            switch (mediaType)
            {
                case MediaType.Image:
                    return await WriteImageAsync(filePath, newDate, originalDate, heuristicIds, ct);
                case MediaType.Video:
                    return new ExifWriteResult(false, "Video metadata writing not yet implemented");
                default:
                    return new ExifWriteResult(false, $"Unsupported media type: {mediaType}");
            }
        }
        catch (MagickCorruptImageErrorException ex)
        {
            return new ExifWriteResult(false, $"Corrupt or invalid image: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ExifWriteResult(false, ex.Message);
        }
    }

    private static async Task<ExifWriteResult> WriteImageAsync(
        string filePath,
        DateTime newDate,
        DateTime? originalDate,
        IReadOnlyList<string> heuristicIds,
        CancellationToken ct)
    {
        var dateStr = newDate.ToString("yyyy:MM:dd HH:mm:ss");
        var annotation = BuildAnnotation(originalDate, heuristicIds);

        using var image = new MagickImage(filePath);

        var profile = image.GetExifProfile();
        if (profile == null)
        {
            profile = new ExifProfile();
            image.SetProfile(profile);
        }

        profile.SetValue(ExifTag.DateTimeOriginal, dateStr);

        var commentBytes = Encoding.ASCII.GetBytes(annotation);
        var userCommentValue = new byte[AsciiPrefix.Length + commentBytes.Length];
        Buffer.BlockCopy(AsciiPrefix, 0, userCommentValue, 0, AsciiPrefix.Length);
        Buffer.BlockCopy(commentBytes, 0, userCommentValue, AsciiPrefix.Length, commentBytes.Length);
        profile.SetValue(ExifTag.UserComment, userCommentValue);

        image.SetProfile(profile);

        await image.WriteAsync(filePath, ct);

        return new ExifWriteResult(true, null);
    }

    private static string BuildAnnotation(DateTime? originalDate, IReadOnlyList<string> heuristicIds)
    {
        var origStr = originalDate.HasValue
            ? originalDate.Value.ToString("yyyy-MM-ddTHH:mm:ss")
            : "unknown";
        var heurStr = heuristicIds.Count > 0
            ? string.Join(",", heuristicIds)
            : "none";
        return $"SnapTime;original={origStr};heuristics={heurStr}";
    }
}
