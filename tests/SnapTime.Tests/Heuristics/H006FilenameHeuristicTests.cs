using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Domain.Services;

// [F3-US-001]
namespace SnapTime.Tests.Heuristics;

public class H006FilenameHeuristicTests
{
    [Fact]
    public async Task Evaluate_FilenameWithDateMatchingMetadata_ReturnsPositiveEvidence()
    {
        var result = await Evaluate("20250315_123456.jpg", new DateTime(2025, 3, 15));

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Positive);
        result.Weight.Should().Be(0.3);
    }

    [Fact]
    public async Task Evaluate_FilenameWithDateDifferentFromMetadata_ReturnsCorrection()
    {
        var result = await Evaluate("20250315_123456.jpg", new DateTime(2024, 7, 10));

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.7);
    }

    [Fact]
    public async Task Evaluate_VideoFileWithDateMatchingMetadata_ReturnsPositiveEvidence()
    {
        var result = await Evaluate("20250315_123456.mp4", new DateTime(2025, 3, 15));

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Positive);
        result.Weight.Should().Be(0.3);
    }

    [Fact]
    public async Task Evaluate_VideoFileWithDateNoMetadata_ReturnsCorrection()
    {
        var result = await Evaluate("20250315_123456.mp4", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.5);
    }

    [Fact]
    public async Task Evaluate_FilenameWithPrefixNoDate_ReturnsNull()
    {
        var result = await Evaluate("IMG_20250315.jpg", new DateTime(2024, 7, 10));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_FilenameWithoutDate_ReturnsNull()
    {
        var result = await Evaluate("vacaciones.jpg", new DateTime(2024, 7, 10));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_FilenameWithDateNoMetadata_ReturnsCorrection()
    {
        var result = await Evaluate("20250315_123456.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.5);
    }

    private static async Task<EvidenceEntry?> Evaluate(string fileName, DateTime? canonicalDate)
    {
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            FilePath = $"/photos/{fileName}",
            FileName = fileName,
            MediaType = fileName.EndsWith(".mp4") ? MediaType.Video : MediaType.Image,
            FileSize = 100,
            ScanJobId = Guid.NewGuid(),
            Status = MediaStatus.Pending
        };

        var metadata = canonicalDate.HasValue
            ? new List<MetadataEntry>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Tag = "EXIF:DateTimeOriginal",
                    Value = canonicalDate.Value.ToString("yyyy:MM:dd HH:mm:ss"),
                    Source = "exif",
                    MediaAssetId = asset.Id
                }
            }
            : [];

        var heuristic = new H006FilenameHeuristic();
        return await heuristic.EvaluateAsync(asset, metadata, CancellationToken.None);
    }
}
