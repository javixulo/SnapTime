using SnapTime.Domain.Entities;

namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Provides operations for managing scan jobs that analyze media files.
/// </summary>
public interface IScanJobService
{
    /// <summary>
    /// Creates a new scan job for the specified root path and starts processing.
    /// </summary>
    /// <param name="rootPath">The root directory path to scan for media files.</param>
    /// <returns>The created <see cref="ScanJob"/> with a Running status.</returns>
    Task<ScanJob> CreateJobAsync(string rootPath);

    /// <summary>
    /// Gets a scan job by its unique identifier.
    /// </summary>
    /// <param name="jobId">The unique identifier of the scan job.</param>
    /// <returns>The scan job if found; otherwise, null.</returns>
    Task<ScanJob?> GetJobAsync(Guid jobId);

    /// <summary>
    /// Gets all scan jobs.
    /// </summary>
    /// <returns>A list of all scan jobs.</returns>
    Task<List<ScanJob>> GetAllJobsAsync();

    /// <summary>
    /// Pauses a running scan job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the scan job to pause.</param>
    Task PauseJobAsync(Guid jobId);

    /// <summary>
    /// Resumes a paused scan job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the scan job to resume.</param>
    Task ResumeJobAsync(Guid jobId);

    /// <summary>
    /// Requests cancellation of a scan job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the scan job to cancel.</param>
    Task CancelJobAsync(Guid jobId);

    /// <summary>
    /// Processes a scan job asynchronously by running the full pipeline.
    /// </summary>
    /// <param name="jobId">The unique identifier of the scan job to process.</param>
    Task ProcessJobAsync(Guid jobId);
}
