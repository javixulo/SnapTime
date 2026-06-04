using SnapTime.Domain.Enums;

namespace SnapTime.Server.Models;

/// <summary>
/// Request to create a new scan job.
/// </summary>
/// <param name="RootPath">Directory path to scan.</param>
public record CreateJobRequest(string RootPath);

/// <summary>
/// Data transfer object representing a scan job.
/// </summary>
/// <param name="Id">Unique job identifier.</param>
/// <param name="Status">Current job status.</param>
/// <param name="RootPath">Directory being scanned.</param>
/// <param name="TotalFiles">Total files discovered.</param>
/// <param name="ProcessedFiles">Files processed so far.</param>
/// <param name="ErrorCount">Files with errors.</param>
/// <param name="CreatedAt">When the job was created.</param>
/// <param name="CompletedAt">When the job completed, if finished.</param>
public record JobDto(
    Guid Id,
    JobStatus Status,
    string RootPath,
    int TotalFiles,
    int ProcessedFiles,
    int ErrorCount,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
