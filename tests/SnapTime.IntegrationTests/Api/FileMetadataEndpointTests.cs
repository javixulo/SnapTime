// [F6] Integration tests for GET /api/media-assets/from-file endpoint
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class FileMetadataEndpointTests
{
    private readonly HttpClient _client;

    public FileMetadataEndpointTests(SqliteDbFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetFileMetadata_ExistingFile_ReturnsMetadata()
    {
        // [F6] GET /api/media-assets/from-file with existing file path returns file metadata
        var sampleFile = GetSampleFilePath("IMG_20230515_123456.jpg");

        // Act
        var response = await _client.GetAsync(
            $"/api/media-assets/from-file?path={Uri.EscapeDataString(sampleFile)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        doc.Should().NotBeNull();

        var root = doc!.RootElement;
        root.GetProperty("filePath").GetString().Should().Be(sampleFile);
        root.GetProperty("fileName").GetString().Should().Be("IMG_20230515_123456.jpg");
        root.GetProperty("fileSize").GetInt64().Should().BeGreaterThan(0);
        root.GetProperty("fileCreatedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetFileMetadata_MissingFile_ReturnsNotFound()
    {
        // [F6] GET /api/media-assets/from-file with non-existent path returns 404
        var missingPath = "/tmp/nonexistent_file_xyz123_no_one_should_have_this.jpg";

        // Act
        var response = await _client.GetAsync(
            $"/api/media-assets/from-file?path={Uri.EscapeDataString(missingPath)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Resolves the absolute path to a sample file from the solution's sample directory.
    /// </summary>
    private static string GetSampleFilePath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var sampleDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "sample"));
        return Path.Combine(sampleDir, fileName);
    }
}
