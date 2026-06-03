using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Data;

namespace SnapTime.Infrastructure.Services;

public record JobProgress(int TotalFiles, int ProcessedFiles, int ErrorCount);

public class ScanJobService : IScanJobService
{
    private readonly IBackgroundJobRunner _jobRunner;
    private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cts = new();
    private readonly ConcurrentDictionary<Guid, ManualResetEventSlim> _pauseEvents = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _completionSources = new();

    private readonly IDirectoryWalker _walker;
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IFileSystemMetadataExtractor _fileSystemExtractor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanJobService> _logger;
    private readonly string[] _imageExtensions;
    private readonly string[] _videoExtensions;

    public ConcurrentDictionary<Guid, JobProgress> Progress { get; } = new();

    public ScanJobService(
        IBackgroundJobRunner jobRunner,
        IDirectoryWalker walker,
        IMetadataExtractor metadataExtractor,
        IFileSystemMetadataExtractor fileSystemExtractor,
        IServiceScopeFactory scopeFactory,
        ILogger<ScanJobService> logger,
        string[]? imageExtensions = null,
        string[]? videoExtensions = null)
    {
        _jobRunner = jobRunner;
        _walker = walker;
        _metadataExtractor = metadataExtractor;
        _fileSystemExtractor = fileSystemExtractor;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _imageExtensions = imageExtensions ?? [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp"];
        _videoExtensions = videoExtensions ?? [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];
    }

    public async Task<ScanJob> CreateJobAsync(string rootPath)
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            RootPath = rootPath,
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow
        };

