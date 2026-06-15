// [F8-US-003] Apply DTOs shared across layers
namespace SnapTime.Domain.Models;

public record ApplyChangesRequest(List<Guid> MediaAssetIds);
public record ApplyChangesResponse(List<ApplyResult> Results, int AppliedCount, int FailedCount, DateTime Timestamp);
public record ApplyResult(Guid MediaAssetId, string FileName, bool Success, string? Error);
