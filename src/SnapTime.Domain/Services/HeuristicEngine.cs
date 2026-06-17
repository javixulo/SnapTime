// [F7-US-002]
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Domain.Services;

/// <summary>
/// Aggregation engine that evaluates all evidence for a media asset and computes
/// a confidence score, analysis status, and optional suggested date correction.
/// 
/// Rules:
/// - Positive evidence: adds weight to the confidence score numerator.
/// - Negative evidence: reduces the relative confidence.
/// - Correction evidence with weight ≥ confidenceThreshold → HasSuggestion (dominant correction wins).
/// - No evidence → Correct (already analyzed, just no signal).
/// - Correction evidence below threshold → NoSuggestion.
/// </summary>
public class HeuristicEngine : IHeuristicEngine
{
    private readonly int _confidenceThreshold;

    /// <summary>
    /// Creates a heuristic engine with the specified confidence threshold (0–100).
    /// </summary>
    /// <param name="confidenceThreshold">
    /// Minimum percentage (weight × 100) required for a correction evidence
    /// to produce a suggestion. Default 80.
    /// </param>
    public HeuristicEngine(int confidenceThreshold = 70)
    {
        _confidenceThreshold = confidenceThreshold;
    }

    /// <inheritdoc />
    public Task<HeuristicResult> EvaluateAsync(
        IReadOnlyList<EvidenceEntry> evidence,
        DateTime? currentCaptureDate,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var positiveSum = evidence.Where(e => e.Direction == EvidenceDirection.Positive).Sum(e => e.Weight);
        var negativeSum = evidence.Where(e => e.Direction == EvidenceDirection.Negative).Sum(e => e.Weight);
        var corrections = evidence.Where(e => e.Direction == EvidenceDirection.Correction).ToList();
        var correctionSum = corrections.Sum(e => e.Weight);

        // Confidence score: proportion of positive+correction weight out of total absolute weight
        var totalWeight = positiveSum + negativeSum + correctionSum;
        var confidenceScore = totalWeight > 0
            ? (int)((positiveSum + correctionSum) / totalWeight * 100)
            : 0;

        // Find dominant correction: highest-weight correction that meets the threshold
        var dominantCorrection = corrections
            .Where(e => e.Weight * 100 >= _confidenceThreshold)
            .MaxBy(e => e.Weight);

        DateTime? suggestedDate = null;
        string? suggestedByHeuristic = null;
        SuggestionReviewStatus? suggestionReviewStatus = null;
        MediaStatus status;

        if (dominantCorrection is not null)
        {
            if (currentCaptureDate.HasValue && dominantCorrection.SuggestedDate?.Date == currentCaptureDate.Value.Date)
            {
                // Correction confirms existing date → Correct (no suggestion needed)
                status = MediaStatus.Correct;
            }
            else
            {
                // Dominant correction with sufficient weight → HasSuggestion
                status = MediaStatus.HasSuggestion;
                suggestedDate = dominantCorrection.SuggestedDate;
                suggestedByHeuristic = dominantCorrection.HeuristicId;
                suggestionReviewStatus = SuggestionReviewStatus.Unreviewed;
            }
        }
        else if (evidence.Count == 0)
        {
            // No evidence collected → already analyzed, no signal found
            status = MediaStatus.Correct;
        }
        else if (corrections.Count > 0 && corrections.All(c => c.Weight * 100 < _confidenceThreshold))
        {
            // Only correction evidence but none meet the weight threshold → NoSuggestion
            status = MediaStatus.NoSuggestion;
        }
        else
        {
            // Positive/Negative or any other mix → status depends on score meeting threshold
            status = confidenceScore >= _confidenceThreshold
                ? MediaStatus.Correct
                : MediaStatus.NoSuggestion;
        }

        return Task.FromResult(new HeuristicResult
        {
            Status = status,
            ConfidenceScore = confidenceScore,
            SuggestedDate = suggestedDate,
            SuggestedByHeuristic = suggestedByHeuristic,
            SuggestionReviewStatus = suggestionReviewStatus
        });
    }
}
