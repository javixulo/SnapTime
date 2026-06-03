// [F0-US-003]
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Entities;

public class ScanJob
{
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<MediaAsset> MediaAssets { get; set; } = [];
}
