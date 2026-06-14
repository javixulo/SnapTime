// [F8-US-001] Apply DTOs: ApplyChangesRequest, ApplyChangesResponse, ApplyResult
namespace SnapTime.Server.Models;

public record ApplyChangesRequest(List<Guid> MediaAssetIds);

public record ApplyChangesResponse(List<ApplyResult> Results, int AppliedCount, int FailedCount, DateTime Timestamp);

public record ApplyResult(Guid MediaAssetId, string FileName, bool Success, string? Error);
