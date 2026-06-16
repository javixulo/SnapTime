// [F10-US-001] Integration tests for POST /api/clear
using System.Net;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class ClearDataTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    public ClearDataTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = _fixture.CreateClient();
    }

    [Fact]
    public async Task ClearData_AfterScan_RemovesAllScanData()
    {
        var scanJobId = Guid.NewGuid();
        var mediaAssetId = Guid.NewGuid();

        await using (var db = _fixture.CreateContext())
        {
            db.ScanJobs.Add(new ScanJob
            {
                Id = scanJobId,
                Status = JobStatus.Completed,
                RootPath = "/test",
                CreatedAt = DateTime.UtcNow
            });

            db.MediaAssets.Add(new MediaAsset
            {
                Id = mediaAssetId,
                FileName = "test.jpg",
                FilePath = "/test/test.jpg",
                MediaType = MediaType.Image,
                Status = MediaStatus.Pending,
                FileSize = 1024,
                ScanJobId = scanJobId
            });

            db.MetadataEntries.Add(new MetadataEntry
            {
                Id = Guid.NewGuid(),
                Tag = "DateTimeOriginal",
                Value = "2024:01:01",
                Source = "exif",
                MediaAssetId = mediaAssetId
            });

            db.EvidenceEntries.Add(new EvidenceEntry
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-001",
                HeuristicName = "Test",
                Weight = 1.0,
                Direction = EvidenceDirection.Positive,
                SuggestedDate = DateTime.UtcNow,
                Description = "test",
                MediaAssetId = mediaAssetId
            });

            db.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.NewGuid(),
                EventType = "ScanCompleted",
                Payload = "{}",
                CreatedAt = DateTime.UtcNow,
                ScanJobId = scanJobId
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync("/api/clear", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = _fixture.CreateContext())
        {
            (await db.ScanJobs.CountAsync()).Should().Be(0);
            (await db.MediaAssets.CountAsync()).Should().Be(0);
            (await db.MetadataEntries.CountAsync()).Should().Be(0);
            (await db.EvidenceEntries.CountAsync()).Should().Be(0);
            (await db.AuditEntries.CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task ClearData_EmptyDatabase_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/clear", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ClearData_PreservesSettings()
    {
        await using (var db = _fixture.CreateContext())
        {
            // Use unique IDs to avoid collisions with other tests in the same collection
            db.Settings.Add(new Settings { Id = 9999 });

            db.HeuristicConfigs.Add(new HeuristicConfigEntity
            {
                Id = "H-999-TEST",
                Enabled = true,
                Weight = 1.0
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync("/api/clear", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = _fixture.CreateContext())
        {
            // Settings and configs should still exist after clear
            (await db.Settings.AnyAsync(s => s.Id == 9999)).Should().BeTrue();
            (await db.HeuristicConfigs.AnyAsync(h => h.Id == "H-999-TEST")).Should().BeTrue();
        }
    }
}
