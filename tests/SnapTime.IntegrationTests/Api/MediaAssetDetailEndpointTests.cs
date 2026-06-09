// [F6] Integration tests for GET /api/media-assets/{id} endpoint
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class MediaAssetDetailEndpointTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    public MediaAssetDetailEndpointTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetMediaAssetDetail_ExistingAsset_ReturnsDetailWithEvidence()
    {
        // [F6] GET /api/media-assets/{id} returns asset detail with evidence entries
        await using var db = _fixture.CreateContext();

        var scanJobId = Guid.NewGuid();
        db.ScanJobs.Add(new ScanJob
        {
            Id = scanJobId,
            Status = JobStatus.Completed,
            RootPath = "/test",
            CreatedAt = DateTime.UtcNow
        });

        var assetId = Guid.NewGuid();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = assetId,
            FileName = "vacation.jpg",
            FilePath = "/test/vacation.jpg",
            MediaType = MediaType.Image,
            Status = MediaStatus.Pending,
            FileSize = 1024,
            ConfidenceScore = 75,
            SuggestedDate = new DateTime(2024, 8, 15),
            SuggestedByHeuristic = "FilenameHeuristic",
            ScanJobId = scanJobId,
            FileCreatedAt = new DateTime(2024, 8, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 8, 16, 12, 0, 0)
        });

        db.EvidenceEntries.Add(new EvidenceEntry
        {
            Id = Guid.NewGuid(),
            HeuristicId = "H006",
            HeuristicName = "Filename heuristic",
            Weight = 0.8,
            Direction = EvidenceDirection.Positive,
            Description = "Nombre de archivo contiene fecha 20240815",
            MediaAssetId = assetId
        });

        db.EvidenceEntries.Add(new EvidenceEntry
        {
            Id = Guid.NewGuid(),
            HeuristicId = "H001",
            HeuristicName = "EXIF date",
            Weight = 0.5,
            Direction = EvidenceDirection.Positive,
            Description = "Fecha EXIF coincide",
            MediaAssetId = assetId
        });

        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/media-assets/{assetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        doc.Should().NotBeNull();

        var root = doc!.RootElement;
        root.GetProperty("id").GetGuid().Should().Be(assetId);
        root.GetProperty("filePath").GetString().Should().Be("/test/vacation.jpg");
        root.GetProperty("fileName").GetString().Should().Be("vacation.jpg");
        root.GetProperty("confidenceScore").GetInt32().Should().Be(75);

        var evidence = root.GetProperty("evidence").EnumerateArray().ToList();
        evidence.Should().HaveCount(2);
        evidence[0].GetProperty("heuristicId").GetString().Should().Be("H006");
        evidence[1].GetProperty("heuristicId").GetString().Should().Be("H001");
    }

    [Fact]
    public async Task GetMediaAssetDetail_NonExistentAsset_ReturnsNotFound()
    {
        // [F6] GET /api/media-assets/{id} with unknown id returns 404
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/media-assets/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
