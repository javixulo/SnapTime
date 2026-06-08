// [F4-US-000] ScanJob DTO
namespace SnapTime.Client.Models;

public class ScanJobDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; } = true;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
