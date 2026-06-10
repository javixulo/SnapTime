// [F7-US-002]
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Services;

/// <summary>
/// Result produced by the HeuristicEngine after evaluating all evidence for a media asset.
/// </summary>
public class HeuristicResult
{
    /// <summary>Computed analysis status (Correct, NoSuggestion, HasSuggestion, etc.).</summary>
    public MediaStatus Status { get; init; }

    /// <summary>Confidence score from 0–100 based on evidence weights.</summary>
    public int ConfidenceScore { get; init; }

    /// <summary>Suggested corrected date, if the engine found a dominant correction.</summary>
    public DateTime? SuggestedDate { get; init; }

    /// <summary>ID of the heuristic that produced the dominant correction evidence.</summary>
    public string? SuggestedByHeuristic { get; init; }

    /// <summary>Review status of the suggestion (Unreviewed if suggestion exists, null otherwise).</summary>
    public SuggestionReviewStatus? SuggestionReviewStatus { get; init; }
}
