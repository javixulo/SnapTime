// [F1-US-007]
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Tests.FileSystem;
using SnapTime.Tests.Metadata;
using SnapTime.Tests.Scanner;

namespace SnapTime.Tests.Jobs;

public class PersistenceTests
{
    [Fact]
    public async Task FirstScan_WithThreeFiles_InsertsThreeMediaAssets()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");
        walker.AddFile("/photos/photo2.jpg");
        walker.AddFile("/photos/photo3.jpg");

        var service = new InMemoryScanJobService(
            walker, new InMemoryMetadataExtractor(), new InMemoryFileSystemMetadataExtractor());

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var assets = service.GetPersistedAssets();
        assets.Should().HaveCount(3);
        assets.Should().AllSatisfy(a => a.ScanJobId.Should().Be(job.Id));
    }

    [Fact]
    public async Task SecondScan_SameFolder_UpsertsMaintainingThreeAssets_AndUpdatesScanJobId()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");
        walker.AddFile("/photos/photo2.jpg");
        walker.AddFile("/photos/photo3.jpg");

        var service = new InMemoryScanJobService(
            walker, new InMemoryMetadataExtractor(), new InMemoryFileSystemMetadataExtractor());

        var firstJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(firstJob.Id);

        var secondJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(secondJob.Id);

        var assets = service.GetPersistedAssets();
        assets.Should().HaveCount(3);
        assets.Should().AllSatisfy(a => a.ScanJobId.Should().Be(secondJob.Id));
    }

    [Fact]
    public async Task SecondScan_Upsert_ReplacesMetadataEntries()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");

        var metadataExtractor = new InMemoryMetadataExtractor();
        metadataExtractor.AddResult("/photos/photo1.jpg",
        [
            new MetadataEntry
            {
                Tag = "Exif SubIFD:Date/Time Original",
                Value = "2024:01:01 12:00:00",
                Source = "exif"
            }
        ]);

        var service = new InMemoryScanJobService(
            walker, metadataExtractor, new InMemoryFileSystemMetadataExtractor());

        var firstJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(firstJob.Id);

        var afterFirst = service.FindPersistedAssetByFilePath("/photos/photo1.jpg");
        afterFirst.Should().NotBeNull();
        afterFirst!.MetadataEntries.Should().HaveCount(1);

        metadataExtractor.AddResult("/photos/photo1.jpg",
        [
            new MetadataEntry
            {
                Tag = "Exif IFD0:Date/Time",
                Value = "2025:06:01 10:30:00",
                Source = "exif"
            }
        ]);

        var secondJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(secondJob.Id);

        var afterSecond = service.FindPersistedAssetByFilePath("/photos/photo1.jpg");
        afterSecond.Should().NotBeNull();
        afterSecond!.MetadataEntries.Should().HaveCount(1);
        afterSecond.MetadataEntries[0].Tag.Should().Be("Exif IFD0:Date/Time");
    }

    [Fact]
    public async Task SecondScan_Upsert_DoesNotChangeMediaType()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");

        var service = new InMemoryScanJobService(
            walker, new InMemoryMetadataExtractor(), new InMemoryFileSystemMetadataExtractor());

        var firstJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(firstJob.Id);

        var afterFirst = service.FindPersistedAssetByFilePath("/photos/photo1.jpg");
        afterFirst.Should().NotBeNull();
        afterFirst!.MediaType.Should().Be(MediaType.Image);

        service.SetMediaTypeOverride("/photos/photo1.jpg", MediaType.Video);

        var secondJob = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(secondJob.Id);

        var afterSecond = service.FindPersistedAssetByFilePath("/photos/photo1.jpg");
        afterSecond.Should().NotBeNull();
        afterSecond!.MediaType.Should().Be(MediaType.Image);
    }

    [Fact]
    public async Task Constructor_WithCustomBatchSize_LogsBatchSizeInAuditTrail()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");

        var service = new InMemoryScanJobService(
            walker, new InMemoryMetadataExtractor(), new InMemoryFileSystemMetadataExtractor(),
            batchSize: 10);

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var entries = service.GetAuditEntries(job.Id);
        entries.Should().Contain(e => e.Payload.Contains("BatchSize: 10"));
    }

    [Fact]
    public async Task PersistenceError_IncrementsErrorCount_AndJobCompletes()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");
        walker.AddFile("/photos/photo2.jpg");
        walker.AddFile("/photos/photo3.jpg");

        var service = new InMemoryScanJobService(
            walker, new InMemoryMetadataExtractor(), new InMemoryFileSystemMetadataExtractor());

        service.AddPersistenceErrorPath("/photos/photo2.jpg");

        var job = await service.CreateJobAsync("/photos");
        await service.WaitForCompletionAsync(job.Id);

        var completedJob = await service.GetJobAsync(job.Id);
        completedJob!.Status.Should().Be(JobStatus.Completed);
        completedJob.ErrorCount.Should().Be(1);
        completedJob.ProcessedFiles.Should().Be(3);
    }
}
