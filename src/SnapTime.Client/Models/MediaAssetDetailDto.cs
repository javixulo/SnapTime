// [F6] Media asset detail DTO from GET /api/media-assets/{id}
namespace SnapTime.Client.Models;

public class MediaAssetDetailDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = "Image";
    public long FileSize { get; set; }
    public DateTime? DateTimeOriginal { get; set; }
    public string? SubSecDateTimeOriginal { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public DateTime? FileCreatedAt { get; set; }
    public DateTime? FileModifiedAt { get; set; }
    public int ConfidenceScore { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string? SuggestedByHeuristic { get; set; }
    public List<EvidenceDto> Evidence { get; set; } = [];
}
