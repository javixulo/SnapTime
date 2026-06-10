// [F0-US-003]
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Entities;

public class MediaAsset
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public long FileSize { get; set; }
    public DateTime? FileCreatedAt { get; set; }
    public DateTime? FileModifiedAt { get; set; }
    public int ConfidenceScore { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string? SuggestedByHeuristic { get; set; }
    public MediaStatus Status { get; set; }
    public SuggestionReviewStatus SuggestionStatus { get; set; } = SuggestionReviewStatus.Unreviewed;
    public Guid ScanJobId { get; set; }
    public ScanJob ScanJob { get; set; } = null!;
    public List<MetadataEntry> MetadataEntries { get; set; } = [];
    public List<EvidenceEntry> EvidenceEntries { get; set; } = [];
}
