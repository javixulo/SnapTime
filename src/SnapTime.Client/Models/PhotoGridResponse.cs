// [F5] PhotoGridResponse DTO — paginated response from GET /api/photos
namespace SnapTime.Client.Models;

public class PhotoGridResponse
{
    public List<PhotoGridItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
}
