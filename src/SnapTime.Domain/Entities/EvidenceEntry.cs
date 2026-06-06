// [F0-US-003]
using SnapTime.Domain.Enums;

namespace SnapTime.Domain.Entities;

public class EvidenceEntry
{
    public Guid Id { get; set; }
    public string HeuristicId { get; set; } = string.Empty;
    public string HeuristicName { get; set; } = string.Empty;
    public double Weight { get; set; }
    public EvidenceDirection Direction { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}
