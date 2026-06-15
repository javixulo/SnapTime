using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Data;

// [F1-US-007] [F1-US-010]
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
    private readonly IEnumerable<IHeuristic> _heuristics;
    private readonly IHeuristicEngine _heuristicEngine;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanJobService> _logger;
    private readonly string[] _imageExtensions;
    private readonly string[] _videoExtensions;
    private readonly int _batchSize;

    public ConcurrentDictionary<Guid, JobProgress> Progress { get; } = new();

    public ScanJobService(
        IBackgroundJobRunner jobRunner,
        IDirectoryWalker walker,
        IMetadataExtractor metadataExtractor,
        IFileSystemMetadataExtractor fileSystemExtractor,
        IEnumerable<IHeuristic> heuristics,
        IHeuristicEngine heuristicEngine,
        IServiceScopeFactory scopeFactory,
        ILogger<ScanJobService> logger,
        string[]? imageExtensions = null,
        string[]? videoExtensions = null,
        int batchSize = 50)
    {
        _jobRunner = jobRunner;
        _walker = walker;
        _metadataExtractor = metadataExtractor;
        _fileSystemExtractor = fileSystemExtractor;
        _heuristics = heuristics;
        _heuristicEngine = heuristicEngine;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _imageExtensions = imageExtensions ?? [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp"];
        _videoExtensions = videoExtensions ?? [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];
        _batchSize = batchSize;
    }

    public async Task<ScanJob> CreateJobAsync(string rootPath, bool includeSubfolders = true)
    {
        var jobId = Guid.NewGuid();
        var job = new ScanJob
        {
            Id = jobId,
            RootPath = rootPath,
            IncludeSubfolders = includeSubfolders,
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow
        };

        _jobs[jobId] = job;
        _cts[jobId] = new CancellationTokenSource();
        _pauseEvents[jobId] = new ManualResetEventSlim(initialState: true);
        _completionSources[jobId] = new TaskCompletionSource();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();

        // [F7-US-001] [F1-US-007] Re-scan: delete existing data for this root path before creating job
        await using var transaction = await db.Database.BeginTransactionAsync();

        await DeleteExistingAssetsAsync(db, rootPath, includeSubfolders);

        db.ScanJobs.Add(job);
        db.AuditEntries.Add(CreateAuditEntry(jobId, "Created", $"Job created for path: {rootPath}"));
        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        await _jobRunner.EnqueueJobAsync(job);

        return job;
    }

    /// <summary>
    /// Deletes all existing <see cref="MediaAsset"/>, <see cref="MetadataEntry"/>,
    /// and <see cref="EvidenceEntry"/> records for files under the given root path.
    /// Uses <c>Path.GetFullPath</c> for path normalization.
    /// Cascading deletes handle child MetadataEntry and EvidenceEntry removal.
    /// </summary>
    private static async Task DeleteExistingAssetsAsync(
        SnapTimeDbContext db,
        string rootPath,
        bool includeSubfolders)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var prefix = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var assetsToDelete = await db.MediaAssets
            .Where(a => a.FilePath == normalizedRoot || a.FilePath.StartsWith(prefix))
            .ToListAsync();

        if (!includeSubfolders)
        {
            // Only direct children: exclude files in subdirectories
            var rootFolderName = Path.GetFileName(normalizedRoot);
            assetsToDelete = assetsToDelete
                .Where(a =>
                {
                    var relative = a.FilePath[prefix.Length..];
                    return !relative.Contains(Path.DirectorySeparatorChar)
                        && !relative.Contains(Path.AltDirectorySeparatorChar);
                })
                .ToList();
        }

        if (assetsToDelete.Count > 0)
            db.MediaAssets.RemoveRange(assetsToDelete);
    }

    public async Task<ScanJob?> GetJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
            return job;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
        return await db.ScanJobs.FindAsync([jobId]);
    }

    public async Task<List<ScanJob>> GetAllJobsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
        return await db.ScanJobs.ToListAsync();
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

    public Task ProcessJobAsync(Guid jobId) => RunPipelineAsync(jobId);

    private async Task RunPipelineAsync(Guid jobId)
    {
        try
        {
            var ct = GetCancellationToken(jobId);

            // Load job from DB when not in memory (background runner creates new scope)
            if (!_jobs.TryGetValue(jobId, out var rootJob))
            {
                using var loadScope = _scopeFactory.CreateScope();
                var db = loadScope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
                var dbJob = await db.ScanJobs.FindAsync([jobId]);
                if (dbJob != null)
                {
                    _jobs[jobId] = dbJob;
                    _cts[jobId] = new CancellationTokenSource();
                    _pauseEvents[jobId] = new ManualResetEventSlim(initialState: true);
                    _completionSources[jobId] = new TaskCompletionSource();
                    rootJob = dbJob;
                }
            }

            var rootPath = rootJob?.RootPath ?? string.Empty;

            if (!Directory.Exists(rootPath))
            {
                await SetJobFinalStateAsync(jobId, JobStatus.Error, $"Root path does not exist: {rootPath}", CancellationToken.None);
                return;
            }

            var includeSubfolders = rootJob?.IncludeSubfolders ?? true;
            var files = await CollectFilesAsync(jobId, rootPath, includeSubfolders, ct);
            await SaveFileCountAsync(jobId, files.Count, ct);

            var pendingAssets = new List<MediaAsset>();

            for (var i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
                    pauseEvent.Wait(ct);

                await ProcessSingleFileAsync(jobId, files[i], pendingAssets, ct);

                var scanJob = _jobs[jobId];
                scanJob.ProcessedFiles = i + 1;
                UpdateProgress(jobId, files.Count, scanJob.ProcessedFiles, scanJob.ErrorCount);

                if ((i + 1) % _batchSize == 0 || i == files.Count - 1)
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
            Cleanup(jobId);
        }
    }

    private CancellationToken GetCancellationToken(Guid jobId)
    {
        return _cts.TryGetValue(jobId, out var cts) ? cts.Token : CancellationToken.None;
    }

    private async Task<List<FileInfo>> CollectFilesAsync(Guid jobId, string rootPath, bool includeSubfolders, CancellationToken ct)
    {
        var files = new List<FileInfo>();
        await foreach (var file in _walker.WalkAsync(rootPath, _imageExtensions, _videoExtensions, ct, includeSubfolders))
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

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
        var dbJob = await db.ScanJobs.FindAsync([jobId], ct);
        if (dbJob != null)
        {
            dbJob.TotalFiles = totalFiles;
            await db.SaveChangesAsync(ct);
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

            foreach (var heuristic in _heuristics)
            {
                if (!heuristic.IsEnabled) continue;

                var evidence = await heuristic.EvaluateAsync(asset, asset.MetadataEntries, ct);
                if (evidence != null)
                    asset.EvidenceEntries.Add(evidence);
            }

            // [F7-US-002] Run HeuristicEngine after all evidence is collected
            var heuristicResult = await _heuristicEngine.EvaluateAsync(asset.EvidenceEntries, ct);
            asset.Status = heuristicResult.Status;
            asset.ConfidenceScore = heuristicResult.ConfidenceScore;
            asset.SuggestedDate = heuristicResult.SuggestedDate;
            asset.SuggestedByHeuristic = heuristicResult.SuggestedByHeuristic;
            if (heuristicResult.SuggestionReviewStatus.HasValue)
                asset.SuggestionStatus = heuristicResult.SuggestionReviewStatus.Value;

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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();

            await db.Database.BeginTransactionAsync(ct);

            var normalizedPaths = assets.Select(a => Path.GetFullPath(a.FilePath)).ToArray();
            var existingAssets = await db.MediaAssets
                .Include(a => a.MetadataEntries)
                .Include(a => a.EvidenceEntries)
                .Where(a => normalizedPaths.Contains(a.FilePath))
                .ToListAsync(ct);

            var existingByPath = existingAssets
                .GroupBy(a => Path.GetFullPath(a.FilePath))
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var asset in assets)
            {
                var normalizedPath = Path.GetFullPath(asset.FilePath);
                if (existingByPath.TryGetValue(normalizedPath, out var existing))
                {
                    existing.ScanJobId = asset.ScanJobId;
                    existing.MetadataEntries.Clear();
                    existing.MetadataEntries.AddRange(asset.MetadataEntries);
                    existing.EvidenceEntries.Clear();
                    existing.EvidenceEntries.AddRange(asset.EvidenceEntries);
                    existing.Status = asset.Status;
                    existing.SuggestedDate = asset.SuggestedDate;
                    existing.ConfidenceScore = asset.ConfidenceScore;
                    existing.SuggestedByHeuristic = asset.SuggestedByHeuristic;
                    existing.SuggestionStatus = asset.SuggestionStatus;
                }
                else
                {
                    db.MediaAssets.Add(asset);
                }
            }

            db.AuditEntries.Add(CreateAuditEntry(jobId, "Checkpoint", $"Processed {processed}/{total} files"));

            await db.SaveChangesAsync(ct);

            await db.Database.CurrentTransaction!.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist checkpoint for job {JobId}", jobId);
            if (_jobs.TryGetValue(jobId, out var job))
                job.ErrorCount++;
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();

            var dbJob = await db.ScanJobs.FindAsync([jobId], ct);
            if (dbJob != null && _jobs.TryGetValue(jobId, out var j))
            {
                dbJob.Status = status;
                dbJob.TotalFiles = j.TotalFiles;
                dbJob.ProcessedFiles = j.ProcessedFiles;
                dbJob.ErrorCount = j.ErrorCount;
                dbJob.CompletedAt = DateTime.UtcNow;
            }

            db.AuditEntries.Add(CreateAuditEntry(jobId, status.ToString(), message));

            await db.SaveChangesAsync(ct);
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
            db.AuditEntries.Add(CreateAuditEntry(jobId, eventType, payload));
            await db.SaveChangesAsync(ct);
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
        if (_completionSources.TryRemove(jobId, out var tcs))
            tcs.TrySetResult();
        _cts.TryRemove(jobId, out _);
        _pauseEvents.TryRemove(jobId, out var pauseEvent);
        pauseEvent?.Dispose();
    }

    private MediaType GetMediaType(string extension)
    {
        if (_imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return MediaType.Image;
        return MediaType.Video;
    }
}
