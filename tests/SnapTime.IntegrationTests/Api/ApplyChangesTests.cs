using System.Net;
using System.Net.Http;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class ApplyChangesTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ApplyChangesTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = _fixture.CreateClient();
    }

    [Fact]
    public async Task PostApply_ReturnsApplyResults_ForExistingAssets()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("snaptime-apply-").FullName;
        try
        {
            // Create two temp files that will be referenced by the seeded MediaAssets
            var file1 = Path.Combine(tempDir, "a.jpg");
            var file2 = Path.Combine(tempDir, "b.jpg");
            File.WriteAllBytes(file1, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(file2, new byte[] { 4, 5, 6 });

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var scanJobId = Guid.NewGuid();

            await using (var db = _fixture.CreateContext())
            {
                db.ScanJobs.Add(new ScanJob
                {
                    Id = scanJobId,
                    Status = JobStatus.Completed,
                    RootPath = tempDir,
                    CreatedAt = DateTime.UtcNow
                });

                db.MediaAssets.Add(new MediaAsset
                {
                    Id = id1,
                    FileName = Path.GetFileName(file1),
                    FilePath = file1,
                    MediaType = MediaType.Image,
                    Status = MediaStatus.Pending,
                    FileSize = 123,
                    ScanJobId = scanJobId,
                    SuggestedDate = DateTime.UtcNow,
                    SuggestionStatus = SuggestionReviewStatus.Approved
                });

                db.MediaAssets.Add(new MediaAsset
                {
                    Id = id2,
                    FileName = Path.GetFileName(file2),
                    FilePath = file2,
                    MediaType = MediaType.Image,
                    Status = MediaStatus.Pending,
                    FileSize = 456,
                    ScanJobId = scanJobId,
                    SuggestedDate = DateTime.UtcNow,
                    SuggestionStatus = SuggestionReviewStatus.Approved
                });

                await db.SaveChangesAsync();
            }

            var payload = new { mediaAssetIds = new[] { id1, id2 } };
            var json = JsonSerializer.Serialize(payload, WebJsonOptions);

            // Act
            var response = await _client.PostAsync(
                "/api/apply",
                new StringContent(json, Encoding.UTF8, "application/json"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(WebJsonOptions);
            doc.Should().NotBeNull();

            var root = doc!.RootElement;
            var results = root.GetProperty("results").EnumerateArray().ToList();
            results.Should().HaveCount(2);

            results.Select(r => r.GetProperty("mediaAssetId").GetGuid())
                .Should().Contain(new[] { id1, id2 });

            root.GetProperty("appliedCount").GetInt32().Should().Be(2);
            root.GetProperty("failedCount").GetInt32().Should().Be(0);
            root.TryGetProperty("timestamp", out var ts).Should().BeTrue();
            ts.GetDateTime().Should().BeAfter(DateTime.UnixEpoch);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task PostApply_SkipsNonApprovedAndReportsMissingAssets()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory("snaptime-apply-").FullName;
        try
        {
            var approvedFile = Path.Combine(tempDir, "approved.jpg");
            var unapprovedFile = Path.Combine(tempDir, "unapproved.jpg");
            File.WriteAllBytes(approvedFile, new byte[] { 1 });
            File.WriteAllBytes(unapprovedFile, new byte[] { 2 });

            var idApproved = Guid.NewGuid();
            var idUnapproved = Guid.NewGuid();
            var missingId = Guid.NewGuid();
            var scanJobId = Guid.NewGuid();

            await using (var db = _fixture.CreateContext())
            {
                db.ScanJobs.Add(new ScanJob
                {
                    Id = scanJobId,
                    Status = JobStatus.Completed,
                    RootPath = tempDir,
                    CreatedAt = DateTime.UtcNow
                });

                db.MediaAssets.Add(new MediaAsset
                {
                    Id = idApproved,
                    FileName = Path.GetFileName(approvedFile),
                    FilePath = approvedFile,
                    MediaType = MediaType.Image,
                    Status = MediaStatus.Pending,
                    FileSize = 100,
                    ScanJobId = scanJobId,
                    SuggestedDate = DateTime.UtcNow,
                    SuggestionStatus = SuggestionReviewStatus.Approved
                });

                db.MediaAssets.Add(new MediaAsset
                {
                    Id = idUnapproved,
                    FileName = Path.GetFileName(unapprovedFile),
                    FilePath = unapprovedFile,
                    MediaType = MediaType.Image,
                    Status = MediaStatus.Pending,
                    FileSize = 200,
                    ScanJobId = scanJobId,
                    SuggestedDate = DateTime.UtcNow,
                    SuggestionStatus = SuggestionReviewStatus.Unreviewed
                });

                await db.SaveChangesAsync();
            }

            var payload = new { mediaAssetIds = new[] { idApproved, idUnapproved, missingId } };
            var json = JsonSerializer.Serialize(payload, WebJsonOptions);

            // Act
            var response = await _client.PostAsync(
                "/api/apply",
                new StringContent(json, Encoding.UTF8, "application/json"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(WebJsonOptions);
            doc.Should().NotBeNull();

            var root = doc!.RootElement;
            var results = root.GetProperty("results").EnumerateArray().ToList();
            results.Should().HaveCount(3);

            // approved should be success
            var approvedResult = results.Single(r => r.GetProperty("mediaAssetId").GetGuid() == idApproved);
            approvedResult.GetProperty("success").GetBoolean().Should().BeTrue();

            // unapproved should be failed with NotApproved error
            var unapprovedResult = results.Single(r => r.GetProperty("mediaAssetId").GetGuid() == idUnapproved);
            unapprovedResult.GetProperty("success").GetBoolean().Should().BeFalse();
            unapprovedResult.GetProperty("error").GetString().Should().BeOneOf("NotApproved", "notApproved", "NOT_APPROVED");

            // missing should be failed with NotFound
            var missingResult = results.Single(r => r.GetProperty("mediaAssetId").GetGuid() == missingId);
            missingResult.GetProperty("success").GetBoolean().Should().BeFalse();
            missingResult.GetProperty("error").GetString().Should().BeOneOf("NotFound", "notFound", "NOT_FOUND");

            root.GetProperty("appliedCount").GetInt32().Should().Be(1);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
