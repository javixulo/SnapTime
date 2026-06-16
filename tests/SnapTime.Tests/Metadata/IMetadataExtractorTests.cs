using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.Metadata;

public class IMetadataExtractorTests
{
    private static readonly DateTime SampleDate = new(2024, 8, 15, 14, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task ExtractAsync_ImageWithAllExifTags_ReturnsFourExifEntries()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/photos/photo.jpg";
        var expected = InMemoryMetadataExtractor.CreateExifEntries(
            dateTimeOriginal: SampleDate,
            subSecDateTimeOriginal: "10",
            createDate: SampleDate.AddHours(1),
            modifyDate: SampleDate.AddHours(2));
        extractor.AddResult(filePath, expected);

        var results = await extractor.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().HaveCount(4);
        results.Should().AllSatisfy(e => e.Source.Should().Be("exif"));
        results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Date/Time Original");
        results.Should().ContainSingle(e => e.Tag == "Exif SubIFD:Sub-Sec Time Original");
        results.Should().ContainSingle(e => e.Tag == "Exif IFD0:Date/Time Digitized");
        results.Should().ContainSingle(e => e.Tag == "Exif IFD0:Date/Time");
    }

    [Fact]
    public async Task ExtractAsync_ImageWithoutMetadata_ReturnsEmptyList()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/photos/no-metadata.jpg";

        var results = await extractor.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_CorruptImage_ReturnsEmptyList()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/photos/corrupt.jpg";
        extractor.AddResult(filePath, []);

        var results = await extractor.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_CorruptImage_ThrowsAndCatchesException_ReturnsEmptyList()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/photos/corrupt-exception.jpg";
        extractor.AddCorruptFile(filePath);

        var results = await extractor.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_VideoWithAllQuickTimeTags_ReturnsFiveQuickTimeEntries()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/videos/video.mov";
        var expected = InMemoryMetadataExtractor.CreateQuickTimeEntries(
            createDate: SampleDate,
            modifyDate: SampleDate.AddHours(1),
            creationDate: SampleDate.AddHours(2),
            mediaCreateDate: SampleDate.AddHours(3),
            trackCreateDate: SampleDate.AddHours(4));
        extractor.AddResult(filePath, expected);

        var results = await extractor.ExtractAsync(filePath, MediaType.Video, CancellationToken.None);

        results.Should().HaveCount(5);
        results.Should().AllSatisfy(e => e.Source.Should().Be("quicktime"));
        results.Should().ContainSingle(e => e.Tag == "QuickTime Movie Header:Create Date");
        results.Should().ContainSingle(e => e.Tag == "QuickTime Movie Header:Modify Date");
        results.Should().ContainSingle(e => e.Tag == "QuickTime Metadata:Creation Date");
        results.Should().ContainSingle(e => e.Tag == "QuickTime Media Header:Media Create Date");
        results.Should().ContainSingle(e => e.Tag == "QuickTime Track Header:Track Create Date");
    }

    [Fact]
    public async Task ExtractAsync_VideoWithoutMetadata_ReturnsEmptyList()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/videos/no-metadata.mov";

        var results = await extractor.ExtractAsync(filePath, MediaType.Video, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_NonExistentFile_ReturnsEmptyList()
    {
        var extractor = new InMemoryMetadataExtractor();
        var filePath = "/nonexistent/file.jpg";

        var results = await extractor.ExtractAsync(filePath, MediaType.Image, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var extractor = new InMemoryMetadataExtractor();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await extractor.ExtractAsync("/photos/photo.jpg", MediaType.Image, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExtractAsync_NullFilePath_ThrowsArgumentNullException()
    {
        var extractor = new InMemoryMetadataExtractor();

        var act = async () => await extractor.ExtractAsync(null!, MediaType.Image, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
