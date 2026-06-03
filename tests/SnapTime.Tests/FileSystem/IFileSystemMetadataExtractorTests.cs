using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.FileSystem;

public class IFileSystemMetadataExtractorTests
{
    private static readonly DateTime SampleCreationTime = new(2024, 3, 10, 9, 15, 0, DateTimeKind.Unspecified);
    private static readonly DateTime SampleLastWriteTime = new(2024, 8, 15, 14, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public void ExtractFileSystemDates_ExistingFile_ReturnsCtimeAndMtimeEntries()
    {
        var extractor = new InMemoryFileSystemMetadataExtractor();
        var filePath = "/photos/photo.jpg";
        extractor.AddResult(filePath, SampleCreationTime, SampleLastWriteTime);

        var results = extractor.ExtractFileSystemDates(filePath);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(e => e.Source.Should().Be("filesystem"));
        results.Should().ContainSingle(e =>
            e.Tag == "Filesystem:ctime" &&
            e.Value == SampleCreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
        results.Should().ContainSingle(e =>
            e.Tag == "Filesystem:mtime" &&
            e.Value == SampleLastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    [Fact]
    public void ExtractFileSystemDates_NonExistentFile_ReturnsEmptyList()
    {
        var extractor = new InMemoryFileSystemMetadataExtractor();
        var filePath = "/nonexistent/file.jpg";

        var results = extractor.ExtractFileSystemDates(filePath);

        results.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFileSystemDates_EmptyFilePath_ThrowsArgumentException()
    {
        var extractor = new InMemoryFileSystemMetadataExtractor();

        var act = () => extractor.ExtractFileSystemDates("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExtractFileSystemDates_NullFilePath_ThrowsArgumentNullException()
    {
        var extractor = new InMemoryFileSystemMetadataExtractor();

        var act = () => extractor.ExtractFileSystemDates(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
