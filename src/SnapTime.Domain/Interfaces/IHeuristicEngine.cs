// [F7-US-002]
using SnapTime.Domain.Entities;
using SnapTime.Domain.Services;

namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Aggregates all collected evidence (Positive, Negative, Correction) for a media asset
/// and produces a <see cref="HeuristicResult"/> with confidence score, status, and optional suggestion.
/// </summary>
public interface IHeuristicEngine
{
    /// <summary>
    /// Evaluates a list of evidence entries and computes the analysis result.
    /// </summary>
    /// <param name="evidence">All evidence entries collected for a single media asset.</param>
    /// <param name="currentCaptureDate">Current EXIF capture date of the asset, if available. Used to detect when heuristics confirm an existing date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The computed heuristic result with status, score, and optional suggestion.</returns>
    Task<HeuristicResult> EvaluateAsync(
        IReadOnlyList<EvidenceEntry> evidence,
        DateTime? currentCaptureDate,
        CancellationToken ct);
}
