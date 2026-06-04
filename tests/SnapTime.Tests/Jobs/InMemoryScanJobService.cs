using System.Collections.Concurrent;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

// [F1-US-006]
namespace SnapTime.Tests.Jobs;

public record JobProgress(int TotalFiles, int ProcessedFiles, int ErrorCount);

public class InMemoryScanJobService(
    IDirectoryWalker walker,
    IMetadataExtractor metadataExtractor,
    IFileSystemMetadataExtractor fileSystemExtractor,
    string[]? imageExtensions = null,
    string[]? videoExtensions = null,
    int artificialDelayMs = 0,
    int batchSize = 50) : IScanJobService
{
    public event Action<Guid, JobProgress>? ProgressChanged;
    public Task ProcessJobAsync(Guid jobId) => Task.CompletedTask;

    private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cts = new();
    private readonly ConcurrentDictionary<Guid, ManualResetEventSlim> _pauseEvents = new();
    private readonly ConcurrentDictionary<Guid, List<AuditEntry>> _auditEntries = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _completionSources = new();

    private readonly IDirectoryWalker _walker = walker;
    private readonly IMetadataExtractor _metadataExtractor = metadataExtractor;
    private readonly IFileSystemMetadataExtractor _fileSystemExtractor = fileSystemExtractor;
    private readonly string[] _imageExtensions = imageExtensions ?? [".jpg", ".jpeg"];
    private readonly string[] _videoExtensions = videoExtensions ?? [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];

    private readonly ConcurrentDictionary<string, MediaAsset> _persistedAssets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, List<MediaAsset>> _pendingAssets = new();
    private readonly Dictionary<string, MediaType> _mediaTypeOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _persistenceErrorPaths = new(StringComparer.OrdinalIgnoreCase);

    public void SetMediaTypeOverride(string filePath, MediaType mediaType) => _mediaTypeOverrides[filePath] = mediaType;

    private readonly HashSet<string> _invalidRootPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _errorFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManualResetEventSlim _startGate = new(initialState: true);

    private record FileEntry(string FullName, string Name, long Length);

    public ConcurrentDictionary<Guid, JobProgress> Progress { get; } = new();

    public void HoldPipelineStart() => _startGate.Reset();
    public void ReleasePipelineStart() => _startGate.Set();

    public void AddInvalidRootPath(string path) => _invalidRootPaths.Add(path);
    public void AddErrorFile(string filePath) => _errorFiles.Add(filePath);

    // [F1-US-007]
    public void AddPersistenceErrorPath(string filePath) => _persistenceErrorPaths.Add(filePath);

    public List<MediaAsset> GetPersistedAssets() => _persistedAssets.Values.ToList();

    public MediaAsset? FindPersistedAssetByFilePath(string filePath)
    {
        var normalized = Path.GetFullPath(filePath);
        _persistedAssets.TryGetValue(normalized, out var asset);
        return asset;
    }

    public List<AuditEntry> GetAuditEntries(Guid jobId) =>
        _auditEntries.GetValueOrDefault(jobId) ?? [];

    public Task WaitForCompletionAsync(Guid jobId)
    {
        if (_completionSources.TryGetValue(jobId, out var tcs))
            return tcs.Task;
        return Task.CompletedTask;
    }

    public Task<ScanJob> CreateJobAsync(string rootPath)
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
        _auditEntries[jobId] = [CreateAuditEntry("Created", $"Job created for path: {rootPath}, BatchSize: {batchSize}")];

        _ = Task.Run(() => RunPipelineAsync(jobId, rootPath));

        return Task.FromResult(job);
    }

    public Task<ScanJob?> GetJobAsync(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<List<ScanJob>> GetAllJobsAsync()
    {
        return Task.FromResult(_jobs.Values.ToList());
    }

    public Task PauseJobAsync(Guid jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.Status == JobStatus.Running)
        {
            job.Status = JobStatus.Paused;
            if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
                pauseEvent.Reset();

            AddAuditEntry(jobId, "Paused", "Job was paused by user");
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

            AddAuditEntry(jobId, "Resumed", "Job was resumed by user");
        }

        return Task.CompletedTask;
    }

    public Task CancelJobAsync(Guid jobId)
    {
        if (_cts.TryGetValue(jobId, out var cts))
            cts.Cancel();

        if (_pauseEvents.TryGetValue(jobId, out var pauseEvent))
            pauseEvent.Set();

        AddAuditEntry(jobId, "CancelRequested", "Job cancellation was requested by user");

        return Task.CompletedTask;
    }

    private async Task RunPipelineAsync(Guid jobId, string rootPath)
    {
        try
        {
            await Task.Yield();
            _startGate.Wait();

            var ct = _cts.TryGetValue(jobId, out var cts) ? cts.Token : CancellationToken.None;
            var job = _jobs[jobId];
            var pauseEvent = _pauseEvents.GetValueOrDefault(jobId);

            if (_invalidRootPaths.Contains(rootPath))
            {
                SetJobFinalState(job, JobStatus.Error, $"Root path does not exist: {rootPath}");
                return;
            }

            var files = new List<FileEntry>();
            await foreach (var fi in _walker.WalkAsync(rootPath, _imageExtensions, _videoExtensions, ct))
            {
                ct.ThrowIfCancellationRequested();
                files.Add(new FileEntry(fi.FullName, fi.Name, 0L));
            }

            var totalFiles = files.Count;
            job.TotalFiles = totalFiles;
            Progress[jobId] = new JobProgress(totalFiles, 0, 0);
            _pendingAssets[jobId] = new List<MediaAsset>();

            for (var i = 0; i < files.Count; i++)
            {
                pauseEvent?.Wait(ct);

                await ProcessSingleFileAsync(jobId, files[i], job, ct);

                job.ProcessedFiles = i + 1;
                UpdateProgress(jobId, totalFiles, job.ProcessedFiles, job.ErrorCount);

                if ((i + 1) % batchSize == 0 || i == files.Count - 1)
                {
                    ct.ThrowIfCancellationRequested();
                    PersistPendingAssets(jobId);
                    AddAuditEntry(jobId, "Checkpoint", $"Processed {i + 1}/{totalFiles} files");
                }
            }

            SetJobFinalState(job, JobStatus.Completed, "Job completed successfully");
        }
        catch (OperationCanceledException)
        {
            SetJobFinalState(_jobs[jobId], JobStatus.Cancelled, "Job was cancelled");
        }
        catch (Exception ex)
        {
            SetJobFinalState(_jobs[jobId], JobStatus.Error, ex.Message);
        }
        finally
        {
            if (_completionSources.TryRemove(jobId, out var tcs))
                tcs.SetResult();

            Cleanup(jobId);
        }
    }

    private int _processingDelayMs = artificialDelayMs;
    public void SetProcessingDelay(int ms) => _processingDelayMs = ms;

    private async Task ProcessSingleFileAsync(Guid jobId, FileEntry file, ScanJob job, CancellationToken ct)
    {
        await Task.Delay(_processingDelayMs, ct);
        try
        {
            if (_errorFiles.Contains(file.FullName))
                throw new InvalidOperationException($"Simulated processing error for: {file.FullName}");

            var ext = Path.GetExtension(file.FullName);
            var mediaType = GetMediaType(ext);
            if (_mediaTypeOverrides.TryGetValue(file.FullName, out var overrideType))
                mediaType = overrideType;

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

            job.MediaAssets.Add(asset);
            _pendingAssets.GetValueOrDefault(jobId)?.Add(asset);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            job.ErrorCount++;
        }
    }

    // [F1-US-007]
    private void PersistPendingAssets(Guid jobId)
    {
        if (!_pendingAssets.TryGetValue(jobId, out var pending) || pending.Count == 0)
            return;

        foreach (var asset in pending)
        {
            if (_persistenceErrorPaths.Contains(asset.FilePath))
            {
                if (_jobs.TryGetValue(jobId, out var errJob))
                    errJob.ErrorCount++;
                continue;
            }

            var normalizedPath = Path.GetFullPath(asset.FilePath);
            if (_persistedAssets.TryGetValue(normalizedPath, out var existing))
            {
                existing.ScanJobId = asset.ScanJobId;
                existing.MetadataEntries.Clear();
                existing.MetadataEntries.AddRange(asset.MetadataEntries);
            }
            else
            {
                _persistedAssets[normalizedPath] = asset;
            }
        }

        pending.Clear();
    }

    private void SetJobFinalState(ScanJob job, JobStatus status, string message)
    {
        job.Status = status;
        job.CompletedAt = DateTime.UtcNow;
        UpdateProgress(job.Id, job.TotalFiles, job.ProcessedFiles, job.ErrorCount);
        AddAuditEntry(job.Id, status.ToString(), message);
    }

    private void AddAuditEntry(Guid jobId, string eventType, string payload)
    {
        if (_auditEntries.TryGetValue(jobId, out var entries))
            entries.Add(CreateAuditEntry(eventType, payload));
    }

    private static AuditEntry CreateAuditEntry(string eventType, string payload) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        Payload = payload,
        CreatedAt = DateTime.UtcNow
    };

    private void UpdateProgress(Guid jobId, int total, int processed, int errors)
    {
        Progress[jobId] = new JobProgress(total, processed, errors);
        ProgressChanged?.Invoke(jobId, Progress[jobId]);
    }

    private void Cleanup(Guid jobId)
    {
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
