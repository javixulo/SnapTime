// [F8-US-001] Apply DTOs for client (mutable classes for Blazor)
namespace SnapTime.Client.Models;

public class ApplyChangesRequest
{
    public List<Guid> MediaAssetIds { get; set; } = new();
}

public class ApplyChangesResponse
{
    public List<ApplyResult> Results { get; set; } = new();
    public int AppliedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ApplyResult
{
    public Guid MediaAssetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
