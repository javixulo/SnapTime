using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;
using SnapTime.Server.Models;
using SnapTime.Tests.FileSystem;
using SnapTime.Tests.Metadata;

namespace SnapTime.Tests.Api;

public class FileMetadataApiTests : IDisposable
{
    private readonly InMemoryFileMetadataWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileMetadataApiTests()
    {
        _factory = new InMemoryFileMetadataWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public class InMemoryFileMetadataWebApplicationFactory : WebApplicationFactory<Program>
    {
        public InMemoryMetadataExtractor MetadataExtractor { get; } = new();
        public InMemoryFileSystemMetadataExtractor FileSystemExtractor { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IMetadataExtractor>(MetadataExtractor);
                services.AddSingleton<IFileSystemMetadataExtractor>(FileSystemExtractor);
            });
        }
    }

    private static List<MetadataEntry> CreateQuickTimeMovieHeaderEntries(
        DateTime? created = null, DateTime? modified = null)
    {
        var entries = new List<MetadataEntry>();
        if (created.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Movie Header:Created",
                Value = created.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }
        if (modified.HasValue)
        {
            entries.Add(new MetadataEntry
            {
                Tag = "QuickTime Movie Header:Modified",
                Value = modified.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                Source = "quicktime"
            });
        }
        return entries;
    }

    [Fact]
    public async Task GetFileMetadata_VideoWithQuickTimeTags_ReturnsAllDates()
    {
        var sampleDate = new DateTime(2024, 8, 15, 14, 30, 0, DateTimeKind.Unspecified);

        var tempFile = Path.GetTempFileName() + ".mp4";
        try
        {
            await File.WriteAllTextAsync(tempFile, "fake video content");

            var expectedMetadata = CreateQuickTimeMovieHeaderEntries(
                created: sampleDate,
                modified: sampleDate.AddHours(1));

            _factory.MetadataExtractor.AddResult(tempFile, expectedMetadata);

            var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString(tempFile)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var metadata = await response.Content.ReadFromJsonAsync<FileMetadataDto>();
            metadata.Should().NotBeNull();
            metadata!.FilePath.Should().Be(tempFile);
            metadata.DateTimeOriginal.Should().Be(sampleDate);
            metadata.CreateDate.Should().Be(sampleDate);
            metadata.ModifyDate.Should().Be(sampleDate.AddHours(1));
            metadata.FileCreatedAt.Should().NotBeNull();
            metadata.FileModifiedAt.Should().NotBeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetFileMetadata_VideoWithOnlyCreatedAndModified_ReturnsPartialDates()
    {
        var createDate = new DateTime(2024, 6, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var modifyDate = new DateTime(2024, 6, 2, 12, 0, 0, DateTimeKind.Unspecified);

        var tempFile = Path.GetTempFileName() + ".mp4";
        try
        {
            await File.WriteAllTextAsync(tempFile, "fake");

            var metadataEntries = CreateQuickTimeMovieHeaderEntries(
                created: createDate, modified: modifyDate);

            _factory.MetadataExtractor.AddResult(tempFile, metadataEntries);

            var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString(tempFile)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var metadata = await response.Content.ReadFromJsonAsync<FileMetadataDto>();
            metadata.Should().NotBeNull();
            metadata!.DateTimeOriginal.Should().Be(createDate);
            metadata.CreateDate.Should().Be(createDate);
            metadata.ModifyDate.Should().Be(modifyDate);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetFileMetadata_ImageWithExifDates_ReturnsAllDates()
    {
        var dateTimeOriginal = new DateTime(2023, 12, 25, 8, 0, 0, DateTimeKind.Unspecified);
        var createDate = new DateTime(2023, 12, 25, 9, 0, 0, DateTimeKind.Unspecified);
        var modifyDate = new DateTime(2023, 12, 25, 10, 0, 0, DateTimeKind.Unspecified);

        var tempFile = Path.GetTempFileName() + ".jpg";
        try
        {
            await File.WriteAllTextAsync(tempFile, "fake image");

            var exifEntries = InMemoryMetadataExtractor.CreateExifEntries(
                dateTimeOriginal: dateTimeOriginal,
                subSecDateTimeOriginal: "10",
                createDate: createDate,
                modifyDate: modifyDate);

            _factory.MetadataExtractor.AddResult(tempFile, exifEntries);

            var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString(tempFile)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var metadata = await response.Content.ReadFromJsonAsync<FileMetadataDto>();
            metadata.Should().NotBeNull();
            metadata!.DateTimeOriginal.Should().Be(dateTimeOriginal);
            metadata.CreateDate.Should().Be(createDate);
            metadata.ModifyDate.Should().Be(modifyDate);
            metadata.SubSecDateTimeOriginal.Should().Be("10");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetFileMetadata_VideoWithLocaleDateFormat_ReturnsParsedDates()
    {
        var sampleDate = new DateTime(2024, 8, 15, 14, 30, 0, DateTimeKind.Unspecified);

        var tempFile = Path.GetTempFileName() + ".mp4";
        try
        {
            await File.WriteAllTextAsync(tempFile, "fake");

            var entries = new List<MetadataEntry>
            {
                new()
                {
                    Tag = "QuickTime Movie Header:Created",
                    Value = sampleDate.ToString(CultureInfo.CurrentCulture),
                    Source = "quicktime"
                },
                new()
                {
                    Tag = "QuickTime Movie Header:Modified",
                    Value = sampleDate.AddHours(1).ToString(CultureInfo.CurrentCulture),
                    Source = "quicktime"
                }
            };

            _factory.MetadataExtractor.AddResult(tempFile, entries);

            var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString(tempFile)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var metadata = await response.Content.ReadFromJsonAsync<FileMetadataDto>();
            metadata.Should().NotBeNull();
            metadata!.DateTimeOriginal.Should().Be(sampleDate);
            metadata.CreateDate.Should().Be(sampleDate);
            metadata.ModifyDate.Should().Be(sampleDate.AddHours(1));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetFileMetadata_NoMetadata_ReturnsFileDatesOnly()
    {
        var tempFile = Path.GetTempFileName() + ".mp4";
        try
        {
            await File.WriteAllTextAsync(tempFile, "fake");
            _factory.MetadataExtractor.AddResult(tempFile, []);

            var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString(tempFile)}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var metadata = await response.Content.ReadFromJsonAsync<FileMetadataDto>();
            metadata.Should().NotBeNull();
            metadata!.DateTimeOriginal.Should().BeNull();
            metadata.CreateDate.Should().BeNull();
            metadata.ModifyDate.Should().BeNull();
            metadata.FileCreatedAt.Should().NotBeNull();
            metadata.FileModifiedAt.Should().NotBeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetFileMetadata_NonExistentFile_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/media-assets/from-file?path={Uri.EscapeDataString("X:\\nonexistent\\file.mp4")}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFileMetadata_EmptyPath_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/media-assets/from-file?path=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
