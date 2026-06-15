using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Domain.Services;

// [F3-US-008]
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
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Evaluate_FilenameWithDateDifferentFromMetadata_ReturnsCorrection()
    {
        var result = await Evaluate("20250315_123456.jpg", new DateTime(2024, 7, 10));

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.7);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Evaluate_VideoFileWithDateMatchingMetadata_ReturnsPositiveEvidence()
    {
        var result = await Evaluate("20250315_123456.mp4", new DateTime(2025, 3, 15));

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Positive);
        result.Weight.Should().Be(0.3);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Evaluate_VideoFileWithDateNoMetadata_ReturnsCorrection()
    {
        var result = await Evaluate("20250315_123456.mp4", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Evaluate_ImgWithPrefix_ExtractsDateFromAnywhere()
    {
        var result = await Evaluate("IMG_20250315.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
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
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_ChatGPTImage_SpanishMonth_ExtractsDate()
    {
        var result = await Evaluate("ChatGPT Image 10 abr 2025, 11_03_36.png", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 4, 10, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_ChatGPTImage_EnglishMonth_ExtractsDate()
    {
        var result = await Evaluate("ChatGPT Image 10 Apr 2025, 11_03_36.png", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 4, 10, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_ScreenshotWithHyphenDate_ExtractsDate()
    {
        var result = await Evaluate("Screenshot 2025-03-15 at 10.30.45.png", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_FilenameWithDotSeparatedDate_ExtractsDate()
    {
        var result = await Evaluate("vacaciones 2025.03.15.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_FilenameWithDDMMHyphen_ExtractsDate()
    {
        var result = await Evaluate("foto 15-03-2025.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_FilenameWithDDMMDots_ExtractsDate()
    {
        var result = await Evaluate("15.03.2025 cumpleaños.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public async Task Parse_FilenameWithMonthFirst_ExtractsDate()
    {
        var result = await Evaluate("abr 10 2025 selfie.jpg", null);

        result.Should().NotBeNull();
        result!.Direction.Should().Be(EvidenceDirection.Correction);
        result.SuggestedDate.Should().Be(new DateTime(2025, 4, 10, 5, 0, 0));
        result.Weight.Should().Be(0.8);
        result.HeuristicId.Should().Be("H-006");
    }

    [Fact]
    public void Parse_UnderscoreSeparatedDate_ExtractsDate()
    {
        // P4: yyyy_MM_dd pattern anywhere in filename
        var asset = new MediaAsset { Id = Guid.NewGuid(), FileName = "IMG_2025_03_15.jpg" };
        var heuristic = new H006FilenameHeuristic();

        var result = heuristic.EvaluateAsync(asset, [], CancellationToken.None).Result;

        Assert.NotNull(result);
        Assert.Equal(EvidenceDirection.Correction, result.Direction);
        Assert.Equal(new DateTime(2025, 3, 15, 5, 0, 0), result.SuggestedDate);
        Assert.Equal("H-006", result.HeuristicId);
    }

    [Fact]
    public void Parse_SlashSeparatedDate_ExtractsDate()
    {
        // P9: DD/MM/YYYY pattern anywhere in filename
        var asset = new MediaAsset { Id = Guid.NewGuid(), FileName = "foto 15/03/2025.jpg" };
        var heuristic = new H006FilenameHeuristic();

        var result = heuristic.EvaluateAsync(asset, [], CancellationToken.None).Result;

        Assert.NotNull(result);
        Assert.Equal(EvidenceDirection.Correction, result.Direction);
        Assert.Equal(new DateTime(2025, 3, 15, 5, 0, 0), result.SuggestedDate);
        Assert.Equal("H-006", result.HeuristicId);
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
