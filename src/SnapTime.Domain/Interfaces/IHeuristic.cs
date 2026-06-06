// [F3-US-001]
using SnapTime.Domain.Entities;

namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Evaluates confidence of a media asset's canonical date and produces evidence.
/// </summary>
public interface IHeuristic
{
    /// <summary>Unique heuristic identifier (e.g. "H-006").</summary>
    string Id { get; }

    /// <summary>Human-readable heuristic name.</summary>
    string Name { get; }

    /// <summary>Whether this heuristic is currently active.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the asset and its metadata, returning evidence if a signal is found.
    /// </summary>
    /// <param name="asset">The media asset to evaluate.</param>
    /// <param name="metadata">Extracted metadata entries for the asset.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Evidence entry if heuristic applies; <c>null</c> otherwise.</returns>
    Task<EvidenceEntry?> EvaluateAsync(
        MediaAsset asset,
        IReadOnlyList<MetadataEntry> metadata,
        CancellationToken ct);
}