        _jobs[jobId] = job;
        _cts[jobId] = new CancellationTokenSource();
        _pauseEvents[jobId] = new ManualResetEventSlim(initialState: true);
        _completionSources[jobId] = new TaskCompletionSource();

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
            db.ScanJobs.Add(job);
            db.AuditEntries.Add(CreateAuditEntry(jobId, "Created", $"Job created for path: {rootPath}"));
            await db.SaveChangesAsync();
        }

        await _jobRunner.EnqueueJobAsync(job);

        return job;
    }

    public Task<ScanJob?> GetJobAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public async Task<List<ScanJob>> GetAllJobsAsync()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
            return await db.ScanJobs.ToListAsync();
        }
    }

    public Task PauseJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == JobStatus.Running)
        {
            job.Status = JobStatus.Paused;
            if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
                pauseEvent.Reset();

            _ = AddAuditEntryAsync(jobId, "Paused", "Job was paused by user", CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public Task ResumeJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == JobStatus.Paused)
        {
            job.Status = JobStatus.Running;
            if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
                pauseEvent.Set();

            _ = AddAuditEntryAsync(jobId, "Resumed", "Job was resumed by user", CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public Task CancelJobAsync(Guid jobId)
    {
        if (_cts.TryGetValue(jobId, out var cts))
            cts.Cancel();

        if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
            pauseEvent.Set();

        _ = AddAuditEntryAsync(jobId, "CancelRequested", "Job cancellation was requested by user", CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task ProcessJobAsync(Guid jobId)
    {
        return RunPipelineAsync(jobId);
    }

    private async Task RunPipelineAsync(Guid jobId)
    {
        try
        {
            var ct = _cts.TryGetValue(jobId, out var cts) ? cts.Token : CancellationToken.None;
            var rootPath = _jobs.TryGetValue(jobId, out var job) ? job.RootPath : string.Empty;

            if (!Directory.Exists(rootPath))
            {
                await SetJobFinalStateAsync(jobId, JobStatus.Error, $"Root path does not exist: {rootPath}", CancellationToken.None);
                return;
            }

            var files = await CollectFilesAsync(jobId, rootPath, ct);
            await SaveFileCountAsync(jobId, files.Count, ct);

            var pendingAssets = new List<MediaAsset>();

            for (var i = 0; i < files.Count; i++)
            {
                if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
                    pauseEvent.Wait(ct);

                await ProcessSingleFileAsync(jobId, files[i], pendingAssets, ct);

                _jobs[jobId].ProcessedFiles = i + 1;
                UpdateProgress(jobId, files.Count, _jobs[jobId].ProcessedFiles, _jobs[jobId].ErrorCount);

                if ((i + 1) % 50 == 0 || i == files.Count - 1)
                {
                    ct.ThrowIfCancellationRequested();
                    await PersistProgressCheckpointAsync(jobId, pendingAssets, i + 1, files.Count, ct);
                    pendingAssets.Clear();
                }
            }

            await SetJobFinalStateAsync(jobId, JobStatus.Completed, "Job completed successfully", CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await SetJobFinalStateAsync(jobId, JobStatus.Cancelled, "Job was cancelled", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);
            await SetJobFinalStateAsync(jobId, JobStatus.Error, "An unexpected error occurred", CancellationToken.None);
        }
        finally
        {
            if (_completionSources.TryRemove(jobId, out var tcs))
                tcs.SetResult();
            Cleanup(jobId);
        }
    }

    private async Task<List<FileInfo>> CollectFilesAsync(Guid jobId, string rootPath, CancellationToken ct)
    {
        var files = new List<FileInfo>();
        await foreach (var file in _walker.WalkAsync(rootPath, _imageExtensions, _videoExtensions, ct))
        {
            ct.ThrowIfCancellationRequested();
            files.Add(file);
        }
        return files;
    }

    private async Task SaveFileCountAsync(Guid jobId, int totalFiles, CancellationToken ct)
    {
        _jobs[jobId].TotalFiles = totalFiles;
        UpdateProgress(jobId, totalFiles, 0, 0);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
            var dbJob = await db.ScanJobs.FindAsync([jobId], ct);
            if (dbJob != null)
            {
                dbJob.TotalFiles = totalFiles;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task ProcessSingleFileAsync(Guid jobId, FileInfo file, List<MediaAsset> pendingAssets, CancellationToken ct)
    {
        try
        {
            var ext = Path.GetExtension(file.FullName);
            var mediaType = GetMediaType(ext);

            var metadataEntries = await _metadataExtractor.ExtractAsync(file.FullName, mediaType, ct);
            var fsEntries = _fileSystemExtractor.ExtractFileSystemDates(file.FullName);

            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                FilePath = file.FullName,
                FileName = file.Name,
                MediaType = mediaType,
                FileSize = file.Length,
                ScanJobId = jobId,
                Status = MediaStatus.Pending
            };
            asset.MetadataEntries.AddRange(metadataEntries);
            asset.MetadataEntries.AddRange(fsEntries);

            pendingAssets.Add(asset);
            _jobs[jobId].MediaAssets.Add(asset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error processing file {File}", file.FullName);
            _jobs[jobId].ErrorCount++;
        }
    }

    private async Task PersistProgressCheckpointAsync(Guid jobId, List<MediaAsset> assets, int processed, int total, CancellationToken ct)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();

                foreach (var asset in assets)
                    db.MediaAssets.Add(asset);

                db.AuditEntries.Add(CreateAuditEntry(jobId, "Checkpoint", $"Processed {processed}/{total} files"));

                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist checkpoint for job {JobId}", jobId);
        }
    }

    private async Task SetJobFinalStateAsync(Guid jobId, JobStatus status, string message, CancellationToken ct)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            job.CompletedAt = DateTime.UtcNow;
            UpdateProgress(jobId, job.TotalFiles, job.ProcessedFiles, job.ErrorCount);
        }

        await PersistJobFinalStateAsync(jobId, status, message, ct);
    }

    private async Task PersistJobFinalStateAsync(Guid jobId, JobStatus status, string message, CancellationToken ct)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();

                var dbJob = await db.ScanJobs.FindAsync([jobId], ct);
                if (dbJob != null)
                {
                    dbJob.Status = status;
                    dbJob.TotalFiles = _jobs.TryGetValue(jobId, out var j) ? j.TotalFiles : dbJob.TotalFiles;
                    dbJob.ProcessedFiles = _jobs.TryGetValue(jobId, out j) ? j.ProcessedFiles : dbJob.ProcessedFiles;
                    dbJob.ErrorCount = _jobs.TryGetValue(jobId, out j) ? j.ErrorCount : dbJob.ErrorCount;
                    dbJob.CompletedAt = DateTime.UtcNow;
                }

                db.AuditEntries.Add(CreateAuditEntry(jobId, status.ToString(), message));

                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist final state for job {JobId}", jobId);
        }
    }

    private async Task AddAuditEntryAsync(Guid jobId, string eventType, string payload, CancellationToken ct = default)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
                db.AuditEntries.Add(CreateAuditEntry(jobId, eventType, payload));
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to add audit entry for job {JobId}", jobId);
        }
    }

    private static AuditEntry CreateAuditEntry(Guid jobId, string eventType, string payload) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        Payload = payload,
        CreatedAt = DateTime.UtcNow,
        ScanJobId = jobId
    };

    private void UpdateProgress(Guid jobId, int total, int processed, int errors)
    {
        Progress[jobId] = new JobProgress(total, processed, errors);
    }

    private void Cleanup(Guid jobId)
    {
        _cts.TryRemove(jobId, out _);
        _pauseEvents.TryRemove(jobId, out var pauseEvent);
        pauseEvent?.Dispose();
        _jobs.TryRemove(jobId, out _);
    }

    private MediaType GetMediaType(string extension)
    {
        if (_imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return MediaType.Image;
        return MediaType.Video;
    }
}
