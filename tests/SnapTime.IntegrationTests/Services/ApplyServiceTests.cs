// [F8-US-003] Integration tests for IApplyService
using System.Net;
using System.Net.Http.Json;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Models;

namespace SnapTime.IntegrationTests.Services;

[Collection("SqliteIntegration")]
public class ApplyServiceTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    public ApplyServiceTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = fixture.CreateClient();
    }

    private async Task<MediaAsset> SeedAssetAsync(
        string fileName = "test.jpg",
        SuggestionReviewStatus suggestionStatus = SuggestionReviewStatus.Approved,
        DateTime? suggestedDate = null,
        string? suggestedByHeuristic = "H-006",
        DateTime? dateTimeOriginal = null)
    {
        using var db = _fixture.CreateContext();
        var scanJobId = Guid.NewGuid();
        db.ScanJobs.Add(new ScanJob
        {
            Id = scanJobId,
            Status = JobStatus.Completed,
            RootPath = Path.GetTempPath(),
            CreatedAt = DateTime.UtcNow
        });
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            FilePath = Path.Combine(Path.GetTempPath(), fileName),
            FileName = fileName,
            MediaType = MediaType.Image,
            Status = MediaStatus.HasSuggestion,
            SuggestionStatus = suggestionStatus,
            SuggestedDate = suggestedDate,
            SuggestedByHeuristic = suggestedByHeuristic,
            FileSize = 100,
            ScanJobId = scanJobId
        };
        
        if (dateTimeOriginal.HasValue)
        {
            asset.MetadataEntries = new List<MetadataEntry>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Tag = "EXIF:DateTimeOriginal",
                    Value = dateTimeOriginal.Value.ToString("yyyy:MM:dd HH:mm:ss"),
                    Source = "exif",
                    MediaAssetId = asset.Id
                }
            };
        }
        
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset;
    }

    [Fact]
    public async Task ApplyAsync_WithApprovedAssetAndValidFile_WritesMetadataAndUpdatesStatus()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);
        var asset = await SeedAssetAsync(
            fileName: Path.GetFileName(tempFile),
            suggestedDate: new DateTime(2025, 4, 10, 5, 0, 0),
            dateTimeOriginal: new DateTime(2024, 1, 15, 10, 30, 0));
        
        // Set the file path to the real temp file
        using (var db = _fixture.CreateContext())
        {
            var a = await db.MediaAssets.FindAsync(asset.Id);
            a!.FilePath = tempFile;
            await db.SaveChangesAsync();
        }

        try
        {
            // Act
            var request = new ApplyChangesRequest(new List<Guid> { asset.Id });
            var response = await _client.PostAsJsonAsync("/api/apply", request);
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApplyChangesResponse>();
            Assert.NotNull(result);
            Assert.Equal(1, result.AppliedCount);
            Assert.Equal(0, result.FailedCount);
            
            var applyResult = result.Results.Single();
            Assert.True(applyResult.Success);
            Assert.Equal(asset.Id, applyResult.MediaAssetId);
            
            // Verify the asset status was updated to Completed
            using (var db2 = _fixture.CreateContext())
            {
                var updated = await db2.MediaAssets.FindAsync(asset.Id);
                Assert.NotNull(updated);
                Assert.Equal(MediaStatus.Completed, updated.Status); // TODO: Change to MediaStatus.Completed when enum is updated
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ApplyAsync_WithApprovedAssetButNoSuggestedDate_ReturnsError()
    {
        // Arrange
        var asset = await SeedAssetAsync(suggestedDate: null);
        
        // Act
        var request = new ApplyChangesRequest(new List<Guid> { asset.Id });
        var response = await _client.PostAsJsonAsync("/api/apply", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApplyChangesResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.FailedCount);
        
        var applyResult = result.Results.Single();
        Assert.False(applyResult.Success);
        Assert.Equal("NoSuggestedDate", applyResult.Error);
    }

    [Fact]
    public async Task ApplyAsync_WithNotApprovedAsset_ReturnsNotApprovedError()
    {
        // Arrange
        var asset = await SeedAssetAsync(suggestionStatus: SuggestionReviewStatus.Unreviewed);
        
        // Act
        var request = new ApplyChangesRequest(new List<Guid> { asset.Id });
        var response = await _client.PostAsJsonAsync("/api/apply", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ApplyChangesResponse>();
        Assert.NotNull(result);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.FailedCount);
        
        var applyResult = result.Results.Single();
        Assert.False(applyResult.Success);
        Assert.Equal("NotApproved", applyResult.Error);
    }

    [Fact]
    public async Task ApplyAsync_WithReadOnlyFile_ReturnsError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);
        File.SetAttributes(tempFile, FileAttributes.ReadOnly);
        var asset = await SeedAssetAsync(
            fileName: Path.GetFileName(tempFile),
            suggestedDate: new DateTime(2025, 4, 10, 5, 0, 0));
        
        using (var db = _fixture.CreateContext())
        {
            var a = await db.MediaAssets.FindAsync(asset.Id);
            a!.FilePath = tempFile;
            await db.SaveChangesAsync();
        }

        try
        {
            // Act
            var request = new ApplyChangesRequest(new List<Guid> { asset.Id });
            var response = await _client.PostAsJsonAsync("/api/apply", request);
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApplyChangesResponse>();
            Assert.NotNull(result);
            Assert.Equal(0, result.AppliedCount);
            Assert.Equal(1, result.FailedCount);
            
            var applyResult = result.Results.Single();
            Assert.False(applyResult.Success);
            Assert.NotNull(applyResult.Error);
            Assert.Contains("read-only", applyResult.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.SetAttributes(tempFile, FileAttributes.Normal);
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ApplyAsync_WithMixedAssets_AppliesPossibleAndReportsErrors()
    {
        // Arrange: 1 approved + file exists, 1 unreviewed
        var tempFile = Path.GetTempFileName() + ".jpg";
        CreateMinimalJpeg(tempFile);
        
        var asset1 = await SeedAssetAsync(
            fileName: Path.GetFileName(tempFile),
            suggestedDate: new DateTime(2025, 4, 10, 5, 0, 0));
        using (var db = _fixture.CreateContext())
        {
            var a = await db.MediaAssets.FindAsync(asset1.Id);
            a!.FilePath = tempFile;
            await db.SaveChangesAsync();
        }
        
        var asset2 = await SeedAssetAsync(
            fileName: "unreviewed.jpg",
            suggestionStatus: SuggestionReviewStatus.Unreviewed);

        try
        {
            // Act
            var request = new ApplyChangesRequest(new List<Guid> { asset1.Id, asset2.Id });
            var response = await _client.PostAsJsonAsync("/api/apply", request);
            
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApplyChangesResponse>();
            Assert.NotNull(result);
            Assert.Equal(1, result.AppliedCount);
            Assert.Equal(1, result.FailedCount);
            
            var successResult = result.Results.Single(r => r.Success);
            Assert.Equal(asset1.Id, successResult.MediaAssetId);
            
            var failedResult = result.Results.Single(r => !r.Success);
            Assert.Equal(asset2.Id, failedResult.MediaAssetId);
            Assert.Equal("NotApproved", failedResult.Error);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ApplyAsync_WithRunningScanJob_ReturnsConflict()
    {
        // Arrange: create a running scan job
        using (var db = _fixture.CreateContext())
        {
            db.ScanJobs.Add(new ScanJob
            {
                Id = Guid.NewGuid(),
                RootPath = "C:\\Test",
                Status = JobStatus.Running,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        
        // Act
        var request = new ApplyChangesRequest(new List<Guid>());
        var response = await _client.PostAsJsonAsync("/api/apply", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ApplyAsync_CreatesAuditEntry()
    {
        // Arrange
        var asset = await SeedAssetAsync();
        
        // Act
        var request = new ApplyChangesRequest(new List<Guid> { asset.Id });
        var response = await _client.PostAsJsonAsync("/api/apply", request);
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Verify AuditEntry was created
        using (var db = _fixture.CreateContext())
        {
            var auditEntries = db.AuditEntries.ToList();
            Assert.NotEmpty(auditEntries);
            var audit = auditEntries.Last();
            Assert.Equal("ApplyChanges", audit.EventType);
            Assert.Contains(asset.Id.ToString(), audit.Payload);
        }
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
