using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Domain.Services;
using SnapTime.Infrastructure.Data;
using SnapTime.Infrastructure.Services;

// [F1-US-010] [F2-US-002] [F3-US-002]
namespace SnapTime.IntegrationTests;

[Collection("SqliteIntegration")]
public class ScanJobServiceIntegrationTests
{
    private readonly SqliteDbFixture _fixture;

    public ScanJobServiceIntegrationTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
    }

    [Fact]
    public async Task ProcessJobAsync_ThreeFiles_CompletesWithThreeAssetsInDb()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        try
        {
            var file1 = CreateRealFile(tempDir, "photo1.jpg");
            var file2 = CreateRealFile(tempDir, "photo2.jpg");
            var file3 = CreateRealFile(tempDir, "photo3.jpg");

            var metadataExtractor = Substitute.For<IMetadataExtractor>();
            metadataExtractor.ExtractAsync(Arg.Any<string>(), Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<MetadataEntry>()));

            var (scanJobService, job) = await CreateSutAsync(tempDir, [file1, file2, file3], metadataExtractor);

            await scanJobService.ProcessJobAsync(job.Id);
            await Task.Delay(200);

            var completedJob = await scanJobService.GetJobAsync(job.Id);
            completedJob.Should().NotBeNull();
            completedJob!.Status.Should().Be(JobStatus.Completed);
            completedJob.ProcessedFiles.Should().Be(3);

            using var db = _fixture.CreateContext();
            var assets = await db.MediaAssets.ToListAsync();
            assets.Should().HaveCount(3);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_OneFileError_CompletesWithErrorCountOne()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        try
        {
            var file1 = CreateRealFile(tempDir, "good1.jpg");
            var file2 = CreateRealFile(tempDir, "bad.jpg");
            var file3 = CreateRealFile(tempDir, "good2.jpg");

            var metadataExtractor = Substitute.For<IMetadataExtractor>();
            metadataExtractor.ExtractAsync(Arg.Any<string>(), Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<MetadataEntry>()));
            metadataExtractor.ExtractAsync(file2.FullName, Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<List<MetadataEntry>>(new InvalidOperationException("Simulated error")));

            var (scanJobService, job) = await CreateSutAsync(tempDir, [file1, file2, file3], metadataExtractor);

            await scanJobService.ProcessJobAsync(job.Id);
            await Task.Delay(200);

            var completedJob = await scanJobService.GetJobAsync(job.Id);
            completedJob.Should().NotBeNull();
            completedJob!.Status.Should().Be(JobStatus.Completed);
            completedJob.ProcessedFiles.Should().Be(3);
            completedJob.ErrorCount.Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_Cancelled_StopsBeforeAllFilesProcessed()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        try
        {
            var files = new FileInfo[10];
            for (var i = 0; i < files.Length; i++)
                files[i] = CreateRealFile(tempDir, $"photo{i + 1}.jpg");

            var metadataExtractor = Substitute.For<IMetadataExtractor>();
            metadataExtractor.ExtractAsync(Arg.Any<string>(), Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(_ => Task.Run(async () =>
                {
                    await Task.Delay(50);
                    return new List<MetadataEntry>();
                }));

            var (scanJobService, job) = await CreateSutAsync(tempDir, files, metadataExtractor);

            var processTask = scanJobService.ProcessJobAsync(job.Id);
            await Task.Delay(100);
            await scanJobService.CancelJobAsync(job.Id);
            await processTask;
            await Task.Delay(100);

            var cancelledJob = await scanJobService.GetJobAsync(job.Id);
            cancelledJob.Should().NotBeNull();
            cancelledJob!.Status.Should().Be(JobStatus.Cancelled);
            cancelledJob.ProcessedFiles.Should().BeLessThan(10);
            cancelledJob.ProcessedFiles.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessJobAsync_FilenameWithH006Heuristic_PersistsEvidenceEntry()
    {
        var tempDir = Directory.CreateTempSubdirectory("snaptime-test-").FullName;
        try
        {
            var mismatchFile = CreateRealFile(tempDir, "20250315_123456.jpg");
            var noDateFile = CreateRealFile(tempDir, "vacation.jpg");

            var mismatchMetadata = new List<MetadataEntry>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Tag = "EXIF:DateTimeOriginal",
                    Value = "2024:07:10 12:34:56",
                    Source = "exif"
                }
            };

            var metadataExtractor = Substitute.For<IMetadataExtractor>();
            metadataExtractor.ExtractAsync(mismatchFile.FullName, Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(mismatchMetadata));
            metadataExtractor.ExtractAsync(noDateFile.FullName, Arg.Any<MediaType>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<MetadataEntry>()));

            var heuristics = new List<IHeuristic> { new H006FilenameHeuristic() };

            var (scanJobService, job) = await CreateSutAsync(tempDir, [mismatchFile, noDateFile], metadataExtractor, heuristics);

            await scanJobService.ProcessJobAsync(job.Id);
            await Task.Delay(200);

            var completedJob = await scanJobService.GetJobAsync(job.Id);
            completedJob.Should().NotBeNull();
            completedJob!.Status.Should().Be(JobStatus.Completed);

            using var db = _fixture.CreateContext();
            var assets = await db.MediaAssets
                .Include(a => a.EvidenceEntries)
                .Include(a => a.MetadataEntries)
                .ToListAsync();

            assets.Should().HaveCount(2);

            var mismatchAsset = assets.First(a => a.FileName == "20250315_123456.jpg");
            mismatchAsset.EvidenceEntries.Should().HaveCount(1);
            var evidence = mismatchAsset.EvidenceEntries.Single();
            evidence.HeuristicId.Should().Be("H-006");
            evidence.Direction.Should().Be(EvidenceDirection.Correction);
            evidence.SuggestedDate.Should().Be(new DateTime(2025, 3, 15, 5, 0, 0));
            evidence.Weight.Should().Be(0.7);

            var noDateAsset = assets.First(a => a.FileName == "vacation.jpg");
            noDateAsset.EvidenceEntries.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static FileInfo CreateRealFile(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xD9]);
        return new FileInfo(path);
    }

    private async Task<(ScanJobService Service, ScanJob Job)> CreateSutAsync(
        string tempDir, FileInfo[] files, IMetadataExtractor metadataExtractor,
        IEnumerable<IHeuristic>? heuristics = null)
    {
        var walker = Substitute.For<IDirectoryWalker>();
        walker.WalkAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(files));

        var fileSystemExtractor = Substitute.For<IFileSystemMetadataExtractor>();
        fileSystemExtractor.ExtractFileSystemDates(Arg.Any<string>())
            .Returns(new List<MetadataEntry>());

        var jobRunner = Substitute.For<IBackgroundJobRunner>();
        var logger = Substitute.For<ILogger<ScanJobService>>();

        var services = new ServiceCollection();
        services.AddScoped<SnapTimeDbContext>(_ => _fixture.CreateContext());
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var scanJobService = new ScanJobService(
            jobRunner, walker, metadataExtractor, fileSystemExtractor,
            heuristics ?? Enumerable.Empty<IHeuristic>(),
            scopeFactory, logger);

        var job = await scanJobService.CreateJobAsync(tempDir);

        return (scanJobService, job);
    }

    private static async IAsyncEnumerable<FileInfo> ToAsyncEnumerable(FileInfo[] files)
    {
        foreach (var f in files)
            yield return f;
        await Task.CompletedTask;
    }
}
