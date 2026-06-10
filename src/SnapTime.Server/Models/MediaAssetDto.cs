using SnapTime.Domain.Enums;

// [F7-US-004] Media asset DTO used in list and review responses
namespace SnapTime.Server.Models;

/// <summary>
/// Lightweight media asset DTO used in paginated lists and review operation responses.
/// </summary>
/// <param name="Id">Unique asset identifier.</param>
/// <param name="FilePath">Full file system path.</param>
/// <param name="FileName">File name with extension.</param>
/// <param name="MediaType">Type of media (Image or Video).</param>
/// <param name="DateTimeOriginal">Original capture date from metadata, if available.</param>
/// <param name="SuggestedDate">Suggested corrected date from heuristic analysis.</param>
/// <param name="ConfidenceScore">Confidence score (0-100) of the suggestion.</param>
/// <param name="SuggestedByHeuristic">ID of the heuristic that produced the suggestion.</param>
/// <param name="Status">Analysis status of the asset (Pending, Correct, Error, NoSuggestion, HasSuggestion).</param>
/// <param name="SuggestionStatus">Review status of the suggestion (Unreviewed, Approved, Rejected).</param>
public record MediaAssetDto(
    Guid Id,
    string FilePath,
    string FileName,
    MediaType MediaType,
    DateTime? DateTimeOriginal,
    DateTime? SuggestedDate,
    int ConfidenceScore,
    string? SuggestedByHeuristic,
    MediaStatus Status,
    SuggestionReviewStatus SuggestionStatus
);
