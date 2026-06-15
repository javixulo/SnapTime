// [F7-US-004] [F8-US-005] MediaAsset DTO — response from review API / apply modal
namespace SnapTime.Client.Models;

public class MediaAssetDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime? DateTimeOriginal { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string SuggestionReviewStatus { get; set; } = string.Empty;
}
