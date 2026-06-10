// [F7-US-004] Review request DTOs for single and batch review operations
namespace SnapTime.Server.Models;

/// <summary>
/// Request to approve or reject a single asset's date suggestion.
/// </summary>
/// <param name="AssetId">The ID of the media asset to review.</param>
/// <param name="Status">New review status: "approved" or "rejected".</param>
public record SingleReviewRequest(
    Guid AssetId,
    string Status  // "approved" | "rejected"
);

/// <summary>
/// Request to approve or reject all unreviewed suggestions in a scope.
/// </summary>
/// <param name="Scope">Scope of the batch operation: "folder" or "total".</param>
/// <param name="Status">New review status: "approved" or "rejected".</param>
/// <param name="RootPath">Root path for folder-scoped operations. Required when Scope is "folder".</param>
public record BatchReviewRequest(
    string Scope,    // "folder" | "total"
    string Status,   // "approved" | "rejected"
    string? RootPath // required if Scope == "folder"
);
