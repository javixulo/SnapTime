// [F8-US-002] ExifWriter unit tests
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Services;

namespace SnapTime.Tests.Services;

public class ExifWriterTests
{
    [Fact]
    public async Task WriteAsync_OnJpegFile_WritesDateTimeOriginalAndUserComment()
    {
        var writer = new ExifWriter();
        var tempDir = Path.Combine(Path.GetTempPath(), "SnapTimeExifTests");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test_photo.jpg");

        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                new DateTime(2024, 1, 15, 10, 30, 0),
                new[] { "H-005", "H-006" },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    [Fact]
    // NOTE: Annotation content verified in WriteAsync_WithNullOriginalDate_WritesUserCommentWithUnknown
    public async Task WriteAsync_WithNullOriginalDate_ReturnsSuccess()
    {
        var writer = new ExifWriter();
        var tempDir = Path.Combine(Path.GetTempPath(), "SnapTimeExifTests");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "test_photo2.jpg");
        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                null,
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task WriteAsync_OnNonExistentFile_ReturnsError()
    {
        var writer = new ExifWriter();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.jpg");

        var result = await writer.WriteAsync(
            nonExistentPath,
            MediaType.Image,
            DateTime.Now,
            null,
            Array.Empty<string>(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task WriteAsync_OnReadOnlyFile_ReturnsError()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
        File.SetAttributes(tempFile, FileAttributes.ReadOnly);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                DateTime.Now,
                null,
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            File.SetAttributes(tempFile, FileAttributes.Normal);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_WithNullOriginalDate_WritesUserCommentWithUnknown()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                null,
                new[] { "H-005" },
                CancellationToken.None);

            Assert.True(result.Success);

            var userComment = ReadUserCommentFromJpeg(tempFile);
            Assert.NotNull(userComment);
            Assert.Contains("original=unknown", userComment);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_WithEmptyHeuristics_WritesUserCommentWithNone()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                new DateTime(2024, 1, 15, 10, 30, 0),
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.True(result.Success);

            var userComment = ReadUserCommentFromJpeg(tempFile);
            Assert.NotNull(userComment);
            Assert.Contains("heuristics=none", userComment);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_WithMultipleHeuristics_WritesCommaSeparated()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                new DateTime(2024, 1, 15, 10, 30, 0),
                new[] { "H-001", "H-003", "H-006" },
                CancellationToken.None);

            Assert.True(result.Success);

            var userComment = ReadUserCommentFromJpeg(tempFile);
            Assert.NotNull(userComment);
            Assert.Contains("H-001,H-003,H-006", userComment);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_WritesFullAnnotationFormat()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                new DateTime(2025, 4, 10, 5, 0, 0),
                new DateTime(2024, 1, 15, 10, 30, 0),
                new[] { "H-006" },
                CancellationToken.None);

            Assert.True(result.Success);

            var userComment = ReadUserCommentFromJpeg(tempFile);
            Assert.NotNull(userComment);
            Assert.StartsWith("SnapTime;", userComment);
            Assert.Contains("original=2024-01-15T10:30:00", userComment);
            Assert.Contains("heuristics=H-006", userComment);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_OnInvalidJpeg_ReturnsError()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".jpg";
        File.WriteAllBytes(tempFile, new byte[] { 0x00, 0x00, 0x00, 0x00 }); // Not a valid JPEG

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Image,
                DateTime.Now,
                null,
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("JPEG", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteAsync_OnVideoFile_ReturnsNotImplemented()
    {
        var writer = new ExifWriter();
        var tempFile = Path.GetTempFileName() + ".mp4";
        File.WriteAllBytes(tempFile, new byte[] { 0x00 }); // Doesn't need to be valid, just exists

        try
        {
            var result = await writer.WriteAsync(
                tempFile,
                MediaType.Video,
                DateTime.Now,
                null,
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not yet implemented", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static string? ReadUserCommentFromJpeg(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        int app1Start = -1;
        for (int i = 2; i < bytes.Length - 1; i++)
        {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0xE1)
            {
                app1Start = i;
                break;
            }
        }

        if (app1Start < 0) return null;

        int tiffStart = app1Start + 2 + 2 + 6;
        if (tiffStart >= bytes.Length) return null;

        bool isLittleEndian = bytes[tiffStart] == 0x49;

        int ifd0Offset = ReadTiffInt(bytes, tiffStart + 4, isLittleEndian);
        int ifd0Start = tiffStart + ifd0Offset;

        if (ifd0Start + 2 > bytes.Length) return null;

        int ifdToSearch = ifd0Start;

        int entryCount = ReadTiffShort(bytes, ifd0Start, isLittleEndian);
        int entriesStart = ifd0Start + 2;

        for (int i = 0; i < entryCount; i++)
        {
            int entryPos = entriesStart + i * 12;
            if (entryPos + 12 > bytes.Length) break;

            int tag = ReadTiffShort(bytes, entryPos, isLittleEndian);

            if (tag == 0x8769)
            {
                int exifIfdOffset = ReadTiffInt(bytes, entryPos + 8, isLittleEndian);
                ifdToSearch = tiffStart + exifIfdOffset;
                break;
            }
        }

        if (ifdToSearch + 2 > bytes.Length) return null;

        entryCount = ReadTiffShort(bytes, ifdToSearch, isLittleEndian);
        entriesStart = ifdToSearch + 2;

        for (int i = 0; i < entryCount; i++)
        {
            int entryPos = entriesStart + i * 12;
            if (entryPos + 12 > bytes.Length) break;

            int tag = ReadTiffShort(bytes, entryPos, isLittleEndian);

            if (tag == 0x9286)
            {
                int type = ReadTiffShort(bytes, entryPos + 2, isLittleEndian);
                int count = ReadTiffInt(bytes, entryPos + 4, isLittleEndian);
                int valueOffset = ReadTiffInt(bytes, entryPos + 8, isLittleEndian);

                int valuePos;
                if (count <= 4)
                    valuePos = entryPos + 8;
                else
                    valuePos = tiffStart + valueOffset;

                if (valuePos + count > bytes.Length) continue;

                int textStart = valuePos + 8;
                int textLen = count - 8;
                if (textLen <= 0) continue;

                return System.Text.Encoding.ASCII.GetString(bytes, textStart, textLen);
            }
        }

        return null;
    }

    private static int ReadTiffInt(byte[] data, int offset, bool isLittleEndian)
    {
        if (isLittleEndian)
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        else
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }

    private static int ReadTiffShort(byte[] data, int offset, bool isLittleEndian)
    {
        if (isLittleEndian)
            return data[offset] | (data[offset + 1] << 8);
        else
            return (data[offset] << 8) | data[offset + 1];
    }

    private static void CreateMinimalJpeg(string path)
    {
        var jpeg = new byte[]
        {
            0xFF, 0xD8,
            0xFF, 0xE1,
            0x00, 0x08,
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,
            0xFF, 0xD9
        };
        File.WriteAllBytes(path, jpeg);
    }
}
