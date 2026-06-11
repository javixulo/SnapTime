// [F7-US-004] Integration tests for POST /api/reviews/single and POST /api/reviews/batch
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Server.Models;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class ReviewEndpointsTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ReviewEndpointsTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task SingleReview_ApproveExistingAsset_ReturnsApproved()
    {
        var assetId = await SeedAssetWithSuggestionAsync();
        var response = await _client.PostAsJsonAsync("/api/reviews/single",
            new { assetId, status = "approved" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verify = _fixture.CreateContext();
        var saved = await verify.MediaAssets.FindAsync(assetId);
        saved.Should().NotBeNull();
        saved!.SuggestionStatus.Should().Be(SuggestionReviewStatus.Approved);
    }

    [Fact]
    public async Task SingleReview_RejectExistingAsset_ReturnsRejected()
    {
        var assetId = await SeedAssetWithSuggestionAsync();
        var response = await _client.PostAsJsonAsync("/api/reviews/single",
            new { assetId, status = "rejected" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verify = _fixture.CreateContext();
        var saved = await verify.MediaAssets.FindAsync(assetId);
        saved.Should().NotBeNull();
        saved!.SuggestionStatus.Should().Be(SuggestionReviewStatus.Rejected);
    }

    [Fact]
    public async Task SingleReview_NonExistentAsset_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews/single",
            new { assetId = Guid.NewGuid(), status = "approved" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SingleReview_InvalidStatus_ReturnsBadRequest()
    {
        var assetId = await SeedAssetWithSuggestionAsync();
        var response = await _client.PostAsJsonAsync("/api/reviews/single",
            new { assetId, status = "invalid" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SingleReview_EmptyAssetId_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews/single",
            new { assetId = Guid.Empty, status = "approved" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BatchReview_ApproveFolder_UpdatesAllFolderAssets()
    {
        var id1 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder", fileName: "photo1.jpg");
        var id2 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder", fileName: "photo2.jpg");
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "folder", status = "approved", rootPath = "/test/folder" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>();
        ids.Should().BeEquivalentTo(new[] { id1, id2 });
    }

    [Fact]
    public async Task BatchReview_RejectFolder_UpdatesAllFolderAssets()
    {
        var id1 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder", fileName: "photo1.jpg");
        var id2 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder", fileName: "photo2.jpg");
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "folder", status = "rejected", rootPath = "/test/folder" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>();
        ids.Should().BeEquivalentTo(new[] { id1, id2 });
    }

    [Fact]
    public async Task BatchReview_ApproveTotal_UpdatesAllScannedAssets()
    {
        var id1 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder1", fileName: "photo1.jpg");
        var id2 = await SeedAssetWithSuggestionAsync(rootPath: "/test/folder2", fileName: "photo2.jpg");
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "total", status = "approved" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await response.Content.ReadFromJsonAsync<List<Guid>>();
        ids.Should().BeEquivalentTo(new[] { id1, id2 });
    }

    [Fact]
    public async Task BatchReview_InvalidStatus_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "folder", status = "invalid", rootPath = "/test" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BatchReview_EmptyScope_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "", status = "approved" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BatchReview_FolderScopeWithoutRootPath_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/reviews/batch",
            new { scope = "folder", status = "approved", rootPath = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> SeedAssetWithSuggestionAsync(string rootPath = "/test", string fileName = "vacation.jpg")
    {
        await using var db = _fixture.CreateContext();
        var scanJobId = Guid.NewGuid();
        db.ScanJobs.Add(new ScanJob
        {
            Id = scanJobId,
            Status = JobStatus.Completed,
            RootPath = rootPath,
            CreatedAt = DateTime.UtcNow
        });
        var assetId = Guid.NewGuid();
        db.MediaAssets.Add(new MediaAsset
        {
            Id = assetId,
            FileName = fileName,
            FilePath = $"{rootPath}/{fileName}",
            MediaType = MediaType.Image,
            Status = MediaStatus.HasSuggestion,
            FileSize = 1024,
            ConfidenceScore = 85,
            SuggestedDate = new DateTime(2024, 8, 15),
            SuggestedByHeuristic = "FilenameHeuristic",
            SuggestionStatus = SuggestionReviewStatus.Unreviewed,
            ScanJobId = scanJobId,
            FileCreatedAt = new DateTime(2024, 8, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 8, 16, 12, 0, 0)
        });
        await db.SaveChangesAsync();
        return assetId;
    }
}
