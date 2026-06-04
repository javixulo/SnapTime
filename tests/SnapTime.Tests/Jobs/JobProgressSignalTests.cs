using FluentAssertions;
using SnapTime.Domain.Enums;
using SnapTime.Tests.FileSystem;
using SnapTime.Tests.Metadata;
using SnapTime.Tests.Scanner;

// [F1-US-006]
namespace SnapTime.Tests.Jobs;

public class JobProgressSignalTests
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

    [Fact(Timeout = 5000)]
    public async Task ProcessedFiles_IncreasesProgressively_DuringExecution()
    {
        const int fileCount = 5;
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= fileCount; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        service.SetProcessingDelay(1);

        var capturedProgress = new List<int>();
        var signal = new TaskCompletionSource();

        service.ProgressChanged += (id, p) =>
        {
            lock (capturedProgress)
            {
                capturedProgress.Add(p.ProcessedFiles);
                if (capturedProgress.Count >= 3 || p.ProcessedFiles >= fileCount)
                    signal.TrySetResult();
            }
        };

        var job = await service.CreateJobAsync("/photos");

        await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await service.WaitForCompletionAsync(job.Id);

        lock (capturedProgress)
        {
            capturedProgress.Should().HaveCountGreaterOrEqualTo(3);
            capturedProgress.Should().BeInAscendingOrder();
            capturedProgress.Last().Should().Be(fileCount);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task ErrorCount_ReflectedInProgress_DuringExecution()
    {
        const int fileCount = 5;
        var service = CreateService(walker =>
        {
            for (var i = 1; i <= fileCount; i++)
                walker.AddFile($"/photos/photo{i}.jpg");
        });

        service.AddErrorFile("/photos/photo2.jpg");
        service.AddErrorFile("/photos/photo4.jpg");
        service.SetProcessingDelay(1);

        var errorSignal = new TaskCompletionSource();
        var observedProgress = new List<(int ErrorCount, int ProcessedFiles)>();

        service.ProgressChanged += (id, p) =>
        {
            lock (observedProgress)
            {
                observedProgress.Add((p.ErrorCount, p.ProcessedFiles));
                if (p.ErrorCount > 0)
                    errorSignal.TrySetResult();
            }
        };

        var job = await service.CreateJobAsync("/photos");

        await errorSignal.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var jobAtError = await service.GetJobAsync(job.Id);
        jobAtError.Should().NotBeNull();
        jobAtError!.Status.Should().Be(JobStatus.Running);

        await service.WaitForCompletionAsync(job.Id);

        var finalJob = await service.GetJobAsync(job.Id);
        finalJob.Should().NotBeNull();
        finalJob!.ErrorCount.Should().Be(2);
        finalJob.Status.Should().Be(JobStatus.Completed);

        lock (observedProgress)
        {
            observedProgress.Should().Contain(p => p.ErrorCount > 0);

            var firstErrorIdx = observedProgress.FindIndex(p => p.ErrorCount > 0);
            firstErrorIdx.Should().BeGreaterThan(0);
            observedProgress[firstErrorIdx].ErrorCount.Should().Be(1);
        }
    }
}
