using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Server.Models;
using SnapTime.Tests.FileSystem;
using SnapTime.Tests.Jobs;
using SnapTime.Tests.Metadata;
using SnapTime.Tests.Scanner;

// [F1-US-006]
namespace SnapTime.Tests.Api;

public class JobProgressApiTests : IDisposable
{
    private readonly InMemoryProgressWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public JobProgressApiTests()
    {
        _factory = new InMemoryProgressWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public class InMemoryProgressWebApplicationFactory : WebApplicationFactory<Program>
    {
        public InMemoryDirectoryWalker Walker { get; } = new();
        public InMemoryMetadataExtractor MetadataExtractor { get; } = new();
        public InMemoryFileSystemMetadataExtractor FileSystemExtractor { get; } = new();
        public InMemoryScanJobService ScanJobService { get; private set; } = null!;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IDirectoryWalker>(Walker);
                services.AddSingleton<IMetadataExtractor>(MetadataExtractor);
                services.AddSingleton<IFileSystemMetadataExtractor>(FileSystemExtractor);

                ScanJobService = new InMemoryScanJobService(Walker, MetadataExtractor, FileSystemExtractor, artificialDelayMs: 15);
                services.AddSingleton<IScanJobService>(ScanJobService);
            });
        }
    }

    [Fact]
    public async Task GetJobEndpoint_ProcessedFilesIncreasesProgressively()
    {
        const int fileCount = 5;
        var tempDir = Directory.CreateTempSubdirectory("snaptime-progress-").FullName;
        try
        {
            for (var i = 1; i <= fileCount; i++)
                _factory.Walker.AddFile($"{tempDir}/photo{i}.jpg");

            var createResponse = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest(tempDir));
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdJob = await createResponse.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
            createdJob.Should().NotBeNull();

            var observedValues = new List<int>();
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline && observedValues.Count < fileCount)
            {
                await Task.Delay(15);
                var getResponse = await _client.GetAsync($"/api/jobs/{createdJob!.Id}");
                getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                var jobDto = await getResponse.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
                if (jobDto is not null && !observedValues.Contains(jobDto.ProcessedFiles))
                    observedValues.Add(jobDto.ProcessedFiles);
            }

            observedValues.Should().HaveCountGreaterOrEqualTo(3);
            observedValues.Should().BeInAscendingOrder();
            observedValues.Last().Should().Be(fileCount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetJobEndpoint_ErrorCountVisibleWhileRunning()
    {
        const int fileCount = 5;
        var tempDir = Directory.CreateTempSubdirectory("snaptime-errors-").FullName;
        try
        {
            for (var i = 1; i <= fileCount; i++)
                _factory.Walker.AddFile($"{tempDir}/photo{i}.jpg");

            _factory.ScanJobService.AddErrorFile($"{tempDir}/photo2.jpg");
            _factory.ScanJobService.AddErrorFile($"{tempDir}/photo4.jpg");

            var createResponse = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest(tempDir));
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var createdJob = await createResponse.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
            createdJob.Should().NotBeNull();

            var errorSeenWhileRunning = false;
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline && !errorSeenWhileRunning)
            {
                await Task.Delay(15);
                var getResponse = await _client.GetAsync($"/api/jobs/{createdJob!.Id}");
                getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
                var jobDto = await getResponse.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
                if (jobDto is not null && jobDto.ErrorCount > 0 && jobDto.Status == JobStatus.Running)
                    errorSeenWhileRunning = true;
            }

            errorSeenWhileRunning.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
