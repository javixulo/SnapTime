// [F0-US-003]
namespace SnapTime.Domain.Entities;

public class MetadataEntry
{
    public Guid Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}
