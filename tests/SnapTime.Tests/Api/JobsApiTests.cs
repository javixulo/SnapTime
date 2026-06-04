using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Server.Models;

namespace SnapTime.Tests.Api;

public class JobsApiTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IScanJobService _mockService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JobsApiTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _mockService = _factory.MockService;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        public IScanJobService MockService { get; private set; } = Substitute.For<IScanJobService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(MockService);
            });
        }
    }

    [Fact]
    public async Task PostJob_ValidRequest_Returns201Created()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        try
        {
            var jobId = Guid.NewGuid();
            var job = new ScanJob
            {
                Id = jobId,
                Status = JobStatus.Running,
                RootPath = tempDir,
                TotalFiles = 0,
                ProcessedFiles = 0,
                ErrorCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _mockService.CreateJobAsync(tempDir).Returns(Task.FromResult(job));

            var response = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest(tempDir));

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var body = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
            body.Should().NotBeNull();
            body!.Id.Should().Be(jobId);
            body.Status.Should().Be(JobStatus.Running);
            body.RootPath.Should().Be(tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PostJob_EmptyRootPath_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest(""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostJob_NullRootPath_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { rootPath = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostJob_DuplicateRootPath_Returns409Conflict()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-dup-").FullName;
        try
        {
            _mockService
                .CreateJobAsync(tempDir)
                .Returns(Task.FromException<ScanJob>(new InvalidOperationException("A job already exists for this path.")));

            var response = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest(tempDir));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetJobs_WhenCalled_Returns200Ok()
    {
        var jobs = new List<ScanJob>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Status = JobStatus.Completed,
                RootPath = "/photos",
                TotalFiles = 10,
                ProcessedFiles = 10,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            }
        };
        _mockService.GetAllJobsAsync().Returns(Task.FromResult(jobs));

        var response = await _client.GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<JobDto>>(JsonOptions);
        body.Should().NotBeNull();
        body.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetJobById_ExistingJob_Returns200Ok()
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            Status = JobStatus.Running,
            RootPath = "/photos",
            TotalFiles = 50,
            ProcessedFiles = 25,
            ErrorCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _mockService.GetJobAsync(jobId).Returns(Task.FromResult<ScanJob?>(job));

        var response = await _client.GetAsync($"/api/jobs/{jobId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(jobId);
        body.ProcessedFiles.Should().Be(25);
        body.TotalFiles.Should().Be(50);
    }

    [Fact]
    public async Task GetJobById_NonExistingJob_Returns404NotFound()
    {
        var nonExistingId = Guid.NewGuid();
        _mockService.GetJobAsync(nonExistingId).Returns(Task.FromResult<ScanJob?>(null));

        var response = await _client.GetAsync($"/api/jobs/{nonExistingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostJobPause_ExistingJob_Returns200Ok()
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            Status = JobStatus.Paused,
            RootPath = "/photos",
            TotalFiles = 50,
            ProcessedFiles = 25,
            CreatedAt = DateTime.UtcNow
        };
        _mockService.PauseJobAsync(jobId).Returns(Task.CompletedTask);
        _mockService.GetJobAsync(jobId).Returns(Task.FromResult<ScanJob?>(job));

        var response = await _client.PostAsync($"/api/jobs/{jobId}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be(JobStatus.Paused);
    }

    [Fact]
    public async Task PostJobResume_ExistingJob_Returns200Ok()
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            Status = JobStatus.Running,
            RootPath = "/photos",
            TotalFiles = 50,
            ProcessedFiles = 25,
            CreatedAt = DateTime.UtcNow
        };
        _mockService.ResumeJobAsync(jobId).Returns(Task.CompletedTask);
        _mockService.GetJobAsync(jobId).Returns(Task.FromResult<ScanJob?>(job));

        var response = await _client.PostAsync($"/api/jobs/{jobId}/resume", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be(JobStatus.Running);
    }

    [Fact]
    public async Task PostJobCancel_ExistingJob_Returns200Ok()
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            Status = JobStatus.Cancelled,
            RootPath = "/photos",
            TotalFiles = 50,
            ProcessedFiles = 25,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _mockService.CancelJobAsync(jobId).Returns(Task.CompletedTask);
        _mockService.GetJobAsync(jobId).Returns(Task.FromResult<ScanJob?>(job));

        var response = await _client.PostAsync($"/api/jobs/{jobId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Status.Should().Be(JobStatus.Cancelled);
        body.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PostJobPause_NonExistingJob_Returns404NotFound()
    {
        var nonExistingId = Guid.NewGuid();
        _mockService.GetJobAsync(nonExistingId).Returns(Task.FromResult<ScanJob?>(null));

        var response = await _client.PostAsync($"/api/jobs/{nonExistingId}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostJobResume_NonExistingJob_Returns404NotFound()
    {
        var nonExistingId = Guid.NewGuid();
        _mockService.GetJobAsync(nonExistingId).Returns(Task.FromResult<ScanJob?>(null));

        var response = await _client.PostAsync($"/api/jobs/{nonExistingId}/resume", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostJobCancel_NonExistingJob_Returns404NotFound()
    {
        var nonExistingId = Guid.NewGuid();
        _mockService.GetJobAsync(nonExistingId).Returns(Task.FromResult<ScanJob?>(null));

        var response = await _client.PostAsync($"/api/jobs/{nonExistingId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostJob_NonExistentRootPath_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new CreateJobRequest("/ruta/que/no/existe"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
