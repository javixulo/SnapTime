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

            if (new FileInfo(filePath).IsReadOnly)
                return new ExifWriteResult(false, "File is read-only");

            switch (mediaType)
            {
                case MediaType.Image:
                    return await WriteJpegAsync(filePath, newDate, originalDate, heuristicIds, ct);
                case MediaType.Video:
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

        var annotation = BuildAnnotation(originalDate, heuristicIds);
        var dateStr = newDate.ToString("yyyy:MM:dd HH:mm:ss").PadRight(20, ' ');

        int app1Start = FindMarker(bytes, 0xFFE1, 2);

        byte[] result;
        if (app1Start > 0 && TryUpdateDateTimeOriginal(bytes, app1Start, dateStr, out var patched))
        {
            result = patched;
        }
        else if (app1Start > 0)
        {
            var app1Segment = BuildApp1Segment(dateStr, annotation);
            int oldLen = 2 + ((bytes[app1Start + 2] << 8) | bytes[app1Start + 3]) + 2;
            result = ReplaceSegment(bytes, app1Start, oldLen, app1Segment);
        }
        else
        {
            var app1Segment = BuildApp1Segment(dateStr, annotation);
            result = InsertAfterMarker(bytes, 0xFFD8, app1Segment);
        }

        await File.WriteAllBytesAsync(filePath, result, ct);
        return new ExifWriteResult(true, null);
    }

    private static bool TryUpdateDateTimeOriginal(byte[] bytes, int app1Start, string dateStr, out byte[] result)
    {
        result = null;

        int exifOffset = app1Start + 4;
        if (exifOffset + 6 > bytes.Length)
            return false;

        string exifId = Encoding.ASCII.GetString(bytes, exifOffset, 6);
        if (exifId != "Exif\0\0")
            return false;

        int tiffStart = exifOffset + 6;
        if (tiffStart + 8 > bytes.Length)
            return false;

        bool isLittleEndian = (bytes[tiffStart] == 0x49 && bytes[tiffStart + 1] == 0x49);
        if (!isLittleEndian && !(bytes[tiffStart] == 0x4D && bytes[tiffStart + 1] == 0x4D))
            return false;

        ushort magic = ReadU16(bytes, tiffStart + 2, isLittleEndian);
        if (magic != 0x002A)
            return false;

        uint ifd0Off = ReadU32(bytes, tiffStart + 4, isLittleEndian);

        uint exifIfdOff = FindTagValueOffset(bytes, tiffStart, ifd0Off, 0x8769, isLittleEndian);
        if (exifIfdOff == 0)
            return false;

        uint dateValueOff = FindTagDataOffset(bytes, tiffStart, exifIfdOff, 0x9003, isLittleEndian);
        if (dateValueOff == 0)
            return false;

        int fileOff = tiffStart + (int)dateValueOff;
        if (fileOff + 20 > bytes.Length)
            return false;

        var dateBytes = Encoding.ASCII.GetBytes(dateStr);
        Array.Copy(dateBytes, 0, bytes, fileOff, 20);

        result = bytes;
        return true;
    }

    private static uint FindTagValueOffset(byte[] data, int tiffBase, uint ifdOffset, ushort targetTag, bool le)
    {
        int pos = tiffBase + (int)ifdOffset;
        if (pos + 2 > data.Length) return 0;

        ushort count = ReadU16(data, pos, le);
        pos += 2;

        for (int i = 0; i < count; i++)
        {
            if (pos + 12 > data.Length) return 0;
            ushort tag = ReadU16(data, pos, le);
            if (tag == targetTag)
                return ReadU32(data, pos + 8, le);
            pos += 12;
        }

        return 0;
    }

    private static uint FindTagDataOffset(byte[] data, int tiffBase, uint ifdOffset, ushort targetTag, bool le)
    {
        int pos = tiffBase + (int)ifdOffset;
        if (pos + 2 > data.Length) return 0;

        ushort count = ReadU16(data, pos, le);
        pos += 2;

        for (int i = 0; i < count; i++)
        {
            if (pos + 12 > data.Length) return 0;
            ushort tag = ReadU16(data, pos, le);
            ushort type = ReadU16(data, pos + 2, le);
            uint typeCount = ReadU32(data, pos + 4, le);
            uint valueOrOffset = ReadU32(data, pos + 8, le);

            if (tag == targetTag)
            {
                int typeSize = GetTypeSize(type);
                if (typeSize == 0) return 0;
                long totalBytes = typeSize * typeCount;
                if (totalBytes <= 4)
                    return (uint)(pos - tiffBase + 8);
                return valueOrOffset;
            }
            pos += 12;
        }

        return 0;
    }

    private static int GetTypeSize(ushort type)
    {
        return type switch
        {
            1 => 1, 2 => 1, 6 => 1, 7 => 1,
            3 => 2, 8 => 2,
            4 => 4, 9 => 4, 11 => 4,
            5 => 8, 10 => 8, 12 => 8,
            _ => 0
        };
    }

    private static ushort ReadU16(byte[] data, int offset, bool le)
    {
        return le
            ? (ushort)(data[offset] | (data[offset + 1] << 8))
            : (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadU32(byte[] data, int offset, bool le)
    {
        return le
            ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
            : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
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

    private static byte[] BuildApp1Segment(string dateStr, string annotation)
    {
        var dateBytes = Encoding.ASCII.GetBytes(dateStr);
        var annotationBytes = Encoding.ASCII.GetBytes(annotation);
        var annotationPrefix = new byte[] { 0x41, 0x53, 0x43, 0x49, 0x49, 0x00, 0x00, 0x00 };

        int tiffHeaderLen = 8;
        int ifdEntries = 2;
        int ifdHeaderLen = 2 + ifdEntries * 12 + 4;
        int dateOffset = tiffHeaderLen + ifdHeaderLen;
        int annotationOffset = dateOffset + dateBytes.Length;

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
        app1Writer.Write((short)(exifData.Length + 6));
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
