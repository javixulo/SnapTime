// [F4-US-005] Integration tests for GET /api/filesystem/directories endpoint
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SnapTime.IntegrationTests;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class FilesystemEndpointTests
{
    private readonly HttpClient _client;

    public FilesystemEndpointTests(SqliteDbFixture fixture)
    {
        // SqliteDbFixture needs CreateClient() -> WebApplicationFactory<Program>.CreateClient()
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetDirectories_NoPath_ReturnsRootDirectories()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/filesystem/directories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var directories = await response.Content.ReadFromJsonAsync<string[]>();
        directories.Should().NotBeNull();
        directories.Should().NotBeEmpty();
        directories.Should().AllSatisfy(d => d.Should().NotBeNullOrWhiteSpace());

        // Must NOT return ["/"] — each element is a directory name, not the root itself
        directories.Should().NotContain("/");
    }

    [Fact]
    public async Task GetDirectories_WithRootPath_ReturnsSubdirectories()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/filesystem/directories?path=/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var directories = await response.Content.ReadFromJsonAsync<string[]>();
        directories.Should().NotBeNull();
        directories.Should().NotBeEmpty();

        // Platform-aware assertions
        if (OperatingSystem.IsMacOS())
        {
            directories.Should().Contain("Users");
        }
        else if (OperatingSystem.IsLinux())
        {
            directories.Should().Contain("home");
        }
    }

    [Fact]
    public async Task GetDirectories_WithDoubleSlash_NormalizesPath()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/filesystem/directories?path=//Users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var directoriesWithDoubleSlash = await response.Content.ReadFromJsonAsync<string[]>();
        directoriesWithDoubleSlash.Should().NotBeNull();

        // Compare with single-slash path for the same result
        var normalResponse = await _client.GetAsync("/api/filesystem/directories?path=/Users");
        normalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var directoriesNormal = await normalResponse.Content.ReadFromJsonAsync<string[]>();

        directoriesWithDoubleSlash.Should().BeEquivalentTo(directoriesNormal);
    }

    [Fact]
    public async Task GetDirectories_NonExistentPath_Returns404()
    {
        // Arrange & Act
        var response = await _client.GetAsync(
            "/api/filesystem/directories?path=/ruta/que/no/existe/xyz123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDirectories_ExcludesSystemDirectories()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/filesystem/directories?path=/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var directories = await response.Content.ReadFromJsonAsync<string[]>();
        directories.Should().NotBeNull();

        // System directories that must be filtered out (macOS/Linux)
        var forbiddenNames = new[] { "System", "proc", "sys", "dev", "cores", "Volumes" };
        directories.Should().NotContain(forbiddenNames);
    }

    [Fact]
    public async Task GetDirectories_WithEmptyPath_SameAsNoPath()
    {
        // Arrange & Act
        var responseWithEmpty = await _client.GetAsync("/api/filesystem/directories?path=");
        var responseWithoutPath = await _client.GetAsync("/api/filesystem/directories");

        // Assert
        responseWithEmpty.StatusCode.Should().Be(HttpStatusCode.OK);
        responseWithoutPath.StatusCode.Should().Be(HttpStatusCode.OK);

        var dirsWithEmpty = await responseWithEmpty.Content.ReadFromJsonAsync<string[]>();
        var dirsWithoutPath = await responseWithoutPath.Content.ReadFromJsonAsync<string[]>();

        dirsWithEmpty.Should().BeEquivalentTo(dirsWithoutPath);
    }

    [Fact]
    public async Task GetDirectories_ValidPath_AllResultsAreDirectoryNamesOnly()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/filesystem/directories?path=/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var directories = await response.Content.ReadFromJsonAsync<string[]>();
        directories.Should().NotBeNull();
        directories.Should().NotBeEmpty();

        // Each result is a simple directory name, not a full path
        directories.Should().AllSatisfy(d =>
        {
            d.Should().NotContain("/");
            d.Should().NotContain("\\");
            d.Should().NotBeNullOrWhiteSpace();
        });
    }
}
