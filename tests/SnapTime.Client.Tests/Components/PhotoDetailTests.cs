// [F6] bUnit tests for PhotoDetail.razor
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class PhotoDetailTests : TestContext
{
    private readonly IPhotoClient _photoClient = Substitute.For<IPhotoClient>();
    private readonly MediaAssetDetailDto _sampleDetail;
    private readonly FileMetadataDto _sampleFileMetadata;

    public PhotoDetailTests()
    {
        Services.AddSingleton(_photoClient);
        Services.AddSingleton(new ApiConfig { BaseUrl = "http://localhost:5000" });

        _sampleDetail = new MediaAssetDetailDto
        {
            Id = Guid.NewGuid(),
            FilePath = "/test/vacation.jpg",
            FileName = "vacation.jpg",
            MediaType = "Image",
            FileSize = 1024,
            DateTimeOriginal = new DateTime(2024, 8, 15, 10, 0, 0),
            SubSecDateTimeOriginal = null,
            CreateDate = new DateTime(2024, 8, 15, 10, 0, 0),
            ModifyDate = new DateTime(2024, 8, 16, 12, 0, 0),
            FileCreatedAt = new DateTime(2024, 8, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 8, 16, 12, 0, 0),
            ConfidenceScore = 85,
            SuggestedDate = new DateTime(2024, 8, 15),
            SuggestedByHeuristic = "FilenameHeuristic",
            Evidence =
            [
                new EvidenceDto
                {
                    HeuristicId = "H006",
                    HeuristicName = "Filename heuristic",
                    Weight = 0.8,
                    Direction = "positive",
                    Description = "Nombre contiene fecha 20240815"
                },
                new EvidenceDto
                {
                    HeuristicId = "H001",
                    HeuristicName = "EXIF date",
                    Weight = 0.5,
                    Direction = "positive",
                    Description = "Fecha EXIF coincide"
                }
            ]
        };

        _sampleFileMetadata = new FileMetadataDto
        {
            FilePath = "/test/unscanned_sunset.jpg",
            FileName = "unscanned_sunset.jpg",
            FileSize = 2048,
            DateTimeOriginal = new DateTime(2024, 9, 10, 18, 30, 0),
            SubSecDateTimeOriginal = "100",
            CreateDate = new DateTime(2024, 9, 10, 18, 30, 0),
            ModifyDate = new DateTime(2024, 9, 11, 10, 0, 0),
            FileCreatedAt = new DateTime(2024, 9, 10, 18, 30, 0),
            FileModifiedAt = new DateTime(2024, 9, 11, 10, 0, 0)
        };
    }

    // ──────────────────────────────────────────────
    // Estados
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsEmpty_whenNoPhotoSelected()
    {
        // [F6] Without a selected photo or path, show placeholder text
        var cut = RenderComponent<PhotoDetail>();

        cut.Markup.Should().Contain("Selecciona una foto");
    }

    // ──────────────────────────────────────────────
    // Metadatos — scanned asset (by id)
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsMetadata_whenPhotoSelected()
    {
        // [F6] When a photo id is selected, show file name, path, and dates
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_sampleDetail);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        cut.Markup.Should().Contain("vacation.jpg");
        cut.Markup.Should().Contain("/test/vacation.jpg");
        cut.Markup.Should().Contain("15/08/2024");
    }

    // ──────────────────────────────────────────────
    // Metadatos — unscanned file (by path, F6 fallback)
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsMetadataFromFile_whenNotScanned()
    {
        // [F6] When only SelectedAssetPath is set (no id), load metadata from file
        _photoClient.GetFileMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_sampleFileMetadata);

        var cut = RenderComponent<PhotoDetail>(p =>
            p.Add(c => c.SelectedAssetPath, _sampleFileMetadata.FilePath));

        cut.Markup.Should().Contain("unscanned_sunset.jpg");
        cut.Markup.Should().Contain("/test/unscanned_sunset.jpg");
        cut.Markup.Should().Contain("10/09/2024");
        cut.Markup.Should().NotContain("photo-detail-evidence-section");
    }

    // ──────────────────────────────────────────────
    // Evidence — full detail when both id and path provided
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsEvidenceOnly_whenScanned()
    {
        // [F6] When SelectedAssetId is set, show full detail with evidence
        // (SelectedAssetPath is ignored when id is present)
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_sampleDetail);
        _photoClient.GetFileMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_sampleFileMetadata);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, "/test/unscanned_sunset.jpg");
        });

        // Should show the scanned asset detail (with evidence), not the file metadata
        cut.Markup.Should().Contain("vacation.jpg");
        cut.Markup.Should().Contain("/test/vacation.jpg");
        cut.Markup.Should().Contain("Filename heuristic");
        cut.Markup.Should().Contain("EXIF date");
        cut.Markup.Should().NotContain("unscanned_sunset.jpg");
    }

    // ──────────────────────────────────────────────
    // Evidence
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsEvidenceList()
    {
        // [F6] Evidence entries are rendered as a list
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_sampleDetail);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        var evidenceItems = cut.FindAll(".photo-detail-evidence-item");
        evidenceItems.Should().HaveCount(2);
        evidenceItems[0].TextContent.Should().Contain("Filename heuristic");
        evidenceItems[1].TextContent.Should().Contain("EXIF date");
    }

    // ──────────────────────────────────────────────
    // Confidence bar
    // ──────────────────────────────────────────────

    [Fact]
    public void photoDetail_showsConfidenceBar()
    {
        // [F6] A confidence bar is rendered with the correct score
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_sampleDetail);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        var bar = cut.Find(".photo-detail-confidence-bar");
        bar.Should().NotBeNull();
        bar.TextContent.Should().Contain("85");
    }

    [Fact]
    public void photoDetail_showsConfidenceBar_green_whenHigh()
    {
        // [F6] Confidence >= 80 shows green class
        var highConfidence = new MediaAssetDetailDto
        {
            Id = _sampleDetail.Id,
            FilePath = _sampleDetail.FilePath,
            FileName = _sampleDetail.FileName,
            MediaType = _sampleDetail.MediaType,
            FileSize = _sampleDetail.FileSize,
            ConfidenceScore = 95,
            Evidence = _sampleDetail.Evidence
        };
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(highConfidence);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        var bar = cut.Find(".photo-detail-confidence-bar");
        bar.ClassName.Should().Contain("confidence-high");
    }

    [Fact]
    public void photoDetail_showsConfidenceBar_yellow_whenMedium()
    {
        // [F6] Confidence 50-79 shows yellow class
        var mediumConfidence = new MediaAssetDetailDto
        {
            Id = _sampleDetail.Id,
            FilePath = _sampleDetail.FilePath,
            FileName = _sampleDetail.FileName,
            MediaType = _sampleDetail.MediaType,
            FileSize = _sampleDetail.FileSize,
            ConfidenceScore = 65,
            Evidence = _sampleDetail.Evidence
        };
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(mediumConfidence);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        var bar = cut.Find(".photo-detail-confidence-bar");
        bar.ClassName.Should().Contain("confidence-medium");
    }

    [Fact]
    public void photoDetail_showsConfidenceBar_red_whenLow()
    {
        // [F6] Confidence < 50 shows red class
        var lowConfidence = new MediaAssetDetailDto
        {
            Id = _sampleDetail.Id,
            FilePath = _sampleDetail.FilePath,
            FileName = _sampleDetail.FileName,
            MediaType = _sampleDetail.MediaType,
            FileSize = _sampleDetail.FileSize,
            ConfidenceScore = 30,
            Evidence = _sampleDetail.Evidence
        };
        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(lowConfidence);

        var cut = RenderComponent<PhotoDetail>(p =>
        {
            p.Add(c => c.SelectedAssetId, _sampleDetail.Id);
            p.Add(c => c.SelectedAssetPath, _sampleDetail.FilePath);
        });

        var bar = cut.Find(".photo-detail-confidence-bar");
        bar.ClassName.Should().Contain("confidence-low");
    }
}
