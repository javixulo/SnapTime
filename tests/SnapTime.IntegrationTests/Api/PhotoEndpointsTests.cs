// [F5] Integration tests for GET /api/photos endpoint
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class PhotoEndpointsTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    public PhotoEndpointsTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetPhotos_NoPath_ReturnsEmpty()
    {
        // [F5] GET /api/photos without a path returns an empty paginated response
        var response = await _client.GetAsync("/api/photos");

        // 200 OK even when no assets exist
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        doc.Should().NotBeNull();

        var root = doc!.RootElement;
        root.GetProperty("items").GetArrayLength().Should().Be(0);
        root.GetProperty("totalCount").GetInt32().Should().Be(0);
        root.GetProperty("page").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetPhotos_WithSamplePath_ReturnsItems()
    {
        // [F5] GET /api/photos with a path returns paginated items from that directory
        // Seed a ScanJob + assets in the test database
        await using var db = _fixture.CreateContext();

        var scanJobId = Guid.NewGuid();
        db.ScanJobs.Add(new ScanJob
        {
            Id = scanJobId,
            Status = JobStatus.Completed,
            RootPath = "/sample",
            CreatedAt = DateTime.UtcNow
        });

        db.MediaAssets.Add(new MediaAsset
        {
            Id = Guid.NewGuid(),
            FileName = "vacation.jpg",
            FilePath = "/sample/vacation.jpg",
            MediaType = MediaType.Image,
            Status = MediaStatus.Pending,
            FileSize = 1024,
            ScanJobId = scanJobId
        });

        db.MediaAssets.Add(new MediaAsset
        {
            Id = Guid.NewGuid(),
            FileName = "party.mp4",
            FilePath = "/sample/party.mp4",
            MediaType = MediaType.Video,
            Status = MediaStatus.Pending,
            FileSize = 2048,
            ScanJobId = scanJobId
        });

        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/photos?path=/sample&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        doc.Should().NotBeNull();

        var root = doc!.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCount(2);
        items.Should().Contain(i => i.GetProperty("name").GetString() == "vacation.jpg");
        items.Should().Contain(i => i.GetProperty("name").GetString() == "party.mp4");

        root.GetProperty("totalCount").GetInt32().Should().Be(2);
        root.GetProperty("page").GetInt32().Should().Be(1);
    }
}
