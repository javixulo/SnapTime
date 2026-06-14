// [F8-US-002] ExifWriter - writes EXIF DateTimeOriginal + UserComment for JPEG
using System.Text;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class ExifWriter : IExifWriter
{
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

            // Check if file is read-only
            if (new FileInfo(filePath).IsReadOnly)
                return new ExifWriteResult(false, "File is read-only");

            switch (mediaType)
            {
                case MediaType.Image:
                    return await WriteJpegAsync(filePath, newDate, originalDate, heuristicIds, ct);
                case MediaType.Video:
                    // TODO: Implement QuickTime metadata writing (F8-US-002 future)
                    return new ExifWriteResult(false, "Video metadata writing not yet implemented");
                default:
                    return new ExifWriteResult(false, $"Unsupported media type: {mediaType}");
            }
        }
        catch (Exception ex)
        {
            return new ExifWriteResult(false, ex.Message);
        }
    }

    private static async Task<ExifWriteResult> WriteJpegAsync(
        string filePath,
        DateTime newDate,
        DateTime? originalDate,
        IReadOnlyList<string> heuristicIds,
        CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, ct);

        if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return new ExifWriteResult(false, "Not a valid JPEG file (SOI marker not found)");

        // Build the EXIF annotation string
        var annotation = BuildAnnotation(originalDate, heuristicIds);

        // Build the new APP1 EXIF segment with DateTimeOriginal and UserComment
        var app1Segment = BuildApp1Segment(newDate, annotation);

        // Find existing APP1 marker (0xFFE1)
        int app1Start = FindMarker(bytes, 0xFFE1, 2);

        byte[] result;
        if (app1Start > 0)
        {
            // Replace existing APP1 segment
            int app1Len = 2 + ((bytes[app1Start + 2] << 8) | bytes[app1Start + 3]) + 2;
            result = ReplaceSegment(bytes, app1Start, app1Len, app1Segment);
        }
        else
        {
            // Insert new APP1 segment after SOI
            result = InsertAfterMarker(bytes, 0xFFD8, app1Segment);
        }

        await File.WriteAllBytesAsync(filePath, result, ct);
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

    private static byte[] BuildApp1Segment(DateTime newDate, string annotation)
    {
        var dateStr = newDate.ToString("yyyy:MM:dd HH:mm:ss");
        var dateBytes = Encoding.ASCII.GetBytes(dateStr.PadRight(20, ' '));
        var annotationBytes = Encoding.ASCII.GetBytes(annotation);
        var annotationPrefix = new byte[] { 0x41, 0x53, 0x43, 0x49, 0x49, 0x00, 0x00, 0x00 };

        int tiffHeaderLen = 8;
        int ifdEntries = 2;
        int ifdHeaderLen = 2 + ifdEntries * 12 + 4;
        int dateOffset = tiffHeaderLen + ifdHeaderLen;
        int annotationOffset = dateOffset + dateBytes.Length;

        int totalExifLen = tiffHeaderLen + ifdHeaderLen + dateBytes.Length + annotationBytes.Length + annotationPrefix.Length;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((byte)0x49);
        writer.Write((byte)0x49);
        writer.Write((short)0x002A);
        writer.Write((int)8);

        writer.Write((ushort)ifdEntries);

        writer.Write((ushort)0x9003);
        writer.Write((ushort)2);
        writer.Write((int)20);
        writer.Write((int)dateOffset);

        writer.Write((ushort)0x9286);
        writer.Write((ushort)7);
        writer.Write((int)(annotationBytes.Length + annotationPrefix.Length));
        writer.Write((int)annotationOffset);

        writer.Write((int)0);

        writer.Write(dateBytes);

        writer.Write(annotationPrefix);
        writer.Write(annotationBytes);

        var exifData = ms.ToArray();

        using var app1Ms = new MemoryStream();
        using var app1Writer = new BinaryWriter(app1Ms);
        app1Writer.Write((byte)0xFF);
        app1Writer.Write((byte)0xE1);
        app1Writer.Write((short)(exifData.Length + 8));
        app1Writer.Write(Encoding.ASCII.GetBytes("Exif\0\0"));
        app1Writer.Write(exifData);

        return app1Ms.ToArray();
    }

    private static int FindMarker(byte[] data, byte markerHigh, byte markerLow, int startIndex)
    {
        for (int i = startIndex; i < data.Length - 1; i++)
        {
            if (data[i] == markerHigh && data[i + 1] == markerLow)
                return i;
        }
        return -1;
    }

    private static int FindMarker(byte[] data, ushort marker, int startIndex)
    {
        return FindMarker(data, (byte)(marker >> 8), (byte)(marker & 0xFF), startIndex);
    }

    private static byte[] ReplaceSegment(byte[] data, int start, int oldLen, byte[] newSegment)
    {
        var result = new byte[data.Length - oldLen + newSegment.Length];
        int pos = 0;
        Array.Copy(data, 0, result, pos, start);
        pos += start;
        Array.Copy(newSegment, 0, result, pos, newSegment.Length);
        pos += newSegment.Length;
        Array.Copy(data, start + oldLen, result, pos, data.Length - (start + oldLen));
        return result;
    }

    private static byte[] InsertAfterMarker(byte[] data, ushort marker, byte[] segment)
    {
        int markerPos = FindMarker(data, marker, 0);
        if (markerPos < 0) return data;

        int insertPos = markerPos + 2;
        var result = new byte[data.Length + segment.Length];
        int pos = 0;
        Array.Copy(data, 0, result, pos, insertPos);
        pos += insertPos;
        Array.Copy(segment, 0, result, pos, segment.Length);
        pos += segment.Length;
        Array.Copy(data, insertPos, result, pos, data.Length - insertPos);
        return result;
    }
}
