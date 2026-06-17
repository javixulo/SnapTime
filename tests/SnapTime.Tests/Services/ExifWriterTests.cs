using System.Text;
using ImageMagick;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Services;

namespace SnapTime.Tests.Services;

public class ExifWriterTests
{
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "SnapTimeExifTests");

    public ExifWriterTests()
    {
        Directory.CreateDirectory(TempDir);
    }

    [Fact]
    public async Task WriteAsync_OnJpegFile_WritesDateTimeOriginalAndUserComment()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_photo.jpg");
        CreateMinimalJpeg(filePath);

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2024, 1, 15, 10, 30, 0),
            new[] { "H-005", "H-006" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        using var image = new MagickImage(filePath);
        var profile = image.GetExifProfile();
        Assert.NotNull(profile);

        var dt = profile.GetValue(ExifTag.DateTimeOriginal);
        Assert.NotNull(dt);
        Assert.Equal("2025:04:10 05:00:00", dt.Value);

        var comment = ReadUserComment(profile);
        Assert.NotNull(comment);
        Assert.Contains("SnapTime;", comment);
    }

    [Fact]
    public async Task WriteAsync_WithNullOriginalDate_WritesUserCommentWithUnknown()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_unknown_orig.jpg");
        CreateMinimalJpeg(filePath);

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            null,
            new[] { "H-005" },
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var comment = ReadUserComment(image.GetExifProfile());
        Assert.NotNull(comment);
        Assert.Contains("original=unknown", comment);
    }

    [Fact]
    public async Task WriteAsync_WithEmptyHeuristics_WritesUserCommentWithNone()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_no_heur.jpg");
        CreateMinimalJpeg(filePath);

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2024, 1, 15, 10, 30, 0),
            Array.Empty<string>(),
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var comment = ReadUserComment(image.GetExifProfile());
        Assert.NotNull(comment);
        Assert.Contains("heuristics=none", comment);
    }

    [Fact]
    public async Task WriteAsync_WithMultipleHeuristics_WritesCommaSeparated()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_multi_heur.jpg");
        CreateMinimalJpeg(filePath);

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2024, 1, 15, 10, 30, 0),
            new[] { "H-001", "H-003", "H-006" },
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var comment = ReadUserComment(image.GetExifProfile());
        Assert.NotNull(comment);
        Assert.Contains("H-001,H-003,H-006", comment);
    }

    [Fact]
    public async Task WriteAsync_WritesFullAnnotationFormat()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_annotation.jpg");
        CreateMinimalJpeg(filePath);

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2024, 1, 15, 10, 30, 0),
            new[] { "H-006" },
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var comment = ReadUserComment(image.GetExifProfile());
        Assert.NotNull(comment);
        Assert.StartsWith("SnapTime;", comment);
        Assert.Contains("original=2024-01-15T10:30:00", comment);
        Assert.Contains("heuristics=H-006", comment);
    }

    [Fact]
    public async Task WriteAsync_OnJpegWithExistingExif_WritesUserCommentToo()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_existing_exif.jpg");
        CreateJpegWithExif(filePath, "2023:07:11 18:51:44");

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2023, 7, 11, 18, 51, 44),
            new[] { "H-003" },
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var profile = image.GetExifProfile();
        Assert.NotNull(profile);

        var dt = profile.GetValue(ExifTag.DateTimeOriginal);
        Assert.NotNull(dt);
        Assert.Equal("2025:04:10 05:00:00", dt.Value);

        var comment = ReadUserComment(profile);
        Assert.NotNull(comment);
        Assert.Contains("SnapTime;original=2023-07-11T18:51:44", comment);
        Assert.Contains("heuristics=H-003", comment);
    }

    [Fact]
    public async Task WriteAsync_PreservesOtherExifTags()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test_preserve.jpg");
        CreateJpegWithExif(filePath, "2023:07:11 18:51:44", make: "Canon", model: "EOS R5");

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            new DateTime(2025, 4, 10, 5, 0, 0),
            new DateTime(2023, 7, 11, 18, 51, 44),
            new[] { "H-003" },
            CancellationToken.None);

        Assert.True(result.Success);

        using var image = new MagickImage(filePath);
        var profile = image.GetExifProfile();
        Assert.NotNull(profile);

        Assert.Equal("Canon", profile.GetValue(ExifTag.Make)?.Value);
        Assert.Equal("EOS R5", profile.GetValue(ExifTag.Model)?.Value);
    }

    [Fact]
    public async Task WriteAsync_OnNonExistentFile_ReturnsError()
    {
        var writer = new ExifWriter();
        var nonExistentPath = Path.Combine(TempDir, $"nonexistent_{Guid.NewGuid():N}.jpg");

        var result = await writer.WriteAsync(
            nonExistentPath, MediaType.Image,
            DateTime.Now, null, Array.Empty<string>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task WriteAsync_OnReadOnlyFile_ReturnsError()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("readonly.jpg");
        CreateMinimalJpeg(filePath);
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        try
        {
            var result = await writer.WriteAsync(
                filePath, MediaType.Image,
                DateTime.Now, null, Array.Empty<string>(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }
        finally
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task WriteAsync_OnInvalidJpeg_ReturnsError()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("invalid.jpg");
        File.WriteAllBytes(filePath, new byte[] { 0x00, 0x00, 0x00, 0x00 });

        var result = await writer.WriteAsync(
            filePath, MediaType.Image,
            DateTime.Now, null, Array.Empty<string>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task WriteAsync_OnVideoFile_ReturnsNotImplemented()
    {
        var writer = new ExifWriter();
        var filePath = GetTempPath("test.mp4");
        File.WriteAllBytes(filePath, new byte[] { 0x00 });

        var result = await writer.WriteAsync(
            filePath, MediaType.Video,
            DateTime.Now, null, Array.Empty<string>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not yet implemented", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTempPath(string fileName)
    {
        var path = Path.Combine(TempDir, fileName);
        if (File.Exists(path)) File.Delete(path);
        return path;
    }

    private static void CreateMinimalJpeg(string path)
    {
        using var image = new MagickImage(MagickColors.White, 1, 1);
        image.Format = MagickFormat.Jpeg;
        image.Write(path);
    }

    private static void CreateJpegWithExif(string path, string dateTimeOriginal, string? make = null, string? model = null)
    {
        using var image = new MagickImage(MagickColors.White, 1, 1);
        image.Format = MagickFormat.Jpeg;
        var profile = new ExifProfile();
        profile.SetValue(ExifTag.DateTimeOriginal, dateTimeOriginal);
        if (make != null) profile.SetValue(ExifTag.Make, make);
        if (model != null) profile.SetValue(ExifTag.Model, model);
        image.SetProfile(profile);
        image.Write(path);
    }

    private static string? ReadUserComment(IExifProfile? profile)
    {
        var raw = profile?.GetValue(ExifTag.UserComment)?.Value;
        if (raw == null || raw.Length <= 8) return null;
        return Encoding.ASCII.GetString(raw, 8, raw.Length - 8);
    }
}
