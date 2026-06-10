// [F7-US-004] MediaAsset DTO — response from review API
namespace SnapTime.Client.Models;

public class MediaAssetDto
{
    public Guid Id { get; set; }
    public string SuggestionReviewStatus { get; set; } = string.Empty;
}
