using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Tests.FileSystem;
using SnapTime.Tests.Metadata;
using SnapTime.Tests.Scanner;

namespace SnapTime.Tests.Jobs;

public class IScanJobServiceTests
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg"];
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];

    private static InMemoryScanJobService CreateService(
        Action<InMemoryDirectoryWalker>? configureWalker = null,
        Action<InMemoryMetadataExtractor>? configureMetadata = null,
        Action<InMemoryFileSystemMetadataExtractor>? configureFileSystem = null)
    {
        var walker = new InMemoryDirectoryWalker();
        var metadataExtractor = new InMemoryMetadataExtractor();
        var fileSystemExtractor = new InMemoryFileSystemMetadataExtractor();

        configureWalker?.Invoke(walker);
        configureMetadata?.Invoke(metadataExtractor);
        configureFileSystem?.Invoke(fileSystemExtractor);

        return new InMemoryScanJobService(walker, metadataExtractor, fileSystemExtractor);
    }

    [Fact]
    public async Task CreateJobAsync_ValidPath_ReturnsScanJobWithStatusRunning()
    {
        var service = CreateService(walker =>
        {
            walker.AddFile("/photos/photo1.jpg");
            walker.AddFile("/photos/photo2.jpg");
        });

        service.HoldPipelineStart();

        var job = await service.CreateJobAsync("/photos");

        job.Should().NotBeNull();
        job.Id.Should().NotBeEmpty();
        job.Status.Should().Be(JobStatus.Running);
        job.RootPath.Should().Be("/photos");

        service.ReleasePipelineStart();
    }

    [Fact]
    public async Task CreateJobAsync_JobCompletes_StatusCompletedAndProcessedFilesEqualsTotal()
    {
        const int fileCount = 10;
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= fileCount; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var completedJob = await service.GetJobAsync(job.Id);
        completedJob.Should().NotBeNull();
        completedJob!.Status.Should().Be(JobStatus.Completed);
        completedJob.ProcessedFiles.Should().Be(fileCount);
        completedJob.TotalFiles.Should().Be(fileCount);
        completedJob.ErrorCount.Should().Be(0);
        completedJob.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PauseJobAsync_WhenRunning_SetsStatusPaused()
    {
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= 100; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        service.SetProcessingDelay(1);
        service.HoldPipelineStart();

        var job = await service.CreateJobAsync("/photos");
        service.ReleasePipelineStart();

        await Task.Delay(10);
        await service.PauseJobAsync(job.Id);

        await Task.Delay(5);

        var pausedJob = await service.GetJobAsync(job.Id);
        pausedJob!.Status.Should().Be(JobStatus.Paused);
        pausedJob.ProcessedFiles.Should().BeGreaterThan(0);

        var processedBefore = pausedJob.ProcessedFiles;
        await Task.Delay(50);
        var afterPause = await service.GetJobAsync(job.Id);
        afterPause!.ProcessedFiles.Should().Be(processedBefore);

        await service.ResumeJobAsync(job.Id);
        await service.WaitForCompletionAsync(job.Id);
    }

    [Fact]
    public async Task ResumeJobAsync_WhenPaused_SetsStatusRunning()
    {
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= 100; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        service.HoldPipelineStart();

        var job = await service.CreateJobAsync("/photos");
        await service.PauseJobAsync(job.Id);
        service.ReleasePipelineStart();

        await Task.Delay(50);
        var pausedJob = await service.GetJobAsync(job.Id);
        pausedJob!.Status.Should().Be(JobStatus.Paused);

        await service.ResumeJobAsync(job.Id);
        await service.WaitForCompletionAsync(job.Id);

        var resumedJob = await service.GetJobAsync(job.Id);
        resumedJob!.Status.Should().Be(JobStatus.Completed);
        resumedJob.TotalFiles.Should().Be(100);
    }

    [Fact]
    public async Task CancelJobAsync_WhenRunning_SetsStatusCancelled()
    {
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= 100; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        service.SetProcessingDelay(1);
        service.HoldPipelineStart();

        var job = await service.CreateJobAsync("/photos");
        service.ReleasePipelineStart();

        await Task.Delay(10);
        await service.CancelJobAsync(job.Id);
        await service.WaitForCompletionAsync(job.Id);

        var cancelledJob = await service.GetJobAsync(job.Id);
        cancelledJob!.Status.Should().Be(JobStatus.Cancelled);
        cancelledJob.ProcessedFiles.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateJobAsync_ProcessCheckpoint_RegisteredAfterEvery50Files()
    {
        const int fileCount = 55;
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= fileCount; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var entries = service.GetAuditEntries(job.Id);
        var checkpoints = entries.Where(e => e.EventType == "Checkpoint").ToList();
        checkpoints.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateJobAsync_InvalidRootPath_SetsStatusError()
    {
        var service = CreateService(walker =>
        {
            walker.AddFile("/photos/photo1.jpg");
        });
        service.AddInvalidRootPath("/nonexistent");

        var job = await service.CreateJobAsync("/nonexistent");
        await service.WaitForCompletionAsync(job.Id);

        var errorJob = await service.GetJobAsync(job.Id);
        errorJob!.Status.Should().Be(JobStatus.Error);
    }

    [Fact]
    public async Task CreateJobAsync_FileProcessingError_ErrorCountIncrementedAndJobContinues()
    {
        var service = CreateService(walker =>
        {
            walker.AddFile("/photos/good1.jpg");
            walker.AddFile("/photos/bad.jpg");
            walker.AddFile("/photos/good2.jpg");
        });
        service.AddErrorFile("/photos/bad.jpg");

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var completedJob = await service.GetJobAsync(job.Id);
        completedJob!.Status.Should().Be(JobStatus.Completed);
        completedJob.ErrorCount.Should().Be(1);
        completedJob.ProcessedFiles.Should().Be(3);
        completedJob.TotalFiles.Should().Be(3);
    }

    [Fact]
    public async Task GetJobAsync_ExistingId_ReturnsScanJob()
    {
        var service = CreateService(walker =>
        {
            walker.AddFile("/photos/photo1.jpg");
        });

        var created = await service.CreateJobAsync("/photos");
        var fetched = await service.GetJobAsync(created.Id);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);
        fetched.RootPath.Should().Be("/photos");
    }

    [Fact]
    public async Task GetJobAsync_NonExistingId_ReturnsNull()
    {
        var service = CreateService();

        var fetched = await service.GetJobAsync(Guid.NewGuid());

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task GetAllJobsAsync_ReturnsAllJobs()
    {
        var service = CreateService();

        service.HoldPipelineStart();

        var job1 = await service.CreateJobAsync("/path1");
        var job2 = await service.CreateJobAsync("/path2");
        var job3 = await service.CreateJobAsync("/path3");

        var allJobs = await service.GetAllJobsAsync();

        allJobs.Should().HaveCount(3);
        allJobs.Select(j => j.Id).Should().Contain([job1.Id, job2.Id, job3.Id]);

        service.ReleasePipelineStart();
    }
}
