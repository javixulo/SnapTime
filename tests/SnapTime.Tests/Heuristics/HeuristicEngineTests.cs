// [F7-US-002]
using FluentAssertions;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Domain.Services;

namespace SnapTime.Tests.Heuristics;

public class HeuristicEngineTests
{
    /// <summary>
    /// All Positive evidence with combined weight >= confidence threshold (80%)
    /// → AnalysisStatus = Correct, ConfidenceScore ≥ 80.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_AllPositiveEvidenceWithHighWeight_ReturnsCorrectStatusAndHighScore()
    {
        // Arrange
        var evidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-001",
                HeuristicName = "ExifHeuristic",
                Weight = 0.6,
                Direction = EvidenceDirection.Positive,
                MediaAssetId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-006",
                HeuristicName = "FilenameHeuristic",
                Weight = 0.4,
                Direction = EvidenceDirection.Positive,
                MediaAssetId = Guid.NewGuid()
            }
        };

        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // Act
        var result = await engine.EvaluateAsync(evidence, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(MediaStatus.Correct);
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(80);
        result.SuggestedDate.Should().BeNull();
        result.SuggestedByHeuristic.Should().BeNull();
        result.SuggestionReviewStatus.Should().BeNull();
    }

    /// <summary>
    /// Mixed Positive and Negative evidence with balanced weight
    /// → score medio (50-79), AnalysisStatus = NoSuggestion.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_MixedPositiveAndNegativeEvidence_ReturnsMediumScoreAndNoSuggestion()
    {
        // Arrange
        var evidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-001",
                HeuristicName = "ExifHeuristic",
                Weight = 0.5,
                Direction = EvidenceDirection.Positive,
                MediaAssetId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-002",
                HeuristicName = "NegativeHeuristic",
                Weight = 0.5,
                Direction = EvidenceDirection.Negative,
                MediaAssetId = Guid.NewGuid()
            }
        };

        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // Act
        var result = await engine.EvaluateAsync(evidence, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().BeInRange(50, 79);
        result.Status.Should().Be(MediaStatus.NoSuggestion);
        result.SuggestedDate.Should().BeNull();
        result.SuggestedByHeuristic.Should().BeNull();
        result.SuggestionReviewStatus.Should().BeNull();
    }

    /// <summary>
    /// Sin evidencias → ConfidenceScore = 0, AnalysisStatus = Correct, SuggestedDate = null.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_NoEvidence_ReturnsScoreZeroAndCorrectStatus()
    {
        // Arrange
        var evidence = new List<EvidenceEntry>();
        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // Act
        var result = await engine.EvaluateAsync(evidence, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ConfidenceScore.Should().Be(0);
        result.Status.Should().Be(MediaStatus.Correct);
        result.SuggestedDate.Should().BeNull();
        result.SuggestedByHeuristic.Should().BeNull();
        result.SuggestionReviewStatus.Should().BeNull();
    }

    /// <summary>
    /// Correction evidence with weight ≥ confidenceThreshold (80%)
    /// → SuggestedDate assigned, SuggestionReviewStatus = Unreviewed, AnalysisStatus = HasSuggestion.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_DominantCorrectionEvidence_ReturnsSuggestedDateAndUnreviewed()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var suggestedDate = new DateTime(2025, 3, 15, 5, 0, 0);
        var evidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-006",
                HeuristicName = "FilenameHeuristic",
                Weight = 0.9,
                Direction = EvidenceDirection.Correction,
                SuggestedDate = suggestedDate,
                MediaAssetId = assetId
            }
        };

        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // Act
        var result = await engine.EvaluateAsync(evidence, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SuggestedDate.Should().Be(suggestedDate);
        result.SuggestedByHeuristic.Should().Be("H-006");
        result.SuggestionReviewStatus.Should().Be(SuggestionReviewStatus.Unreviewed);
        result.Status.Should().Be(MediaStatus.HasSuggestion);
        result.ConfidenceScore.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Correction evidence with weight < confidenceThreshold (80%)
    /// → SuggestedDate = null, AnalysisStatus = NoSuggestion.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_InsufficientWeightCorrection_ReturnsNoSuggestionAndNullSuggestedDate()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var suggestedDate = new DateTime(2025, 3, 15, 5, 0, 0);
        var evidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-006",
                HeuristicName = "FilenameHeuristic",
                Weight = 0.5,
                Direction = EvidenceDirection.Correction,
                SuggestedDate = suggestedDate,
                MediaAssetId = assetId
            }
        };

        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // Act
        var result = await engine.EvaluateAsync(evidence, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SuggestedDate.Should().BeNull();
        result.SuggestedByHeuristic.Should().BeNull();
        result.SuggestionReviewStatus.Should().BeNull();
        result.Status.Should().Be(MediaStatus.NoSuggestion);
    }

    /// <summary>
    /// Re-scaneo: una nueva llamada al engine con evidencias distintas descarta
    /// y recalcula scores, status y sugerencias anteriores desde cero.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_NewCallDiscardsPreviousResults_ReturnsRecalculatedValues()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var engine = new HeuristicEngine(confidenceThreshold: 80);

        // First evaluation: positive evidence → Correct, high score, no suggestion
        var firstEvidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-001",
                HeuristicName = "ExifHeuristic",
                Weight = 0.8,
                Direction = EvidenceDirection.Positive,
                MediaAssetId = assetId
            }
        };

        // Act
        var firstResult = await engine.EvaluateAsync(firstEvidence, CancellationToken.None);

        // Second evaluation: correction evidence → HasSuggestion with date
        var suggestedDate = new DateTime(2025, 6, 10, 5, 0, 0);
        var secondEvidence = new List<EvidenceEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeuristicId = "H-006",
                HeuristicName = "FilenameHeuristic",
                Weight = 0.9,
                Direction = EvidenceDirection.Correction,
                SuggestedDate = suggestedDate,
                MediaAssetId = assetId
            }
        };

        var secondResult = await engine.EvaluateAsync(secondEvidence, CancellationToken.None);

        // Assert first result
        firstResult.Should().NotBeNull();
        firstResult.Status.Should().Be(MediaStatus.Correct);
        firstResult.ConfidenceScore.Should().BeGreaterThanOrEqualTo(80);
        firstResult.SuggestedDate.Should().BeNull();
        firstResult.SuggestedByHeuristic.Should().BeNull();
        firstResult.SuggestionReviewStatus.Should().BeNull();

        // Assert second result — completely recalculated, no trace of first call
        secondResult.Should().NotBeNull();
        secondResult.Status.Should().Be(MediaStatus.HasSuggestion);
        secondResult.SuggestedDate.Should().Be(suggestedDate);
        secondResult.SuggestedByHeuristic.Should().Be("H-006");
        secondResult.SuggestionReviewStatus.Should().Be(SuggestionReviewStatus.Unreviewed);
        secondResult.ConfidenceScore.Should().BeGreaterThan(0);

        // Prove the second call recalculated — results are independent
        firstResult.Status.Should().NotBe(secondResult.Status);
        secondResult.SuggestedDate.Should().NotBe(firstResult.SuggestedDate);
    }
}
